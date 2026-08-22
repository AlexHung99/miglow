using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GongWei.Infrastructure.Persistence.Migrations;

/// <summary>
/// Applies everything the EF model cannot express: the touch/append-only/no-delete
/// trigger functions, the eight cross-table validation triggers, and the singleton
/// control rows.
///
/// The SQL lives in <c>Migrations/Sql/hardening_v1.1.sql</c> as an embedded resource,
/// copied verbatim from db/authoritative/v1.1/schema_v1.1.sql. Keeping it as SQL rather
/// than C# string literals means the two files can be diffed directly, which is what
/// caught the missing <c>gen_random_uuid()</c> defaults.
/// </summary>
public partial class SqlHardeningV11 : Migration
{
    private const string UpResource =
        "GongWei.Infrastructure.Persistence.Migrations.Sql.hardening_v1.1.sql";

    /// <summary>Names used by <see cref="Down"/>; the Up path drives them from SQL loops.</summary>
    private static readonly string[] TouchTables =
    [
        "users", "admin_role_assignments", "preset_portraits", "media_assets",
        "player_portrait_submissions", "character_applications", "ranks",
        "character_title_definitions", "residences", "characters",
        "character_title_assignments", "character_stats",
        "ability_label_definitions", "character_progress",
        "world_state", "game_settings", "world_locations", "npcs", "event_rooms", "event_posts",
        "external_play_submissions", "wallets", "inventory_entries", "market_offers",
        "reproduction_control", "heir_wait_pool_entries", "pregnancies",
        "audience_requests", "intrigue_actions", "approval_requests",
        "announcements", "scheduled_jobs"
    ];

    private static readonly string[] ImmutableTables =
    [
        "audit_logs", "ledger_entries", "inventory_transactions",
        "character_application_revisions", "character_status_history", "rank_history",
        "game_setting_revisions", "npc_revisions", "character_chronicle_entries",
        "event_post_revisions", "event_results", "births",
        "offspring_links", "deaths", "approval_decisions", "job_runs"
    ];

    private static readonly string[] ValidationFunctions =
    [
        "validate_birth_selection", "validate_pregnancy_mother", "validate_wait_pool_character",
        "validate_character_master_data", "validate_application_portrait",
        "validate_player_portrait_asset", "validate_character_title_assignment",
        "validate_title_definition_update", "reject_deletion", "reject_mutation",
        "touch_updated_at"
    ];

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(ReadEmbeddedSql());

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM game.currencies WHERE code = 'silver';");
        migrationBuilder.Sql("DELETE FROM game.world_state WHERE singleton_id = 1;");
        migrationBuilder.Sql("DELETE FROM game.reproduction_control WHERE singleton_id = 1;");

        foreach (var table in TouchTables)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS tr_{table}_touch ON game.{table};");
        }

        migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_event_posts_no_delete ON game.event_posts;");

        foreach (var table in ImmutableTables)
        {
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS tr_{table}_immutable ON game.{table};");
        }

        migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_births_validate_selection ON game.births;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_pregnancies_validate_mother ON game.pregnancies;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_heir_wait_pool_validate ON game.heir_wait_pool_entries;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_characters_validate_master_data ON game.characters;");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS tr_character_applications_validate_portrait ON game.character_applications;");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS tr_player_portrait_submissions_validate_asset ON game.player_portrait_submissions;");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS tr_character_title_assignments_validate ON game.character_title_assignments;");
        migrationBuilder.Sql(
            "DROP TRIGGER IF EXISTS tr_character_title_definitions_validate_update ON game.character_title_definitions;");

        foreach (var function in ValidationFunctions)
        {
            migrationBuilder.Sql($"DROP FUNCTION IF EXISTS game.{function}();");
        }
    }

    private static string ReadEmbeddedSql()
    {
        var assembly = typeof(SqlHardeningV11).Assembly;

        using var stream = assembly.GetManifestResourceStream(UpResource)
            ?? throw new InvalidOperationException(
                $"Embedded migration SQL '{UpResource}' is missing. Check that the .sql file is " +
                "included as an EmbeddedResource in GongWei.Infrastructure.csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
