using System.Runtime.CompilerServices;
using Microsoft.Extensions.Azure;
using Microsoft.KernelMemory;
using Serilog;
using SharpNlp.Core;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var kmBuilder = new KernelMemoryBuilder(builder.Services);

switch (builder.Configuration.GetValue<string>("LLM"))
{
    case "openai":
        kmBuilder.UseOpenAI(builder.Configuration);
        break;
    case "lmstudio":
        kmBuilder
            .UseOpenAITextEmbeddingGeneration(builder.Configuration)
            .UseLMStudioTextGeneration(builder.Configuration);
        break;
    case "km-llamasharp":
        kmBuilder
            .UseOpenAITextEmbeddingGeneration(builder.Configuration)
            .UseLlamaTextGeneration(builder.Configuration);
        break;
    case "llamasharp":
        kmBuilder.UseLLamaSharp(builder.Configuration);
        break;
    default:
        throw new NotImplementedException();
}

var kernelMemory = kmBuilder
    .UseRabbitMQ(builder.Configuration)
    .UseCustomTextPartitioningOptions(builder.Configuration)
    .WithCustomPromptProvider<PromptProvider>()
    .UseCustomSearchClient<SearchClient>(builder.Configuration)
    .UseSimpleStorage(builder.Configuration)
    .Build<MemoryService>();

builder.Services.AddSingleton<IKernelMemory>(kernelMemory);

//builder.Services.AddSingleton<IKernelMemory>(provider =>
//{
//    var kmBuilder = new KernelMemoryBuilder(builder.Services);

//    var config = builder.Configuration;

//    switch (config.GetValue<string>("LLM"))
//    {
//        case "openai":
//            kmBuilder.UseOpenAI(config);
//            break;
//        case "lmstudio":
//            kmBuilder
//                .UseOpenAITextEmbeddingGeneration(config)
//                .UseLMStudioTextGeneration(config);
//            break;
//        case "km-llamasharp":
//            kmBuilder
//                .UseOpenAITextEmbeddingGeneration(config)
//                .UseLlamaTextGeneration(config);
//            break;
//        case "llamasharp":
//            kmBuilder.UseLLamaSharp(config);
//            break;
//        default:
//            throw new NotImplementedException();
//    }

//    return kmBuilder
//        .UseCustomTextPartitioningOptions(config)
//        .WithCustomPromptProvider<PromptProvider>()
//        .UseCustomSearchClient<SearchClient>(config)
//        .UseSimpleStorage(config)
//        .Build<MemoryService>();
//});

//builder.AddKernelMemory(kmBuilder =>
//{
//    var config = builder.Configuration;

//    switch (config.GetValue<string>("LLM"))
//    {
//        case "openai":
//            kmBuilder.UseOpenAI(config);
//            break;
//        case "lmstudio":
//            kmBuilder
//                .UseOpenAITextEmbeddingGeneration(config)
//                .UseLMStudioTextGeneration(config);
//            break;
//        case "km-llamasharp":
//            kmBuilder
//                .UseOpenAITextEmbeddingGeneration(config)
//                .UseLlamaTextGeneration(config);
//            break;
//        case "llamasharp":
//            kmBuilder.UseLLamaSharp(config);
//            break;
//        default:
//            throw new NotImplementedException();
//    }

//    kmBuilder
//        .UseCustomTextPartitioningOptions(config)
//        .WithCustomPromptProvider<PromptProvider>()
//        .UseCustomSearchClient<SearchClient>(config)
//        .UseSimpleStorage(config);
//});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

app.UseRouting();
app.MapControllers();

app.Run();
