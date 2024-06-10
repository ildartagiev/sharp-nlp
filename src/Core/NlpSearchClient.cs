using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Prompts;
using Newtonsoft.Json;
using SharpNlp.Core;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpNlp.Core;

public class NlpSearchClient : SearchClient
{
    private new readonly string _answerPrompt;

    private static readonly Regex s_MultiLineBreakRegex = new("\n{2,}", RegexOptions.Multiline);
    private static readonly string s_DoubleEnvBreak = Environment.NewLine + Environment.NewLine;

    public NlpSearchClient(
        IMemoryDb memoryDb,
        ITextGenerator textGenerator,
        IPromptProvider promptProvider,
        SearchClientConfig? config = null,
        ILogger<SearchClient>? log = null)
        : base(memoryDb, textGenerator, promptProvider, config, log)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        _answerPrompt = promptProvider.ReadPrompt(Constants.PromptNamesNerV6Ru);
    }

    private MemoryAnswer GetNoAnswer(string question) =>
        new()
        {
            Question = question,
            NoResult = true,
            Result = this._config.EmptyAnswer
        };

    private int CountAvailableTokens(string question)
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

        string fileName = memory.GetFileName(this._log);

        var citation = answer.RelevantSources.FirstOrDefault(x => x.Link == linkToFile);
        if (citation == null)
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
        citation.SourceName = fileName;
        citation.SourceUrl = memory.GetWebPageUrl(index);

        citation.Partitions.Add(new Citation.Partition
        {
            Text = partitionText,
            Relevance = (float)float.MinValue,
            PartitionNumber = memory.GetPartitionNumber(this._log),
            SectionNumber = memory.GetSectionNumber(),
            LastUpdate = memory.GetLastUpdate(),
            Tags = memory.Tags,
        });
    }

    private bool TryAddOutputToDocuments(string output, List<DocumentInfo> documents)
    {
        try
        {
            if (JsonConvert.DeserializeObject<DocumentInfo>(output) is DocumentInfo document)
            {
                documents.Add(document);
                return true;
            }
            else
            {
                _log.LogError("Deserialized object was null");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error occured during deserialization");
        }

        return false;
    }

    public override async Task<MemoryAnswer> AskAsync(
        string index,
        string question,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        CancellationToken cancellationToken = default)
    {
        var answer = GetNoAnswer(question);

        var facts = new StringBuilder();
        var documents = new List<DocumentInfo>();

        var tokensAvailable = CountAvailableTokens(question);
        var currentTokensAvailable = tokensAvailable;

        var factsUsedCount = 0;
        var factsAvailableCount = 0;

        this._log.LogTrace("Fetching relevant memories");
        IAsyncEnumerable<MemoryRecord> matches = this._memoryDb.GetListAsync(
            index: index,
            filters: filters,
            limit: this._config.MaxMatchesCount,
            withEmbeddings: false,
            cancellationToken: cancellationToken);

        // Memories are sorted by relevance, starting from the most relevant
        await foreach (var memory in matches.ConfigureAwait(false))
        {
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
                var output = await GenerateAnswer(question, facts.ToString(), cancellationToken).ConfigureAwait(false);

                _log.LogInformation($"Generated answer:\n{output}");

                TryAddOutputToDocuments(output, documents);

                // Обнуляем документы и кол-во доступных токенов, для следующих чанков
                facts.Clear();
                currentTokensAvailable = tokensAvailable;
            }
            else
            {
                factsUsedCount++;
                this._log.LogTrace("Adding text {0} with relevance {1}", factsUsedCount, float.MinValue);

                facts.AppendLine(partitionText);
                currentTokensAvailable -= partitionSize;
                AddPartitionToAnswer(answer, index, memory, partitionText);
            }

            // In cases where a buggy storage connector is returning too many records
            if (factsUsedCount >= this._config.MaxMatchesCount)
            {
                break;
            }
        }

        // Если контекст не закончился, но мы уже вышли из цикла
        if (facts.Length > 0)
        {
            var output = await GenerateAnswer(question, facts.ToString(), cancellationToken).ConfigureAwait(false);

            _log.LogInformation($"Generated answer:\n{output}");

            TryAddOutputToDocuments(output, documents);
        }

        if (factsAvailableCount > 0 && factsUsedCount == 0)
        {
            this._log.LogError("Unable to inject memories in the prompt, not enough tokens available");
            answer.NoResultReason = "Unable to use memories";
            return answer;
        }

        if (factsUsedCount == 0)
        {
            this._log.LogWarning("No memories available");
            answer.NoResultReason = "No memories available";
            return answer;
        }

        if (documents.Count == 0)
        {
            this._log.LogWarning("Documents count was zero");
            answer.NoResultReason = "Documents count was zero";
            return answer;
        }

        answer.NoResult = false;
        answer.Result = JsonConvert.SerializeObject(Utils.MergeDocuments(documents));

        return answer;
    }

    private async Task<string> GenerateAnswer(string question, string facts, CancellationToken cancellationToken)
    {
        var prompt = this._answerPrompt;
        prompt = prompt.Replace("{{$facts}}", s_MultiLineBreakRegex.Replace(facts, s_DoubleEnvBreak).Trim(), StringComparison.OrdinalIgnoreCase);
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
            TokenSelectionBiases = this._config.TokenSelectionBiases
        };

        if (this._log.IsEnabled(LogLevel.Debug))
        {
            this._log.LogDebug("Running RAG prompt, size: {0} tokens, requesting max {1} tokens",
                this._textGenerator.CountTokens(prompt),
                this._config.AnswerTokens);
        }

        var text = new StringBuilder();
        //var charsGenerated = 0;
        var watch = new Stopwatch();
        watch.Restart();
        await foreach (var x in this._textGenerator.GenerateTextAsync(prompt, options)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            text.Append(x);
            Console.Write(x);

            //if (this._log.IsEnabled(LogLevel.Trace) && text.Length - charsGenerated >= 30)
            //{
            //    charsGenerated = text.Length;
            //    this._log.LogTrace("{0} chars generated", charsGenerated);
            //}
        }

        Console.WriteLine();

        watch.Stop();
        this._log.LogTrace("Answer generated in {0} msecs", watch.ElapsedMilliseconds);

        return text.ToString();
    }
}
