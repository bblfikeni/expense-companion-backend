using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Companion.Api.Tests.Infrastructure;
using Companion.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ExpenseExtractionApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private CompanionApiFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _pendingExpenseId = Guid.NewGuid();
    private string _ownerToken = null!;
    private string _otherToken = null!;

    public async Task InitializeAsync()
    {
        _factory = new CompanionApiFactory(await postgres.CreateEmptyDatabaseAsync());
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Companion.Data.CompanionDbContext>();
        var owner = NewProfile("Extraction owner");
        var other = NewProfile("Other workspace member");
        var workspace = new Workspace { Name = "Extraction workspace" };
        var otherWorkspace = new Workspace { Name = "Another workspace" };
        var expense = new Expense { PublicId = _expenseId, Workspace = workspace, Status = "Processing" };
        var pendingExpense = new Expense { PublicId = _pendingExpenseId, Workspace = workspace, Status = "Uploaded" };
        db.AddRange(owner, other, expense, pendingExpense,
            new WorkspaceMembership { Workspace = workspace, Profile = owner },
            new WorkspaceMembership { Workspace = otherWorkspace, Profile = other });
        await db.SaveChangesAsync();
        db.ExpenseExtractions.Add(new ExpenseExtraction
        {
            ExpenseId = expense.Id,
            ExpensePublicId = expense.PublicId,
            ImageSetHash = new string('a', 64),
            Status = "NeedsReview",
            Vendor = "Example Market",
            ExpenseDate = "2026-11-25",
            TotalAmountCents = 12345,
            VatAmountCents = 1609,
            VatNumber = "4123456789",
            VatClassification = "Standard_15",
            IsValidTaxInvoice = true,
            VendorConfidence = 0.9,
            DateConfidence = 0.8,
            TotalConfidence = 0.95,
            VatAmountConfidence = 0.7,
            VatNumberConfidence = 0.6,
            DocumentValidityConfidence = 0.85,
            OverallConfidence = 0.8,
            ReviewRequired = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var jwt = scope.ServiceProvider.GetRequiredService<Companion.Api.Options.AuthOptions>().Jwt;
        _ownerToken = Token(owner, jwt.SigningKey, jwt.Issuer, jwt.Audience);
        _otherToken = Token(other, jwt.SigningKey, jwt.Issuer, jwt.Audience);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExtractionStatusIsWorkspaceScopedAndContainsOnlyPublicResultFields()
    {
        var path = $"/api/expenses/{_expenseId}/extraction";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(path)).StatusCode);

        Authenticate(_ownerToken);
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(_expenseId, body.GetProperty("expenseId").GetGuid());
        Assert.Equal("Processing", body.GetProperty("expenseStatus").GetString());
        Assert.Equal("NeedsReview", body.GetProperty("extractionStatus").GetString());
        Assert.Equal("Example Market", body.GetProperty("vendor").GetString());
        Assert.Equal(12345, body.GetProperty("totalAmountCents").GetInt64());
        Assert.Equal(0.85, body.GetProperty("documentValidityConfidence").GetDouble(), 2);
        Assert.False(body.TryGetProperty("id", out _));
        Assert.False(body.TryGetProperty("expenseInternalId", out _));
        Assert.DoesNotContain("ocr", body.ToString(), StringComparison.OrdinalIgnoreCase);

        Authenticate(_otherToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(path)).StatusCode);

        Authenticate(_ownerToken);
        using var pending = await _client.GetFromJsonAsync<JsonDocument>($"/api/expenses/{_pendingExpenseId}/extraction");
        Assert.Equal("Uploaded", pending!.RootElement.GetProperty("expenseStatus").GetString());
        Assert.Equal("Pending", pending.RootElement.GetProperty("extractionStatus").GetString());
        Assert.True(pending.RootElement.GetProperty("totalAmountCents").ValueKind == JsonValueKind.Null);
    }

    private void Authenticate(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Profile NewProfile(string name) => new()
    {
        UserName = name,
        FullName = name,
        PublicId = Guid.NewGuid(),
        SecurityStamp = Guid.NewGuid().ToString(),
        PopiaConsentDate = DateTimeOffset.UtcNow,
    };

    private static string Token(Profile profile, string signingKey, string issuer, string audience)
    {
        var jwt = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, profile.PublicId.ToString()),
                new Claim("security_stamp", profile.SecurityStamp!),
                new Claim(Role.PermissionClaimType, "images:read")],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(signingKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}