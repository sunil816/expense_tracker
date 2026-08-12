using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_imports", x => x.id);
                    table.CheckConstraint("ck_document_imports_content_hash_length", "length(content_hash) = 64");
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_position = table.Column<int>(type: "integer", nullable: false),
                    source_sequence = table.Column<int>(type: "integer", nullable: true),
                    source_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    account_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_url = table.Column<string>(type: "text", nullable: true),
                    line_extraction_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.CheckConstraint("ck_transactions_amount_non_negative", "amount >= 0");
                    table.CheckConstraint("ck_transactions_import_position_non_negative", "import_position >= 0");
                    table.ForeignKey(
                        name: "FK_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_document_imports_document_import_id",
                        column: x => x.document_import_id,
                        principalTable: "document_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    line_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    unit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_lines", x => x.id);
                    table.CheckConstraint("ck_transaction_lines_amount_non_negative", "amount >= 0");
                    table.CheckConstraint("ck_transaction_lines_position_non_negative", "position >= 0");
                    table.CheckConstraint("ck_transaction_lines_quantity_non_negative", "quantity IS NULL OR quantity >= 0");
                    table.CheckConstraint("ck_transaction_lines_unit_amount_non_negative", "unit_amount IS NULL OR unit_amount >= 0");
                    table.ForeignKey(
                        name: "FK_transaction_lines_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "kind", "name", "parent_category_id", "slug" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Expense", "Investment", null, "investment" },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "Expense", "Food", null, "food" },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "Expense", "Grocery", null, "grocery" },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "Expense", "Rent", null, "rent" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "Income", "Salary", null, "salary" },
                    { new Guid("10000000-0000-0000-0000-000000000012"), "Expense", "Travel", null, "travel" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Expense", "MF", new Guid("10000000-0000-0000-0000-000000000001"), "mutual-fund" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Expense", "PPF", new Guid("10000000-0000-0000-0000-000000000001"), "ppf" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Expense", "FD", new Guid("10000000-0000-0000-0000-000000000001"), "fixed-deposit" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Expense", "Bonds", new Guid("10000000-0000-0000-0000-000000000001"), "bonds" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Expense", "Stocks", new Guid("10000000-0000-0000-0000-000000000001"), "stocks" },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "Expense", "Gold", new Guid("10000000-0000-0000-0000-000000000001"), "gold" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_category_id",
                table: "categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_imports_content_hash",
                table: "document_imports",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transaction_lines_transaction_position",
                table: "transaction_lines",
                columns: new[] { "transaction_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_document_import_id",
                table: "transactions",
                column: "document_import_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_duplicate_detection",
                table: "transactions",
                columns: new[] { "transaction_date", "amount", "direction", "external_reference" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_external_reference",
                table: "transactions",
                column: "external_reference");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_import_position",
                table: "transactions",
                columns: new[] { "document_import_id", "import_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_transaction_date",
                table: "transactions",
                column: "transaction_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_lines");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "document_imports");
        }
    }
}
