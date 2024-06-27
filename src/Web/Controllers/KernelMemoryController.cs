using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory;
using Newtonsoft.Json;

namespace SharpNlp.Web.Controllers;

public class KernelMemoryController : ApiControllerBase
{
    private readonly ILogger<KernelMemoryController> _logger;
    private readonly IKernelMemory _kernelMemory;

    public KernelMemoryController(
        ILogger<KernelMemoryController> logger,
        IKernelMemory kernelMemory)
    {
        _logger = logger;
        _kernelMemory = kernelMemory;
    }

    [HttpPost("upload")]
    [ProducesResponseType<UploadAccepted>(StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/json")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        Stream? fileStream = null;

        try
        {
            fileStream = request.File.OpenReadStream();

            DataPipelineStatus? pipeline = await _kernelMemory.GetDocumentStatusAsync(documentId: request.DocumentId, index: request.Index, cancellationToken)
                .ConfigureAwait(false);

            if (pipeline is not null)
            {
                return Problem(detail: "Document already exists.", statusCode: 400);
            }

            await _kernelMemory.ImportDocumentAsync(
                content: fileStream,
                fileName: Uri.UnescapeDataString(request.File.FileName),
                documentId: request.DocumentId,
                index: request.Index,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return AcceptedAtAction(
                nameof(UploadStatus),
                new { request.Index, request.DocumentId },
                new UploadAccepted
                {
                    DocumentId = request.DocumentId,
                    Index = request.Index,
                    Message = "Document upload completed, ingestion pipeline started."
                });
        }
        catch (Exception e)
        {
            return Problem(title: "Document upload failed.", detail: e.Message, statusCode: 503);
        }
        finally
        {
            fileStream?.Dispose();
        }
    }

    [HttpGet("upload-status")]
    [ProducesResponseType<DataPipelineStatus>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/json")]
    public async Task<IActionResult> UploadStatus(
        [FromQuery] string? index,
        [FromQuery] string documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Problem(detail: $"'documentId' query parameter is missing or has no value.", statusCode: 400);
        }

        DataPipelineStatus? pipeline = await _kernelMemory.GetDocumentStatusAsync(documentId: documentId, index: index, cancellationToken)
            .ConfigureAwait(false);

        if (pipeline is null)
        {
            return Problem(detail: "Document not found.", statusCode: 404);
        }

        if (pipeline.Empty)
        {
            return Problem(detail: "Empty pipeline.", statusCode: 404);
        }

        return Ok(pipeline);
    }

    [HttpDelete("documents")]
    [ProducesResponseType<DeleteAccepted>(StatusCodes.Status202Accepted, "application/json")]
    public async Task<IActionResult> DeleteDocument(
        [FromQuery] string? index,
        [FromQuery] string documentId,
        CancellationToken cancellationToken)
    {
        await _kernelMemory.DeleteDocumentAsync(documentId: documentId, index: index, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return AcceptedAtAction(nameof(UploadStatus),
            new { index, documentId },
            new DeleteAccepted
            {
                DocumentId = documentId,
                Index = index ?? string.Empty,
                Message = "Document deletion request received, pipeline started."
            });
    }

    /// <summary>
    /// Метод не очень работает, не понятно как передать фильтры.
    /// Лучше переписать с более удобными аргументами как в <see cref="Nlp(string?, string?, string, CancellationToken)"/>.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("ask")]
    [ProducesResponseType<MemoryAnswer>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> Ask(
        [FromQuery] MemoryQuery query,
        CancellationToken cancellationToken)
    {
        MemoryAnswer answer = await _kernelMemory.AskAsync(
            question: query.Question,
            index: query.Index,
            filters: query.Filters,
            minRelevance: query.MinRelevance,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return Ok(answer);
    }

    [HttpGet("nlp")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> Nlp(
        [FromQuery] string? question,
        [FromQuery] string? index,
        [FromQuery][Required] string documentId,
        CancellationToken cancellationToken)
    {
        MemoryAnswer answer = await _kernelMemory.AskAsync(
            question: question ?? "",
            index: index,
            filter: MemoryFilters.ByDocument(documentId),
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (answer.NoResult)
        {
            return NotFound();
        }

        var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(answer.Result);
        return Ok(result);
    }

    /// <summary>
    /// Этот метод только для тестовых целей.
    /// </summary>
    [HttpGet("nlp-all-docs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> NlpAllDocs(
        [FromQuery] string? question,
        [FromQuery] string? index,
        CancellationToken cancellationToken)
    {
        List<Dictionary<string, List<string>>> result = new();

        MemoryAnswer answer = await _kernelMemory.AskAsync(
            question: question ?? "",
            index: index,
            filter: MemoryFilters.ByDocument("doc001"),
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        result.Add(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(answer.Result)!);

        answer = await _kernelMemory.AskAsync(
            question: question ?? "",
            index: index,
            filter: MemoryFilters.ByDocument("doc002"),
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        result.Add(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(answer.Result)!);

        answer = await _kernelMemory.AskAsync(
            question: question ?? "",
            index: index,
            filter: MemoryFilters.ByDocument("doc003"),
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        result.Add(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(answer.Result)!);

        answer = await _kernelMemory.AskAsync(
            question: question ?? "",
            index: index,
            filter: MemoryFilters.ByDocument("doc004"),
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        result.Add(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(answer.Result)!);

        return Ok(result);
    }
}
