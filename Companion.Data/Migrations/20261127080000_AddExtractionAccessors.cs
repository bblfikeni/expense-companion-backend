using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Companion.Data.Migrations;

[DbContext(typeof(CompanionDbContext))]
[Migration("20261127080000_AddExtractionAccessors")]
public sealed class AddExtractionAccessors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE FUNCTION public.lock_extraction_expense(target_expense_id integer)
            RETURNS boolean
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, pg_temp
            AS $$
            BEGIN
                PERFORM 1 FROM public.expenses
                WHERE id = target_expense_id AND status = 'Processing'
                FOR UPDATE;
                RETURN FOUND;
            END;
            $$;
            REVOKE ALL ON FUNCTION public.lock_extraction_expense(integer) FROM PUBLIC;

            CREATE VIEW public.published_receipt_events AS
            SELECT payload ->> 'expensePublicId' AS expense_public_id, published_at
            FROM public.outbox_messages
            WHERE type = 'ReceiptUploadedEvent' AND published_at IS NOT NULL;
            REVOKE ALL ON public.published_receipt_events FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP VIEW public.published_receipt_events;
            DROP FUNCTION public.lock_extraction_expense(integer);
            """);
    }
}
