using Npgsql;
using Testcontainers.PostgreSql;

namespace Companion.Api.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL 17 container shared by every test. Each test asks for its own empty
/// database so tests never see each other's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Image passed to the constructor: the parameterless overload is obsolete in
    // Testcontainers 4.x and fails the build under TreatWarningsAsErrors.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a brand-new empty database and returns a connection string for it.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var database = $"companion_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
        }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
