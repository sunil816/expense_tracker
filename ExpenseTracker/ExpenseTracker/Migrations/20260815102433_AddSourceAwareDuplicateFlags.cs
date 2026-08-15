using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceAwareDuplicateFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transactions_duplicate_detection",
                table: "transactions");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "document_imports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM document_imports document_import
                        WHERE NOT EXISTS (
                                     SELECT 1 FROM transactions tx
                                     WHERE tx.document_import_id = document_import.id)
                           OR EXISTS (
                                     SELECT 1 FROM transactions tx
                                     WHERE tx.document_import_id = document_import.id
                                        AND tx.source_format NOT IN ('PaymentExport', 'OrderHistory', 'BankStatement'))
                                    OR (SELECT COUNT(DISTINCT tx.source_format)
                                         FROM transactions tx
                                         WHERE tx.document_import_id = document_import.id) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot infer one provider for every existing document import.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE document_imports document_import
                SET provider = source.provider
                FROM (
                    SELECT tx.document_import_id,
                        CASE MIN(tx.source_format)
                            WHEN 'PaymentExport' THEN 'SuperMoney'
                            WHEN 'OrderHistory' THEN 'Instamart'
                            WHEN 'BankStatement' THEN 'BankStatement'
                        END AS provider
                    FROM transactions tx
                    GROUP BY tx.document_import_id
                ) source
                WHERE document_import.id = source.document_import_id;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM document_imports WHERE provider IS NULL) THEN
                        RAISE EXCEPTION 'Provider backfill left one or more document imports unresolved.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "provider",
                table: "document_imports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "transaction_duplicate_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matched_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_duplicate_flags", x => x.id);
                    table.CheckConstraint("ck_transaction_duplicate_flags_not_self", "transaction_id <> matched_transaction_id");
                    table.ForeignKey(
                        name: "FK_transaction_duplicate_flags_transactions_matched_transactio~",
                        column: x => x.matched_transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transaction_duplicate_flags_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_duplicate_flags_matched_transaction_id",
                table: "transaction_duplicate_flags",
                column: "matched_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_duplicate_flags_transaction_id",
                table: "transaction_duplicate_flags",
                column: "transaction_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_duplicate_flags");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "document_imports");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_duplicate_detection",
                table: "transactions",
                columns: new[] { "transaction_date", "amount", "direction", "external_reference" });
        }
    }
}
