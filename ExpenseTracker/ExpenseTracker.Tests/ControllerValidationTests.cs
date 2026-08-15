using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Controllers;
using ExpenseTracker.Models.Reports;
using ExpenseTracker.Models.Transactions;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class ControllerValidationTests
{
    [Fact]
    public async Task TransactionsListRejectsFromAfterTo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateTransactionsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.ListAsync(new TransactionListQuery
        {
            From = new DateOnly(2026, 8, 10),
            To = new DateOnly(2026, 8, 1)
        }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("The date range is invalid.", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    [Fact]
    public async Task TransactionsListRejectsCategoryIdCombinedWithUncategorized()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateTransactionsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.ListAsync(new TransactionListQuery
        {
            CategoryId = Guid.NewGuid(),
            Uncategorized = true
        }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("The filter combination is invalid.", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    [Fact]
    public async Task DuplicateDecisionRequiresHttps()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateTransactionsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.DecideDuplicateFlagAsync(
            Guid.NewGuid(),
            new DuplicateFlagDecisionRequest { State = ExpenseTracker.Models.Persistence.DuplicateFlagState.Confirmed },
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("HTTPS is required.", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    [Fact]
    public async Task ReportsSpendingRejectsMissingGranularity()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateReportsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.GetSpendingAsync(new SpendingReportQuery(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("The granularity is invalid.", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    [Fact]
    public async Task ReportsSpendingRejectsUnrecognizedGranularity()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateReportsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.GetSpendingAsync(
            new SpendingReportQuery { Granularity = "year" }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task ReportsSpendingRejectsFromAfterTo()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = CreateReportsController(database);

        var result = Assert.IsType<ObjectResult>(await controller.GetSpendingAsync(
            new SpendingReportQuery
            {
                Granularity = "month",
                From = new DateOnly(2026, 8, 1),
                To = new DateOnly(2026, 7, 1)
            },
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("The date range is invalid.", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    private static TransactionsController CreateTransactionsController(TestDatabase database) =>
        new(database.Services.GetRequiredService<TransactionService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ReportsController CreateReportsController(TestDatabase database) =>
        new(database.Services.GetRequiredService<ReportService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
