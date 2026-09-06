using ExpenseTracker.Models.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data;

public sealed class ExpenseTrackerDbContext(DbContextOptions<ExpenseTrackerDbContext> options) : DbContext(options)
{
    private static readonly Guid InvestmentId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly string ProvenanceConstraint =
        $"(origin = '{nameof(TransactionOrigin.Manual)}' AND document_import_id IS NULL AND import_position IS NULL AND source_format IS NULL) OR " +
        $"(origin = '{nameof(TransactionOrigin.Imported)}' AND document_import_id IS NOT NULL AND import_position IS NOT NULL AND source_format IS NOT NULL)";

    public DbSet<DocumentImport> DocumentImports => Set<DocumentImport>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ExpenseTransaction> Transactions => Set<ExpenseTransaction>();
    public DbSet<TransactionLine> TransactionLines => Set<TransactionLine>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<TransactionLineTag> TransactionLineTags => Set<TransactionLineTag>();
    public DbSet<TransactionDuplicateFlag> TransactionDuplicateFlags => Set<TransactionDuplicateFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureDocumentImport(modelBuilder.Entity<DocumentImport>());
        ConfigureCategory(modelBuilder.Entity<Category>());
        ConfigureTransaction(modelBuilder.Entity<ExpenseTransaction>());
        ConfigureTransactionLine(modelBuilder.Entity<TransactionLine>());
        ConfigureTag(modelBuilder.Entity<Tag>());
        ConfigureTransactionTag(modelBuilder.Entity<TransactionTag>());
        ConfigureTransactionLineTag(modelBuilder.Entity<TransactionLineTag>());
        ConfigureTransactionDuplicateFlag(modelBuilder.Entity<TransactionDuplicateFlag>());
    }

    private static void ConfigureDocumentImport(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<DocumentImport> entity)
    {
        entity.ToTable("document_imports", table =>
            table.HasCheckConstraint("ck_document_imports_content_hash_length", "length(content_hash) = 64"));
        entity.HasKey(import => import.Id);
        entity.Property(import => import.Id).HasColumnName("id");
        entity.Property(import => import.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        entity.Property(import => import.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamp with time zone");
        entity.Property(import => import.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.HasIndex(import => import.ContentHash).IsUnique().HasDatabaseName("ix_document_imports_content_hash");
    }

    private static void ConfigureCategory(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> entity)
    {
        entity.ToTable("categories");
        entity.HasKey(category => category.Id);
        entity.Property(category => category.Id).HasColumnName("id");
        entity.Property(category => category.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        entity.Property(category => category.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.Property(category => category.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        entity.Property(category => category.ParentCategoryId).HasColumnName("parent_category_id");
        entity.HasIndex(category => category.Slug).IsUnique().HasDatabaseName("ix_categories_slug");
        entity.HasOne(category => category.ParentCategory)
            .WithMany(category => category.Children)
            .HasForeignKey(category => category.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasData(CategorySeeds());
    }

    private static void ConfigureTransaction(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ExpenseTransaction> entity)
    {
        entity.ToTable("transactions", table =>
        {
            table.HasCheckConstraint("ck_transactions_amount_non_negative", "amount >= 0");
            table.HasCheckConstraint("ck_transactions_import_position_non_negative", "import_position IS NULL OR import_position >= 0");
            table.HasCheckConstraint("ck_transactions_provenance", ProvenanceConstraint);
        });
        entity.HasKey(transaction => transaction.Id);
        entity.Property(transaction => transaction.Id).HasColumnName("id");
        entity.Property(transaction => transaction.Origin).HasColumnName("origin").HasConversion<string>().HasMaxLength(20);
        entity.Property(transaction => transaction.DocumentImportId).HasColumnName("document_import_id");
        entity.Property(transaction => transaction.ImportPosition).HasColumnName("import_position");
        entity.Property(transaction => transaction.SourceSequence).HasColumnName("source_sequence");
        entity.Property(transaction => transaction.SourceFormat).HasColumnName("source_format").HasConversion<string>().HasMaxLength(30);
        entity.Property(transaction => transaction.TransactionDate).HasColumnName("transaction_date");
        entity.Property(transaction => transaction.Description).HasColumnName("description").IsRequired();
        entity.Property(transaction => transaction.Note).HasColumnName("note").HasMaxLength(2000);
        entity.Property(transaction => transaction.AccountLabel).HasColumnName("account_label").HasMaxLength(200);
        entity.Property(transaction => transaction.ExternalReference).HasColumnName("external_reference").HasMaxLength(200);
        entity.Property(transaction => transaction.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(10);
        entity.Property(transaction => transaction.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(10);
        entity.Property(transaction => transaction.Amount).HasColumnName("amount").HasPrecision(18, 2);
        entity.Property(transaction => transaction.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        entity.Property(transaction => transaction.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2);
        entity.Property(transaction => transaction.CategoryId).HasColumnName("category_id");
        entity.Property(transaction => transaction.ReceiptUrl).HasColumnName("receipt_url");
        entity.Property(transaction => transaction.LineExtractionStatus).HasColumnName("line_extraction_status").HasConversion<string>().HasMaxLength(20);
        entity.HasOne(transaction => transaction.DocumentImport)
            .WithMany(import => import.Transactions)
            .HasForeignKey(transaction => transaction.DocumentImportId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(transaction => transaction.Category)
            .WithMany(category => category.Transactions)
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasIndex(transaction => new { transaction.DocumentImportId, transaction.ImportPosition })
            .IsUnique()
            .HasDatabaseName("ix_transactions_import_position");
        entity.HasIndex(transaction => transaction.TransactionDate).HasDatabaseName("ix_transactions_transaction_date");
        entity.HasIndex(transaction => transaction.CategoryId).HasDatabaseName("ix_transactions_category_id");
        entity.HasIndex(transaction => transaction.DocumentImportId).HasDatabaseName("ix_transactions_document_import_id");
        entity.HasIndex(transaction => transaction.ExternalReference).HasDatabaseName("ix_transactions_external_reference");
    }

    private static void ConfigureTransactionDuplicateFlag(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TransactionDuplicateFlag> entity)
    {
        entity.ToTable("transaction_duplicate_flags", table =>
            table.HasCheckConstraint("ck_transaction_duplicate_flags_not_self", "transaction_id <> matched_transaction_id"));
        entity.HasKey(flag => flag.Id);
        entity.Property(flag => flag.Id).HasColumnName("id");
        entity.Property(flag => flag.TransactionId).HasColumnName("transaction_id");
        entity.Property(flag => flag.MatchedTransactionId).HasColumnName("matched_transaction_id");
        entity.Property(flag => flag.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30).IsRequired();
        entity.Property(flag => flag.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20).IsRequired();
        entity.Property(flag => flag.SuggestedAt).HasColumnName("suggested_at").HasColumnType("timestamp with time zone");
        entity.Property(flag => flag.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamp with time zone");
        entity.HasOne(flag => flag.Transaction)
            .WithMany(transaction => transaction.DuplicateFlags)
            .HasForeignKey(flag => flag.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(flag => flag.MatchedTransaction)
            .WithMany(transaction => transaction.MatchedByDuplicateFlags)
            .HasForeignKey(flag => flag.MatchedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(flag => flag.TransactionId)
            .IsUnique()
            .HasDatabaseName("ix_transaction_duplicate_flags_transaction_id");
        entity.HasIndex(flag => flag.MatchedTransactionId)
            .HasDatabaseName("ix_transaction_duplicate_flags_matched_transaction_id");
    }

    private static void ConfigureTransactionLine(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TransactionLine> entity)
    {
        entity.ToTable("transaction_lines", table =>
        {
            table.HasCheckConstraint("ck_transaction_lines_position_non_negative", "position >= 0");
            table.HasCheckConstraint("ck_transaction_lines_quantity_non_negative", "quantity IS NULL OR quantity >= 0");
            table.HasCheckConstraint("ck_transaction_lines_unit_amount_non_negative", "unit_amount IS NULL OR unit_amount >= 0");
            table.HasCheckConstraint("ck_transaction_lines_amount_non_negative", "amount >= 0");
        });
        entity.HasKey(line => line.Id);
        entity.Property(line => line.Id).HasColumnName("id");
        entity.Property(line => line.TransactionId).HasColumnName("transaction_id");
        entity.Property(line => line.Position).HasColumnName("position");
        entity.Property(line => line.LineType).HasColumnName("line_type").HasConversion<string>().HasMaxLength(20);
        entity.Property(line => line.Description).HasColumnName("description").IsRequired();
        entity.Property(line => line.Quantity).HasColumnName("quantity").HasPrecision(12, 3);
        entity.Property(line => line.UnitAmount).HasColumnName("unit_amount").HasPrecision(18, 2);
        entity.Property(line => line.Amount).HasColumnName("amount").HasPrecision(18, 2);
        entity.HasOne(line => line.Transaction)
            .WithMany(transaction => transaction.Lines)
            .HasForeignKey(line => line.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(line => new { line.TransactionId, line.Position })
            .IsUnique()
            .HasDatabaseName("ix_transaction_lines_transaction_position");
    }

    private static void ConfigureTag(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Tag> entity)
    {
        entity.ToTable("tags");
        entity.HasKey(tag => tag.Id);
        entity.Property(tag => tag.Id).HasColumnName("id");
        entity.Property(tag => tag.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        entity.Property(tag => tag.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        entity.HasIndex(tag => tag.Slug).IsUnique().HasDatabaseName("ix_tags_slug");
    }

    private static void ConfigureTransactionTag(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TransactionTag> entity)
    {
        entity.ToTable("transaction_tags");
        entity.HasKey(assignment => new { assignment.TransactionId, assignment.TagId });
        entity.Property(assignment => assignment.TransactionId).HasColumnName("transaction_id");
        entity.Property(assignment => assignment.TagId).HasColumnName("tag_id");
        entity.Property(assignment => assignment.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20);
        entity.Property(assignment => assignment.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20);
        entity.Property(assignment => assignment.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamp with time zone");
        entity.HasOne(assignment => assignment.Transaction)
            .WithMany(transaction => transaction.TagAssignments)
            .HasForeignKey(assignment => assignment.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(assignment => assignment.Tag)
            .WithMany(tag => tag.TransactionAssignments)
            .HasForeignKey(assignment => assignment.TagId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(assignment => assignment.TagId).HasDatabaseName("ix_transaction_tags_tag_id");
        entity.HasIndex(assignment => assignment.State).HasDatabaseName("ix_transaction_tags_state");
    }

    private static void ConfigureTransactionLineTag(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TransactionLineTag> entity)
    {
        entity.ToTable("transaction_line_tags");
        entity.HasKey(assignment => new { assignment.TransactionLineId, assignment.TagId });
        entity.Property(assignment => assignment.TransactionLineId).HasColumnName("transaction_line_id");
        entity.Property(assignment => assignment.TagId).HasColumnName("tag_id");
        entity.Property(assignment => assignment.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20);
        entity.Property(assignment => assignment.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20);
        entity.Property(assignment => assignment.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamp with time zone");
        entity.HasOne(assignment => assignment.TransactionLine)
            .WithMany(line => line.TagAssignments)
            .HasForeignKey(assignment => assignment.TransactionLineId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(assignment => assignment.Tag)
            .WithMany(tag => tag.LineAssignments)
            .HasForeignKey(assignment => assignment.TagId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(assignment => assignment.TagId).HasDatabaseName("ix_transaction_line_tags_tag_id");
        entity.HasIndex(assignment => assignment.State).HasDatabaseName("ix_transaction_line_tags_state");
    }

    private static Category[] CategorySeeds() =>
    [
        new() { Id = InvestmentId, Slug = "investment", Name = "Investment", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Slug = "mutual-fund", Name = "MF", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Slug = "ppf", Name = "PPF", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Slug = "fixed-deposit", Name = "FD", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Slug = "bonds", Name = "Bonds", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Slug = "stocks", Name = "Stocks", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), Slug = "gold", Name = "Gold", Kind = CategoryKind.Expense, ParentCategoryId = InvestmentId },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), Slug = "food", Name = "Food", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), Slug = "grocery", Name = "Grocery", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), Slug = "rent", Name = "Rent", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), Slug = "salary", Name = "Salary", Kind = CategoryKind.Income },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), Slug = "travel", Name = "Travel", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000013"), Slug = "hundi", Name = "Hundi", Kind = CategoryKind.Expense },
        new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000014"), Slug = "fuel", Name = "Fuel", Kind = CategoryKind.Expense }
    ];
}