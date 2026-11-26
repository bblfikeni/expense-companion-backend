namespace Companion.Core.Interfaces;

public record ImageMetadata(Guid ImageId, int PageNo, string ContentType, int SizeBytes, string Sha256, string? Label, int Width, int Height, DateTimeOffset CreatedAt, Guid UploadedByProfileId);