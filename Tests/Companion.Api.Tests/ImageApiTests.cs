using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Companion.Api.Options;
using Companion.Api.Features.Images;
using Companion.Api.Tests.Infrastructure;
using Companion.Data;
using Companion.Data.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ImageApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly byte[] Png = Convert.FromHexString("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360606060000000050001A5F645400000000049454E44AE426082");
    private static readonly byte[] Jpeg = Convert.FromHexString("FFD8FFE000104A46494600010100000100010000FFC00011080002000303011100021101031101FFDA000C03010002110311003F0000FFD9");
    private static readonly byte[] Webp = Convert.FromHexString("5249464616000000574542505650384C090000002F024000000000000000");
    private CompanionApiFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _expense = Guid.NewGuid();
    private string _ownerToken = null!;
    private string _otherToken = null!;
    private readonly CaptureLogs _logs = new();
    private string Path => $"/api/expenses/{_expense}/images";

    public async Task InitializeAsync()
    {
        _factory = new CompanionApiFactory(await postgres.CreateEmptyDatabaseAsync(), services =>
            services.AddSingleton<ILoggerFactory>(_ => LoggerFactory.Create(logging => logging.AddProvider(_logs).SetMinimumLevel(LogLevel.Information))));
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var owner = new Profile { FullName = "Owner", PublicId = Guid.NewGuid(), SecurityStamp = Guid.NewGuid().ToString(), PopiaConsentDate = DateTimeOffset.UtcNow };
        var other = new Profile { FullName = "Other", PublicId = Guid.NewGuid(), SecurityStamp = Guid.NewGuid().ToString(), PopiaConsentDate = DateTimeOffset.UtcNow };
        var workspace = new Workspace { PublicId = Guid.NewGuid(), Name = "Workspace" };
        db.Users.AddRange(owner, other);
        db.Workspaces.Add(workspace);
        db.WorkspaceMemberships.Add(new WorkspaceMembership { Workspace = workspace, Profile = owner });
        db.Expenses.Add(new Expense { PublicId = _expense, Workspace = workspace });
        await db.SaveChangesAsync();
        var options = scope.ServiceProvider.GetRequiredService<AuthOptions>().Jwt;
        _ownerToken = Token(owner, options);
        _otherToken = Token(other, options);
    }

    public Task DisposeAsync() { _client.Dispose(); _factory.Dispose(); return Task.CompletedTask; }

    private static string Token(Profile user, JwtOptions options, params string[] permissions)
    {
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.PublicId.ToString()), new Claim("security_stamp", user.SecurityStamp!), .. (permissions.Length == 0 ? new[] { "images:read", "images:write" } : permissions).Select(p => new Claim(Role.PermissionClaimType, p))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void Authenticate(string token) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static MultipartFormDataContent Upload(byte[] bytes, string type = "image/png")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", "receipt.png");
        return form;
    }

    private static byte[] PngWithPadding(int size, byte marker = 0)
    {
        var padding = size - Png.Length - 12;
        var result = new byte[size];
        Png.AsSpan(0, Png.Length - 12).CopyTo(result);
        var chunk = result.AsSpan(Png.Length - 12);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(chunk, padding);
        "npAd"u8.CopyTo(chunk[4..]);
        if (padding > 0) chunk[8] = marker;
        uint crc = 0xffffffff;
        foreach (var b in chunk.Slice(4, padding + 4))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk[(padding + 8)..], ~crc);
        Png.AsSpan(Png.Length - 12).CopyTo(chunk[(padding + 12)..]);
        return result;
    }

    [Fact]
    public async Task Lifecycle_EnforcesTenancy_AndConditionalGet()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Path)).StatusCode);
        Authenticate(_ownerToken);
        using var form = Upload(Png);
        form.Add(new StringContent("Receipt"), "label");
        var created = await _client.PostAsync(Path, form);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var metadata = (await created.Content.ReadFromJsonAsync<JsonElement>());
        var imagePath = $"{Path}/{metadata.GetProperty("public_id").GetGuid()}";
        Assert.False(metadata.TryGetProperty("imageId", out _));
        Assert.Equal("Receipt", metadata.GetProperty("label").GetString());
        Assert.Equal(1, metadata.GetProperty("width").GetInt32());
        Assert.Single((await _client.GetFromJsonAsync<JsonElement[]>(Path))!);
        var duplicate = await _client.PostAsync(Path, Upload(Png));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var image = await _client.GetAsync(imagePath + "/content");
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("inline", image.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Null(image.Content.Headers.ContentDisposition?.FileName);
        Assert.True(image.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(300), image.Headers.CacheControl?.MaxAge);
        Assert.NotNull(image.Headers.ETag);
        Assert.NotEmpty(_logs.Messages);
        Assert.DoesNotContain("receipt.png", string.Join('\n', _logs.Messages));
        using var conditional = new HttpRequestMessage(HttpMethod.Get, imagePath + "/content");
        conditional.Headers.IfNoneMatch.Add(image.Headers.ETag!);
        var notModified = await _client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.True(notModified.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(300), notModified.Headers.CacheControl?.MaxAge);
        Assert.Equal("inline", notModified.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Null(notModified.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(HttpStatusCode.OK, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal("Receipt", (await _client.GetFromJsonAsync<JsonElement[]>(Path))![0].GetProperty("label").GetString());
        Authenticate(_otherToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(imagePath + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(imagePath)).StatusCode);
        Authenticate(_ownerToken);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(imagePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(imagePath + "/content")).StatusCode);
    }

    [Fact]
    public async Task MalformedCrossTenantRequestsReturnNotFound()
    {
        Authenticate(_otherToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync(Path, new StringContent("invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync(Path, Upload(new byte[5 * 1024 * 1024 + 1]))).StatusCode);
        using var malformed = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsync($"{Path}/{Guid.NewGuid()}", malformed)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync($"{Path}/{Guid.NewGuid()}", new { pageNo = "bad" })).StatusCode);
        using var wrongType = new StringContent("invalid");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsync($"{Path}/{Guid.NewGuid()}", wrongType)).StatusCode);
        Authenticate(_ownerToken);
        using var ownerMalformed = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsync($"{Path}/{Guid.NewGuid()}", ownerMalformed)).StatusCode);
    }

    [Fact]
    public async Task EndpointsRequireTheirPermissionCodes()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var owner = await db.Users.SingleAsync(user => user.FullName == "Owner");
        var options = scope.ServiceProvider.GetRequiredService<AuthOptions>().Jwt;
        var imagePath = $"{Path}/{Guid.NewGuid()}";
        Authenticate(Token(owner, options, "unrelated"));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(imagePath + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.DeleteAsync(imagePath)).StatusCode);
        Authenticate(Token(owner, options, "images:read"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
        Authenticate(Token(owner, options, "images:write"));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsInvalidRequests()
    {
        Authenticate(_ownerToken);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await _client.PostAsync(Path, Upload(Png, "image/gif"))).StatusCode);
        foreach (var request in new[] { Upload([1, 2, 3]), Upload(Png, "image/jpeg"), Upload("%PDF-1.7"u8.ToArray(), "application/pdf"), Upload([0, 0, 0, 0], "image/heic") })
        {
            var response = await _client.PostAsync(Path, request);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await _client.PostAsync(Path, Upload(new byte[5 * 1024 * 1024 + 1]))).StatusCode);
        using var multiple = Upload(Png);
        multiple.Add(new ByteArrayContent(Png), "file", "second.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync(Path, multiple)).StatusCode);
        var invalidPatch = await _client.PatchAsJsonAsync($"{Path}/{Guid.NewGuid()}", new { pageNo = "bad" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPatch.StatusCode);
        Assert.Equal("application/problem+json", invalidPatch.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EveryEndpointRequiresAuthentication()
    {
        var imagePath = $"{Path}/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(imagePath + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync(imagePath)).StatusCode);
    }

    [Fact]
    public async Task MissingResourcesReturnNotFound()
    {
        Authenticate(_ownerToken);
        var imagePath = $"{Path}/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(imagePath + "/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(imagePath)).StatusCode);
        var missing = $"/api/expenses/{Guid.NewGuid()}/images";
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(missing)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync(missing, Upload(Png))).StatusCode);
    }

    [Fact]
    public async Task SupportedFormatsRoundTripAndProcessingBlocksMutations()
    {
        Authenticate(_ownerToken);
        string? imagePath = null;
        foreach (var (bytes, type) in new[] { (Png, "image/png"), (Jpeg, "image/jpeg"), (Webp, "image/webp") })
        {
            var response = await _client.PostAsync(Path, Upload(bytes, type));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var metadata = await response.Content.ReadFromJsonAsync<JsonElement>();
            imagePath = $"{Path}/{metadata.GetProperty("public_id").GetGuid()}";
            var content = await _client.GetAsync(imagePath + "/content");
            Assert.Equal(type, content.Content.Headers.ContentType?.MediaType);
            Assert.Equal(bytes, await content.Content.ReadAsByteArrayAsync());
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var expense = await db.Expenses.SingleAsync(e => e.PublicId == _expense);
        expense.Status = "Processing";
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync(Path, Upload(Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PatchAsJsonAsync(imagePath, new { pageNo = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync(imagePath)).StatusCode);
    }

    [Fact]
    public async Task FileLimitAndImageCountAreEnforced()
    {
        Authenticate(_ownerToken);
        var max = PngWithPadding(5 * 1024 * 1024);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync(Path, new UnknownLengthContent(Upload(max)))).StatusCode);
        var oversized = new byte[max.Length + 1];
        Png.CopyTo(oversized, 0);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await _client.PostAsync(Path, new UnknownLengthContent(Upload(oversized)))).StatusCode);
        for (var i = 0; i < 9; i++)
        {
            var bytes = PngWithPadding(Png.Length + 13, (byte)i);
            Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync(Path, Upload(bytes))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync(Path, Upload(PngWithPadding(Png.Length + 13, 10)))).StatusCode);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly HttpContent _inner;
        public UnknownLengthContent(HttpContent inner) { _inner = inner; Headers.ContentType = inner.Headers.ContentType; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _inner.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }

    [Fact]
    public async Task UploadSetsServerRequestLimitBeforeReading()
    {
        using var form = Upload(Png);
        var body = await form.ReadAsByteArrayAsync();
        var feature = new RequestSizeFeature();
        var response = await _factory.Server.SendAsync(context =>
        {
            context.Request.Method = "POST";
            context.Request.Path = Path;
            context.Request.Headers.Authorization = $"Bearer {_ownerToken}";
            context.Request.ContentType = form.Headers.ContentType!.ToString();
            context.Request.Body = new MemoryStream(body);
            context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
        });
        Assert.Equal(201, response.Response.StatusCode);
        Assert.Equal(5 * 1024 * 1024 + 64 * 1024, feature.MaxRequestBodySize);
    }

    private sealed class RequestSizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }

    [Fact]
    public async Task ConfiguredCacheAgeIsUsed()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("Images:CacheMaxAgeSeconds", "42"));
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanionDbContext>();
        var owner = await db.Users.SingleAsync(user => user.FullName == "Owner");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(owner, scope.ServiceProvider.GetRequiredService<AuthOptions>().Jwt));
        var created = await client.PostAsync(Path, Upload(Png));
        var metadata = await created.Content.ReadFromJsonAsync<JsonElement>();
        var response = await client.GetAsync($"{Path}/{metadata.GetProperty("public_id").GetGuid()}/content");
        Assert.Equal(TimeSpan.FromSeconds(42), response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public void NegativeCacheAgeIsRejected()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Images:CacheMaxAgeSeconds"] = "-1";
        var app = builder.Build();
        Assert.Throws<InvalidOperationException>(() => app.MapImageEndpoints(builder.Configuration));
    }

    private sealed class CaptureLogs : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
    }

    [Fact]
    public async Task OpenApiAndCors_ExposeImageContract()
    {
        var document = await _client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("multipart/form-data", document);
        Assert.Contains("/api/expenses/{expenseId}/images", document);
        Assert.Contains("/api/expenses/{expenseId}/images/{imageId}/content", document);
        Assert.Contains("public_id", document);
        using var request = new HttpRequestMessage(HttpMethod.Options, $"{Path}/{Guid.NewGuid()}");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "PATCH");
        var response = await _client.SendAsync(request);
        Assert.Contains("PATCH", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }
}