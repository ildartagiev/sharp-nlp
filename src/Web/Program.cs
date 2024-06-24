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

builder.AddKernelMemory(kernelMemoryBuilder =>
{
    switch (builder.Configuration.GetValue<string>("LLM"))
    {
        case "openai":
            kernelMemoryBuilder.UseOpenAI(builder.Configuration);
            break;
        case "lmstudio":
            kernelMemoryBuilder
                .UseOpenAITextEmbeddingGeneration(builder.Configuration)
                .UseLMStudioTextGeneration(builder.Configuration);
            break;
        case "llamasharp":
            kernelMemoryBuilder.UseLLamaSharpDefaults(builder.Configuration);
            break;
        default:
            throw new NotImplementedException();
    }

    kernelMemoryBuilder
        .UseSimpleStorage(builder.Configuration)
        .UseCustomTextPartitioningOptions(builder.Configuration)
        .UseSearchClientConfig(builder.Configuration)
        .WithCustomSearchClient<SearchClientNlp>()
        .WithCustomPromptProvider<PromptProvider>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseRouting();
app.MapControllers();

app.Run();
