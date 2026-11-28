using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Net.Http.Json;
using Companion.Api.Dtos;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Companion.Api.Tests;

// Proves that a freshly registered+logged-in user can access POST /api/expenses
// because they now have "permission" claims in their JWT token (platform-admin role).
[Collection(PostgresCollection.Name)]
public sealed class ExpensesPermissionTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CompanionApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ExpensesPermissionTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        var connectionString = await _postgres.CreateEmptyDatabaseAsync();

        _factory = new CompanionApiFactory(
            connectionString);
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FreshUserCanAccessExpenses_WithPermissionClaims()
    {
        // Arrange: register a new user
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registerBody = new RegisterRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
            FullName = "Test User",
            PopiaConsent = true,
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerBody);
        Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);

        // Verify email via OTP (read from outbox)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var messages = await db.OutboxMessages.Where(o => o.Type == "EmailVerificationRequested").OrderBy(o => o.CreatedAt).ToListAsync();
        var message = messages.Last();
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload!);
        var otpCode = payload.GetProperty("otpCode").GetString()!;

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = otpCode,
            Password = "Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.NoContent, verifyResponse.StatusCode);

        // Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Str0ng-pass!",
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody!.GetProperty("accessToken").GetString()!;

        // Set the Bearer token on the client
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Capture the claims the policy sees and verify the authorization works
        using var claimScope = _factory.Services.CreateScope();
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);

        // Print claims the policy actually sees (types and values, no secrets)
        Console.WriteLine($"=== JWT Claims for user {email} ===");
        foreach (var claim in jwtToken.Claims)
        {
            Console.WriteLine($"  {claim.Type}: {claim.Value}");
        }

        // Act: try to post an expense (requires images:write policy)
        var expenseResponse = await _client.PostAsJsonAsync("/api/expenses", new { requestId = Guid.NewGuid() });

        // Assert: fresh user should now be able to access expenses because they have permission claims
        Assert.Equal(HttpStatusCode.Created, expenseResponse.StatusCode);

        // The test validates the 201 Created above; the console output provides diagnostics
    }
}