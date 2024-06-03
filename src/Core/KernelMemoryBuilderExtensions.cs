using LLama;
using LLama.Common;
using LLama.Grammars;
using LLamaSharp.KernelMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.KernelMemory.Search;

namespace SharpNlp.Core;

public static class KernelMemoryBuilderExtensions
{
    public static IKernelMemoryBuilder UseRabbitMQ(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var rabbitMqConfig = new RabbitMqConfig();
        config.BindSection("KernelMemory:Services:RabbitMQ", rabbitMqConfig);

        return builder.WithRabbitMQOrchestration(rabbitMqConfig);
    }

    /// <summary>
    /// Добавить хранилище на диске.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseSimpleStorage(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var storageFolder = Path.GetFullPath($"./storage-{nameof(SharpNlp)}");

        if (!Directory.Exists(storageFolder))
            Directory.CreateDirectory(storageFolder);

        var fileStorageConfig = new SimpleFileStorageConfig
        {
            Directory = storageFolder,
            StorageType = FileSystemTypes.Disk
        };

        var vectorDbConfig = new SimpleVectorDbConfig()
        {
            Directory = storageFolder,
            StorageType = FileSystemTypes.Disk
        };

        return builder
            .WithSimpleFileStorage(fileStorageConfig)
            .WithSimpleVectorDb(vectorDbConfig);
    }

    /// <summary>
    /// Добавить хранилище в qdrant (см. README, appsettings.json).
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UsePostgres(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var postgresConfig = new PostgresConfig();
        config.BindSection("KernelMemory:Services:Postgres", postgresConfig);

        return builder.WithPostgresMemoryDb(postgresConfig);
    }

    /// <summary>
    /// Добавить хранилище в виде Qdrant (см. README, appsettings.json).
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseQdrant(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var qdrantConfig = new QdrantConfig();
        config.BindSection("KernelMemory:Services:Qdrant", qdrantConfig);

        return builder.WithQdrantMemoryDb(qdrantConfig);
    }

    /// <summary>
    /// Использовать конфигурацию приложения для партиции текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseCustomTextPartitioningOptions(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var textPartitioningOptions = new TextPartitioningOptions();
        config.BindSection("KernelMemory:DataIngestion:TextPartitioning", textPartitioningOptions);

        return builder.WithCustomTextPartitioningOptions(textPartitioningOptions);
    }


    /// <summary>
    /// Добавить кастомный ISearchClient с конфигурацией приложения.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseCustomSearchClient<T>(this IKernelMemoryBuilder builder, IConfiguration config)
        where T : class, ISearchClient
    {
        var searchClientConfig = new SearchClientConfig();
        config.BindSection("KernelMemory:NERSearchClient", searchClientConfig);

        return builder
            .WithSearchClientConfig(searchClientConfig)
            .WithCustomSearchClient<T>();
    }

    /// <summary>
    /// Использовать LLM от OpenAI.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseOpenAI(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var openAIEmbeddingConfig = new OpenAIConfig();
        config.BindSection("KernelMemory:Services:OpenAI:TextEmbeddingGeneration", openAIEmbeddingConfig);

        var openAIConfig = new OpenAIConfig();
        config.BindSection("KernelMemory:Services:OpenAI:TextGeneration", openAIConfig);

        return builder
            .WithOpenAITextEmbeddingGeneration(openAIEmbeddingConfig)
            .WithOpenAITextGeneration(openAIConfig);
    }

    /// <summary>
    /// Использовать embedders-ы от OpenAI.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseOpenAITextEmbeddingGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var openAIEmbeddingConfig = new OpenAIConfig();
        config.BindSection("KernelMemory:Services:OpenAI:TextEmbeddingGeneration", openAIEmbeddingConfig);

        return builder.WithOpenAITextEmbeddingGeneration(openAIEmbeddingConfig);
    }

    /// <summary>
    /// Использовать локальную LLM на LM Studio.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLMStudioTextGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var lmStudioConfig = new OpenAIConfig();
        config.BindSection("KernelMemory:Services:LMStudio:TextGeneration", lmStudioConfig);

        return builder.WithOpenAITextGeneration(lmStudioConfig);
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLlamaTextGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var llamaSharpConfig = new LlamaSharpConfig();
        config.BindSection("KernelMemory:Services:KMLlamaSharp:TextGeneration", llamaSharpConfig);

        return builder.WithLlamaTextGeneration(llamaSharpConfig);
    }

    /// <summary>
    /// Использовать локальную LLM для генерации эмбеддингов.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextEmbeddingGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        ModelParams @params = new ModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration:ModelPath")!)
        {
            Embeddings = true
        };

        config.BindSection("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration", @params);

        var weights = LLamaWeights.LoadFromFile(@params);

        LLamaEmbedder embedder = new LLamaEmbedder(weights, @params);
        return builder.WithLLamaSharpTextEmbeddingGeneration(new LLamaSharpTextEmbeddingGenerator(embedder));
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var gbnf = Utils.ReadResourceAsText("json.gbnf", "Assets").Trim();
        var grammar = Grammar.Parse(gbnf, "root");

        var inferenceParams = new InferenceParams
        {
            AntiPrompts = ["Output:", "User:", "\n\n"],
            Temperature = 0.0f,
            Grammar = grammar.CreateInstance()
        };

        ModelParams @params = new ModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!)
        {
            Embeddings = false
        };

        config.BindSection("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration", @params);

        var weights = LLamaWeights.LoadFromFile(@params);
        var context = weights.CreateContext(@params);
        StatelessExecutor executor = new StatelessExecutor(weights, @params);

        return builder.WithLLamaSharpTextGeneration(new LlamaSharpTextGenerator(weights, context, executor, inferenceParams));
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharp(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var gbnf = Utils.ReadResourceAsText("json.gbnf", "Assets").Trim();
        var grammar = Grammar.Parse(gbnf, "root");

        var inferenceParams = new InferenceParams
        {
            AntiPrompts = ["Document:", "DOCUMENT:", "Output:", "OUTPUT:", "User:", "USER:", "\n\n"],
            Temperature = 0.0f,
            Grammar = grammar.CreateInstance()
        };

        ModelParams @params = new ModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!)
        {
            Embeddings = true
        };

        config.BindSection("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration", @params);

        var weights = LLamaWeights.LoadFromFile(@params);
        var context = weights.CreateContext(@params);

        StatelessExecutor executor = new StatelessExecutor(weights, @params);
        LLamaEmbedder embedder = new LLamaEmbedder(weights, @params);

        return builder
            .WithLLamaSharpTextEmbeddingGeneration(new LLamaSharpTextEmbeddingGenerator(embedder))
            .WithLLamaSharpTextGeneration(new LlamaSharpTextGenerator(weights, context, executor, inferenceParams));
    }
}
