using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class SplitOriginAndSourceFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_import_position_non_negative",
                table: "transactions");

            migrationBuilder.AlterColumn<int>(
                name: "import_position",
                table: "transactions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_import_id",
                table: "transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.RenameColumn(
                name: "source_kind",
                table: "transactions",
                newName: "source_format");

            migrationBuilder.AlterColumn<string>(
                name: "source_format",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)");

            migrationBuilder.Sql("UPDATE transactions SET source_format = 'PaymentExport' WHERE source_format = 'Payment'");
            migrationBuilder.Sql("UPDATE transactions SET source_format = 'OrderHistory' WHERE source_format = 'Order'");

            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("UPDATE transactions SET origin = 'Imported'");

            migrationBuilder.AlterColumn<string>(
                name: "origin",
                table: "transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_import_position_non_negative",
                table: "transactions",
                sql: "import_position IS NULL OR import_position >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_provenance",
                table: "transactions",
                sql: "(origin = 'Manual' AND document_import_id IS NULL AND import_position IS NULL AND source_format IS NULL) OR (origin = 'Imported' AND document_import_id IS NOT NULL AND import_position IS NOT NULL AND source_format IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_import_position_non_negative",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_provenance",
                table: "transactions");

            migrationBuilder.Sql("DELETE FROM transactions WHERE origin = 'Manual'");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "transactions");

            migrationBuilder.Sql("UPDATE transactions SET source_format = 'Payment' WHERE source_format = 'PaymentExport'");
            migrationBuilder.Sql("UPDATE transactions SET source_format = 'Order' WHERE source_format = 'OrderHistory'");

            migrationBuilder.RenameColumn(
                name: "source_format",
                table: "transactions",
                newName: "source_kind");

            migrationBuilder.AlterColumn<string>(
                name: "source_kind",
                table: "transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "import_position",
                table: "transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "document_import_id",
                table: "transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_import_position_non_negative",
                table: "transactions",
                sql: "import_position >= 0");
        }
    }
}
