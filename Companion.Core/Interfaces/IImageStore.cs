namespace Companion.Core.Interfaces;

public interface IImageStore
{
    Task<ImageStoreResult<ImageMetadata>> SaveAsync(Guid profileId, Guid expenseId, string contentType, byte[] content, int width, int height, string? label = null, int? pageNo = null, CancellationToken cancellationToken = default);
    Task<ImageStoreResult<ImageMetadata>> UpdateAsync(Guid profileId, Guid expenseId, Guid imageId, bool updateLabel, string? label, int? pageNo = null, CancellationToken cancellationToken = default);
    Task<ImageStoreResult<IReadOnlyList<ImageMetadata>>> ListAsync(Guid profileId, Guid expenseId, CancellationToken cancellationToken = default);
    Task<ImageStoreResult<ImageContent>> GetAsync(Guid profileId, Guid expenseId, Guid imageId, CancellationToken cancellationToken = default);
    Task<ImageStoreStatus> DeleteAsync(Guid profileId, Guid expenseId, Guid imageId, CancellationToken cancellationToken = default);
}