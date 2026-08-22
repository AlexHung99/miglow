using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GongWei.Postgres.Tests;

/// <summary>
/// These tests run against a real PostgreSQL database — the point is to prove the EF
/// model and db/schema_v0.8.sql agree, and that the triggers actually fire, neither of
/// which an in-memory provider can tell you.
///
/// Set GONGWEI_TEST_CONNECTION to a throwaway database and they run; leave it unset and
/// they skip rather than failing a machine that has no database (spec §14).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "GONGWEI_TEST_CONNECTION";

    public string? ConnectionString { get; private set; }

    public bool IsAvailable => ConnectionString is not null;

    public string SkipReason =>
        $"Set {ConnectionVariable} to a throwaway PostgreSQL database to run the schema tests.";

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // Prove the server is reachable before declaring the fixture available, so an
        // unreachable database skips rather than failing every test with a socket error.
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (NpgsqlException)
        {
            return;
        }

        ConnectionString = connectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public GongWeiDbContext CreateContext()
    {
        if (ConnectionString is null)
        {
            throw new InvalidOperationException(SkipReason);
        }

        var options = new DbContextOptionsBuilder<GongWeiDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", GongWeiDbContext.SchemaName))
            .Options;

        return new GongWeiDbContext(options);
    }

    public NpgsqlConnection CreateConnection()
    {
        if (ConnectionString is null)
        {
            throw new InvalidOperationException(SkipReason);
        }

        return new NpgsqlConnection(ConnectionString);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
