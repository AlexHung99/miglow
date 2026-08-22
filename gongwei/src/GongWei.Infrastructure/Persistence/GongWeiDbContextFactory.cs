using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GongWei.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. It never opens a connection when only
/// generating migrations, so no real credentials are needed here — the connection
/// string comes from GONGWEI_DESIGN_CONNECTION when a command does need a database.
/// </summary>
public class GongWeiDbContextFactory : IDesignTimeDbContextFactory<GongWeiDbContext>
{
    private const string PlaceholderConnection =
        "Host=localhost;Port=5433;Database=gongwei;Username=design_time;Password=design_time";

    public GongWeiDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("GONGWEI_DESIGN_CONNECTION") ?? PlaceholderConnection;

        var options = new DbContextOptionsBuilder<GongWeiDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", GongWeiDbContext.SchemaName);
            })
            .Options;

        return new GongWeiDbContext(options);
    }
}
