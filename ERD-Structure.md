# Expense Companion - Database ERD Structure

Based on the SARS Expense Companion application architecture, this document defines the database entity relationship structure for AI ERD generation.

## Overview
- **Database**: PostgreSQL 17, single flat schema
- **Naming**: snake_case tables and columns, plural table names
- **PKs**: int identity (never UUID in API), Guid PublicId for external exposure
- **Timestamps**: timestamptz, UTC only
- **Money**: long AmountInCents end-to-end

## Core Identity Tables (inherited from Identity)

### `profiles` (users)
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | `int` | PK, Identity | Internal surrogate key |
| `full_name` | `varchar(255)` | NOT NULL | Display name |
| `email` | `varchar(255)` | NOT NULL, Unique | Normalized email |
| `public_id` | `uuid` | Default: `gen_random_uuid()`, Unique | External identifier |
| `popia_consent_given` | `boolean` | DEFAULT false | POPIA consent flag |
| `popia_consent_date` | `timestamptz` | Nullable | When consent was given |
| `is_anonymized` | `boolean` | DEFAULT false | Personal data anonymized flag |
| `password_hash` | `varchar(255)` | NOT NULL | Identity password hash |
| `security_stamp` | `varchar(255)` | NOT NULL | For re-authentication |
| `concurrency_stamp` | `varchar(255)` | NOT NULL | For optimistic concurrency |
| `phone_number` | `varchar(255)` | Nullable | Optional phone |
| `phone_number_verified_at` | `timestamptz` | Nullable | Phone verification date |
| `two_factor_enabled` | `boolean` | DEFAULT false | 2FA status |
| `lockout_end` | `timestamptz` | Nullable | Lockout end time |
| `lockout_enabled` | `boolean` | DEFAULT true | Lockout enabled |
| `access_failed_count` | `int` | DEFAULT 0 | Failed attempt count |

**Indexes**:
- `ix_profiles_normalized_email` (Unique on `lower(email)`)
- `ix_profiles_normalized_user_name` (Unique on `lower(user_name)`)
- `ix_profiles_public_id` (Unique on `public_id`)

## Application Tables (continued)

### `otp_codes` (One-Time Passwords)
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | `int` | PK, Identity | Internal surrogate key |
| `profile_id` | `int` | FK → `profiles.id` | Associated user |
| `purpose` | `text` | NOT NULL | `otp_purpose` enum value (`email_verification` or `password_reset`) |
| `code_hash` | `bytea` | NOT NULL | HMAC-SHA256 hash of the OTP code (plaintext never logged) |
| `expires_at` | `timestamptz` | NOT NULL | When OTP expires |
| `consumed_at` | `timestamptz` | Nullable | When OTP was consumed |
| `attempts` | `int` | DEFAULT 0 | Verification attempt count |
| `created_at` | `timestamptz` | NOT NULL | When OTP was created |

**Indexes**:
- `ix_otp_codes_profile_id_purpose` (Unique on `profile_id, purpose`)

**Foreign Keys**:
- `fk_otp_codes_profiles_profile_id` → `profiles.id`, ON DELETE Cascade

## Expenses (Receipt/Expense Records)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | `int` | PK, Identity | Internal surrogate key |
| `profile_id` | `int` | FK → `profiles.id` | Owner (tenant isolation) |
| `public_id` | `uuid` | Default: `gen_random_uuid()`, Unique | External identifier |
| `supplier_name` | `varchar(255)` | NOT NULL | Name of the supplier/trader |
| `supplier_tax_number` | `varchar(50)` | Nullable | Supplier VAT/tax ID |
| `total_amount_cents` | `bigint` | NOT NULL | Total amount in cents (positive) |
| `vat_amount_cents` | `bigint` | DEFAULT 0 | VAT portion in cents |
| `category` | `varchar(100)` | NOT NULL | Expense category (e.g., "office_supplies", "accommodation") |
| `description` | `text` | Nullable | Free-text description |
| `proof_status` | `varchar(20)` | NOT NULL, Default: `'none'` | `receipt_status` enum: none | uploaded | processing | qualified | rejected |
| `receipt_file_key` | `varchar(255)` | Nullable | Cloudflare R2 object key for stored receipt |
| `created_at` | `timestamptz` | NOT NULL | When expense was created |
| `updated_at` | `timestamptz` | NOT NULL | Last update timestamp |
| `processed_at` | `timestamptz` | Nullable | When OCR processing completed |

**Indexes**:
- `ix_expenses_profile_id` on `profile_id`
- `ix_expenses_public_id` (Unique on `public_id`)
- `ix_expenses_proof_status` on `proof_status`
- `ix_expenses_created_at` on `created_at`

**Foreign Keys**:
- `fk_expenses_profile_id` → `profiles.id`, ON DELETE Cascade (user-scoped data)

## Workspaces (Optional — workspace/organization grouping)

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | `int` | PK, Identity | Internal surrogate key |
| `name` | `varchar(255)` | NOT NULL | Workspace name |
| `description` | `text` | Nullable | Optional description |
| `created_at` | `timestamptz` | NOT NULL | When workspace was created |
| `updated_at` | `timestamptz` | NOT NULL | Last update timestamp |
| `owner_profile_id` | `int` | FK → `profiles.id` | User who created the workspace |

**Indexes**:
- `ix_workspaces_owner_profile_id` on `owner_profile_id`

**Foreign Keys**:
- `fk_workspaces_owner_profile_id` → `profiles.id`

---

## ERD Relationship Diagram

```mermaid
erDiagram
    PROFILES ||--o{ OUTBOX_MESSAGES : "sends"
    PROFILES ||--o{ OTP_CODES : "has"
    PROFILES ||--o{ EXPENSES : "owns"
    PROFILES ||--o{ WORKSPACES : "creates"
    
    OUTBOX_MESSAGES {
        int id PK
        text type
        jsonb payload
        text correlation_id
        timestamptz created_at
        timestamptz published_at
        int attempts
        timestamptz failed_at
    }
    
    OTP_CODES {
        int id PK
        int profile_id FK
        text purpose
        bytea code_hash
        timestamptz expires_at
        timestamptz consumed_at
        int attempts
        timestamptz created_at
    }
    
    EXPENSES {
        int id PK
        int profile_id FK
        uuid public_id Unique
        varchar supplier_name
        varchar supplier_tax_number
        bigint total_amount_cents
        bigint vat_amount_cents
        varchar category
        text description
        varchar proof_status
        varchar receipt_file_key
        timestamptz created_at
        timestamptz updated_at
        timestamptz processed_at
    }
    
    WORKSPACES {
        int id PK
        varchar name
        text description
        timestamptz created_at
        timestamptz updated_at
        int owner_profile_id FK
    }
    
    ROLES {
        int id PK
        varchar name
        varchar normalized_name Unique
    }
---

## Key Relationships

### One-to-Many
- **profiles → otp_codes**: One user can have multiple OTP codes (but unique on `profile_id, purpose`)
- **profiles → expenses**: One user can have many expenses
- **workspaces → expenses**: (if enabled) one workspace has many expenses

### Many-to-Many (via Identity)
- **profiles ↔ roles**: Through `profile_roles` join table
- **roles ↔ permissions**: Through `role_claims` join table (claim type `permission`)

### Constraints & Rules (from Architecture.md)
- **Money**: `long AmountInCents` — never `decimal`/`double` for storage/transmission
- **Receipt status**: `None | Uploaded | Processing | Qualified | Rejected`
- **POPIA erasure**: `is_anonymized` flag; strip personal identification fields
- **Write ownership**: Extraction writes only result columns; user-origin data always wins
- **Timestamps**: `timestamptz`, UTC only, snake_case column names
```

## Migration Entry Points

The `CompanionDbContext` exposes these `DbSet` properties:

- `OtpCodes` → `otp_codes` table
- `OutboxMessages` → `outbox_messages` table

Identity-derived tables are automatically included via `IdentityDbContext` base:

- `profile_claims`, `profile_roles`, `profile_logins`, `profile_tokens`, `role_claims`

Additional tables to be added via future migrations:

- `expenses` (receipt tracking)
- `workspaces` (optional multi-tenant grouping)
```

## AI Prompt for ERD Generation

When passing this to an AI ERD generator, use the following prompt:

```
Generate an ERD diagram for a PostgreSQL 17 database with the following entities and constraints:

1. Tables (plural, snake_case):
   - profiles: int PK id, varchar full_name(255), uuid public_id (default gen_random_uuid), boolean popia_consent_given, boolean is_anonymized, timestamptz timestamps
   - roles: int PK id, varchar name(256), varchar normalized_name(256) unique
   - outbox_messages: int PK id, type, payload (jsonb), correlation_id text, timestamptz created_at/published_at, int attempts
   - otp_codes: int PK id, int profile_id FK→profiles.id, text purpose, bytea code_hash, timestamptz expires_at/consumed_at/created_at, int attempts
   - expenses: int PK id, int profile_id FK→profiles.id, uuid public_id, varchar supplier_name(255), bigint total_amount_cents, bigint vat_amount_cents default 0, varchar category(100), varchar proof_status (none|uploaded|processing|qualified|rejected), varchar receipt_file_key, timestamptz created_at/updated_at/processed_at
   - workspaces: int PK id, varchar name(255), int owner_profile_id FK→profiles.id, timestamptz timestamps

2. Indexes/Constraints:
   - Unique on profiles.public_id, profiles.normalized_email, profiles.normalized_user_name, roles.normalized_name, otp_codes.profile_id+purpose
   - Filtered index on outbox_messages: published_at IS NULL AND failed_at IS NULL
   - All timestamptz columns UTC only

3. Money: long AmountInCents end-to-end
4. Receipt status enum: None | Uploaded | Processing | Qualified | Rejected
5. POPIA: is_anonymized flag for erasure
6. Use snake_case column names, plural table names, int identity PKs, Guid PublicId for external exposure
```