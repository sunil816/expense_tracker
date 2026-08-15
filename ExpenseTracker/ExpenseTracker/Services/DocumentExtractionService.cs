using System.Security.Cryptography;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Options;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Services;

public sealed class DocumentExtractionService(
    IPdfPreparationService pdfPreparationService,
    IDocumentExtractionProvider extractionProvider,
    TransactionResultParser transactionResultParser,
    DocumentImportService documentImportService,
    SemaphoreSlim extractionSlots,
    IOptions<DocumentExtractionOptions> options)
{
    private readonly long maximumFileSizeBytes = options.Value.MaximumFileSizeBytes;

    public async Task<TransactionImportResult> ExtractAndImportAsync(
        Stream input,
        string? password,
        CancellationToken cancellationToken)
    {
        MemoryStream? bufferedInput = null;
        var workingInput = input;

        try
        {
            if (!workingInput.CanSeek)
            {
                bufferedInput = await BufferInputAsync(workingInput, cancellationToken);
                workingInput = bufferedInput;
            }

            if (workingInput.Length == 0 || workingInput.Length > maximumFileSizeBytes)
            {
                throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file is empty or exceeds the configured size limit.");
            }

            workingInput.Position = 0;
            var contentHash = Convert.ToHexString(await SHA256.HashDataAsync(workingInput, cancellationToken)).ToLowerInvariant();
            workingInput.Position = 0;

            var existingImport = await documentImportService.FindAsync(contentHash, cancellationToken);
            if (existingImport is not null)
            {
                return existingImport;
            }

            if (!await extractionSlots.WaitAsync(TimeSpan.Zero, cancellationToken))
            {
                throw new DocumentExtractionException(DocumentExtractionError.Busy, "Extraction capacity is currently busy.");
            }

            try
            {
                await using var preparedPdf = await pdfPreparationService.PrepareAsync(workingInput, password, cancellationToken);
                var result = await extractionProvider.ExtractAsync(preparedPdf.Path, cancellationToken);
                var transactions = transactionResultParser.Parse(result.Body, preparedPdf.HyperlinksByPage);
                if (transactions.Count == 0)
                {
                    throw new DocumentExtractionException(DocumentExtractionError.NoSupportedTransactionData, "No supported transaction data was found.");
                }

                var provider = GetProvider(transactions);
                return await documentImportService.SaveAsync(contentHash, transactions, provider, cancellationToken);
            }
            finally
            {
                extractionSlots.Release();
            }
        }
        finally
        {
            bufferedInput?.Dispose();
        }
    }

    private static SourceProvider GetProvider(IReadOnlyList<ExtractedTransaction> transactions)
    {
        var providers = transactions
            .Select(transaction => transaction.SourceFormat switch
            {
                SourceDocumentFormat.PaymentExport => SourceProvider.SuperMoney,
                SourceDocumentFormat.OrderHistory => SourceProvider.Instamart,
                SourceDocumentFormat.BankStatement => SourceProvider.BankStatement,
                _ => throw new DocumentExtractionException(
                    DocumentExtractionError.NoSupportedTransactionData,
                    "The extraction contains an unsupported source format.")
            })
            .Distinct()
            .ToArray();

        return providers.Length == 1
            ? providers[0]
            : throw new DocumentExtractionException(
                DocumentExtractionError.NoSupportedTransactionData,
                "An uploaded document must contain one source provider.");
    }

    private async Task<MemoryStream> BufferInputAsync(Stream input, CancellationToken cancellationToken)
    {
        var bufferedInput = new MemoryStream();
        var buffer = new byte[81920];
        var totalBytes = 0L;

        try
        {
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    bufferedInput.Position = 0;
                    return bufferedInput;
                }

                totalBytes += bytesRead;
                if (totalBytes > maximumFileSizeBytes)
                {
                    throw new DocumentExtractionException(DocumentExtractionError.InvalidFile, "The uploaded file exceeds the configured size limit.");
                }

                await bufferedInput.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        catch
        {
            bufferedInput.Dispose();
            throw;
        }
    }
}