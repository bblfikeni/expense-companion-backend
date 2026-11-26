using System.Security.Cryptography;
using Companion.Core.Interfaces;
using Companion.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Companion.Data.Services;

public class PostgresImageStore(IDbContextFactory<CompanionDbContext> contextFactory) : IImageStore
{
    public const int MaxImages = 10;
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<ImageStoreResult<ImageMetadata>> SaveAsync(Guid profileId, Guid expenseId, string contentType, byte[] content, int width, int height, string? label = null, int? pageNo = null, CancellationToken cancellationToken = default)
    {
        Validate(contentType, content);
        ValidateMetadata(label, width, height);
        var imageId = Guid.NewGuid();
        var hash = Hash(content);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var expense = await LockExpense(db, profileId, expenseId, cancellationToken);
            if (expense == null) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.NotFound);
            if (!Editable(expense)) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.InvalidStatus);
            var existing = await MetadataQuery(db.ExpenseImages.AsNoTracking().Where(i => i.ExpenseId == expense.Id && i.Sha256 == hash)).SingleOrDefaultAsync(cancellationToken);
            if (existing != null)
            {
                if (expense.Status == "None")
                {
                    expense.Status = "Uploaded";
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.AlreadyExists, existing);
            }
            var images = await db.ExpenseImages.Where(i => i.ExpenseId == expense.Id).OrderBy(i => i.PageNo).ToListAsync(cancellationToken);
            if (images.Count >= MaxImages) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.LimitReached);
            var page = pageNo ?? images.Count + 1;
            if (page < 1 || page > images.Count + 1) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.InvalidPage);
            foreach (var item in images.Where(i => i.PageNo >= page)) item.PageNo++;
            var uploaderId = await db.Users.Where(p => p.PublicId == profileId).Select(p => p.Id).SingleAsync(cancellationToken);
            var image = new ExpenseImage
            {
                PublicId = imageId,
                ExpenseId = expense.Id,
                PageNo = page,
                ContentType = contentType,
                SizeBytes = content.Length,
                Sha256 = hash,
                Label = label,
                Width = width,
                Height = height,
                CreatedAt = DateTimeOffset.UtcNow,
                UploadedByProfileId = uploaderId,
                Blob = new ExpenseImageBlob { Content = content }
            };
            db.ExpenseImages.Add(image);
            expense.Status = "Uploaded";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.Success, Metadata(image, profileId));
        });
    }

    public async Task<ImageStoreResult<ImageMetadata>> UpdateAsync(Guid profileId, Guid expenseId, Guid imageId, bool updateLabel, string? label, int? pageNo = null, CancellationToken cancellationToken = default)
    {
        if (updateLabel) ValidateLabel(label);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var expense = await LockExpense(db, profileId, expenseId, cancellationToken);
            if (expense == null) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.NotFound);
            if (!Editable(expense)) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.InvalidStatus);
            var images = await db.ExpenseImages.Include(i => i.UploadedByProfile).Where(i => i.ExpenseId == expense.Id).ToListAsync(cancellationToken);
            var image = images.SingleOrDefault(i => i.PublicId == imageId);
            if (image == null) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.NotFound);
            if (pageNo is < 1 || pageNo > images.Count) return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.InvalidPage);
            if (pageNo.HasValue && pageNo.Value != image.PageNo)
            {
                var other = images.Single(i => i.PageNo == pageNo.Value);
                other.PageNo = image.PageNo;
                image.PageNo = pageNo.Value;
            }
            if (updateLabel) image.Label = label;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ImageStoreResult<ImageMetadata>(ImageStoreStatus.Success, Metadata(image, image.UploadedByProfile.PublicId));
        });
    }

    public async Task<ImageStoreResult<IReadOnlyList<ImageMetadata>>> ListAsync(Guid profileId, Guid expenseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await AuthorizedExpenses(db, profileId, expenseId).AnyAsync(cancellationToken)) return new(ImageStoreStatus.NotFound);
        var images = await MetadataQuery(AuthorizedImages(db, profileId, expenseId).OrderBy(i => i.PageNo)).ToListAsync(cancellationToken);
        return new(ImageStoreStatus.Success, images);
    }

    public async Task<ImageStoreResult<IReadOnlyList<UploadedImageMetadata>>> ListUploadedAsync(
        Guid profileId, Guid? before, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var expenses = AuthorizedExpenses(db, profileId);
        var images = db.ExpenseImages.AsNoTracking()
            .Where(i => i.UploadedByProfile.PublicId == profileId && expenses.Any(e => e.Id == i.ExpenseId));
        if (before.HasValue)
        {
            var cursor = await images.Where(i => i.PublicId == before.Value)
                .Select(i => new { i.CreatedAt, i.Id }).SingleOrDefaultAsync(cancellationToken);
            if (cursor is null) return new(ImageStoreStatus.NotFound);
            images = images.Where(i => i.CreatedAt < cursor.CreatedAt ||
                (i.CreatedAt == cursor.CreatedAt && i.Id < cursor.Id));
        }

        var items = await images.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Take(take).Select(i => new UploadedImageMetadata(i.Expense.PublicId,
                new ImageMetadata(i.PublicId, i.PageNo, i.ContentType, i.SizeBytes, i.Sha256, i.Label,
                    i.Width, i.Height, i.CreatedAt, profileId)))
            .ToListAsync(cancellationToken);
        return new(ImageStoreStatus.Success, items);
    }

    public async Task<ImageStoreResult<ImageContent>> GetAsync(Guid profileId, Guid expenseId, Guid imageId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var images = AuthorizedImages(db, profileId, expenseId).Where(i => i.PublicId == imageId);
        var metadata = await MetadataQuery(images).SingleOrDefaultAsync(cancellationToken);
        if (metadata == null) return new(ImageStoreStatus.NotFound);
        var content = await db.ExpenseImageBlobs.AsNoTracking()
            .Where(b => images.Any(i => i.Id == b.ExpenseImageId))
            .Select(b => b.Content).SingleOrDefaultAsync(cancellationToken);
        return content == null ? new(ImageStoreStatus.NotFound) : new(ImageStoreStatus.Success, new ImageContent(metadata, content));
    }

    public async Task<ImageStoreStatus> DeleteAsync(Guid profileId, Guid expenseId, Guid imageId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var expense = await LockExpense(db, profileId, expenseId, cancellationToken);
            if (expense == null) return ImageStoreStatus.NotFound;
            if (!Editable(expense)) return ImageStoreStatus.InvalidStatus;
            var images = await db.ExpenseImages.Where(i => i.ExpenseId == expense.Id).OrderBy(i => i.PageNo).ToListAsync(cancellationToken);
            var image = images.SingleOrDefault(i => i.PublicId == imageId);
            if (image == null) return ImageStoreStatus.NotFound;
            db.ExpenseImages.Remove(image);
            images.Remove(image);
            for (var index = 0; index < images.Count; index++) images[index].PageNo = index + 1;
            if (images.Count == 0) expense.Status = "None";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ImageStoreStatus.Success;
        });
    }

    private static IQueryable<Expense> AuthorizedExpenses(CompanionDbContext db, Guid profileId) =>
        db.Expenses.AsNoTracking().Where(e => db.WorkspaceMemberships.Any(m => m.WorkspaceId == e.WorkspaceId && m.Profile.PublicId == profileId));

    private static IQueryable<Expense> AuthorizedExpenses(CompanionDbContext db, Guid profileId, Guid expenseId) =>
        AuthorizedExpenses(db, profileId).Where(e => e.PublicId == expenseId);

    private static IQueryable<ExpenseImage> AuthorizedImages(CompanionDbContext db, Guid profileId, Guid expenseId)
    {
        var expenses = AuthorizedExpenses(db, profileId, expenseId);
        return db.ExpenseImages.AsNoTracking().Where(i => expenses.Any(e => e.Id == i.ExpenseId));
    }

    private static async Task<Expense?> LockExpense(CompanionDbContext db, Guid profileId, Guid expenseId, CancellationToken cancellationToken)
    {
        var expenses = await db.Expenses.FromSqlInterpolated($"""
            SELECT e.* FROM expenses e
            JOIN workspace_memberships m ON m.workspace_id = e.workspace_id
            JOIN profiles p ON p.id = m.profile_id
            WHERE e.public_id = {expenseId} AND p.public_id = {profileId}
            FOR UPDATE OF e
            FOR SHARE OF m
            """).ToListAsync(cancellationToken);
        return expenses.SingleOrDefault();
    }

    private static void Validate(string contentType, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100) throw new ArgumentException("Content type must be 1-100 characters.", nameof(contentType));
        if (content.Length is < 1 or > MaxBytes) throw new ArgumentOutOfRangeException(nameof(content), "Image must be between 1 byte and 5 MiB.");
    }

    private static void ValidateMetadata(string? label, int width, int height)
    {
        ValidateLabel(label);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
    }

    private static void ValidateLabel(string? label)
    {
        if (label is not (null or "Receipt" or "ProofOfPayment")) throw new ArgumentException("Invalid image label.", nameof(label));
    }

    private static bool Editable(Expense expense) => expense.Status is "None" or "Uploaded";
    private static IQueryable<ImageMetadata> MetadataQuery(IQueryable<ExpenseImage> images) => images.Select(i => new ImageMetadata(i.PublicId, i.PageNo, i.ContentType, i.SizeBytes, i.Sha256, i.Label, i.Width, i.Height, i.CreatedAt, i.UploadedByProfile.PublicId));
    private static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
    private static ImageMetadata Metadata(ExpenseImage image, Guid uploaderId) => new(image.PublicId, image.PageNo, image.ContentType, image.SizeBytes, image.Sha256, image.Label, image.Width, image.Height, image.CreatedAt, uploaderId);
}
