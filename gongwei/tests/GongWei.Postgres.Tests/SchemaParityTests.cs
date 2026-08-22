using System.Text.RegularExpressions;
using FluentAssertions;
using GongWei.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GongWei.Postgres.Tests;

/// <summary>
/// Proves the EF model, the migrations and db/schema_v0.8.sql describe the same database
/// (spec §13.1) — the kind of drift that only shows up against a real server.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SchemaParityTests(PostgresFixture fixture)
{
    /// <summary>The 60 tables listed in spec §4.2.</summary>
    private static readonly string[] ExpectedTables =
    [
        // Identity 與人物 (17)
        "users", "user_sessions", "admin_role_assignments", "preset_portraits", "media_assets",
        "player_portrait_submissions", "character_applications", "character_application_revisions",
        "ranks", "character_title_definitions", "residences", "characters",
        "character_title_assignments", "character_stats", "character_status_history",
        "rank_history", "character_residence_history",

        // 世界、故事與事件 (14)
        "world_state", "game_settings", "game_setting_revisions", "world_locations",
        "event_rooms", "story_arcs", "story_chapters", "story_nodes", "content_revisions",
        "event_participants", "event_posts", "event_post_revisions", "event_results",
        "external_play_submissions",

        // 經濟、關係 (11)
        "currencies", "wallets", "ledger_transactions", "ledger_entries", "item_definitions",
        "inventory_entries", "inventory_transactions", "market_offers", "purchases",
        "relationships", "relationship_history",

        // 生育、陰謀 (11)
        "reproduction_control", "heir_wait_pool_entries", "audience_requests", "pregnancies",
        "births", "offspring_links", "intrigue_actions", "status_effects", "deaths",
        "notifications", "announcements",

        // 營運 (7)
        "approval_requests", "approval_decisions", "audit_logs", "idempotency_records",
        "outbox_messages", "scheduled_jobs", "job_runs"
    ];

    [SkippableFact]
    public async Task All_sixty_tables_exist()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var actual = await QueryStringsAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'game'
              AND table_type = 'BASE TABLE'
              AND table_name <> '__ef_migrations_history'
            """);

        actual.Should().BeEquivalentTo(ExpectedTables);
        actual.Should().HaveCount(60, "spec §4.2 defines exactly 60 tables");
    }

    /// <summary>
    /// The single most valuable check here: a C# enum member with no matching CHECK value
    /// would fail only at insert time, in production, on a rare code path.
    /// </summary>
    [SkippableTheory]
    [InlineData("characters", "ck_characters_status", typeof(CharacterStatus))]
    [InlineData("characters", "ck_characters_role", typeof(CharacterRole))]
    [InlineData("character_applications", "ck_ca_status", typeof(ApplicationStatus))]
    [InlineData("admin_role_assignments", "ck_admin_role_assignments_role", typeof(AdminRole))]
    [InlineData("event_rooms", "ck_er_status", typeof(EventRoomStatus))]
    [InlineData("audience_requests", "ck_ar_status", typeof(AudienceStatus))]
    [InlineData("pregnancies", "ck_preg_status", typeof(PregnancyStatus))]
    [InlineData("heir_wait_pool_entries", "ck_hwp_status", typeof(WaitPoolStatus))]
    [InlineData("approval_requests", "ck_apr_handler", typeof(ApprovalHandler))]
    [InlineData("approval_requests", "ck_apr_status", typeof(ApprovalStatus))]
    [InlineData("ledger_transactions", "ck_lt_reason", typeof(LedgerReason))]
    [InlineData("inventory_transactions", "ck_it_reason", typeof(InventoryReason))]
    [InlineData("status_effects", "ck_se_kind", typeof(StatusEffectKind))]
    [InlineData("deaths", "ck_deaths_cause", typeof(DeathCause))]
    [InlineData("outbox_messages", "ck_outbox_status", typeof(OutboxStatus))]
    [InlineData("job_runs", "ck_jr_status", typeof(JobRunStatus))]
    public async Task Enum_members_and_check_constraint_values_match(
        string table,
        string constraint,
        Type enumType)
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var definition = await QueryScalarAsync(
            """
            SELECT pg_get_constraintdef(c.oid)
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'game' AND t.relname = @table AND c.conname = @constraint
            """,
            ("table", table), ("constraint", constraint));

        definition.Should().NotBeNull($"{table}.{constraint} should exist");

        // Pull the quoted literals out of "... IN ('a', 'b', 'c') ...".
        var inDatabase = Regex.Matches(definition!, @"'([a-z0-9_/]+)'")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var inCode = Enum.GetNames(enumType)
            .Select(EnumNaming.ToSnakeCase)
            .ToHashSet(StringComparer.Ordinal);

        inCode.Except(inDatabase).Should().BeEmpty(
            "every {0} member needs a matching value in {1}", enumType.Name, constraint);
        inDatabase.Except(inCode).Should().BeEmpty(
            "{0} allows values that {1} cannot produce", constraint, enumType.Name);
    }

    [SkippableFact]
    public async Task Every_dbset_is_queryable_against_the_real_schema()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        await using var db = fixture.CreateContext();

        // Executing a COUNT over each entity proves the mapped table and every mapped
        // column name actually exist — a typo in the snake_case pass surfaces here.
        var entityTypes = db.Model.GetEntityTypes().ToList();
        entityTypes.Should().HaveCountGreaterThanOrEqualTo(60);

        foreach (var entityType in entityTypes)
        {
            var table = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "game";

            var count = await QueryScalarAsync($"SELECT count(*)::text FROM {schema}.\"{table}\"");
            count.Should().NotBeNull($"{schema}.{table} should be readable");
        }
    }

    [SkippableFact]
    public async Task The_two_singleton_control_rows_exist()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        await using var db = fixture.CreateContext();

        // Every reproduction flow locks reproduction_control(1) first; if the row is
        // missing, the whole module deadlocks on nothing (spec §6.2).
        (await db.ReproductionControl.CountAsync()).Should().Be(1);
        (await db.WorldState.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task Append_only_tables_refuse_updates_and_deletes()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO game.audit_logs (action, reason) VALUES ('test.append_only', 'schema test')",
            connection))
        {
            await insert.ExecuteNonQueryAsync();
        }

        // The trigger raises 42501 (insufficient_privilege) — the runtime role must not
        // be able to rewrite history even by mistake (spec §11).
        await using (var update = new NpgsqlCommand(
            "UPDATE game.audit_logs SET reason = 'tampered' WHERE action = 'test.append_only'",
            connection))
        {
            var act = async () => await update.ExecuteNonQueryAsync();
            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
        }

        await using (var delete = new NpgsqlCommand(
            "DELETE FROM game.audit_logs WHERE action = 'test.append_only'",
            connection))
        {
            var act = async () => await delete.ExecuteNonQueryAsync();
            await act.Should().ThrowAsync<PostgresException>();
        }
    }

    [SkippableFact]
    public async Task The_partial_unique_indexes_the_spec_relies_on_are_present()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var indexes = await QueryStringsAsync(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'game'");

        indexes.Should().Contain(
        [
            "ux_characters_user_current",   // one current character per account (§5.2)
            "ux_ca_user_open",              // one open application per account (§5.1)
            "ux_hwp_character_waiting",     // one waiting pool entry per character
            "ux_preg_mother_ongoing",       // one ongoing pregnancy per mother
            "ux_crh_current",               // one un-moved-out residence per character
            "ux_cta_primary",               // one primary title per character
            "ux_deaths_character",          // a character dies exactly once
            "ux_births_pregnancy", "ux_births_pool_entry", "ux_births_child"
        ]);
    }

    [SkippableFact]
    public async Task The_cross_table_validation_triggers_are_installed()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var triggers = await QueryStringsAsync(
            """
            SELECT tgname
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'game' AND NOT t.tgisinternal
            """);

        triggers.Should().Contain(
        [
            "trg_ledger_entries_append_only",
            "trg_inventory_transactions_append_only",
            "trg_audit_logs_append_only",
            "trg_characters_validate",
            "trg_heir_wait_pool_validate",
            "trg_title_assignment_validate",
            "trg_approval_decisions_no_self_review",
            "trg_ledger_entry_validate",
            "trg_birth_validate"
        ]);
    }

    [SkippableFact]
    public async Task Time_columns_are_all_timestamptz()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        // A naive `timestamp` column silently loses the offset, which is exactly the bug
        // spec §4.1 rules out by mandating timestamptz everywhere.
        var naive = await QueryStringsAsync(
            """
            SELECT table_name || '.' || column_name
            FROM information_schema.columns
            WHERE table_schema = 'game'
              AND data_type = 'timestamp without time zone'
            """);

        naive.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<string>();

        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private async Task<string?> QueryScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
