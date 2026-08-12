using ExpenseTracker.Data;
using ExpenseTracker.Models.Categories;
using ExpenseTracker.Models.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public sealed class CategoryService(IDbContextFactory<ExpenseTrackerDbContext> contextFactory)
{
    /// <summary>Income before Expense, parents before children within a kind, then name.</summary>
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Categories
            .AsNoTracking()
            // Explicit rank avoids ordering by the converted string column, which would sort "Expense" before "Income".
            .OrderBy(category => category.Kind == CategoryKind.Income ? 0 : 1)
            .ThenBy(category => category.ParentCategoryId == null ? 0 : 1)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Slug,
                category.Name,
                category.Kind,
                category.ParentCategoryId))
            .ToListAsync(cancellationToken);
    }
}
