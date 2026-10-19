using System.Security.Cryptography;
using System.Text;
using Companion.Data.Interfaces;
using Companion.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Companion.Data;

/// <summary>
/// HMAC-verified OTP store. The plaintext code is never persisted; only
/// <see cref="OtpCode.CodeHash"/> (HMAC-SHA256 under the server pepper) is written to the
/// database.
/// </summary>
public sealed class OtpService : IOtpService
{
    private readonly CompanionDbContext _db;
    private readonly OtpOptions _options;
    private readonly byte[] _pepper;
    private readonly TimeProvider _clock;

    public OtpService(CompanionDbContext db, OtpOptions options, byte[] pepper, TimeProvider clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pepper = pepper ?? throw new ArgumentNullException(nameof(pepper));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<string> GenerateAsync(int profileId, OtpPurpose purpose, CancellationToken cancellationToken = default)
        => WithProfileLockAsync(profileId, async ct =>
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000)
                .ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var now = _clock.GetUtcNow();
            await _db.OtpCodes.Where(o => o.ProfileId == profileId && o.Purpose == purpose)
                .ExecuteDeleteAsync(ct);
            _db.OtpCodes.Add(new OtpCode
            {
                ProfileId = profileId,
                Purpose = purpose,
                CodeHash = ComputeHash(code),
                ExpiresAt = now.AddMinutes(_options.ExpiresInMinutes),
                CreatedAt = now,
            });
            await _db.SaveChangesAsync(ct);
            return code;
        }, cancellationToken);

    public Task<OtpVerifyResult> VerifyAsync(int profileId, OtpPurpose purpose, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return WithProfileLockAsync(profileId, async ct =>
        {
            var query = _db.OtpCodes.Where(o => o.ProfileId == profileId && o.Purpose == purpose && o.ConsumedAt == null);
            var otp = await query.AsNoTracking().OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
            if (otp is null)
                return new OtpVerifyResult { Failure = OtpValidationFailure.Invalid };

            var now = _clock.GetUtcNow();
            if (otp.ExpiresAt <= now)
                return new OtpVerifyResult { Failure = OtpValidationFailure.Expired };
            if (otp.Attempts >= _options.MaxAttempts)
                return new OtpVerifyResult { Failure = OtpValidationFailure.Invalid };

            if (!CryptographicOperations.FixedTimeEquals(otp.CodeHash, ComputeHash(code)))
            {
                await query.Where(o => o.Id == otp.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Attempts, o => o.Attempts + 1), ct);
                return new OtpVerifyResult
                {
                    Failure = OtpValidationFailure.CodeMismatch,
                    RemainingAttempts = _options.MaxAttempts - otp.Attempts - 1,
                };
            }

            await query.Where(o => o.Id == otp.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.ConsumedAt, (DateTimeOffset?)now), ct);
            return new OtpVerifyResult { Success = true, ExpiresAtUtc = otp.ExpiresAt };
        }, cancellationToken);
    }

    private async Task<T> WithProfileLockAsync<T>(int profileId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        async Task<T> ExecuteAsync(CancellationToken token)
        {
            // Separate advisory-lock namespace from the account-level lock. All
            // issuers and verifiers serialize, even when using independent contexts.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(5198928, {profileId})", token);
            return await action(token);
        }

        if (_db.Database.CurrentTransaction is not null)
            return await ExecuteAsync(ct);

        var firstAttempt = true;
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            if (!firstAttempt)
                _db.ChangeTracker.Clear();
            firstAttempt = false;
            await using var transaction = await _db.Database.BeginTransactionAsync(token);
            var result = await ExecuteAsync(token);
            await transaction.CommitAsync(token);
            return result;
        }, ct);
    }

    private byte[] ComputeHash(string code)
    {
        using var hmac = new HMACSHA256(_pepper);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
    }

}
