using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Companion.Api.Dtos;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Extensions;
using Companion.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AuthApiTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CompanionApiFactory? _factory;
    private HttpClient? _client;

    public AuthApiTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new CompanionApiFactory(await _postgres.CreateEmptyDatabaseAsync());
        _client = _factory!.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private static string RandomEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<HttpResponseMessage> PostJson(string path, object body)
    {
        var content = JsonContent.Create(body);
        return await _client!.PostAsync(path, content);
    }

    private async Task<T> ReadJson<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
    }

    private async Task<Dictionary<string, object?>> ReadProblem(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
    }

    private async Task RegisterAsync(string email)
    {
        var response = await PostJson("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
            FullName = "Test User",
            PopiaConsent = true,
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private async Task VerifyEmailAsync(string email)
    {
        string code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");
        await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });
    }

    private async Task<string> ReadOtpFromOutbox(string email, string type)
    {
        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var messages = await db.OutboxMessages.Where(o => o.Type == type).OrderBy(o => o.CreatedAt).ToListAsync();
        var message = messages.First(m => JsonSerializer.Deserialize<JsonElement>(m.Payload)
            .GetProperty("profilePublicId").GetGuid() == profile.PublicId);

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.Payload);
        Assert.NotNull(payload);
        return payload!["otpCode"].GetString()!;
    }

    private async Task<string> ReadLatestOtpFromOutbox(string email, string type)
    {
        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var messages = await db.OutboxMessages.Where(o => o.Type == type).OrderByDescending(o => o.Id).ToListAsync();
        var message = messages.First(m => JsonSerializer.Deserialize<JsonElement>(m.Payload)
            .GetProperty("profilePublicId").GetGuid() == profile.PublicId);

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.Payload);
        Assert.NotNull(payload);
        return payload!["otpCode"].GetString()!;
    }

    private static string ExtractJwtClaim(string token, string claimType)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Not a JWT.");

        var payload = parts[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(claimType, out var claim))
            throw new InvalidOperationException($"Claim '{claimType}' not found in JWT.");
        return claim.GetString()!;
    }

    private static IEnumerable<string> ExtractJwtClaims(string token, string claimType)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Not a JWT.");

        var payload = parts[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(claimType).ValueKind == JsonValueKind.Array
            ? doc.RootElement.GetProperty(claimType).EnumerateArray().Select(e => e.GetString()!).ToArray()
            : new[] { doc.RootElement.GetProperty(claimType).GetString()! };
    }

    // ── Register ────

    [Fact]
    public async Task Register_HappyPath_ReturnsAccepted_WithOutboxAndProfile()
    {
        var email = RandomEmail();
        var body = new RegisterRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
            FullName = "Alice Example",
            PopiaConsent = true,
        };

        var response = await PostJson("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The same email may be re-registered: identical accepted response, no duplicate profile.
        var response2 = await PostJson("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Accepted, response2.StatusCode);

        // Profile exists exactly once.
        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant()));

        // Outbox contains exactly one EmailVerificationRequested.
        var outbox = await db.OutboxMessages
            .Where(o => o.Type == "EmailVerificationRequested")
            .ToListAsync();
        Assert.Single(outbox);
    }

    [Fact]
    public async Task Register_ConsentFalse_Returns400()
    {
        var email = RandomEmail();
        var body = new RegisterRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
            FullName = "Bob Example",
            PopiaConsent = false,
        };

        var response = await PostJson("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadJson<JsonElement>(response);
        Assert.Equal("Consent required", problem.GetProperty("title").GetString());

        // No profile created.
        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(0, await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var email = RandomEmail();
        foreach (var (password, fullName, consent) in new[]
        {
            ("", "Name", true),
            ("Str0ng-pass!", "", true),
        })
        {
            var body = new RegisterRequest
            {
                Email = email,
                Password = password,
                FullName = fullName,
                PopiaConsent = consent,
            };
            var response = await PostJson("/api/auth/register", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    // ── Verify email ─────

    [Fact]
    public async Task VerifyEmail_ValidCode_Returns204()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        // Read OTP from outbox payload only (never from service response).
        string code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");

        var response = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        Assert.True(profile.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmail_WrongCode_Returns400()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        var response = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = "000000",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredCode_Returns400()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        // Manually expire the OTP by setting its expiry in the past.
        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
            var otp = await db.OtpCodes
                .SingleAsync(o => o.ProfileId == profile.Id && o.Purpose == OtpPurpose.EmailVerification);
            otp.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // Read the (now expired) code from outbox and try to verify.
        string code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");

        var response = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_AttemptCap_Returns400_AfterMaxAttempts()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        string code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");

        // Use a wrong code up to MaxAttempts (5) times.
        for (int i = 0; i < 5; i++)
        {
            var response = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
            {
                Email = email,
                Code = "000000",
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Even the correct code is now rejected because the OTP is locked.
        var final = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });
        Assert.Equal(HttpStatusCode.BadRequest, final.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_SingleUse_CannotReuseAfterSuccess()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        string code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");

        var first = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Same code again must fail (consumed).
        var second = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_Supersession_NewCodeInvalidatesOld()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        string oldCode = await ReadOtpFromOutbox(email, "EmailVerificationRequested");

        // Bypass the 60s resend cooldown by backdating the OTP row, so resend issues a new code.
        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
            var otp = await db.OtpCodes
                .Where(o => o.ProfileId == profile.Id && o.Purpose == OtpPurpose.EmailVerification)
                .OrderByDescending(o => o.CreatedAt)
                .FirstAsync();
            otp.CreatedAt = otp.CreatedAt.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var resend = await PostJson("/api/auth/resend-verification", new ResendVerificationRequest { Email = email });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        string newCode = await ReadLatestOtpFromOutbox(email, "EmailVerificationRequested");

        // Old code no longer works.
        var oldResponse = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = oldCode,
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldResponse.StatusCode);

        // New code works.
        var newResponse = await PostJson("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = newCode,
        });
        Assert.Equal(HttpStatusCode.NoContent, newResponse.StatusCode);
    }

    // ── Resend verification ─────

    [Fact]
    public async Task ResendVerification_Cooldown_ReturnsAccepted()
    {
        var email = RandomEmail();
        await RegisterAsync(email);

        // First resend is accepted.
        var response = await PostJson("/api/auth/resend-verification", new ResendVerificationRequest { Email = email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_UnknownEmail_ReturnsAccepted_NoEnumeration()
    {
        // Must not reveal whether the email exists.
        var response = await PostJson("/api/auth/resend-verification", new ResendVerificationRequest
        {
            Email = "nobody@example.com",
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // ── Login ─────

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithToken()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        var response = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson<JsonElement>(response);
        Assert.True(body.TryGetProperty("accessToken", out var token));
        Assert.False(string.IsNullOrWhiteSpace(token.GetString()));
        Assert.Equal(900, body.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());

        // JWT sub must be the Guid PublicId, never the int PK.
        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        var tokenString = token.GetString()!;
        var sub = ExtractJwtClaim(tokenString, "sub");
        Assert.Equal(profile.PublicId.ToString("D"), sub);
        Assert.NotEqual(profile.Id.ToString(), sub);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_Returns401()
    {
        var email = RandomEmail();
        await RegisterAsync(email); // no verification

        var response = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401_NoEnumeration()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        // Wrong password.
        var response = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Wrong-pass!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Unknown email also returns 401 (same generic message).
        var response2 = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response2.StatusCode);
    }

    // ── Forgot / Reset password ─────

    [Fact]
    public async Task ForgotPassword_ConfirmedEmail_Returns202_WithOutbox()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        var response = await PostJson("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = email,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = _factory!.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var outbox = await db.OutboxMessages
            .Where(o => o.Type == "PasswordResetRequested")
            .ToListAsync();
        Assert.Single(outbox);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns202_NoEnumeration()
    {
        // Must not reveal whether the email exists.
        var response = await PostJson("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "nobody@example.com",
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ValidCode_Returns204()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        await PostJson("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });

        string code = await ReadOtpFromOutbox(email, "PasswordResetRequested");

        var response = await PostJson("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = email,
            Code = code,
            NewPassword = "New-Str0ng-pass!",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Can now log in with the new password.
        var login = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "New-Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WrongCode_Returns400()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        await PostJson("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });

        var response = await PostJson("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = email,
            Code = "000000",
            NewPassword = "New-Str0ng-pass!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Profile ─────

    [Fact]
    public async Task GetProfile_WithoutToken_Returns401()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        // Extract the public id from the database.
        Guid publicId;
        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            publicId = await db.Users
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .Select(u => u.PublicId)
                .SingleAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/profile?profilePublicId={publicId}");
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WithToken_Returns200()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        var login = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadJson<JsonElement>(login);
        var token = body.GetProperty("accessToken").GetString()!;

        Guid publicId;
        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            publicId = await db.Users
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .Select(u => u.PublicId)
                .SingleAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/profile?profilePublicId={publicId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithToken_Returns200()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        var login = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });
        var body = await ReadJson<JsonElement>(login);
        var token = body.GetProperty("accessToken").GetString()!;

        Guid publicId;
        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            publicId = await db.Users
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .Select(u => u.PublicId)
                .SingleAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/profile")
        {
            Content = JsonContent.Create(new ProfileUpdateRequest { FullName = "Updated Name" }),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.RequestUri = new Uri($"/api/profile?profilePublicId={publicId}", UriKind.Relative);

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── JWT claims ─────

    [Fact]
    public async Task Jwt_ContainsPlatformAdminPermissionClaim()
    {
        // Seed a platform-admin role claim directly into the DB, then assign the role to a user.
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);

        using (var scope = _factory!.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            var profile = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());

            // Add the platform-admin role (seeded by migration) to this profile.
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Profile>>();
            var result = await userManager.AddToRoleAsync(profile, Role.PlatformAdmin);
            Assert.True(result.Succeeded);
        }

        var login = await PostJson("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokenBody = await ReadJson<JsonElement>(login);
        var token = tokenBody.GetProperty("accessToken").GetString()!;

        var permissions = ExtractJwtClaims(token, Role.PermissionClaimType);
        Assert.Contains(Role.PlatformPermission, permissions);
    }
    private async Task<(string Token, Guid PublicId)> CreateAuthenticatedAccountAsync()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        var response = await PostJson("/api/auth/login", new LoginRequest { Email = email, Password = "Str0ng-pass!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await ReadJson<JsonElement>(response)).GetProperty("accessToken").GetString()!;
        return (token, Guid.Parse(ExtractJwtClaim(token, "sub")));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Profile_AnotherAccount_ReturnsForbidden(string method)
    {
        var owner = await CreateAuthenticatedAccountAsync();
        var other = await CreateAuthenticatedAccountAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/profile?profilePublicId={owner.PublicId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", other.Token);
        if (method == "PUT")
            request.Content = JsonContent.Create(new ProfileUpdateRequest { FullName = "Unauthorized change" });
        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal("Test User", (await db.Users.SingleAsync(p => p.PublicId == owner.PublicId)).FullName);
    }

    [Fact]
    public async Task Register_ConcurrentRequests_CreateOneAccountAndMessage()
    {
        var email = RandomEmail();
        var payload = new RegisterRequest { Email = email, Password = "Str0ng-pass!", FullName = "Concurrent User", PopiaConsent = true };
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostJson("/api/auth/register", payload)));
        foreach (var response in responses)
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.OtpCodes.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task VerifyEmail_ConcurrentRequests_ConsumeCodeOnce()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        var code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostJson("/api/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = code })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task ForgotPassword_ConcurrentRequests_RespectCooldown()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostJson("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email })));
        foreach (var response in responses)
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Type == "PasswordResetRequested"));
        Assert.Equal(1, await db.OtpCodes.CountAsync(o => o.Purpose == OtpPurpose.PasswordReset));
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_PreservesCode_AndSuccessRevokesOldToken()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        var login = await PostJson("/api/auth/login", new LoginRequest { Email = email, Password = "Str0ng-pass!" });
        var token = (await ReadJson<JsonElement>(login)).GetProperty("accessToken").GetString()!;
        await PostJson("/api/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        var code = await ReadOtpFromOutbox(email, "PasswordResetRequested");
        var invalid = await PostJson("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Code = code, NewPassword = "weak" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var valid = await PostJson("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Code = code, NewPassword = "New-Str0ng-pass!" });
        Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/profile?profilePublicId={ExtractJwtClaim(token, "sub")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client!.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task ResendVerification_ResponsesDoNotRevealRegisteredAccounts()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        var registered = await PostJson("/api/auth/resend-verification", new ResendVerificationRequest { Email = email });
        var unknown = await PostJson("/api/auth/resend-verification", new ResendVerificationRequest { Email = RandomEmail() });
        Assert.Equal(await registered.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_OverlongName_ReturnsBadRequestWithoutPersisting()
    {
        var response = await PostJson("/api/auth/register", new RegisterRequest
        {
            Email = RandomEmail(), Password = "Str0ng-pass!", FullName = new string('a', 256), PopiaConsent = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_OutboxFailure_RollsBackProfileAndOtp()
    {
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_outbox() RETURNS trigger LANGUAGE plpgsql AS
                $$ BEGIN RAISE EXCEPTION 'Injected outbox failure'; END $$;
                CREATE TRIGGER reject_outbox BEFORE INSERT ON outbox_messages
                FOR EACH ROW EXECUTE FUNCTION reject_outbox();
                """);
        }
        var response = await PostJson("/api/auth/register", new RegisterRequest
        {
            Email = RandomEmail(), Password = "Str0ng-pass!", FullName = "Rollback User", PopiaConsent = true,
        });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var checkScope = _factory!.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(0, await checkDb.Users.CountAsync());
        Assert.Equal(0, await checkDb.OtpCodes.CountAsync());
        Assert.Equal(0, await checkDb.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task VerifyEmail_ProfileUpdateFailure_DoesNotConsumeCode()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        var code = await ReadOtpFromOutbox(email, "EmailVerificationRequested");
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_confirmation() RETURNS trigger LANGUAGE plpgsql AS
                $$ BEGIN RAISE EXCEPTION 'Injected confirmation failure'; END $$;
                CREATE TRIGGER reject_confirmation BEFORE UPDATE ON profiles
                FOR EACH ROW EXECUTE FUNCTION reject_confirmation();
                """);
        }
        var response = await PostJson("/api/auth/verify-email", new VerifyEmailRequest { Email = email, Code = code });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
            Assert.Null((await db.OtpCodes.SingleAsync()).ConsumedAt);
            Assert.False((await db.Users.SingleAsync()).EmailConfirmed);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_confirmation ON profiles");
        }
        var retry = await PostJson("/api/auth/verify-email", new VerifyEmailRequest { Email = email, Code = code });
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task OtpService_ConcurrentWrongGuesses_DoNotLoseAttemptCounts()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        int profileId;
        using (var scope = _factory!.Services.CreateScope())
            profileId = await scope.ServiceProvider.GetRequiredService<CompanionDbContext>().Users.Select(p => p.Id).SingleAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using var scope = _factory!.Services.CreateScope();
            var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
            return await otp.VerifyAsync(profileId, OtpPurpose.EmailVerification, "000000");
        }));
        Assert.All(results, r => Assert.False(r.Success));
        using var checkScope = _factory!.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        Assert.Equal(5, (await checkDb.OtpCodes.SingleAsync()).Attempts);
    }

    [Fact]
    public async Task Register_WeakPassword_DoesNotRevealExistingAccounts()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        foreach (var address in new[] { email, RandomEmail() })
        {
            var response = await PostJson("/api/auth/register", new RegisterRequest
            {
                Email = address, Password = "weak", FullName = "Test User", PopiaConsent = true,
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_ConcurrentWrongPasswords_EnforcesLockout()
    {
        var email = RandomEmail();
        await RegisterAsync(email);
        await VerifyEmailAsync(email);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostJson("/api/auth/login",
            new LoginRequest { Email = email, Password = "Wrong-pass!" })));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        var valid = await PostJson("/api/auth/login", new LoginRequest { Email = email, Password = "Str0ng-pass!" });
        Assert.Equal(HttpStatusCode.Unauthorized, valid.StatusCode);
        using var scope = _factory!.Services.CreateScope();
        var profile = await scope.ServiceProvider.GetRequiredService<CompanionDbContext>().Users.SingleAsync();
        Assert.True(profile.LockoutEnd > DateTimeOffset.UtcNow);
    }

}