-- Run once in the Companion database after the gateway has applied migrations.
-- Cloud SQL: connect with psql as the migration owner with CREATEROLE, or have the
-- appropriate administrators run the role creation and object grants separately.
-- Create this role through SQL. A built-in user created with Cloud SQL's default
-- user setup receives cloudsqlsuperuser, which is inappropriate for this account.
-- After applying, set the operator-supplied password with psql's \password extraction_svc.
-- Store the resulting connection string as ConnectionStrings__ExtractionDb.
-- Keep public-schema application objects private from PUBLIC and this login out of
-- other roles. Existing grants to PUBLIC apply to it just as to other database users.

BEGIN;
CREATE ROLE extraction_svc LOGIN NOINHERIT NOCREATEDB NOCREATEROLE;
REVOKE ALL ON SCHEMA public FROM extraction_svc;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM extraction_svc;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM extraction_svc;

GRANT USAGE ON SCHEMA public TO extraction_svc;
GRANT SELECT ON public.expenses, public.expense_images, public.expense_image_blobs,
    public.expense_extractions, public.published_receipt_events TO extraction_svc;
-- Claiming needs a row lock. The function restricts it to Processing expenses.
-- Extraction never updates any columns in expenses.
GRANT EXECUTE ON FUNCTION public.lock_extraction_expense(integer) TO extraction_svc;

GRANT INSERT (
    expense_id, expense_public_id, image_set_hash, status, overall_confidence,
    review_required, started_at, attempt_count, next_attempt_at, completed_at,
    rejection_reason, vendor, expense_date, total_amount_cents, vat_amount_cents, vat_number,
    vendor_confidence, date_confidence, total_confidence, vat_amount_confidence,
    vat_number_confidence, document_validity_confidence, vat_classification, is_valid_tax_invoice
) ON public.expense_extractions TO extraction_svc;
GRANT UPDATE (
    status, started_at, attempt_count, next_attempt_at, completed_at, vendor, expense_date,
    total_amount_cents, vat_amount_cents, vat_number, vendor_confidence, date_confidence,
    total_confidence, vat_amount_confidence, vat_number_confidence, document_validity_confidence,
    vat_classification, is_valid_tax_invoice, review_required, overall_confidence
) ON public.expense_extractions TO extraction_svc;

GRANT INSERT (
    extraction_id, page_no, document_type, ocr_engine, image_quality_score,
    ocr_mean_confidence, proof_amount_cents, proof_date, failure_reason
) ON public.expense_image_results TO extraction_svc;
-- EF uses INSERT RETURNING id for the generated identity column.
GRANT SELECT (id) ON public.expense_image_results TO extraction_svc;
COMMIT;
