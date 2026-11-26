using Npgsql;

namespace Companion.Api.Tests.Infrastructure;

[Collection(PostgresCollection.Name)]
public sealed class PostgresFixtureTests(PostgresFixture postgres)
{
    [Fact]
    public async Task DisposedConnections_DoNotRemainOpenForEachTestDatabase()
    {
        var target = await postgres.CreateEmptyDatabaseAsync();
        var database = new NpgsqlConnectionStringBuilder(target).Database;
        await using (var connection = new NpgsqlConnection(target))
            await connection.OpenAsync();

        var observer = await postgres.CreateEmptyDatabaseAsync();
        await using var observerConnection = new NpgsqlConnection(observer);
        await observerConnection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE datname = @database", observerConnection);
        command.Parameters.AddWithValue("database", database!);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}