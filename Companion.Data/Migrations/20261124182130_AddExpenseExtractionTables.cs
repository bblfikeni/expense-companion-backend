using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Companion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseExtractionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_extractions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    expense_public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    overall_confidence = table.Column<double>(type: "double precision", nullable: false),
                    image_set_hash = table.Column<string>(type: "varchar(64)", nullable: false),
                    review_required = table.Column<bool>(type: "boolean", nullable: false),
                    rejection_reason = table.Column<string>(type: "varchar(50)", nullable: true),
                    vendor = table.Column<string>(type: "varchar(255)", nullable: true),
                    expense_date = table.Column<string>(type: "varchar(50)", nullable: true),
                    total_amount_cents = table.Column<long>(type: "bigint", nullable: true),
                    vat_amount_cents = table.Column<long>(type: "bigint", nullable: true),
                    vat_classification = table.Column<string>(type: "varchar(30)", nullable: true),
                    is_valid_tax_invoice = table.Column<bool>(type: "boolean", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_extractions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_image_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    extraction_id = table.Column<int>(type: "integer", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    document_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    ocr_engine = table.Column<string>(type: "varchar(50)", nullable: false),
                    image_quality_score = table.Column<double>(type: "double precision", nullable: false),
                    ocr_mean_confidence = table.Column<double>(type: "double precision", nullable: false),
                    proof_amount_cents = table.Column<long>(type: "bigint", nullable: true),
                    proof_date = table.Column<string>(type: "varchar(50)", nullable: true),
                    failure_reason = table.Column<string>(type: "varchar(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_image_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_image_results_expense_extractions_extraction_id",
                        column: x => x.extraction_id,
                        principalTable: "expense_extractions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_extractions_expense_id_status",
                table: "expense_extractions",
                columns: new[] { "expense_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_extractions_expense_public_id",
                table: "expense_extractions",
                column: "expense_public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_image_results_extraction_id_page_no",
                table: "expense_image_results",
                columns: new[] { "extraction_id", "page_no" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_image_results");

            migrationBuilder.DropTable(
                name: "expense_extractions");
        }
    }
}
