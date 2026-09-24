using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transaction_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    suggested_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    resolved_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    suggested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_suggestions", x => x.id);
                    table.CheckConstraint("ck_transaction_suggestions_decision_timestamp", "(state = 'Suggested' AND decided_at IS NULL) OR (state <> 'Suggested' AND decided_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_transaction_suggestions_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_suggestions_state",
                table: "transaction_suggestions",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_suggestions_transaction_field",
                table: "transaction_suggestions",
                columns: new[] { "transaction_id", "field" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_suggestions");
        }
    }
}
