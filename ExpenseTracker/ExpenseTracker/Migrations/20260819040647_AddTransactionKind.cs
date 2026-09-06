using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "transactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Expense");

            migrationBuilder.Sql("UPDATE transactions SET kind = 'Income' WHERE direction = 'Credit'");
            migrationBuilder.Sql("UPDATE transactions SET kind = 'Transfer' WHERE description ~* '(\\m(atm|cash withdrawal|cash wd|fund transfer|bank transfer|neft|imps|rtgs|upi transfer|card payment)\\M)'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "transactions");
        }
    }
}
