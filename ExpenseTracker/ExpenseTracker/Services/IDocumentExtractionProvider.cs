namespace ExpenseTracker.Services;

public interface IDocumentExtractionProvider
{
    Task<ExtractionResult> ExtractAsync(string pdfPath, CancellationToken cancellationToken);
}

public sealed record ExtractionResult(byte[] Body, string ContentType);

public enum DocumentExtractionError
{
    InvalidFile,
    InvalidPasswordOrPdf,
    ProviderUnavailable,
    ProviderFailed,
    ProviderResponseTooLarge,
    DeadlineExceeded,
    Cancelled,
    Busy
}

public sealed class DocumentExtractionException : Exception
{
    public DocumentExtractionException(DocumentExtractionError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public DocumentExtractionError Error { get; }
}