using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Prompts;
using Newtonsoft.Json;

namespace SharpNlp.Core;

public class SearchClientNlpV2 : SearchClient
{
    private static readonly Regex s_MultiLineBreakRegex = new("\n{2,}", RegexOptions.Multiline);
    private static readonly string s_DoubleEnvBreak = Environment.NewLine + Environment.NewLine;
    private readonly IPromptProvider _promptProvider;

    //private readonly List<string> _prompts = [
    //    Constants.Prompts.Person, Constants.Prompts.Date, Constants.Prompts.Organisation,
    //    Constants.Prompts.Mineraldeposit, Constants.Prompts.Reservoir, Constants.Prompts.Npt];

    private readonly List<string> _prompts = [Constants.Prompts.Person];

    private readonly string _promptLang = "ru";

    public SearchClientNlpV2(
        IMemoryDb memoryDb,
        ITextGenerator textGenerator,
        IPromptProvider promptProvider,
        SearchClientConfig? config = null,
        ILogger<SearchClient>? log = null)
        : base(memoryDb, textGenerator, promptProvider, config, log)
    {
        _promptProvider = promptProvider;
    }

    public override async Task<MemoryAnswer> AskAsync(
        string index,
        string question,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, List<string>> namedEntities = new();

        var answer = GetNoAnswer(question);

        foreach (var promptName in _prompts)
        {
            var promptFullName = $"ner_{promptName}_{_promptLang}";
            var prompt = _promptProvider.ReadPrompt(promptFullName);

            _log.LogTrace($"Genereting result for prompt: {promptFullName}");

            var result = await AskForPrompt(answer, prompt, index, question, filters, minRelevance, cancellationToken)
                .ConfigureAwait(false);

            if (result.Count > 0)
            {
                namedEntities.Add(promptName, result);
            }
        }

        if (namedEntities.Count == 0)
        {
            this._log.LogWarning("Documents count was zero");
            answer.NoResultReason = "Documents count was zero";
            return answer;
        }

        answer.NoResult = false;
        answer.Result = JsonConvert.SerializeObject(namedEntities);

        return answer;
    }

    private async Task<List<string>> AskForPrompt(
        MemoryAnswer answer,
        string prompt,
        string index,
        string question,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        CancellationToken cancellationToken = default)
    {
        List<string> entities = new();
        var facts = new StringBuilder();
        var tokensAvailable = CountAvailableTokens(prompt: prompt, question: question);
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
                var output = await GenerateAnswer(prompt: prompt, question: question, facts: facts.ToString(), cancellationToken)
                    .ConfigureAwait(false);

                _log.LogTrace($"Generated answer:\n{output}");

                entities.AddRange(output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

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
            var output = await GenerateAnswer(prompt: prompt, question: question, facts: facts.ToString(), cancellationToken)
                .ConfigureAwait(false);

            _log.LogTrace($"Generated answer:\n{output}");

            entities.AddRange(output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (factsAvailableCount > 0 && factsUsedCount == 0)
        {
            this._log.LogError("Unable to inject memories in the prompt, not enough tokens available");
            answer.NoResultReason = "Unable to use memories";
        }

        if (factsUsedCount == 0)
        {
            this._log.LogWarning("No memories available");
            answer.NoResultReason = "No memories available";
        }

        return entities;
    }

    private MemoryAnswer GetNoAnswer(string question) =>
        new()
        {
            Question = question,
            NoResult = true,
            Result = this._config.EmptyAnswer
        };

    private int CountAvailableTokens(string prompt, string question)
    {
        var maxTokens = this._config.MaxAskPromptSize > 0
            ? this._config.MaxAskPromptSize
            : this._textGenerator.MaxTokenTotal;

        return maxTokens
            - this._textGenerator.CountTokens(prompt)
            - this._textGenerator.CountTokens(question)
            - this._config.AnswerTokens;
    }

    private async Task<string> GenerateAnswer(string prompt, string question, string facts, CancellationToken cancellationToken)
    {
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
        var watch = new Stopwatch();
        watch.Restart();
        await foreach (var x in this._textGenerator.GenerateTextAsync(prompt, options)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            text.Append(x);
            Console.Write(x);
        }

        Console.WriteLine();

        watch.Stop();
        this._log.LogTrace("Answer generated in {0} msecs", watch.ElapsedMilliseconds);

        return text.ToString();
    }
}
