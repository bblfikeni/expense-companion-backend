using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptExtractionRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "expense_extractions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "expense_extractions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "expense_extractions");
        }
    }
}
