using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Prompts;
using Microsoft.KernelMemory.Search;
using Newtonsoft.Json;

namespace SharpNlp.Core;

public class SearchClient : DefaultSearchClient
{
    public SearchClient(
    IMemoryDb memoryDb,
    ITextGenerator textGenerator,
        SearchClientConfig? config = null,
        IPromptProvider? promptProvider = null,
        ILogger<SearchClient>? log = null)
        : base(memoryDb, textGenerator, config, promptProvider, log)
    {
        this._answerPrompt = _promptProvider.ReadPrompt(Constants.PromptNamesNerV6Ru);
    }

    private MemoryAnswer GetNoAnswerFound(string question) =>
        new MemoryAnswer
        {
            Question = question,
            NoResult = true,
            Result = this._config.EmptyAnswer
        };

    private int CountTokensAvailable(string question)
    {
        var maxTokens = this._config.MaxAskPromptSize > 0
            ? this._config.MaxAskPromptSize
            : this._textGenerator.MaxTokenTotal;

        return maxTokens
            - this._textGenerator.CountTokens(this._answerPrompt)
            - this._textGenerator.CountTokens(question)
            - this._config.AnswerTokens;
    }

    private void AddPartitionToAnswer(MemoryAnswer answer, string index, MemoryRecord memory, string partitionText)
    {
        // Note: a document can be composed by multiple files
        string documentId = memory.GetDocumentId(this._log);

        // Identify the file in case there are multiple files
        string fileId = memory.GetFileId(this._log);

        // Note: this is not a URL and perhaps could be dropped. For now it acts as a unique identifier. See also SourceUrl.
        string linkToFile = $"{index}/{documentId}/{fileId}";

        // If the file is already in the list of citations, only add the partition
        var citation = answer.RelevantSources.FirstOrDefault(x => x.Link == linkToFile);
        if (citation is null)
        {
            citation = new Citation();
            answer.RelevantSources.Add(citation);
        }

        // Add the partition to the list of citations
        citation.Index = index;
        citation.DocumentId = documentId;
        citation.FileId = fileId;
        citation.Link = linkToFile;
        citation.SourceContentType = memory.GetFileContentType(this._log);
        citation.SourceName = memory.GetFileName(this._log);
        citation.SourceUrl = memory.GetWebPageUrl(index);

        citation.Partitions.Add(new Citation.Partition
        {
            Text = partitionText,
            Relevance = 1,
            PartitionNumber = memory.GetPartitionNumber(this._log),
            SectionNumber = memory.GetSectionNumber(),
            LastUpdate = memory.GetLastUpdate(),
            Tags = memory.Tags,
        });
    }

    private bool TryAddOutputToDocuments(List<DocumentInfo> documents, StringBuilder output)
    {
        try
        {
            if (JsonConvert.DeserializeObject<DocumentInfo>(output.ToString()) is DocumentInfo document)
            {
                documents.Add(document);
                return true;
            }
            else
            {
                _log.LogError("JsonConvert DeserializeObject вернул NULL");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Результат работы LLM не получилось преобразовать в {0}", nameof(DocumentInfo));
        }

        return false;
    }

    /// <inheritdoc />
    public override async Task<MemoryAnswer> AskAsync(
        string index,
        string question, ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        CancellationToken cancellationToken = default)
    {
        // Подсчитываем количество доступных токенов (кол-во доступных токинов - это будет макимальный размер чанка)
        var tokensAvailable = CountTokensAvailable(question);
        var answer = GetNoAnswerFound(question);

        this._log.LogTrace("Fetching relevant memories");
        var matches = this._memoryDb.GetListAsync(index, filters, this._config.MaxMatchesCount, false, cancellationToken);

        // Кол-во найденных в памяти чанков, нужно просто для для проверки, чтобы потом вернуть NoResult
        var factsUsedCount = 0;
        var factsAvailableCount = 0;
        int currentTokensAvailable = tokensAvailable;
        var facts = new StringBuilder();
        var documents = new List<DocumentInfo>();

        // Проход по всем записям в памяти, которые подходят для переданных фильтров
        await foreach (var memory in matches.ConfigureAwait(false))
        {
            // Чтобы не обрабатывать слишком большой документ
            if (factsUsedCount >= this._config.MaxMatchesCount)
            {
                break;
            }

            var partitionText = memory.GetPartitionText(this._log).Trim();

            if (string.IsNullOrEmpty(partitionText))
            {
                this._log.LogError("The document partition is empty, doc: {0}", memory.Id);
                continue;
            }

            factsAvailableCount++;

            // Use the partition/chunk only if there's room for it
            var partitionSize = this._textGenerator.CountTokens(partitionText);

            if (partitionSize >= currentTokensAvailable)
            {
                // Если даже добавление одного чанка уже превышает контекст.
                if (factsUsedCount == 0)
                {
                    break;
                }

                // Когда контекст закончился генерируем ответ
                var output = await GenerateAnswerForPartitionAsync(question, facts.ToString(), cancellationToken);
                TryAddOutputToDocuments(documents, output);

                // Обнуляем документы и кол-во доступных токенов, для следующих чанков
                facts.Clear();
                currentTokensAvailable = tokensAvailable;
            }
            else
            {
                factsUsedCount++;
                facts.AppendLine(partitionText);
                currentTokensAvailable -= partitionSize;

                AddPartitionToAnswer(answer, index, memory, partitionText);
            }
        }

        if (facts.Length > 0)
        {
            // Когда контекст закончился генерируем ответ
            var output = await GenerateAnswerForPartitionAsync(question, facts.ToString(), cancellationToken);
            TryAddOutputToDocuments(documents, output);
        }

        if (factsAvailableCount > 0 && factsUsedCount == 0)
        {
            this._log.LogError("Unable to inject memories in the prompt, not enough tokens available");
            answer.NoResultReason = Constants.ErrorsUnableToUseMemories;
            return answer;
        }

        if (factsUsedCount == 0)
        {
            this._log.LogWarning("No memories available");
            answer.NoResultReason = Constants.ErrorsNoMemoriesAvailable;
            return answer;
        }

        if (documents.Count == 0)
        {
            this._log.LogWarning("Unable to get result");
            answer.NoResultReason = Constants.ErrorsEmptyResult;
            return answer;
        }

        answer.NoResult = false;
        answer.Result = JsonConvert.SerializeObject(Utils.MergeDocuments(documents));
        return answer;
    }

    private async Task<StringBuilder> GenerateAnswerForPartitionAsync(string question, string partitionText, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var charsGenerated = 0;

        var watch = new Stopwatch();
        watch.Restart();

        await foreach (var x in GenerateAnswerAsync(question, partitionText)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            result.Append(x);

            if (this._log.IsEnabled(LogLevel.Trace) && result.Length - charsGenerated >= 30)
            {
                charsGenerated = result.Length;
                this._log.LogTrace("{0} chars generated", charsGenerated);
            }
        }

        watch.Stop();
        this._log.LogTrace("Answer generated in {0} msecs.", watch.ElapsedMilliseconds);

        return result;
    }

    protected override IAsyncEnumerable<string> GenerateAnswerAsync(string question, string facts)
    {
        var prompt = _promptProvider.ReadPrompt(Constants.PromptNamesNerV6Ru);
        prompt = prompt.Replace("{{$facts}}", facts.Trim(), StringComparison.OrdinalIgnoreCase);
        prompt = prompt.Replace("{{$input}}", question, StringComparison.OrdinalIgnoreCase);
        prompt = prompt.Replace("{{$notFound}}", this._config.EmptyAnswer, StringComparison.OrdinalIgnoreCase);

        var options = new TextGenerationOptions
        {
            Temperature = this._config.Temperature,
            TopP = this._config.TopP,
            PresencePenalty = this._config.PresencePenalty,
            FrequencyPenalty = this._config.FrequencyPenalty,
            MaxTokens = this._config.AnswerTokens,
            StopSequences = this._config.StopSequences,
            TokenSelectionBiases = this._config.TokenSelectionBiases,
        };

        if (this._log.IsEnabled(LogLevel.Debug))
        {
            this._log.LogDebug("Running RAG prompt, size: {0} tokens, requesting max {1} tokens",
                this._textGenerator.CountTokens(prompt),
                this._config.AnswerTokens);
        }

        return this._textGenerator.GenerateTextAsync(prompt, options);
    }
}
