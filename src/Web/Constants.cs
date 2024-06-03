namespace SharpNlp.Web;

// DELETE
public static class Constants
{
    public static class Endpoints
    {
        public const string Ask = "ask";

        public const string Search = "search";

        public const string Download = "download";

        public const string Upload = "upload";

        public const string UploadStatus = "upload-status";

        public const string Documents = "documents";

        public const string Indexes = "indexes";

        public const string DeleteDocumentWithParams = "documents?index={index}&documentId={documentId}";

        public const string DeleteIndexWithParams = "indexes?index={index}";

        public const string UploadStatusWithParams = "upload-status?index={index}&documentId={documentId}";

        public const string DownloadWithParams = "download?index={index}&documentId={documentId}&filename={filename}";

        public const string IndexPlaceholder = "{index}";

        public const string DocumentIdPlaceholder = "{documentId}";

        public const string FilenamePlaceholder = "{filename}";
    }
}
