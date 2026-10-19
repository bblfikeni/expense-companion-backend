using Companion.Api.Dtos;

using Companion.Data;
using Companion.Data.Interfaces;
using Companion.Data.Models;
using Companion.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Companion.Api.Services;

public interface IAuthenticationService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<VerificationResult> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<ResendResult> ResendVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<ForgotResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<ResetResult> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
    Task<ProfileResult?> GetProfileAsync(Guid profilePublicId, CancellationToken cancellationToken = default);
    Task<ProfileResult> UpdateProfileAsync(Guid profilePublicId, ProfileUpdateRequest request, CancellationToken cancellationToken = default);
}

public sealed class RegisterResult
{
    public static RegisterResult Fresh(string email)
        => new(email, success: true, created: true, consent: true, confirmed: false);

    public static RegisterResult Already(string email)
        => new(email, success: true, created: false, consent: true, confirmed: false);

    public static RegisterResult ConsentRejected(string email)
        => new(email, success: false, created: false, consent: false, confirmed: false);

    public static RegisterResult Failed(string email)
        => new(email, success: false, created: false, consent: true, confirmed: false);

    private RegisterResult(string email, bool success, bool created, bool consent, bool confirmed)
    {
        Email = email;
        Success = success;
        Created = created;
        ConsentGiven = consent;
        EmailConfirmed = confirmed;
    }

    public string Email { get; }

    public bool Success { get; }

    public bool Created { get; }

    public bool ConsentGiven { get; }

    public bool EmailConfirmed { get; }
}

public sealed class VerificationResult
{
    public static VerificationResult Succeeded => new(success: true, outcome: Outcome.Success);
    public static VerificationResult Failed => new(success: false, outcome: Outcome.Failed);

    private VerificationResult(bool success, Outcome outcome)
    {
        Success = success;
    }

    public bool Success { get; }

    public enum Outcome
    {
        Success,
        Failed,
    }
}

public sealed class ResendResult
{
    public static ResendResult Accepted => new(success: true, cooldown: false);
    public static ResendResult Cooldown => new(success: false, cooldown: true);
    public static ResendResult NotRegistered => new(success: true, cooldown: false);

    private ResendResult(bool success, bool cooldown)
    {
        Success = success;
        CooldownApplied = cooldown;
    }

    public bool Success { get; }

    public bool CooldownApplied { get; }
}

public sealed class LoginResult
{
    public static LoginResult Authenticated(string token, int expiresInSeconds)
        => new(token, expiresInSeconds, success: true);

    public static LoginResult Failed => new(null, 0, success: false);

    private LoginResult(string? token, int expiresInSeconds, bool success)
    {
        Success = success;
        AccessToken = token;
        ExpiresInSeconds = expiresInSeconds;
    }

    public bool Success { get; }

    public string? AccessToken { get; init; }

    public int ExpiresInSeconds { get; init; }

    public string TokenType { get; init; } = "Bearer";
}
public sealed class ForgotResult
{
    public static ForgotResult Sent(string email)
        => new(email, success: true, created: true);

    public static ForgotResult AlreadySent(string email)
        => new(email, success: true, created: false);

    private ForgotResult(string email, bool success, bool created)
    {
        Email = email;
        Success = success;
        Created = created;
    }

    public string Email { get; }

    public bool Success { get; }

    public bool Created { get; }
}

public sealed class ResetResult
{
    public static ResetResult Succeeded => new(success: true, consumed: true, updatedStamp: true);
    public static ResetResult Failed => new(success: false, consumed: false, updatedStamp: false);

    private ResetResult(bool success, bool consumed, bool updatedStamp)
    {
        Success = success;
        Consumed = consumed;
        UpdatedSecurityStamp = updatedStamp;
    }

    public bool Success { get; }

    public bool Consumed { get; }

    public bool UpdatedSecurityStamp { get; }
}

public sealed class ProfileResult
{
    public static ProfileResult FoundProfile(string displayName, Guid publicId)
        => new(found: true, displayName: displayName, publicId: publicId);

    public static ProfileResult NotFound => new(found: false);

    private ProfileResult(bool found, string? displayName = null, Guid? publicId = null)
    {
        Found = found;
        DisplayName = displayName;
        PublicId = publicId;
    }

    public bool Found { get; }

    public string? DisplayName { get; }

    public Guid? PublicId { get; }
}

