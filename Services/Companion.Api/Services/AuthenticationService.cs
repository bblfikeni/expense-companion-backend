using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Companion.Api.Dtos;
using Companion.Api.Options;
using Companion.Data;
using Companion.Data.Interfaces;
using Companion.Data.Models;
using Companion.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Companion.Api.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly CompanionDbContext _db;
    private readonly IOtpService _otp;
    private readonly UserManager<Profile> _users;
    private readonly SignInManager<Profile> _signInManager;
    private readonly IOptions<AuthOptions> _auth;
    private readonly TimeProvider _clock;

    public AuthenticationService(
        CompanionDbContext db,
        IOtpService otp,
        UserManager<Profile> users,
        SignInManager<Profile> signInManager,
        IOptions<AuthOptions> auth,
        TimeProvider clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _otp = otp ?? throw new ArgumentNullException(nameof(otp));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email ?? string.Empty);
        if (!request.PopiaConsent)
            return RegisterResult.ConsentRejected(email);
        if (email.Length == 0 || email.Length > 256 || string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 255)
            return RegisterResult.Failed(email);

        return await WithAccountTransactionAsync(email, async ct =>
        {
            var profile = new Profile
            {
                UserName = email,
                Email = email,
                FullName = request.FullName.Trim(),
                PopiaConsentGiven = true,
                PopiaConsentDate = _clock.GetUtcNow(),
            };
            // Apply the same password policy to new and existing addresses so a
            // weak-password request cannot reveal whether an account exists.
            foreach (var validator in _users.PasswordValidators)
            {
                if (!(await validator.ValidateAsync(_users, profile, request.Password)).Succeeded)
                    return RegisterResult.Failed(email);
            }
            if (await ProfileExistsAsync(email, ct))
                return RegisterResult.Already(email);

            var create = await _users.CreateAsync(profile, request.Password);
            if (!create.Succeeded)
                return RegisterResult.Failed(email);

            await QueueCodeAsync(profile, OtpPurpose.EmailVerification, ct);
            return RegisterResult.Fresh(email);
        }, cancellationToken);
    }

    public async Task<VerificationResult> VerifyEmailAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return VerificationResult.Failed;

        return await WithAccountTransactionAsync(NormalizeEmail(email), async ct =>
        {
            var profile = await FindByEmailAsync(NormalizeEmail(email), ct);
            if (profile is null)
                return VerificationResult.Failed;

            var result = await _otp.VerifyAsync(profile.Id, OtpPurpose.EmailVerification, code, ct);
            if (!result.Success)
                return VerificationResult.Failed;

            profile.EmailConfirmed = true;
            return VerificationResult.Succeeded;
        }, cancellationToken);
    }

    public async Task<ResendResult> ResendVerificationAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ResendResult.NotRegistered;

        return await WithAccountTransactionAsync(NormalizeEmail(email), async ct =>
        {
            var profile = await FindByEmailAsync(NormalizeEmail(email), ct);
            if (profile is null || profile.EmailConfirmed)
                return ResendResult.NotRegistered;
            if (await IsCoolingDownAsync(profile.Id, OtpPurpose.EmailVerification, ct))
                return ResendResult.Cooldown;

            await QueueCodeAsync(profile, OtpPurpose.EmailVerification, ct);
            return ResendResult.Accepted;
        }, cancellationToken);
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return LoginResult.Failed;

        // Failed password counters must also serialize across concurrent requests.
        return await WithAccountTransactionAsync(NormalizeEmail(email), async ct =>
        {
            var profile = await FindByEmailAsync(NormalizeEmail(email), ct);
            if (profile is null || profile.IsAnonymized || !profile.EmailConfirmed)
                return LoginResult.Failed;

            var signIn = await _signInManager.CheckPasswordSignInAsync(profile, password, lockoutOnFailure: true);
            if (!signIn.Succeeded)
                return LoginResult.Failed;

            var token = await BuildTokenAsync(profile, ct);
            return LoginResult.Authenticated(token, expiresInSeconds: 900);
        }, cancellationToken);
    }

    public async Task<ForgotResult> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ForgotResult.AlreadySent(string.Empty);

        return await WithAccountTransactionAsync(NormalizeEmail(email), async ct =>
        {
            var profile = await FindByEmailAsync(NormalizeEmail(email), ct);
            if (profile is null || !profile.EmailConfirmed ||
                await IsCoolingDownAsync(profile.Id, OtpPurpose.PasswordReset, ct))
                return ForgotResult.AlreadySent(email);

            await QueueCodeAsync(profile, OtpPurpose.PasswordReset, ct);
            return ForgotResult.Sent(email);
        }, cancellationToken);
    }

    public async Task<ResetResult> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(newPassword))
            return ResetResult.Failed;

        return await WithAccountTransactionAsync(NormalizeEmail(email), async ct =>
        {
            var profile = await FindByEmailAsync(NormalizeEmail(email), ct);
            if (profile is null)
                return ResetResult.Failed;

            // Reject weak passwords before consuming the user's valid code.
            foreach (var validator in _users.PasswordValidators)
            {
                if (!(await validator.ValidateAsync(_users, profile, newPassword)).Succeeded)
                    return ResetResult.Failed;
            }

            var verify = await _otp.VerifyAsync(profile.Id, OtpPurpose.PasswordReset, code, ct);
            if (!verify.Success)
                return ResetResult.Failed;

            profile.PasswordHash = _users.PasswordHasher.HashPassword(profile, newPassword);
            profile.SecurityStamp = Guid.NewGuid().ToString("N");
            profile.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            profile.AccessFailedCount = 0;
            profile.LockoutEnd = null;
            return ResetResult.Succeeded;
        }, cancellationToken);
    }

    // Serialize account mutations across gateway instances, including registration
    // where no profile row exists yet. Persist OTP, account and outbox atomically.
    private Task<T> WithAccountTransactionAsync<T>(string email, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        => _db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // A retry must reload database state after the previous rollback.
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({email}, 0))", ct);
            var result = await action(ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);

    private async Task<bool> IsCoolingDownAsync(int profileId, OtpPurpose purpose, CancellationToken ct)
    {
        var lastCode = await _db.OtpCodes
            .Where(o => o.ProfileId == profileId && o.Purpose == purpose)
            .MaxAsync(o => (DateTimeOffset?)o.CreatedAt, ct);
        return lastCode is not null &&
            _clock.GetUtcNow() < lastCode.Value.AddSeconds(_auth.Value.Otps.ResendCooldownInSeconds);
    }

    private async Task QueueCodeAsync(Profile profile, OtpPurpose purpose, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var code = await _otp.GenerateAsync(profile.Id, purpose, ct);
        var eventId = Guid.NewGuid();
        var correlationId = eventId.ToString("D");
        object payload = purpose == OtpPurpose.EmailVerification
            ? new EmailVerificationRequested
            {
                ProfilePublicId = profile.PublicId, Email = profile.Email!, OtpCode = code,
                ExpiresAtUtc = now.AddMinutes(_auth.Value.Otps.ExpiresInMinutes),
                EventId = eventId, OccurredAtUtc = now, CorrelationId = correlationId,
            }
            : new PasswordResetRequested
            {
                ProfilePublicId = profile.PublicId, Email = profile.Email!, OtpCode = code,
                ExpiresAtUtc = now.AddMinutes(_auth.Value.Otps.ExpiresInMinutes),
                EventId = eventId, OccurredAtUtc = now, CorrelationId = correlationId,
            };
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = purpose == OtpPurpose.EmailVerification ? nameof(EmailVerificationRequested) : nameof(PasswordResetRequested),
            Payload = JsonSerializer.Serialize(payload, _jsonOptions),
            CorrelationId = correlationId,
            CreatedAt = now,
        });
    }

    public async Task<ProfileResult?> GetProfileAsync(Guid profilePublicId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == profilePublicId, cancellationToken);
        if (profile is null)
            return ProfileResult.NotFound;

        return ProfileResult.FoundProfile(profile.FullName, profile.PublicId);
    }

    public async Task<ProfileResult> UpdateProfileAsync(Guid profilePublicId, ProfileUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return ProfileResult.NotFound;

        var profile = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == profilePublicId, cancellationToken);
        if (profile is null)
            return ProfileResult.NotFound;

        var trimmed = Trim(request.FullName);
        if (trimmed.Length < 1 || trimmed.Length > 255)
            return ProfileResult.NotFound;

        profile.FullName = trimmed;
        await _db.SaveChangesAsync(cancellationToken);

        return ProfileResult.FoundProfile(trimmed, profile.PublicId);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string Trim(string value) => value.Trim();

    private async Task<Profile?> FindByEmailAsync(string normalized, CancellationToken cancellationToken) =>
        await _db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

    private async Task<bool> ProfileExistsAsync(string normalized, CancellationToken cancellationToken) =>
        await _db.Users.AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private async Task<string> BuildTokenAsync(Profile profile, CancellationToken cancellationToken)
    {
        var roleNames = await _users.GetRolesAsync(profile);
        var subject = profile.PublicId.ToString("D");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("security_stamp", profile.SecurityStamp!),
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, profile.NormalizedEmail ?? profile.Email ?? string.Empty),
        };

        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
        }

        var roleIds = await _db.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        var permissionClaims = await _db.RoleClaims
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == Role.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .ToListAsync(cancellationToken);

        foreach (var claimValue in permissionClaims)
        {
            claims.Add(new Claim(Role.PermissionClaimType, claimValue!));
        }

        var signingKey = Convert.FromBase64String(_auth.Value.Jwt.SigningKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(signingKey), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _auth.Value.Jwt.Issuer,
            audience: _auth.Value.Jwt.Audience,
            claims: claims,
            notBefore: _clock.GetUtcNow().UtcDateTime,
            expires: _clock.GetUtcNow().AddMinutes(15).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

