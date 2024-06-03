namespace SharpNlp.Web;

public record DocumentUploadRequest(
    IFormFile File,
    string DocumentId,
    string Index = "default");
