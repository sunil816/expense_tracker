using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Controllers;
using ExpenseTracker.Data;
using ExpenseTracker.Models.Extraction;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Options;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class TransactionPersistenceTests
{
    [Fact]
    public async Task ImportPersistsNormalizedRowsAndReusesExistingHash()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<DocumentImportService>();
        var transactions = new ExtractedTransaction[]
        {
            new(null, SourceDocumentFormat.PaymentExport, new DateOnly(2026, 7, 12), "SHOP", "Kotak 4603", null, TransactionDirection.Debit, 160m, null, null),
            new(null, SourceDocumentFormat.OrderHistory, new DateOnly(2026, 5, 1), "Instamart", null, "236616081015876", TransactionDirection.Debit, 758.62m, null, "https://example.test/order.pdf")
        };
        var contentHash = new string('a', 64);

        var created = await service.SaveAsync(contentHash, transactions, CancellationToken.None);
        var duplicate = await service.SaveAsync(contentHash, [], CancellationToken.None);

        Assert.False(created.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(created.ImportId, duplicate.ImportId);
        Assert.Equal([0, 1], created.Transactions.Select(transaction => transaction.ImportPosition));
        Assert.Equal(LineExtractionStatus.NotApplicable, created.Transactions[0].LineExtractionStatus);
        Assert.Equal(LineExtractionStatus.Available, created.Transactions[1].LineExtractionStatus);
        Assert.All(created.Transactions, transaction => Assert.Equal("INR", transaction.Currency));

        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.DocumentImports.CountAsync());
        Assert.Equal(2, await context.Transactions.CountAsync());
        Assert.All(await context.Transactions.ToListAsync(), transaction =>
        {
            Assert.Equal(TransactionOrigin.Imported, transaction.Origin);
            Assert.NotNull(transaction.DocumentImportId);
            Assert.NotNull(transaction.ImportPosition);
            Assert.NotNull(transaction.SourceFormat);
        });
    }

    [Fact]
    public async Task SchemaSeedsCategoriesAndRejectsNegativeAmountsAtomically()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<DocumentImportService>();
        var invalidTransactions = new ExtractedTransaction[]
        {
            new(null, SourceDocumentFormat.PaymentExport, new DateOnly(2026, 7, 12), "SHOP", null, null, TransactionDirection.Debit, -1m, null, null)
        };

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.SaveAsync(new string('b', 64), invalidTransactions, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        Assert.Equal(15, await context.Categories.CountAsync());
        Assert.Equal(CategoryKind.Expense, (await context.Categories.SingleAsync(category => category.Slug == "hundi")).Kind);
        Assert.Equal(CategoryKind.Income, (await context.Categories.SingleAsync(category => category.Slug == "salary")).Kind);
        Assert.Equal(CategoryKind.Income, (await context.Categories.SingleAsync(category => category.Slug == "interest-income")).Kind);
        Assert.Equal(6, await context.Categories.CountAsync(category => category.ParentCategoryId != null));
        Assert.Empty(await context.DocumentImports.ToListAsync());
        Assert.Empty(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task DuplicateUploadSkipsPreparationAndBusyNewUploadFastFails()
    {
        await using var database = await TestDatabase.CreateAsync();
        var importService = database.Services.GetRequiredService<DocumentImportService>();
        var preparationService = new StubPdfPreparationService();
        var extractionProvider = new StubExtractionProvider();
        using var availableSlot = new SemaphoreSlim(1);
        var firstController = CreateController(CreateExtractionService(preparationService, extractionProvider, importService, availableSlot));

        var first = Assert.IsType<OkObjectResult>(await firstController.ExtractAsync(
            CreatePdf("%PDF-same"), null, CancellationToken.None));
        var firstImport = Assert.IsType<TransactionImportResult>(first.Value);

        using var saturatedSlot = new SemaphoreSlim(0);
        var secondController = CreateController(CreateExtractionService(preparationService, extractionProvider, importService, saturatedSlot));
        var duplicate = Assert.IsType<OkObjectResult>(await secondController.ExtractAsync(
            CreatePdf("%PDF-same"), null, CancellationToken.None));
        var duplicateImport = Assert.IsType<TransactionImportResult>(duplicate.Value);
        var busy = Assert.IsType<ObjectResult>(await secondController.ExtractAsync(
            CreatePdf("%PDF-different"), null, CancellationToken.None));

        Assert.False(firstImport.IsDuplicate);
        Assert.True(duplicateImport.IsDuplicate);
        Assert.Equal(firstImport.ImportId, duplicateImport.ImportId);
        Assert.Equal(1, preparationService.Calls);
        Assert.Equal(1, extractionProvider.Calls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, busy.StatusCode);
    }

    private static DocumentExtractionService CreateExtractionService(
        IPdfPreparationService preparationService,
        IDocumentExtractionProvider extractionProvider,
        DocumentImportService importService,
        SemaphoreSlim extractionSlots)
    {
        return new DocumentExtractionService(
            preparationService,
            extractionProvider,
            new TransactionResultParser(),
            importService,
            extractionSlots,
            Microsoft.Extensions.Options.Options.Create(new DocumentExtractionOptions()));
    }

    private static DocumentExtractionsController CreateController(DocumentExtractionService extractionService)
    {
        var controller = new DocumentExtractionsController(
            extractionService,
            NullLogger<DocumentExtractionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = Uri.UriSchemeHttps;
        return controller;
    }

    private static FormFile CreatePdf(string content)
    {
        var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private sealed class StubPdfPreparationService : IPdfPreparationService
    {
        public int Calls { get; private set; }

        public async Task<PreparedPdf> PrepareAsync(Stream input, string? password, CancellationToken cancellationToken)
        {
            Calls++;
            var directory = Path.Combine(Path.GetTempPath(), "expense-tracker-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "prepared.pdf");
            await File.WriteAllTextAsync(path, "%PDF-test", cancellationToken);
            return new PreparedPdf(path, directory, new Dictionary<int, IReadOnlyList<string>>());
        }
    }

    private sealed class StubExtractionProvider : IDocumentExtractionProvider
    {
        public int Calls { get; private set; }

        public Task<ExtractionResult> ExtractAsync(string pdfPath, CancellationToken cancellationToken)
        {
            Calls++;
            var body = Encoding.UTF8.GetBytes("""
                {
                    "document": {
                        "json_content": {
                            "tables": [{
                                "data": {
                                    "grid": [
                                        [{"text":"Name"},{"text":"Bank"},{"text":"Amount"},{"text":"Date"},{"text":"Status"}],
                                        [{"text":"SHOP"},{"text":"Kotak 4603"},{"text":"-160.00"},{"text":"12 July 2026"},{"text":"SUCCESS"}]
                                    ]
                                }
                            }]
                        },
                        "md_content": null
                    }
                }
                """);
            return Task.FromResult(new ExtractionResult(body, "application/json"));
        }
    }
}