using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Companion.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceExpenseImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workspace_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_expenses_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_memberships",
                columns: table => new
                {
                    workspace_id = table.Column<int>(type: "integer", nullable: false),
                    profile_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_memberships", x => new { x.workspace_id, x.profile_id });
                    table.ForeignKey(
                        name: "fk_workspace_memberships_users_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workspace_memberships_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_profile_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_images", x => x.id);
                    table.CheckConstraint("ck_expense_images_dimensions", "width > 0 AND height > 0");
                    table.CheckConstraint("ck_expense_images_label", "label IS NULL OR label IN ('Receipt', 'ProofOfPayment')");
                    table.CheckConstraint("ck_expense_images_page_no", "page_no BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_expense_images_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_expense_images_size", "size_bytes BETWEEN 1 AND 5242880");
                    table.ForeignKey(
                        name: "fk_expense_images_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_expense_images_users_uploaded_by_profile_id",
                        column: x => x.uploaded_by_profile_id,
                        principalTable: "profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_image_blobs",
                columns: table => new
                {
                    expense_image_id = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_image_blobs", x => x.expense_image_id);
                    table.CheckConstraint("ck_expense_image_blobs_size", "octet_length(data) BETWEEN 1 AND 5242880");
                    table.ForeignKey(
                        name: "fk_expense_image_blobs_expense_images_expense_image_id",
                        column: x => x.expense_image_id,
                        principalTable: "expense_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_images_expense_id_sha256",
                table: "expense_images",
                columns: new[] { "expense_id", "sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_images_public_id",
                table: "expense_images",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_images_uploaded_by_profile_id",
                table: "expense_images",
                column: "uploaded_by_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_public_id",
                table: "expenses",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_workspace_id",
                table: "expenses",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_memberships_profile_id",
                table: "workspace_memberships",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_public_id",
                table: "workspaces",
                column: "public_id",
                unique: true);

            migrationBuilder.Sql("ALTER TABLE expense_images ADD CONSTRAINT uq_expense_images_expense_page UNIQUE (expense_id, page_no) DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql("ALTER TABLE expense_image_blobs ALTER COLUMN data SET STORAGE EXTERNAL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_image_blobs");

            migrationBuilder.DropTable(
                name: "workspace_memberships");

            migrationBuilder.DropTable(
                name: "expense_images");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}
