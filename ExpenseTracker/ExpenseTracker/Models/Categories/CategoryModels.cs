using ExpenseTracker.Models.Persistence;

namespace ExpenseTracker.Models.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string Slug,
    string Name,
    CategoryKind Kind,
    Guid? ParentCategoryId);
