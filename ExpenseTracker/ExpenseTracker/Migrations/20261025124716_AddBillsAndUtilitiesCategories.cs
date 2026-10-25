using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExpenseTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddBillsAndUtilitiesCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "kind", "name", "parent_category_id", "slug" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000022"), "Expense", "Bills & Utilities", null, "bills-utilities" },
                    { new Guid("10000000-0000-0000-0000-000000000023"), "Expense", "Mobile", new Guid("10000000-0000-0000-0000-000000000022"), "mobile" },
                    { new Guid("10000000-0000-0000-0000-000000000024"), "Expense", "Internet", new Guid("10000000-0000-0000-0000-000000000022"), "internet" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000022"));
        }
    }
}
