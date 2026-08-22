using GongWei.AdminCli;
using GongWei.AdminCli.Commands;
using GongWei.Application.Abstractions;
using GongWei.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var parsed = CliArguments.Parse(args);

if (parsed is null || parsed.Command is "help" or "--help" or "-h")
{
    PrintUsage();
    return parsed is null ? ExitCode.BadArguments : ExitCode.Success;
}

// Generic Host only — no Kestrel, no listening port (bootstrap §6.4). The content root is
// pinned to the binary's own folder so the tool behaves identically whichever directory
// the operator happens to run it from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables("GONGWEI_");

builder.Services.AddSingleton<ICurrentUser, ConsoleOperator>();
builder.Services.AddGongWeiInfrastructure(builder.Configuration);

builder.Services.AddScoped<GrantSuperAdminCommand>();
builder.Services.AddScoped<VerifyDatabaseCommand>();
builder.Services.AddScoped<ShowMigrationStatusCommand>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    return parsed.Command switch
    {
        "grant-super-admin" => await scope.ServiceProvider
            .GetRequiredService<GrantSuperAdminCommand>()
            .RunAsync(parsed, cancellation.Token),

        "verify-database" => await scope.ServiceProvider
            .GetRequiredService<VerifyDatabaseCommand>()
            .RunAsync(cancellation.Token),

        "show-migration-status" => await scope.ServiceProvider
            .GetRequiredService<ShowMigrationStatusCommand>()
            .RunAsync(cancellation.Token),

        _ => UnknownCommand(parsed.Command)
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled.");
    return ExitCode.BadArguments;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    return ExitCode.BadArguments;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        GongWei.AdminCli — server-local maintenance for 宮闈浮生.

        Commands:
          grant-super-admin --line-user-id <LINE_SUB> --reason '<why>' [--confirm '<phrase>']
              Promotes an account that has already signed in through LINE Login.
              Idempotent. Prompts for confirmation unless --confirm carries the exact phrase.

          verify-database
              Checks migrations, table count, triggers, uuid defaults, the super admin,
              and the rules/NPC seeds.

          show-migration-status
              Lists applied and pending migrations.

        Configuration:
          ConnectionStrings__GameDb must be set through user-secrets or the environment.
          It is never read from appsettings.json.

        Exit codes: 0 ok · 2 bad arguments · 3 user not found · 4 database failure
                    5 not confirmed · 6 verification failed
        """);
}

/// <summary>Named so <c>AddUserSecrets&lt;Program&gt;</c> can resolve the assembly.</summary>
public partial class Program;
