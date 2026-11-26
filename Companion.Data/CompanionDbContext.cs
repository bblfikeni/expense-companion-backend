using Companion.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Companion.Data;

/// <summary>
/// The single flat-schema DbContext for the shared PostgreSQL 17 database.
/// Every service points at the same schema; only <c>Companion.Api</c> applies
/// migrations, the workers only check for pending ones.
/// </summary>
public class CompanionDbContext
    : IdentityDbContext<
        Profile,
        Role,
        int,
        IdentityUserClaim<int>,
        IdentityUserRole<int>,
        IdentityUserLogin<int>,
        IdentityRoleClaim<int>,
        IdentityUserToken<int>>
{
    public CompanionDbContext(DbContextOptions<CompanionDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // snake_case is applied at the options level via
        // DbContextOptionsBuilderExtensions.UseCompanionDatabase.

        builder.Entity<Profile>(profile =>
        {
            profile.ToTable("profiles");

            profile.Property(p => p.FullName).HasMaxLength(255);

            // PopiaConsentGiven / PopiaConsentDate are intentionally NOT configured
            // further. Both are non-nullable value types, so they are NOT NULL with
            // no database default: the absence of a consent decision must be a
            // programming error, not a silent false. There is deliberately no CHECK
            // constraint — see Profile.PopiaConsentGiven.

            profile.Property(p => p.IsAnonymized).HasDefaultValue(false);

            profile.Property(p => p.PublicId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();
            profile.HasIndex(p => p.PublicId).IsUnique();

            // Identity configures its own index names explicitly, and
            // EFCore.NamingConventions leaves explicit names alone — so these are
            // renamed by hand to keep every index in the schema snake_case.
            // Reconfiguring NormalizedEmail's existing index also makes it unique,
            // replacing Identity's non-unique default rather than adding a second index.
            profile.HasIndex(p => p.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("ix_profiles_normalized_email");
            profile.HasIndex(p => p.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("ix_profiles_normalized_user_name");
        });

        builder.Entity<Role>(role =>
        {
            role.ToTable("roles");

            role.HasIndex(r => r.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ix_roles_normalized_name");
        });

        builder.Entity<IdentityUserClaim<int>>(entity => entity.ToTable("profile_claims"));
        builder.Entity<IdentityUserRole<int>>(entity => entity.ToTable("profile_roles"));
        builder.Entity<IdentityUserLogin<int>>(entity => entity.ToTable("profile_logins"));
        builder.Entity<IdentityUserToken<int>>(entity => entity.ToTable("profile_tokens"));
        builder.Entity<IdentityRoleClaim<int>>(entity => entity.ToTable("role_claims"));

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.Property(e => e.Type).HasColumnType("text");
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Attempts).HasColumnName("attempts");

            // Partial index on rows that have not been relayed yet.
            entity.HasIndex(e => e.PublishedAt)
                .HasFilter("published_at IS NULL AND failed_at IS NULL")
                .HasDatabaseName("ix_outbox_messages_unpublished");
        });

        builder.Entity<OtpCode>(entity =>
        {
            entity.ToTable("otp_codes");

            entity.Property(e => e.Purpose)
                .HasConversion<string>()
                .HasColumnType("text");
            entity.Property(e => e.CodeHash).HasColumnType("bytea");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            entity.Property(e => e.Attempts).HasColumnName("attempts");

            entity.HasIndex(e => new { e.ProfileId, e.Purpose })
                .HasDatabaseName("ix_otp_codes_profile_id_purpose");
        });

        builder.Entity<ExpenseExtraction>(entity =>
        {
            entity.ToTable("expense_extractions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExpenseId).HasColumnName("expense_id");

            entity.Property(e => e.ExpensePublicId)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Status).HasColumnType("varchar(20)");

            entity.Property(e => e.ImageSetHash).HasColumnType("varchar(64)");

            entity.Property(e => e.OverallConfidence).HasColumnName("overall_confidence").HasColumnType("double precision");

            entity.Property(e => e.ReviewRequired).HasColumnName("review_required");

            entity.Property(e => e.RejectionReason).HasColumnType("varchar(50)");

            entity.Property(e => e.Vendor).HasColumnType("varchar(255)");

            entity.Property(e => e.ExpenseDate).HasColumnName("expense_date").HasColumnType("varchar(50)");

            entity.Property(e => e.TotalAmountCents).HasColumnName("total_amount_cents").HasColumnType("bigint");

            entity.Property(e => e.VatAmountCents).HasColumnName("vat_amount_cents").HasColumnType("bigint");

            entity.Property(e => e.VatClassification).HasColumnType("varchar(30)");

            entity.Property(e => e.IsValidTaxInvoice).HasColumnName("is_valid_tax_invoice");

            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");

            entity.Property(e => e.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");

            // Indexes
            entity.HasIndex(e => e.ExpensePublicId).IsUnique();

            entity.HasIndex(e => new { e.ExpenseId, e.Status });

            entity.HasIndex(e => new { e.ExpenseId, e.ImageSetHash }).IsUnique();

            });

        builder.Entity<ExpenseImageResult>(entity =>
        {
            entity.ToTable("expense_image_results");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExtractionId).HasColumnName("extraction_id");

            entity.Property(e => e.PageNo).HasColumnName("page_no");

            entity.Property(e => e.DocumentType).HasColumnType("varchar(50)");

            entity.Property(e => e.OcrEngine).HasColumnType("varchar(50)");

            entity.Property(e => e.ImageQualityScore).HasColumnName("image_quality_score").HasColumnType("double precision");

            entity.Property(e => e.OcrMeanConfidence).HasColumnName("ocr_mean_confidence").HasColumnType("double precision");

            entity.Property(e => e.ProofAmountCents).HasColumnName("proof_amount_cents").HasColumnType("bigint");

            entity.Property(e => e.ProofDate).HasColumnName("proof_date").HasColumnType("varchar(50)");

            entity.Property(e => e.FailureReason).HasColumnType("varchar(100)");

            // Indexes
            entity.HasIndex(e => new { e.ExtractionId, e.PageNo });

            // Foreign key to expense_extractions
            entity.HasOne(e => e.Extraction)
                .WithMany()
                .HasForeignKey(e => e.ExtractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        SeedPlatformAdmin(builder);
    }

    /// <summary>
    /// Seeds the platform-admin role and its single <c>platform:*</c> permission
    /// claim through the migration, so every environment converges on the same
    /// RBAC baseline. Deliberately seeds no users.
    /// </summary>
    private static void SeedPlatformAdmin(ModelBuilder builder)
    {
        builder.Entity<Role>().HasData(
            new Role
            {
                Id = PlatformAdminRoleId,
                Name = Role.PlatformAdmin,
                NormalizedName = Role.PlatformAdmin.ToUpperInvariant(),

                // Pinned so the seed is fully deterministic in the migration rather
                // than relying on EF's generated value for this token.
                ConcurrencyStamp = PlatformAdminConcurrencyStamp,
            });

        builder.Entity<IdentityRoleClaim<int>>().HasData(
            new IdentityRoleClaim<int>
            {
                Id = PlatformAdminPermissionClaimId,
                RoleId = PlatformAdminRoleId,
                ClaimType = Role.PermissionClaimType,
                ClaimValue = Role.PlatformPermission,
            });
    }

    // Explicit ids keep the seed deterministic so the migration is idempotent and
    // the FK between role_claims and roles is stable across environments.
    private const int PlatformAdminRoleId = 1;

    private const int PlatformAdminPermissionClaimId = 1;

    private const string PlatformAdminConcurrencyStamp = "00000000-0000-0000-0000-000000000001";

    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ExpenseExtraction> ExpenseExtractions => Set<ExpenseExtraction>();

    public DbSet<ExpenseImageResult> ExpenseImageResults => Set<ExpenseImageResult>();
}
