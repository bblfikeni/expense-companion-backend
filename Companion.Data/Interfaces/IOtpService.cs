using System.Runtime.CompilerServices;

using Companion.Data.Interfaces;
using Companion.Data.Models;

namespace Companion.Data;

/// <summary>One-time-password store + verifier.</summary>
public interface IOtpService
{
    /// <summary>Creates a fresh code for <paramref name="profileId"/> and deletes any
    /// older code of the same <paramref name="purpose"/> (supersession).</summary>
    /// <returns>The plaintext code. It is logged nowhere and returned to the caller
    /// only so an outbox event may carry it for later delivery.</returns>
    Task<string> GenerateAsync(int profileId, OtpPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>Verifies a code and, on success, consumes it.</summary>
    Task<OtpVerifyResult> VerifyAsync(int profileId, OtpPurpose purpose, string code, CancellationToken cancellationToken = default);
}
