using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Companion.Api.Options;
using Microsoft.Extensions.Options;

namespace Companion.Api.Services;

/// <summary>
/// Validates Cloudflare Turnstile tokens server-side.
/// </summary>
public interface ITurnstileValidator
{
    /// <summary>
    /// Validates a Turnstile token and returns whether it's valid.
    /// </summary>
    /// <param name="token">The Turnstile response token from the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Record representing Turnstile validation result.
/// </summary>
public sealed class TurnstileValidationResult
{
    public static TurnstileValidationResult Success => new(true);

    public static TurnstileValidationResult Failure => new(false);

    private TurnstileValidationResult(bool valid)
    {
        Valid = valid;
    }

    public bool Valid { get; }

    public static implicit operator bool(TurnstileValidationResult result) => result.Valid;
}

/// <summary>
/// Implementation of ITurnstileValidator using Cloudflare's Verify API endpoint.
/// </summary>
public sealed class TurnstileValidator : ITurnstileValidator
{
    private readonly AuthOptions _options;
    private readonly HttpClient _httpClient;

    public TurnstileValidator(IOptions<AuthOptions> options, HttpClient httpClient)
    {
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        // If Turnstile is not configured (blank secret key), allow through
        if (string.IsNullOrWhiteSpace(_options.Turnstile.SecretKey))
            return true;

        cancellationToken.ThrowIfCancellationRequested();

        var requestBody = new
        {
            secret = _options.Turnstile.SecretKey,
            response = token
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://challenges.cloudflare.com/v1/siteverify")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Log and treat as failure
            return false;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("success", out var successElem) &&
            successElem.GetBoolean())
        {
            return true;
        }

        return false;
    }
}