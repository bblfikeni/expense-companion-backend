using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptureRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "capture_request_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_capture_request_id",
                table: "expenses",
                column: "capture_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_expenses_capture_request_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "capture_request_id",
                table: "expenses");
        }
    }
}
