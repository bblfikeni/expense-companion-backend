namespace Companion.Core.Interfaces;

public enum ImageStoreStatus
{
    Success,
    AlreadyExists,
    NotFound,
    InvalidStatus,
    LimitReached,
    InvalidPage
}