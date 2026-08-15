using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Controllers;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class ManualTransactionTests
{
    private static readonly Guid FoodCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000008");

    [Fact]
    public async Task ManualCashEntryPersistsWithoutDocumentProvenance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<TransactionService>();

        var created = await service.CreateManualAsync(new ManualTransactionRequest
        {
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "  Cash lunch  ",
            Note = "  Lunch with team  ",
            Direction = TransactionDirection.Debit,
            Amount = 250m,
            AccountLabel = " Cash ",
            CategoryId = FoodCategoryId
        }, CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal("Cash lunch", created.Description);
        Assert.Equal("Lunch with team", created.Note);
        Assert.Equal("Cash", created.AccountLabel);
        Assert.Equal("INR", created.Currency);
        Assert.Equal(250m, created.Amount);

        await using var context = await database.CreateContextAsync();
        var stored = await context.Transactions.SingleAsync();
        Assert.Equal(TransactionOrigin.Manual, stored.Origin);
        Assert.Null(stored.DocumentImportId);
        Assert.Null(stored.ImportPosition);
        Assert.Null(stored.SourceFormat);
        Assert.Equal(LineExtractionStatus.NotApplicable, stored.LineExtractionStatus);
        Assert.Equal(FoodCategoryId, stored.CategoryId);
        Assert.Equal("Lunch with team", stored.Note);
        Assert.Empty(await context.DocumentImports.ToListAsync());
    }

    [Fact]
    public async Task UnknownCategoryIsRejectedWithoutInsertingARow()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<TransactionService>();

        var created = await service.CreateManualAsync(new ManualTransactionRequest
        {
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "Cash lunch",
            Direction = TransactionDirection.Debit,
            Amount = 250m,
            CategoryId = Guid.NewGuid()
        }, CancellationToken.None);

        Assert.Null(created);

        await using var context = await database.CreateContextAsync();
        Assert.Empty(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task ProvenanceConstraintRejectsMismatchedOriginAndDocumentColumns()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();
        var import = new DocumentImport
        {
            Id = Guid.NewGuid(),
            ContentHash = new string('c', 64),
            ImportedAt = DateTimeOffset.UtcNow
        };
        context.DocumentImports.Add(import);
        await context.SaveChangesAsync();

        context.Transactions.Add(new ExpenseTransaction
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Manual,
            DocumentImportId = import.Id,
            ImportPosition = 0,
            SourceFormat = SourceDocumentFormat.PaymentExport,
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "Manual row carrying import provenance",
            Direction = TransactionDirection.Debit,
            Amount = 10m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await using var secondContext = await database.CreateContextAsync();
        secondContext.Transactions.Add(new ExpenseTransaction
        {
            Id = Guid.NewGuid(),
            Origin = TransactionOrigin.Imported,
            DocumentImportId = import.Id,
            ImportPosition = 0,
            SourceFormat = null,
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "Imported row without a source format",
            Direction = TransactionDirection.Debit,
            Amount = 10m,
            LineExtractionStatus = LineExtractionStatus.NotApplicable
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ControllerReturnsCreatedAndRejectsUnknownCategory()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = new TransactionsController(database.Services.GetRequiredService<TransactionService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = Uri.UriSchemeHttps;

        var created = Assert.IsType<ObjectResult>(await controller.CreateAsync(new ManualTransactionRequest
        {
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "Cash lunch",
            Direction = TransactionDirection.Debit,
            Amount = 250m
        }, CancellationToken.None));
        var rejected = Assert.IsAssignableFrom<ObjectResult>(await controller.CreateAsync(new ManualTransactionRequest
        {
            TransactionDate = new DateOnly(2026, 8, 2),
            Description = "Cash lunch",
            Direction = TransactionDirection.Debit,
            Amount = 250m,
            CategoryId = Guid.NewGuid()
        }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.IsType<ManualTransactionResponse>(created.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, rejected.StatusCode);

        await using var context = await database.CreateContextAsync();
        Assert.Single(await context.Transactions.ToListAsync());
    }
}
