using Companion.Api.Options;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Companion.Api.Services;

public sealed record GoogleIdentity(string Subject, string Email, string? Name, bool EmailVerified, string? HostedDomain = null)
{
    public bool HasAuthoritativeEmail => EmailVerified &&
        (Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(HostedDomain));
}

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly string _clientId;

    public GoogleIdTokenValidator(IOptions<AuthOptions> options)
        => _clientId = options.Value.Google.ClientId;

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_clientId],
                });

            cancellationToken.ThrowIfCancellationRequested();
            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name, payload.EmailVerified, payload.HostedDomain);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
