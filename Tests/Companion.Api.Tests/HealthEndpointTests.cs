using System.Net;
using Companion.Api.Tests.Infrastructure;

namespace Companion.Api.Tests;

[Collection(PostgresCollection.Name)]
public class HealthEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    private CompanionApiFactory? _factory;

    public HealthEndpointTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
        => _factory = new CompanionApiFactory(await _postgres.CreateEmptyDatabaseAsync());

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var client = _factory!.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
