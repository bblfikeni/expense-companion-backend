using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Companion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "date_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "document_validity_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "total_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "vat_amount_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_number",
                table: "expense_extractions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "vat_number_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "vendor_confidence",
                table: "expense_extractions",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_extractions_expense_id_image_set_hash",
                table: "expense_extractions",
                columns: new[] { "expense_id", "image_set_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_expense_extractions_expense_id_image_set_hash",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "date_confidence",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "document_validity_confidence",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "total_confidence",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "vat_amount_confidence",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "vat_number",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "vat_number_confidence",
                table: "expense_extractions");

            migrationBuilder.DropColumn(
                name: "vendor_confidence",
                table: "expense_extractions");
        }
    }
}
