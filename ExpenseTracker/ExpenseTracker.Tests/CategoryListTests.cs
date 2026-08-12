using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.Models.Persistence;
using ExpenseTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class CategoryListTests
{
    [Fact]
    public async Task ListReturnsSeededCategoriesWithKindsAndParents()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<CategoryService>();

        var categories = await service.ListAsync(CancellationToken.None);

        var investment = Assert.Single(categories, category => category.Slug == "investment");
        Assert.Equal(CategoryKind.Expense, investment.Kind);
        Assert.Null(investment.ParentCategoryId);

        var mutualFund = Assert.Single(categories, category => category.Slug == "mutual-fund");
        Assert.Equal(investment.Id, mutualFund.ParentCategoryId);

        var salary = Assert.Single(categories, category => category.Slug == "salary");
        Assert.Equal(CategoryKind.Income, salary.Kind);
    }

    [Fact]
    public async Task ListOrdersIncomeBeforeExpenseThenParentsBeforeChildrenThenName()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = database.Services.GetRequiredService<CategoryService>();

        var categories = await service.ListAsync(CancellationToken.None);

        var firstExpenseIndex = categories.ToList().FindIndex(category => category.Kind == CategoryKind.Expense);
        var lastIncomeIndex = categories.ToList().FindLastIndex(category => category.Kind == CategoryKind.Income);
        Assert.True(lastIncomeIndex < firstExpenseIndex, "Income categories must all precede Expense categories.");

        var investmentIndex = categories.ToList().FindIndex(category => category.Slug == "investment");
        var mutualFundIndex = categories.ToList().FindIndex(category => category.Slug == "mutual-fund");
        Assert.True(investmentIndex < mutualFundIndex, "Parent categories must precede their children.");

        var childSlugsInOrder = categories
            .Where(category => category.ParentCategoryId is not null)
            .Select(category => category.Name)
            .ToList();
        Assert.Equal(childSlugsInOrder.OrderBy(name => name, System.StringComparer.Ordinal), childSlugsInOrder);
    }
}
