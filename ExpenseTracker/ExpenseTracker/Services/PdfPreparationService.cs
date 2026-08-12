using System.Diagnostics;
using System.Text;
using ExpenseTracker.Options;
using ICSharpCode.SharpZipLib;
using Microsoft.Extensions.Options;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;

namespace ExpenseTracker.Services;

public sealed class PdfPreparationService(IOptions<DocumentExtractionOptions> options, IOptions<PdfDecryptionOptions> decryptionOptions) : IPdfPreparationService
{
    private readonly DocumentExtractionOptions options = options.Value;
    private readonly PdfDecryptionOptions decryptionOptions = decryptionOptions.Value;

    public async Task<PreparedPdf> PrepareAsync(Stream input, string? password, CancellationToken cancellationToken)
    {
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "expense-tracker", Path.GetRandomFileName());
        Directory.CreateDirectory(stagingDirectory);
        var inputPath = Path.Combine(stagingDirectory, Path.GetRandomFileName());
        var outputPath = Path.Combine(stagingDirectory, Path.GetRandomFileName());

        try
        {
            await using (var output = File.Create(inputPath))
            {
                await CopyInputAsync(input, output, cancellationToken);
            }

            var signature = new byte[5];
            await using (var signatureStream = File.OpenRead(inputPath))
            {
                if (await signatureStream.ReadAsync(signature, cancellationToken) != signature.Length || Encoding.ASCII.GetString(signature) != "%PDF-")
                {
                    throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file is not a valid PDF.");
                }
            }

            if (!string.IsNullOrEmpty(password))
            {
                var decryptedPath = Path.Combine(stagingDirectory, Path.GetRandomFileName());
                await DecryptWithQpdfAsync(inputPath, decryptedPath, password, cancellationToken);
                File.Delete(inputPath);
                inputPath = decryptedPath;
                password = null;
            }

            IReadOnlyDictionary<int, IReadOnlyList<string>> hyperlinksByPage;
            using (var source = OpenPdf(inputPath, password))
            {
                hyperlinksByPage = ExtractHyperlinks(source);
                using var destination = new PdfDocument();
                foreach (var page in source.Pages)
                {
                    destination.AddPage(page);
                }

                destination.Save(outputPath);
            }

            File.Delete(inputPath);
            return new PreparedPdf(outputPath, stagingDirectory, hyperlinksByPage);
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
        catch (SharpZipBaseException exception)
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

    private async Task CopyInputAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var totalBytes = 0L;

        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;
            if (totalBytes > options.MaximumFileSizeBytes)
            {
                throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file exceeds the configured size limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        if (totalBytes == 0)
        {
            throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file is empty.");
        }
    }

    private static PdfDocument OpenPdf(string path, string? password)
    {
        return string.IsNullOrEmpty(password)
            ? PdfReader.Open(path, PdfDocumentOpenMode.Import)
            : PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<string>> ExtractHyperlinks(PdfDocument document)
    {
        var hyperlinksByPage = new Dictionary<int, IReadOnlyList<string>>();
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            var annotations = page.Elements.GetArray("/Annots");
            if (annotations is null)
            {
                continue;
            }

            var hyperlinks = new List<(double Top, string Uri)>();
            for (var annotationIndex = 0; annotationIndex < annotations.Elements.Count; annotationIndex++)
            {
                var annotation = annotations.Elements[annotationIndex] switch
                {
                    PdfReference reference => reference.Value as PdfDictionary,
                    PdfDictionary dictionary => dictionary,
                    _ => null
                };
                if (annotation is null)
                {
                    continue;
                }

                var action = annotation.Elements.GetDictionary("/A");
                var uri = action?.Elements.GetString("/URI");
                if (string.IsNullOrWhiteSpace(uri)
                    || !Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)
                    || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
                {
                    continue;
                }

                var rectangle = annotation.Elements.GetRectangle("/Rect");
                hyperlinks.Add((Math.Max(rectangle.Y1, rectangle.Y2), uri));
            }

            if (hyperlinks.Count > 0)
            {
                hyperlinksByPage[pageIndex + 1] = hyperlinks
                    .OrderByDescending(hyperlink => hyperlink.Top)
                    .Select(hyperlink => hyperlink.Uri)
                    .ToArray();
            }
        }

        return hyperlinksByPage;
    }

    private async Task DecryptWithQpdfAsync(string inputPath, string decryptedPath, string password, CancellationToken cancellationToken)
    {
        var passwordFilePath = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetRandomFileName());

        try
        {
            await File.WriteAllTextAsync(passwordFilePath, password, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = decryptionOptions.QpdfExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add($"--password-file={passwordFilePath}");
            startInfo.ArgumentList.Add("--decrypt");
            startInfo.ArgumentList.Add("--object-streams=disable");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(decryptedPath);

            using var process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                throw new DocumentExtractionException(DocumentExtractionError.DecryptionUnavailable, "The PDF decryption tool is not available.", exception);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(decryptionOptions.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKillProcess(process);
                throw new DocumentExtractionException(DocumentExtractionError.DecryptionUnavailable, "The PDF decryption tool timed out.");
            }

            // qpdf exit codes: 0 = success, 3 = success with warnings. Anything else indicates a wrong password or an unreadable/corrupt PDF.
            if (process.ExitCode != 0 && process.ExitCode != 3)
            {
                throw new DocumentExtractionException(DocumentExtractionError.InvalidPasswordOrPdf, "The PDF could not be opened with the supplied password.");
            }
        }
        finally
        {
            TryDeleteFile(passwordFilePath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
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