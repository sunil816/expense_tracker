namespace ExpenseTracker.Models.Persistence;

public enum CategoryKind
{
    Income,
    Expense
}

public enum TransactionOrigin
{
    Imported,
    Manual
}

public enum SourceProvider
{
    SuperMoney,
    Instamart,
    BankStatement
}

public enum SourceDocumentFormat
{
    PaymentExport,
    BankStatement,
    OrderHistory
}

public enum TransactionDirection
{
    Debit,
    Credit
}

public enum TransactionKind
{
    Expense,
    Income,
    Transfer
}

public enum LineExtractionStatus
{
    NotApplicable,
    Unavailable,
    Available
}

public enum TransactionLineType
{
    Product,
    Tax,
    Fee,
    Discount,
    Other
}

public enum TagAssignmentState
{
    Suggested,
    Confirmed,
    Rejected
}

public enum TagAssignmentSource
{
    Rule,
    Manual
}

public enum DuplicateMatchReason
{
    ExternalReference,
    Composite
}

public enum DuplicateFlagState
{
    Suggested,
    Confirmed,
    Rejected
}

public sealed class DocumentImport
{
    public Guid Id { get; set; }
    public required string ContentHash { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public SourceProvider Provider { get; set; }
    public List<ExpenseTransaction> Transactions { get; set; } = [];
}

public sealed class Category
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public CategoryKind Kind { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public List<Category> Children { get; set; } = [];
    public List<ExpenseTransaction> Transactions { get; set; } = [];
}

public sealed class ExpenseTransaction
{
    public Guid Id { get; set; }
    public TransactionOrigin Origin { get; set; }
    public Guid? DocumentImportId { get; set; }
    public DocumentImport? DocumentImport { get; set; }
    public int? ImportPosition { get; set; }
    public int? SourceSequence { get; set; }
    public SourceDocumentFormat? SourceFormat { get; set; }
    public DateOnly TransactionDate { get; set; }
    public required string Description { get; set; }
    public string? Note { get; set; }
    public string? AccountLabel { get; set; }
    public string? ExternalReference { get; set; }
    public TransactionDirection Direction { get; set; }
    public TransactionKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public decimal? BalanceAfter { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? ReceiptUrl { get; set; }
    public LineExtractionStatus LineExtractionStatus { get; set; }
    public List<TransactionLine> Lines { get; set; } = [];
    public List<TransactionTag> TagAssignments { get; set; } = [];
    public List<TransactionDuplicateFlag> DuplicateFlags { get; set; } = [];
    public List<TransactionDuplicateFlag> MatchedByDuplicateFlags { get; set; } = [];
}

public sealed class TransactionDuplicateFlag
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public ExpenseTransaction Transaction { get; set; } = null!;
    public Guid MatchedTransactionId { get; set; }
    public ExpenseTransaction MatchedTransaction { get; set; } = null!;
    public DuplicateMatchReason Reason { get; set; }
    public DuplicateFlagState State { get; set; }
    public DateTimeOffset SuggestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed class TransactionLine
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public ExpenseTransaction Transaction { get; set; } = null!;
    public int Position { get; set; }
    public TransactionLineType LineType { get; set; }
    public required string Description { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitAmount { get; set; }
    public decimal Amount { get; set; }
    public List<TransactionLineTag> TagAssignments { get; set; } = [];
}

public sealed class Tag
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public List<TransactionTag> TransactionAssignments { get; set; } = [];
    public List<TransactionLineTag> LineAssignments { get; set; } = [];
}

public interface ITagAssignment
{
    Guid TagId { get; set; }
    Tag Tag { get; }
    TagAssignmentState State { get; set; }
    TagAssignmentSource Source { get; set; }
    DateTimeOffset DecidedAt { get; set; }
}

public sealed class TransactionTag : ITagAssignment
{
    public Guid TransactionId { get; set; }
    public ExpenseTransaction Transaction { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public TagAssignmentState State { get; set; }
    public TagAssignmentSource Source { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

public sealed class TransactionLineTag : ITagAssignment
{
    public Guid TransactionLineId { get; set; }
    public TransactionLine TransactionLine { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public TagAssignmentState State { get; set; }
    public TagAssignmentSource Source { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
