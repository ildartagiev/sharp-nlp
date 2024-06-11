using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.KernelMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

namespace SharpNlp.Core;

public static class KernelMemoryBuilderExtensions
{
    private static readonly IReadOnlyList<string> s_AntiPrompts = ["Document:", "DOCUMENT:", "Output:", "OUTPUT:", "User:", "USER:", "\n\n"];

    private static readonly InferenceParams s_InferenceParams = new()
    {
        AntiPrompts = s_AntiPrompts,
        Temperature = 0.0f
    };

    private static LLamaSharpConfig CreateLLamaSharpConfig(string modelPath, InferenceParams? inferenceParams = null)
    {
        return new(modelPath)
        {
            ContextSize = 8192,
            GpuLayerCount = 5,
            MainGpu = 0,
            Seed = 1337,
            SplitMode = GPUSplitMode.None,
            DefaultInferenceParams = inferenceParams
        };
    }

    private static ModelParams CreateModelParams(string modelPath, bool embedding)
    {
        return new(modelPath)
        {
            Embeddings = embedding,
            ContextSize = 8192,
            GpuLayerCount = 5,
            MainGpu = 0,
            Seed = 1337,
            SplitMode = GPUSplitMode.None
        };
    }

    /// <summary>
    /// Использовать брокер сообщений
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseRabbitMQ(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var rabbitMqConfig = new RabbitMqConfig();
        config.BindSection("KernelMemory:Services:RabbitMQ", rabbitMqConfig);

        return builder.WithRabbitMQOrchestration(rabbitMqConfig);
    }

    /// <summary>
    /// Использовать хранилище на локальном диске.
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
    /// Использовать хранилище в виде PostgreSQL (см. README, appsettings.json).
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
    /// Использовать хранилище в виде Qdrant (см. README, appsettings.json).
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
    /// Использовать кастомный ISearchClient с конфигурацией приложения.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseSearchClientConfig(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var searchClientConfig = new SearchClientConfig();
        config.BindSection("KernelMemory:NERSearchClient", searchClientConfig);

        return builder.WithSearchClientConfig(searchClientConfig);
    }

    /// <summary>
    /// Использовать Open AI для эмбеддинга и генерации текста.
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
    /// Использовать Open AI для эмбеддинга.
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
    /// Использовать Open AI для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseOpenAITextGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        var openAIConfig = new OpenAIConfig();
        config.BindSection("KernelMemory:Services:OpenAI:TextGeneration", openAIConfig);

        return builder.WithOpenAITextGeneration(openAIConfig);
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
    /// Использовать локальную LLM для генерации эмбеддингов.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextEmbeddingGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        ModelParams modelParams = CreateModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration:ModelPath")!, true);
        config.BindSection("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration", modelParams);

        var weights = LLamaWeights.LoadFromFile(modelParams);
        LLamaEmbedder embedder = new LLamaEmbedder(weights, modelParams);

        return builder.WithLLamaSharpTextEmbeddingGeneration(new LLamaSharpTextEmbeddingGenerator(embedder));
    }

    /// <summary>
    /// Использовать локальную LLM для генерации эмбеддингов.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextEmbeddingGenerationAsConfig(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        LLamaSharpConfig llamaSharpConfig = CreateLLamaSharpConfig(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration:ModelPath")!);
        config.BindSection("KernelMemory:Services:LLamaSharp:TextEmbeddingGeneration", llamaSharpConfig);

        return builder.WithLLamaSharpTextEmbeddingGeneration(llamaSharpConfig);
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextGeneration(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        ModelParams modelParams = CreateModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!, false);
        config.BindSection("KernelMemory:Services:LLamaSharp:TextGeneration", modelParams);

        var weights = LLamaWeights.LoadFromFile(modelParams);
        var context = weights.CreateContext(modelParams);
        StatelessExecutor executor = new StatelessExecutor(weights, modelParams);

        return builder.AddSingleton<ITextGenerator>(new CustomLlamaSharpTextGenerator(weights, context, executor, s_InferenceParams));
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpTextGenerationAsConfig(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        LLamaSharpConfig llamaSharpConfig = CreateLLamaSharpConfig(
            config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!,
            s_InferenceParams);

        config.BindSection("KernelMemory:Services:LLamaSharp:TextGeneration", llamaSharpConfig);

        return builder.AddSingleton<ITextGenerator>(new CustomLlamaSharpTextGenerator(llamaSharpConfig));
    }

    /// <summary>
    /// Использовать локальную LLM для генерации текста.
    /// ОСТОРОЖНО. Не использует реализацию <see cref="CustomLlamaSharpTextGenerator"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static IKernelMemoryBuilder UseLLamaSharpDefaults(this IKernelMemoryBuilder builder, IConfiguration config)
    {
        LLamaSharpConfig llamaSharpConfig = CreateLLamaSharpConfig(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!);
        ModelParams modelParams = CreateModelParams(config.GetValue<string>("KernelMemory:Services:LLamaSharp:TextGeneration:ModelPath")!, true);

        var weights = LLamaWeights.LoadFromFile(modelParams);
        var context = weights.CreateContext(modelParams);
        var executor = new StatelessExecutor(weights, modelParams);

        return builder
            .WithLLamaSharpTextEmbeddingGeneration(new LLamaSharpTextEmbeddingGenerator(llamaSharpConfig, weights))
            .AddSingleton<ITextGenerator>(new CustomLlamaSharpTextGenerator(weights, context, executor, s_InferenceParams));
    }
}
