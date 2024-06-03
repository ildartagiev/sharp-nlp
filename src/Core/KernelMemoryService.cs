using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace SharpNlp.Core;

public class KernelMemoryService
{
    private readonly IKernelMemory _memory;
    private readonly ILogger<KernelMemoryService> _logger;

    public KernelMemoryService(IKernelMemory memory,
        ILogger<KernelMemoryService> logger)
    {
        _memory = memory;
        _logger = logger;
    }

    public async Task ImportTextAsync(string text, string documentId)
    {
        var status = await _memory.GetDocumentStatusAsync(documentId);

        if (status is null)
        {
            _logger.LogInformation("Импортируем текст.");
            await _memory.ImportTextAsync(text, documentId);
            _logger.LogInformation("Текст импортирован.");
        }
        else
            _logger.LogInformation("Текст уже импортирован.");
    }

    public async Task ImportDocumentAsync(string filePath, string documentId)
    {
        FileStream? file = null;
        try
        {
            file = File.OpenRead(filePath);
            var fileName = file.Name.Substring(file.Name.LastIndexOf('\\') + 1);

            await ImportDocumentAsync(file, fileName, documentId);
        }
        finally
        {
            file?.Dispose();
        }
    }

    public async Task ImportDocumentAsync(Stream content, string fileName, string documentId)
    {
        var status = await _memory.GetDocumentStatusAsync(documentId);

        if (status is null)
        {
            _logger.LogInformation("Импортируем документ.");
            await _memory.ImportDocumentAsync(content, fileName, documentId).ConfigureAwait(false);
            _logger.LogInformation("Документ импортирован.");
        }
        else
            _logger.LogInformation("Документ уже импортирован.");
    }

    public Task DeleteDocumentAsync(string documentId)
        => _memory.DeleteDocumentAsync(documentId);

    public Task<bool> IsDocumentReadyAsync(string docuemntId)
        => _memory.IsDocumentReadyAsync(docuemntId);

    public Task<DataPipelineStatus?> GetDocumentStatusAsync(string documentId)
        => _memory.GetDocumentStatusAsync(documentId);

    public Task<MemoryAnswer> Ner(string documentId, string question = "Please extract the entities from the given document and provide them in the required output format.")
    {
        return _memory.AskAsync(
            question: question,
            filter: MemoryFilters.ByDocument(documentId));
    }

    public Task<MemoryAnswer> Classifier(string documentId, string question = "Классифицируй данный документ и приведи результат в нужном формате.")
    {
        return _memory.AskAsync(question, filter: MemoryFilters.ByDocument(documentId));
    }

    public Task<MemoryAnswer> Nlp(string documentId, string question = "Process this document and provide the result in the required output format.")
    {
        return _memory.AskAsync(question: question, filter: MemoryFilters.ByDocument(documentId));
    }
}
