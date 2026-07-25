using System.Text;
using ExpenseTracker.Options;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace ExpenseTracker.Services;

public sealed class PdfPreparationService(IOptions<DocumentExtractionOptions> options) : IPdfPreparationService
{
    private readonly DocumentExtractionOptions options = options.Value;

    public async Task<PreparedPdf> PrepareAsync(IFormFile file, string? password, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > options.MaximumFileSizeBytes)
        {
            throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file is empty or exceeds the configured size limit.");
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "expense-tracker", Path.GetRandomFileName());
        Directory.CreateDirectory(stagingDirectory);
        var inputPath = Path.Combine(stagingDirectory, Path.GetRandomFileName());
        var outputPath = Path.Combine(stagingDirectory, Path.GetRandomFileName());

        try
        {
            await using (var input = File.Create(inputPath))
            {
                await file.CopyToAsync(input, cancellationToken);
            }

            var signature = new byte[5];
            await using (var signatureStream = File.OpenRead(inputPath))
            {
                if (await signatureStream.ReadAsync(signature, cancellationToken) != signature.Length || Encoding.ASCII.GetString(signature) != "%PDF-")
                {
                    throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file is not a valid PDF.");
                }
            }

            using (var source = OpenPdf(inputPath, password))
            {
                using var destination = new PdfDocument();
                foreach (var page in source.Pages)
                {
                    destination.AddPage(page);
                }

                destination.Save(outputPath);
            }

            File.Delete(inputPath);
            return new PreparedPdf(outputPath, stagingDirectory);
        }
        catch (DocumentExtractionException)
        {
            await DeleteStagingDirectoryAsync(stagingDirectory);
            throw;
        }
        catch (PdfReaderException exception)
        {
            await DeleteStagingDirectoryAsync(stagingDirectory);
            throw new DocumentExtractionException(DocumentExtractionError.InvalidPasswordOrPdf, "The PDF could not be opened with the supplied password.", exception);
        }
        catch (InvalidOperationException exception)
        {
            await DeleteStagingDirectoryAsync(stagingDirectory);
            throw new DocumentExtractionException(DocumentExtractionError.InvalidPasswordOrPdf, "The PDF could not be opened.", exception);
        }
        catch
        {
            await DeleteStagingDirectoryAsync(stagingDirectory);
            throw;
        }
    }

    private static PdfDocument OpenPdf(string path, string? password)
    {
        return string.IsNullOrEmpty(password)
            ? PdfReader.Open(path, PdfDocumentOpenMode.Import)
            : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
    }

    private static Task DeleteStagingDirectoryAsync(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.CompletedTask;
    }
}