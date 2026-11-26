namespace Companion.Core.Interfaces;

public record ImageStoreResult<T>(ImageStoreStatus Status, T? Value = default);