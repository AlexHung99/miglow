using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GongWei.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchemaV11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "ability_label_definitions",
                schema: "game",
                columns: table => new
                {
                    ability_code = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    min_value = table.Column<short>(type: "smallint", nullable: false),
                    max_value = table.Column<short>(type: "smallint", nullable: false),
                    display_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ability_label_definitions", x => new { x.ability_code, x.min_value });
                    table.CheckConstraint("ck_ald_ability_code", "ability_code IN ('vitality', 'appearance', 'strategy', 'luck')");
                    table.CheckConstraint("ck_ald_max", "max_value BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_ald_min", "min_value BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_ald_range", "min_value <= max_value");
                    table.CheckConstraint("ck_ald_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "currencies",
                schema: "game",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currencies", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "item_definitions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false, defaultValue: ""),
                    category = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    stack_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 999),
                    is_consumable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    effect_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    usage_rules = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_definitions", x => x.id);
                    table.CheckConstraint("ck_id_category", "category IN ('clothing', 'medicine', 'poison', 'gift', 'quest', 'material', 'other')");
                    table.CheckConstraint("ck_id_effect_payload", "jsonb_typeof(effect_payload) = 'object'");
                    table.CheckConstraint("ck_id_stack_limit", "stack_limit > 0");
                    table.CheckConstraint("ck_id_usage_rules", "jsonb_typeof(usage_rules) = 'object'");
                    table.CheckConstraint("ck_id_version_no", "version_no > 0");
                });

            migrationBuilder.CreateTable(
                name: "line_login_attempts",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    state_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    nonce_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    protected_payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    return_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_line_login_attempts", x => x.id);
                    table.CheckConstraint("ck_lla_consumed", "consumed_at IS NULL OR consumed_at >= created_at");
                    table.CheckConstraint("ck_lla_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_lla_return_url", "return_url LIKE 'https://miglow.vip/gongwei/%' OR return_url = 'https://miglow.vip/gongwei/'");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_attempts", "attempt_count >= 0");
                    table.CheckConstraint("ck_outbox_payload", "jsonb_typeof(payload) = 'object'");
                });

            migrationBuilder.CreateTable(
                name: "preset_portraits",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    role = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    asset_url = table.Column<string>(type: "text", nullable: false),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preset_portraits", x => x.id);
                    table.CheckConstraint("ck_preset_portraits_metadata", "jsonb_typeof(metadata) = 'object'");
                    table.CheckConstraint("ck_preset_portraits_role", "role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_preset_portraits_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "ranks",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    applies_to_role = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    grade_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    prestige_required = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    monthly_stipend = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    source_annual_stipend = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_lead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_application_option = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    initial_stats = table.Column<string>(type: "jsonb", nullable: true),
                    promotion_rules = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranks", x => x.id);
                    table.CheckConstraint("ck_ranks_annual_stipend", "source_annual_stipend >= 0");
                    table.CheckConstraint("ck_ranks_capacity", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("ck_ranks_initial_stats", "initial_stats IS NULL OR jsonb_typeof(initial_stats) = 'object'");
                    table.CheckConstraint("ck_ranks_monthly_stipend", "monthly_stipend >= 0");
                    table.CheckConstraint("ck_ranks_ordinal", "ordinal >= 0");
                    table.CheckConstraint("ck_ranks_prestige", "prestige_required >= 0");
                    table.CheckConstraint("ck_ranks_promotion_rules", "jsonb_typeof(promotion_rules) = 'object'");
                    table.CheckConstraint("ck_ranks_role", "applies_to_role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_ranks_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "residences",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    map_x = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    map_y = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_residences", x => x.id);
                    table.CheckConstraint("ck_residences_capacity", "capacity IS NULL OR capacity > 0");
                    table.CheckConstraint("ck_residences_map_x", "map_x IS NULL OR map_x BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_residences_map_y", "map_y IS NULL OR map_y BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_residences_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "scheduled_jobs",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    job_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cron_expression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_jobs", x => x.id);
                    table.CheckConstraint("ck_sj_payload", "jsonb_typeof(payload) = 'object'");
                    table.CheckConstraint("ck_sj_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    line_user_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "zh-TW"),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "active"),
                    terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    privacy_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_display_name_len", "char_length(btrim(display_name)) BETWEEN 1 AND 80");
                    table.CheckConstraint("ck_users_line_user_id_len", "char_length(btrim(line_user_id)) BETWEEN 1 AND 255");
                    table.CheckConstraint("ck_users_status", "status IN ('active', 'suspended', 'deleted')");
                    table.CheckConstraint("ck_users_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "world_locations",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false, defaultValue: ""),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    map_x = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    map_y = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    access_rules = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_locations", x => x.id);
                    table.CheckConstraint("ck_wl_access_rules", "jsonb_typeof(access_rules) = 'object'");
                    table.CheckConstraint("ck_wl_map_x", "map_x BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_wl_map_y", "map_y BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_wl_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "world_state",
                schema: "game",
                columns: table => new
                {
                    singleton_id = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    era_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_year = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    season = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    day_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    calendar_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "realtime_1to1"),
                    calendar_timezone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Asia/Taipei"),
                    calendar_anchor_real_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    calendar_anchor_game_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CURRENT_DATE"),
                    reproduction_open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    maintenance_mode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_state", x => x.singleton_id);
                    table.CheckConstraint("ck_world_state_calendar_mode", "calendar_mode = 'realtime_1to1'");
                    table.CheckConstraint("ck_world_state_config", "jsonb_typeof(config) = 'object'");
                    table.CheckConstraint("ck_world_state_season", "season IN ('spring', 'summer', 'autumn', 'winter')");
                    table.CheckConstraint("ck_world_state_singleton", "singleton_id = 1");
                    table.CheckConstraint("ck_world_state_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    scheduled_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    result_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    worker_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_runs", x => x.id);
                    table.CheckConstraint("ck_jr_attempt_no", "attempt_no > 0");
                    table.CheckConstraint("ck_jr_finished_pair", "(status = 'running' AND finished_at IS NULL) OR (status <> 'running' AND finished_at IS NOT NULL)");
                    table.CheckConstraint("ck_jr_result_payload", "jsonb_typeof(result_payload) = 'object'");
                    table.CheckConstraint("ck_jr_status", "status IN ('running', 'succeeded', 'failed', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_job_runs_scheduled_jobs_scheduled_job_id",
                        column: x => x.scheduled_job_id,
                        principalSchema: "game",
                        principalTable: "scheduled_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_role_assignments",
                schema: "game",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", maxLength: 40, nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    public_display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    public_title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    public_duty = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_role_assignments", x => new { x.user_id, x.role });
                    table.CheckConstraint("ck_admin_role_assignments_expiry", "expires_at IS NULL OR expires_at > granted_at");
                    table.CheckConstraint("ck_admin_role_assignments_public", "is_public = false OR public_display_name IS NOT NULL");
                    table.CheckConstraint("ck_admin_role_assignments_role", "role IN ('super_admin', 'character_reviewer', 'game_master', 'economy_manager', 'moderator', 'auditor', 'content_editor', 'character_manager', 'system_config_manager')");
                    table.CheckConstraint("ck_admin_role_assignments_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_admin_role_assignments_users_granted_by",
                        column: x => x.granted_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_admin_role_assignments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "announcements",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body_markdown = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "info"),
                    audience = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "all"),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                    table.CheckConstraint("ck_ann_audience", "audience IN ('all', 'players', 'admins')");
                    table.CheckConstraint("ck_ann_severity", "severity IN ('info', 'warning', 'critical')");
                    table.CheckConstraint("ck_ann_version", "version > 0");
                    table.CheckConstraint("ck_ann_window", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_announcements_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    target_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "pending"),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                    table.CheckConstraint("ck_apr_expiry", "expires_at > requested_at");
                    table.CheckConstraint("ck_apr_payload", "jsonb_typeof(payload) = 'object'");
                    table.CheckConstraint("ck_apr_status", "status IN ('pending', 'approved', 'rejected', 'expired', 'executed', 'cancelled')");
                    table.CheckConstraint("ck_apr_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_approval_requests_users_requested_by",
                        column: x => x.requested_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_data = table.Column<string>(type: "jsonb", nullable: true),
                    after_data = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_metadata", "jsonb_typeof(metadata) = 'object'");
                    table.ForeignKey(
                        name: "FK_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_title_definitions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    category = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    applies_to_role = table.Column<string>(type: "text", maxLength: 20, nullable: true),
                    visibility = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "public"),
                    style_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_title_definitions", x => x.id);
                    table.CheckConstraint("ck_ctd_category", "category IN ('rank', 'achievement', 'story', 'honorary', 'secret')");
                    table.CheckConstraint("ck_ctd_display_name_len", "char_length(btrim(display_name)) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_ctd_role", "applies_to_role IS NULL OR applies_to_role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_ctd_version", "version > 0");
                    table.CheckConstraint("ck_ctd_visibility", "visibility IN ('public', 'owner_only', 'admin_only')");
                    table.ForeignKey(
                        name: "FK_character_title_definitions_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_settings",
                schema: "game",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    published_value = table.Column<string>(type: "jsonb", nullable: false),
                    draft_value = table.Column<string>(type: "jsonb", nullable: true),
                    validation_schema = table.Column<string>(type: "jsonb", nullable: false),
                    risk_level = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "normal"),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_settings", x => x.setting_key);
                    table.CheckConstraint("ck_gs_key_len", "char_length(btrim(setting_key)) BETWEEN 3 AND 120");
                    table.CheckConstraint("ck_gs_published_by", "published_at IS NULL OR published_by IS NOT NULL");
                    table.CheckConstraint("ck_gs_risk_level", "risk_level IN ('normal', 'high')");
                    table.CheckConstraint("ck_gs_validation_schema", "jsonb_typeof(validation_schema) = 'object'");
                    table.CheckConstraint("ck_gs_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_game_settings_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_game_settings_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    request_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "processing"),
                    response_status = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                    table.CheckConstraint("ck_idem_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_idem_response_status", "response_status IS NULL OR response_status BETWEEN 100 AND 599");
                    table.CheckConstraint("ck_idem_status", "status IN ('processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_idempotency_records_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger_transactions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    transaction_type = table.Column<string>(type: "text", maxLength: 40, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_transactions", x => x.id);
                    table.CheckConstraint("ck_lt_type", "transaction_type IN ('stipend', 'purchase', 'reward', 'item_use', 'admin_grant', 'admin_correction', 'refund')");
                    table.ForeignKey(
                        name: "FK_ledger_transactions_users_initiated_by",
                        column: x => x.initiated_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "market_offers",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    item_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<long>(type: "bigint", nullable: false),
                    stock_total = table.Column<int>(type: "integer", nullable: true),
                    stock_sold = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    per_character_limit = table.Column<int>(type: "integer", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eligibility_rules = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_offers", x => x.id);
                    table.CheckConstraint("ck_mo_eligibility", "jsonb_typeof(eligibility_rules) = 'object'");
                    table.CheckConstraint("ck_mo_limit", "per_character_limit IS NULL OR per_character_limit > 0");
                    table.CheckConstraint("ck_mo_sold_within_total", "stock_total IS NULL OR stock_sold <= stock_total");
                    table.CheckConstraint("ck_mo_stock_sold", "stock_sold >= 0");
                    table.CheckConstraint("ck_mo_stock_total", "stock_total IS NULL OR stock_total >= 0");
                    table.CheckConstraint("ck_mo_unit_price", "unit_price >= 0");
                    table.CheckConstraint("ck_mo_version", "version > 0");
                    table.CheckConstraint("ck_mo_window", "ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "FK_market_offers_currencies_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "game",
                        principalTable: "currencies",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_market_offers_item_definitions_item_definition_id",
                        column: x => x.item_definition_id,
                        principalSchema: "game",
                        principalTable: "item_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_market_offers_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stored_mime_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "uploaded"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_byte_size", "byte_size BETWEEN 1 AND 8388608");
                    table.CheckConstraint("ck_media_assets_height", "height >= 800");
                    table.CheckConstraint("ck_media_assets_original_mime", "original_mime_type IN ('image/jpeg', 'image/png', 'image/webp')");
                    table.CheckConstraint("ck_media_assets_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_media_assets_status", "status IN ('uploaded', 'processing', 'ready', 'quarantined', 'deleted')");
                    table.CheckConstraint("ck_media_assets_storage_key_len", "char_length(btrim(storage_key)) BETWEEN 1 AND 1024");
                    table.CheckConstraint("ck_media_assets_stored_mime", "stored_mime_type IS NULL OR stored_mime_type IN ('image/webp', 'image/jpeg')");
                    table.CheckConstraint("ck_media_assets_version", "version > 0");
                    table.CheckConstraint("ck_media_assets_width", "width >= 600");
                    table.ForeignKey(
                        name: "FK_media_assets_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notif_payload", "jsonb_typeof(payload) = 'object'");
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reproduction_control",
                schema: "game",
                columns: table => new
                {
                    singleton_id = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    is_open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    closed_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    conception_rate_percent = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)100),
                    pregnancy_duration_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)10),
                    miscarriage_mode = table.Column<string>(type: "text", maxLength: 30, nullable: false, defaultValue: "event_only"),
                    miscarriage_rules = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{\"baseRatePercent\":0}'::jsonb"),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "reproduction-1"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reproduction_control", x => x.singleton_id);
                    table.CheckConstraint("ck_rc_conception_rate", "conception_rate_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_rc_duration", "pregnancy_duration_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_rc_miscarriage_mode", "miscarriage_mode IN ('disabled', 'event_only', 'threshold', 'daily_probability')");
                    table.CheckConstraint("ck_rc_miscarriage_rules", "jsonb_typeof(miscarriage_rules) = 'object'");
                    table.CheckConstraint("ck_rc_singleton", "singleton_id = 1");
                    table.CheckConstraint("ck_rc_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_reproduction_control_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    csrf_secret_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    idle_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.CheckConstraint("ck_user_sessions_absolute_after_created", "absolute_expires_at > created_at");
                    table.CheckConstraint("ck_user_sessions_expiry_order", "idle_expires_at <= absolute_expires_at");
                    table.ForeignKey(
                        name: "FK_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_rooms",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    body_markdown = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    event_type = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "draft"),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visibility = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "public"),
                    participant_limit = table.Column<int>(type: "integer", nullable: true),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rules_snapshot = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_rooms", x => x.id);
                    table.CheckConstraint("ck_er_event_type", "event_type IN ('main', 'social', 'investigation', 'limited', 'private', 'admin')");
                    table.CheckConstraint("ck_er_participant_limit", "participant_limit IS NULL OR participant_limit > 0");
                    table.CheckConstraint("ck_er_rules_snapshot", "jsonb_typeof(rules_snapshot) = 'object'");
                    table.CheckConstraint("ck_er_settled_at", "(status = 'settled' AND settled_at IS NOT NULL) OR status <> 'settled'");
                    table.CheckConstraint("ck_er_status", "status IN ('draft', 'scheduled', 'open', 'locked', 'settled', 'cancelled')");
                    table.CheckConstraint("ck_er_version", "version > 0");
                    table.CheckConstraint("ck_er_visibility", "visibility IN ('public', 'invited', 'private')");
                    table.CheckConstraint("ck_er_window", "deadline_at IS NULL OR opens_at IS NULL OR deadline_at > opens_at");
                    table.ForeignKey(
                        name: "FK_event_rooms_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_rooms_world_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "game",
                        principalTable: "world_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_decisions", x => x.id);
                    table.CheckConstraint("ck_apd_decision", "decision IN ('approve', 'reject')");
                    table.ForeignKey(
                        name: "FK_approval_decisions_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "game",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_approval_decisions_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_setting_revisions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    setting_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    previous_value = table.Column<string>(type: "jsonb", nullable: true),
                    published_value = table.Column<string>(type: "jsonb", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_setting_revisions", x => x.id);
                    table.CheckConstraint("ck_gsr_revision_no", "revision_no > 0");
                    table.ForeignKey(
                        name: "FK_game_setting_revisions_game_settings_setting_key",
                        column: x => x.setting_key,
                        principalSchema: "game",
                        principalTable: "game_settings",
                        principalColumn: "setting_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_game_setting_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_setting_revisions_approval_request",
                        column: x => x.approval_request_id,
                        principalSchema: "game",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "npcs",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    sex = table.Column<string>(type: "text", maxLength: 10, nullable: true),
                    summary = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false, defaultValue: ""),
                    story_markdown = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    public_profile = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    portrait_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    portrait_url = table.Column<string>(type: "text", nullable: true),
                    primary_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "draft"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npcs", x => x.id);
                    table.CheckConstraint("ck_npc_display_name_len", "char_length(btrim(display_name)) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_npc_portrait_present", "portrait_asset_id IS NOT NULL OR portrait_url IS NOT NULL");
                    table.CheckConstraint("ck_npc_public_profile", "jsonb_typeof(public_profile) = 'object'");
                    table.CheckConstraint("ck_npc_published_pair", "(status = 'published' AND published_by IS NOT NULL AND published_at IS NOT NULL) OR status <> 'published'");
                    table.CheckConstraint("ck_npc_sex", "sex IN ('female', 'male', 'unknown')");
                    table.CheckConstraint("ck_npc_status", "status IN ('draft', 'review', 'published', 'archived')");
                    table.CheckConstraint("ck_npc_story_len", "char_length(story_markdown) <= 50000");
                    table.CheckConstraint("ck_npc_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_npcs_media_assets_portrait_asset_id",
                        column: x => x.portrait_asset_id,
                        principalSchema: "game",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_npcs_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_npcs_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_npcs_world_locations_primary_location_id",
                        column: x => x.primary_location_id,
                        principalSchema: "game",
                        principalTable: "world_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "player_portrait_submissions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    crop_x = table.Column<decimal>(type: "numeric(6,5)", nullable: false, defaultValue: 0m),
                    crop_y = table.Column<decimal>(type: "numeric(6,5)", nullable: false, defaultValue: 0m),
                    crop_width = table.Column<decimal>(type: "numeric(6,5)", nullable: false, defaultValue: 1m),
                    crop_height = table.Column<decimal>(type: "numeric(6,5)", nullable: false, defaultValue: 1m),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "pending"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_portrait_submissions", x => x.id);
                    table.CheckConstraint("ck_pps_crop_height", "crop_height > 0 AND crop_height <= 1");
                    table.CheckConstraint("ck_pps_crop_width", "crop_width > 0 AND crop_width <= 1");
                    table.CheckConstraint("ck_pps_crop_x", "crop_x BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_pps_crop_x_bounds", "crop_x + crop_width <= 1.00001");
                    table.CheckConstraint("ck_pps_crop_y", "crop_y BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_pps_crop_y_bounds", "crop_y + crop_height <= 1.00001");
                    table.CheckConstraint("ck_pps_reviewed_pair", "(status IN ('approved', 'rejected') AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL) OR status IN ('pending', 'withdrawn')");
                    table.CheckConstraint("ck_pps_role", "role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_pps_status", "status IN ('pending', 'approved', 'rejected', 'withdrawn')");
                    table.CheckConstraint("ck_pps_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_player_portrait_submissions_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalSchema: "game",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_player_portrait_submissions_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_player_portrait_submissions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "npc_revisions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    change_kind = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    change_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npc_revisions", x => x.id);
                    table.CheckConstraint("ck_npcrev_change_kind", "change_kind IN ('create', 'edit', 'publish', 'archive', 'restore')");
                    table.CheckConstraint("ck_npcrev_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_npcrev_snapshot", "jsonb_typeof(snapshot) = 'object'");
                    table.ForeignKey(
                        name: "FK_npc_revisions_npcs_npc_id",
                        column: x => x.npc_id,
                        principalSchema: "game",
                        principalTable: "npcs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_npc_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audience_requests",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience_type = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "submitted"),
                    qualification_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    result_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audience_requests", x => x.id);
                    table.CheckConstraint("ck_ar_qualification", "jsonb_typeof(qualification_snapshot) = 'object'");
                    table.CheckConstraint("ck_ar_resolved_pair", "(status IN ('resolved', 'rejected', 'cancelled') AND resolved_at IS NOT NULL) OR status IN ('submitted', 'approved')");
                    table.CheckConstraint("ck_ar_result_payload", "jsonb_typeof(result_payload) = 'object'");
                    table.CheckConstraint("ck_ar_status", "status IN ('submitted', 'approved', 'rejected', 'resolved', 'cancelled')");
                    table.CheckConstraint("ck_ar_type", "audience_type IN ('meal', 'bedchamber')");
                    table.CheckConstraint("ck_ar_version", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "births",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    pregnancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wait_pool_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_count = table.Column<int>(type: "integer", nullable: false),
                    candidate_set_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    random_algorithm = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    random_proof_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    drawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    born_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_births", x => x.id);
                    table.CheckConstraint("ck_births_candidate_count", "candidate_count > 0");
                    table.ForeignKey(
                        name: "FK_births_users_drawn_by",
                        column: x => x.drawn_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_application_revisions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_application_revisions", x => x.id);
                    table.CheckConstraint("ck_car_revision_no", "revision_no > 0");
                    table.CheckConstraint("ck_car_snapshot", "jsonb_typeof(snapshot) = 'object'");
                    table.ForeignKey(
                        name: "FK_character_application_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_applications",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    sex = table.Column<string>(type: "text", maxLength: 10, nullable: false),
                    family_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    given_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    courtesy_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    birth_date_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    age = table.Column<short>(type: "smallint", nullable: true),
                    appearance = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false, defaultValue: ""),
                    biography = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    personality = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    strengths = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    weaknesses = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    likes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    dislikes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    portrait_id = table.Column<Guid>(type: "uuid", nullable: true),
                    player_portrait_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", maxLength: 30, nullable: false, defaultValue: "draft"),
                    form_data = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_applications", x => x.id);
                    table.CheckConstraint("ck_ca_age_and_family", "status = 'draft' OR (role = 'consort' AND age BETWEEN 15 AND 18 AND char_length(btrim(family_name)) > 0) OR (role IN ('prince', 'princess') AND age = 0 AND family_name = '蕭')");
                    table.CheckConstraint("ck_ca_appearance_len", "status = 'draft' OR char_length(appearance) >= 60");
                    table.CheckConstraint("ck_ca_approved_reviewed", "(status = 'approved' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL) OR status <> 'approved'");
                    table.CheckConstraint("ck_ca_biography_len", "status = 'draft' OR char_length(biography) >= 200");
                    table.CheckConstraint("ck_ca_dislikes_len", "status = 'draft' OR char_length(dislikes) >= 50");
                    table.CheckConstraint("ck_ca_draft_not_submitted", "(status = 'draft' AND submitted_at IS NULL) OR status <> 'draft'");
                    table.CheckConstraint("ck_ca_form_data", "jsonb_typeof(form_data) = 'object'");
                    table.CheckConstraint("ck_ca_given_name", "status = 'draft' OR char_length(btrim(given_name)) BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_ca_likes_len", "status = 'draft' OR char_length(likes) >= 50");
                    table.CheckConstraint("ck_ca_personality_len", "status = 'draft' OR char_length(personality) >= 50");
                    table.CheckConstraint("ck_ca_portrait_xor", "status = 'draft' OR ((portrait_id IS NOT NULL)::integer + (player_portrait_submission_id IS NOT NULL)::integer = 1)");
                    table.CheckConstraint("ck_ca_role", "role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_ca_role_sex", "(role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')");
                    table.CheckConstraint("ck_ca_sex", "sex IN ('female', 'male')");
                    table.CheckConstraint("ck_ca_status", "status IN ('draft', 'submitted', 'needs_revision', 'approved', 'rejected', 'cancelled')");
                    table.CheckConstraint("ck_ca_strengths_len", "status = 'draft' OR char_length(strengths) >= 50");
                    table.CheckConstraint("ck_ca_version", "version > 0");
                    table.CheckConstraint("ck_ca_weaknesses_len", "status = 'draft' OR char_length(weaknesses) >= 50");
                    table.ForeignKey(
                        name: "FK_character_applications_player_portrait_submissions_player_p~",
                        column: x => x.player_portrait_submission_id,
                        principalSchema: "game",
                        principalTable: "player_portrait_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_applications_preset_portraits_portrait_id",
                        column: x => x.portrait_id,
                        principalSchema: "game",
                        principalTable: "preset_portraits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_applications_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_character_applications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    sex = table.Column<string>(type: "text", maxLength: 10, nullable: false),
                    family_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    given_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    courtesy_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    birth_date_label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    age_at_creation = table.Column<short>(type: "smallint", nullable: false),
                    appearance = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    biography = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    personality = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    strengths = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    weaknesses = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    likes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    dislikes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    portrait_id = table.Column<Guid>(type: "uuid", nullable: true),
                    player_portrait_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rank_id = table.Column<Guid>(type: "uuid", nullable: true),
                    residence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    pause_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    died_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.id);
                    table.CheckConstraint("ck_characters_archived_at", "(status = 'archived' AND archived_at IS NOT NULL) OR status <> 'archived'");
                    table.CheckConstraint("ck_characters_dead_at", "(status = 'dead' AND died_at IS NOT NULL) OR status <> 'dead'");
                    table.CheckConstraint("ck_characters_portrait_xor", "(portrait_id IS NOT NULL)::integer + (player_portrait_submission_id IS NOT NULL)::integer = 1");
                    table.CheckConstraint("ck_characters_role", "role IN ('consort', 'prince', 'princess')");
                    table.CheckConstraint("ck_characters_role_sex", "(role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')");
                    table.CheckConstraint("ck_characters_sex", "sex IN ('female', 'male')");
                    table.CheckConstraint("ck_characters_status", "status IN ('waiting_birth', 'active', 'paused', 'dead', 'suspended', 'archived')");
                    table.CheckConstraint("ck_characters_version", "version > 0");
                    table.CheckConstraint("ck_characters_waiting_birth_role", "(status = 'waiting_birth' AND role IN ('prince', 'princess')) OR status <> 'waiting_birth'");
                    table.ForeignKey(
                        name: "FK_characters_character_applications_source_application_id",
                        column: x => x.source_application_id,
                        principalSchema: "game",
                        principalTable: "character_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_characters_player_portrait_submissions_player_portrait_subm~",
                        column: x => x.player_portrait_submission_id,
                        principalSchema: "game",
                        principalTable: "player_portrait_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_characters_preset_portraits_portrait_id",
                        column: x => x.portrait_id,
                        principalSchema: "game",
                        principalTable: "preset_portraits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_characters_ranks_rank_id",
                        column: x => x.rank_id,
                        principalSchema: "game",
                        principalTable: "ranks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_characters_residences_residence_id",
                        column: x => x.residence_id,
                        principalSchema: "game",
                        principalTable: "residences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_characters_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_chronicle_entries",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    visibility = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "public"),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    detail = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false, defaultValue: ""),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stat_changes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    resource_changes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    happened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_chronicle_entries", x => x.id);
                    table.CheckConstraint("ck_cce_entry_type", "entry_type IN ('event', 'economy', 'inventory', 'rank', 'status', 'reproduction', 'intrigue', 'admin', 'system')");
                    table.CheckConstraint("ck_cce_metadata", "jsonb_typeof(metadata) = 'object'");
                    table.CheckConstraint("ck_cce_resource_changes", "jsonb_typeof(resource_changes) = 'array'");
                    table.CheckConstraint("ck_cce_stat_changes", "jsonb_typeof(stat_changes) = 'array'");
                    table.CheckConstraint("ck_cce_visibility", "visibility IN ('public', 'owner_only', 'admin_only')");
                    table.ForeignKey(
                        name: "FK_character_chronicle_entries_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_chronicle_entries_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_character_chronicle_entries_world_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "game",
                        principalTable: "world_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_progress",
                schema: "game",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settled_event_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    approved_event_post_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    approved_external_play_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    self_play_word_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weekly_message_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_progress", x => x.character_id);
                    table.CheckConstraint("ck_cp_approved_external", "approved_external_play_count >= 0");
                    table.CheckConstraint("ck_cp_approved_posts", "approved_event_post_count >= 0");
                    table.CheckConstraint("ck_cp_self_play_words", "self_play_word_count >= 0");
                    table.CheckConstraint("ck_cp_settled_events", "settled_event_count >= 0");
                    table.CheckConstraint("ck_cp_version", "version > 0");
                    table.CheckConstraint("ck_cp_weekly_messages", "weekly_message_count >= 0");
                    table.ForeignKey(
                        name: "FK_character_progress_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_residence_history",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    residence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moved_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    moved_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_residence_history", x => x.id);
                    table.CheckConstraint("ck_crh_order", "moved_out_at IS NULL OR moved_out_at >= moved_in_at");
                    table.ForeignKey(
                        name: "FK_character_residence_history_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_residence_history_residences_residence_id",
                        column: x => x.residence_id,
                        principalSchema: "game",
                        principalTable: "residences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_residence_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_stats",
                schema: "game",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vitality = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    appearance = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    strategy = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    luck = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    prestige = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    favor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_stats", x => x.character_id);
                    table.CheckConstraint("ck_cs_appearance", "appearance BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_cs_favor", "favor BETWEEN -1000 AND 1000");
                    table.CheckConstraint("ck_cs_luck", "luck BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_cs_prestige", "prestige >= 0");
                    table.CheckConstraint("ck_cs_strategy", "strategy BETWEEN 0 AND 1000");
                    table.CheckConstraint("ck_cs_version", "version > 0");
                    table.CheckConstraint("ck_cs_vitality", "vitality BETWEEN 0 AND 1000");
                    table.ForeignKey(
                        name: "FK_character_stats_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_status_history",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_character_status_history_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "character_title_assignments",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    grant_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_title_assignments", x => x.id);
                    table.CheckConstraint("ck_cta_revoked_triple", "(revoked_at IS NULL AND revoked_by IS NULL AND revoke_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revoke_reason IS NOT NULL)");
                    table.CheckConstraint("ck_cta_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_character_title_assignments_character_title_definitions_tit~",
                        column: x => x.title_definition_id,
                        principalSchema: "game",
                        principalTable: "character_title_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_title_assignments_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_title_assignments_users_granted_by",
                        column: x => x.granted_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_character_title_assignments_users_revoked_by",
                        column: x => x.revoked_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "deaths",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cause_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    public_cause = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    private_details = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    source_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ruled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deaths", x => x.id);
                    table.CheckConstraint("ck_deaths_private_details", "jsonb_typeof(private_details) = 'object'");
                    table.ForeignKey(
                        name: "FK_deaths_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deaths_users_ruled_by",
                        column: x => x.ruled_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_deaths_approval_request",
                        column: x => x.approval_request_id,
                        principalSchema: "game",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_participants",
                schema: "game",
                columns: table => new
                {
                    event_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "participant"),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "joined"),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_participants", x => new { x.event_room_id, x.character_id });
                    table.CheckConstraint("ck_ep_metadata", "jsonb_typeof(metadata) = 'object'");
                    table.CheckConstraint("ck_ep_status", "status IN ('invited', 'joined', 'left', 'removed', 'completed')");
                    table.ForeignKey(
                        name: "FK_event_participants_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participants_event_rooms_event_room_id",
                        column: x => x.event_room_id,
                        principalSchema: "game",
                        principalTable: "event_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_posts",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body_markdown = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "draft"),
                    client_request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    moderated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    moderation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_posts", x => x.id);
                    table.CheckConstraint("ck_epost_approved_published", "status <> 'approved' OR (reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL AND published_at IS NOT NULL)");
                    table.CheckConstraint("ck_epost_body_len", "char_length(body_markdown) <= 10000");
                    table.CheckConstraint("ck_epost_body_not_blank", "status = 'draft' OR char_length(btrim(body_markdown)) > 0");
                    table.CheckConstraint("ck_epost_status", "status IN ('draft', 'submitted', 'under_review', 'approved', 'needs_revision', 'rejected', 'withdrawn', 'moderated')");
                    table.CheckConstraint("ck_epost_submitted_at", "status NOT IN ('submitted', 'under_review', 'approved', 'rejected', 'needs_revision') OR submitted_at IS NOT NULL");
                    table.CheckConstraint("ck_epost_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_event_posts_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_posts_event_rooms_event_room_id",
                        column: x => x.event_room_id,
                        principalSchema: "game",
                        principalTable: "event_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_posts_users_moderated_by",
                        column: x => x.moderated_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_posts_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_results",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    public_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    private_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    rewards_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    settled_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_results", x => x.id);
                    table.CheckConstraint("ck_eres_private_payload", "jsonb_typeof(private_payload) = 'object'");
                    table.CheckConstraint("ck_eres_rewards_payload", "jsonb_typeof(rewards_payload) = 'object'");
                    table.ForeignKey(
                        name: "FK_event_results_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_results_event_rooms_event_room_id",
                        column: x => x.event_room_id,
                        principalSchema: "game",
                        principalTable: "event_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_results_users_settled_by",
                        column: x => x.settled_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_play_submissions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    submitted_by_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "line_group"),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    evidence_urls = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    involved_character_ids = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    status = table.Column<string>(type: "text", maxLength: 30, nullable: false, defaultValue: "submitted"),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_play_submissions", x => x.id);
                    table.CheckConstraint("ck_eps_evidence_urls", "jsonb_typeof(evidence_urls) = 'array'");
                    table.CheckConstraint("ck_eps_involved", "jsonb_typeof(involved_character_ids) = 'array'");
                    table.CheckConstraint("ck_eps_source_type", "source_type IN ('line_group', 'other')");
                    table.CheckConstraint("ck_eps_status", "status IN ('submitted', 'under_review', 'approved', 'rejected', 'cancelled')");
                    table.CheckConstraint("ck_eps_summary_len", "char_length(btrim(summary)) BETWEEN 1 AND 4000");
                    table.CheckConstraint("ck_eps_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_external_play_submissions_characters_submitted_by_character~",
                        column: x => x.submitted_by_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_play_submissions_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "heir_wait_pool_entries",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "waiting"),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heir_wait_pool_entries", x => x.id);
                    table.CheckConstraint("ck_hwp_resolved_pair", "(status = 'waiting' AND resolved_at IS NULL) OR (status <> 'waiting' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_hwp_status", "status IN ('waiting', 'drawn', 'withdrawn', 'suspended')");
                    table.CheckConstraint("ck_hwp_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_heir_wait_pool_entries_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_heir_wait_pool_entries_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "intrigue_actions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    actor_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "submitted"),
                    input_payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    secret_result = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    public_result = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolve_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intrigue_actions", x => x.id);
                    table.CheckConstraint("ck_ia_action_type", "action_type IN ('poison', 'investigate', 'countermeasure')");
                    table.CheckConstraint("ck_ia_input_payload", "jsonb_typeof(input_payload) = 'object'");
                    table.CheckConstraint("ck_ia_not_self", "actor_character_id <> target_character_id");
                    table.CheckConstraint("ck_ia_public_result", "jsonb_typeof(public_result) = 'object'");
                    table.CheckConstraint("ck_ia_secret_result", "jsonb_typeof(secret_result) = 'object'");
                    table.CheckConstraint("ck_ia_status", "status IN ('submitted', 'processing', 'resolved', 'failed', 'cancelled')");
                    table.CheckConstraint("ck_ia_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_intrigue_actions_characters_actor_character_id",
                        column: x => x.actor_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_intrigue_actions_characters_target_character_id",
                        column: x => x.target_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_entries",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_entries", x => x.id);
                    table.CheckConstraint("ck_ie_quantity", "quantity >= 0");
                    table.CheckConstraint("ck_ie_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_inventory_entries_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_entries_item_definitions_item_definition_id",
                        column: x => x.item_definition_id,
                        principalSchema: "game",
                        principalTable: "item_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offspring_links",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    child_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_type = table.Column<string>(type: "text", maxLength: 20, nullable: false),
                    parent_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_npc_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offspring_links", x => x.id);
                    table.CheckConstraint("ck_ol_parent_type", "parent_type IN ('mother', 'father')");
                    table.CheckConstraint("ck_ol_parent_xor", "(parent_character_id IS NOT NULL)::integer + (parent_npc_code IS NOT NULL)::integer = 1");
                    table.ForeignKey(
                        name: "FK_offspring_links_characters_child_character_id",
                        column: x => x.child_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_offspring_links_characters_parent_character_id",
                        column: x => x.parent_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pregnancies",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mother_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "ongoing"),
                    conceived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    conception_rate_percent = table.Column<short>(type: "smallint", nullable: false),
                    conception_roll = table.Column<short>(type: "smallint", nullable: false),
                    slot_reserved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    slot_released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rules_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rules_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pregnancies", x => x.id);
                    table.CheckConstraint("ck_preg_due", "due_at > conceived_at");
                    table.CheckConstraint("ck_preg_miscarriage_reason", "status <> 'miscarried' OR (resolution_code IS NOT NULL AND char_length(btrim(resolution_reason)) >= 5)");
                    table.CheckConstraint("ck_preg_rate", "conception_rate_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_preg_roll", "conception_roll BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_preg_rules_snapshot", "jsonb_typeof(rules_snapshot) = 'object'");
                    table.CheckConstraint("ck_preg_slot_release", "(status = 'ongoing' AND slot_released_at IS NULL) OR (status <> 'ongoing' AND slot_released_at IS NOT NULL)");
                    table.CheckConstraint("ck_preg_slot_reserved", "slot_reserved_at >= conceived_at");
                    table.CheckConstraint("ck_preg_status", "status IN ('ongoing', 'miscarried', 'completed', 'cancelled')");
                    table.CheckConstraint("ck_preg_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_pregnancies_audience_requests_audience_request_id",
                        column: x => x.audience_request_id,
                        principalSchema: "game",
                        principalTable: "audience_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pregnancies_characters_mother_character_id",
                        column: x => x.mother_character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pregnancies_users_resolved_by",
                        column: x => x.resolved_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<long>(type: "bigint", nullable: false),
                    total_price = table.Column<long>(type: "bigint", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ledger_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    purchased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.id);
                    table.CheckConstraint("ck_pur_quantity", "quantity > 0");
                    table.CheckConstraint("ck_pur_total_matches", "total_price = unit_price * quantity");
                    table.CheckConstraint("ck_pur_total_price", "total_price >= 0");
                    table.CheckConstraint("ck_pur_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "FK_purchases_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_currencies_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "game",
                        principalTable: "currencies",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_ledger_transactions_ledger_transaction_id",
                        column: x => x.ledger_transaction_id,
                        principalSchema: "game",
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchases_market_offers_market_offer_id",
                        column: x => x.market_offer_id,
                        principalSchema: "game",
                        principalTable: "market_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rank_history",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_rank_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_rank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rank_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_rank_history_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_history_ranks_from_rank_id",
                        column: x => x.from_rank_id,
                        principalSchema: "game",
                        principalTable: "ranks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_history_ranks_to_rank_id",
                        column: x => x.to_rank_id,
                        principalSchema: "game",
                        principalTable: "ranks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rank_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "status_effects",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effect_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    visibility = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "private"),
                    severity = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_effects", x => x.id);
                    table.CheckConstraint("ck_se_expiry", "expires_at IS NULL OR expires_at > starts_at");
                    table.CheckConstraint("ck_se_payload", "jsonb_typeof(payload) = 'object'");
                    table.CheckConstraint("ck_se_resolved", "resolved_at IS NULL OR resolved_at >= starts_at");
                    table.CheckConstraint("ck_se_severity", "severity BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_se_visibility", "visibility IN ('private', 'public', 'admin_only')");
                    table.ForeignKey(
                        name: "FK_status_effects_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    balance = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.id);
                    table.CheckConstraint("ck_wallets_balance", "balance >= 0");
                    table.CheckConstraint("ck_wallets_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_wallets_characters_character_id",
                        column: x => x.character_id,
                        principalSchema: "game",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallets_currencies_currency_code",
                        column: x => x.currency_code,
                        principalSchema: "game",
                        principalTable: "currencies",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_post_revisions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    body_markdown = table.Column<string>(type: "text", nullable: false),
                    revision_kind = table.Column<string>(type: "text", maxLength: 20, nullable: false, defaultValue: "draft_save"),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_post_revisions", x => x.id);
                    table.CheckConstraint("ck_epr_revision_kind", "revision_kind IN ('draft_save', 'submit', 'revision_request', 'approval', 'moderation')");
                    table.CheckConstraint("ck_epr_revision_no", "revision_no > 0");
                    table.ForeignKey(
                        name: "FK_event_post_revisions_event_posts_event_post_id",
                        column: x => x.event_post_id,
                        principalSchema: "game",
                        principalTable: "event_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_post_revisions_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    inventory_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "text", maxLength: 30, nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    quantity_after = table.Column<int>(type: "integer", nullable: false),
                    effect_snapshot = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    reference_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    request_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.id);
                    table.CheckConstraint("ck_it_after", "quantity_after >= 0");
                    table.CheckConstraint("ck_it_delta", "quantity_delta <> 0");
                    table.CheckConstraint("ck_it_effect_snapshot", "jsonb_typeof(effect_snapshot) = 'object'");
                    table.CheckConstraint("ck_it_type", "transaction_type IN ('purchase', 'reward', 'use', 'expire', 'admin_grant', 'admin_correction', 'refund')");
                    table.ForeignKey(
                        name: "FK_inventory_transactions_inventory_entries_inventory_entry_id",
                        column: x => x.inventory_entry_id,
                        principalSchema: "game",
                        principalTable: "inventory_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_users_initiated_by",
                        column: x => x.initiated_by,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "game",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_le_amount", "amount <> 0");
                    table.CheckConstraint("ck_le_balance_after", "balance_after >= 0");
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "game",
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalSchema: "game",
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ability_label_definitions_ability_code_display_label",
                schema: "game",
                table: "ability_label_definitions",
                columns: new[] { "ability_code", "display_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_role_assignments_granted_by",
                schema: "game",
                table: "admin_role_assignments",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_active",
                schema: "game",
                table: "announcements",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_published_by",
                schema: "game",
                table: "announcements",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_approval_decisions_approval_request_id_reviewer_id",
                schema: "game",
                table: "approval_decisions",
                columns: new[] { "approval_request_id", "reviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_decisions_reviewer_id",
                schema: "game",
                table: "approval_decisions",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_pending",
                schema: "game",
                table: "approval_requests",
                column: "requested_at",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_requested_by",
                schema: "game",
                table: "approval_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_audience_requests_character",
                schema: "game",
                table: "audience_requests",
                columns: new[] { "character_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audience_requests_character_id_idempotency_key",
                schema: "game",
                table: "audience_requests",
                columns: new[] { "character_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor",
                schema: "game",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target",
                schema: "game",
                table: "audit_logs",
                columns: new[] { "target_type", "target_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_births_child_character_id",
                schema: "game",
                table: "births",
                column: "child_character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_births_drawn_by",
                schema: "game",
                table: "births",
                column: "drawn_by");

            migrationBuilder.CreateIndex(
                name: "IX_births_pregnancy_id",
                schema: "game",
                table: "births",
                column: "pregnancy_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_births_wait_pool_entry_id",
                schema: "game",
                table: "births",
                column: "wait_pool_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_application_revisions_application_id_revision_no",
                schema: "game",
                table: "character_application_revisions",
                columns: new[] { "application_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_application_revisions_changed_by",
                schema: "game",
                table: "character_application_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_character_applications_created_character_id",
                schema: "game",
                table: "character_applications",
                column: "created_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_character_applications_player_portrait_submission_id",
                schema: "game",
                table: "character_applications",
                column: "player_portrait_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_character_applications_portrait_id",
                schema: "game",
                table: "character_applications",
                column: "portrait_id");

            migrationBuilder.CreateIndex(
                name: "ix_character_applications_review_queue",
                schema: "game",
                table: "character_applications",
                columns: new[] { "status", "submitted_at" },
                filter: "status IN ('submitted', 'needs_revision')");

            migrationBuilder.CreateIndex(
                name: "IX_character_applications_reviewed_by",
                schema: "game",
                table: "character_applications",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ux_character_applications_one_open_per_user",
                schema: "game",
                table: "character_applications",
                column: "user_id",
                unique: true,
                filter: "status IN ('draft', 'submitted', 'needs_revision')");

            migrationBuilder.CreateIndex(
                name: "ix_character_chronicle_character",
                schema: "game",
                table: "character_chronicle_entries",
                columns: new[] { "character_id", "happened_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_character_chronicle_entries_created_by",
                schema: "game",
                table: "character_chronicle_entries",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_character_chronicle_entries_location_id",
                schema: "game",
                table: "character_chronicle_entries",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_character_chronicle_source",
                schema: "game",
                table: "character_chronicle_entries",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_character_residence_history_changed_by",
                schema: "game",
                table: "character_residence_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_character_residence_history_residence_id",
                schema: "game",
                table: "character_residence_history",
                column: "residence_id");

            migrationBuilder.CreateIndex(
                name: "ux_character_residence_current",
                schema: "game",
                table: "character_residence_history",
                column: "character_id",
                unique: true,
                filter: "moved_out_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_character_status_history_changed_by",
                schema: "game",
                table: "character_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_character_status_history_character",
                schema: "game",
                table: "character_status_history",
                columns: new[] { "character_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_character_title_assignments_granted_by",
                schema: "game",
                table: "character_title_assignments",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "IX_character_title_assignments_revoked_by",
                schema: "game",
                table: "character_title_assignments",
                column: "revoked_by");

            migrationBuilder.CreateIndex(
                name: "IX_character_title_assignments_title_definition_id",
                schema: "game",
                table: "character_title_assignments",
                column: "title_definition_id");

            migrationBuilder.CreateIndex(
                name: "ux_character_title_assignments_active",
                schema: "game",
                table: "character_title_assignments",
                columns: new[] { "character_id", "title_definition_id" },
                unique: true,
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_character_title_assignments_one_primary",
                schema: "game",
                table: "character_title_assignments",
                column: "character_id",
                unique: true,
                filter: "revoked_at IS NULL AND is_primary = true");

            migrationBuilder.CreateIndex(
                name: "IX_character_title_definitions_code",
                schema: "game",
                table: "character_title_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_character_title_definitions_created_by",
                schema: "game",
                table: "character_title_definitions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_characters_player_portrait_submission_id",
                schema: "game",
                table: "characters",
                column: "player_portrait_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_portrait_id",
                schema: "game",
                table: "characters",
                column: "portrait_id");

            migrationBuilder.CreateIndex(
                name: "ix_characters_public_name",
                schema: "game",
                table: "characters",
                columns: new[] { "family_name", "given_name" });

            migrationBuilder.CreateIndex(
                name: "IX_characters_rank_id",
                schema: "game",
                table: "characters",
                column: "rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_residence_id",
                schema: "game",
                table: "characters",
                column: "residence_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_source_application_id",
                schema: "game",
                table: "characters",
                column: "source_application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_characters_status_role",
                schema: "game",
                table: "characters",
                columns: new[] { "status", "role" });

            migrationBuilder.CreateIndex(
                name: "ux_characters_one_current_per_user",
                schema: "game",
                table: "characters",
                column: "user_id",
                unique: true,
                filter: "status IN ('waiting_birth', 'active', 'paused', 'suspended')");

            migrationBuilder.CreateIndex(
                name: "IX_deaths_approval_request_id",
                schema: "game",
                table: "deaths",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_deaths_character_id",
                schema: "game",
                table: "deaths",
                column: "character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deaths_ruled_by",
                schema: "game",
                table: "deaths",
                column: "ruled_by");

            migrationBuilder.CreateIndex(
                name: "ix_event_participants_character",
                schema: "game",
                table: "event_participants",
                columns: new[] { "character_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_post_revisions_changed_by",
                schema: "game",
                table: "event_post_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_event_post_revisions_event_post_id_revision_no",
                schema: "game",
                table: "event_post_revisions",
                columns: new[] { "event_post_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_posts_character_id",
                schema: "game",
                table: "event_posts",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_posts_event_room_id_character_id_client_request_id",
                schema: "game",
                table: "event_posts",
                columns: new[] { "event_room_id", "character_id", "client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_posts_moderated_by",
                schema: "game",
                table: "event_posts",
                column: "moderated_by");

            migrationBuilder.CreateIndex(
                name: "ix_event_posts_review_queue",
                schema: "game",
                table: "event_posts",
                columns: new[] { "status", "submitted_at" },
                filter: "status IN ('submitted', 'under_review')");

            migrationBuilder.CreateIndex(
                name: "IX_event_posts_reviewed_by",
                schema: "game",
                table: "event_posts",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "ix_event_posts_room_feed",
                schema: "game",
                table: "event_posts",
                columns: new[] { "event_room_id", "published_at", "id" },
                filter: "status = 'approved'");

            migrationBuilder.CreateIndex(
                name: "IX_event_results_character_id",
                schema: "game",
                table: "event_results",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_results_event_room_id_character_id",
                schema: "game",
                table: "event_results",
                columns: new[] { "event_room_id", "character_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_event_results_settled_by",
                schema: "game",
                table: "event_results",
                column: "settled_by");

            migrationBuilder.CreateIndex(
                name: "IX_event_rooms_code",
                schema: "game",
                table: "event_rooms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_rooms_created_by",
                schema: "game",
                table: "event_rooms",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_event_rooms_location_id",
                schema: "game",
                table: "event_rooms",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_rooms_player_list",
                schema: "game",
                table: "event_rooms",
                columns: new[] { "status", "opens_at", "deadline_at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_play_review_queue",
                schema: "game",
                table: "external_play_submissions",
                columns: new[] { "status", "created_at" },
                filter: "status IN ('submitted', 'under_review')");

            migrationBuilder.CreateIndex(
                name: "IX_external_play_submissions_reviewed_by",
                schema: "game",
                table: "external_play_submissions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_external_play_submissions_submitted_by_character_id",
                schema: "game",
                table: "external_play_submissions",
                column: "submitted_by_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_setting_revisions_approval_request_id",
                schema: "game",
                table: "game_setting_revisions",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_setting_revisions_changed_by",
                schema: "game",
                table: "game_setting_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_game_setting_revisions_setting_key_revision_no",
                schema: "game",
                table: "game_setting_revisions",
                columns: new[] { "setting_key", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_settings_published_by",
                schema: "game",
                table: "game_settings",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_game_settings_updated_by",
                schema: "game",
                table: "game_settings",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_heir_wait_pool_draw_candidates",
                schema: "game",
                table: "heir_wait_pool_entries",
                columns: new[] { "entered_at", "id" },
                filter: "status = 'waiting'");

            migrationBuilder.CreateIndex(
                name: "IX_heir_wait_pool_entries_created_by",
                schema: "game",
                table: "heir_wait_pool_entries",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ux_heir_wait_pool_one_waiting_per_character",
                schema: "game",
                table: "heir_wait_pool_entries",
                column: "character_id",
                unique: true,
                filter: "status = 'waiting'");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expiry",
                schema: "game",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_user_id_http_method_request_path_idempo~",
                schema: "game",
                table: "idempotency_records",
                columns: new[] { "user_id", "http_method", "request_path", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_intrigue_actions_actor_character_id_idempotency_key",
                schema: "game",
                table: "intrigue_actions",
                columns: new[] { "actor_character_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intrigue_actions_pending",
                schema: "game",
                table: "intrigue_actions",
                column: "resolve_after",
                filter: "status IN ('submitted', 'processing')");

            migrationBuilder.CreateIndex(
                name: "IX_intrigue_actions_target_character_id",
                schema: "game",
                table: "intrigue_actions",
                column: "target_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_entries_character_available",
                schema: "game",
                table: "inventory_entries",
                columns: new[] { "character_id", "item_definition_id" },
                filter: "quantity > 0");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_entries_character_id_item_definition_id_expires_at",
                schema: "game",
                table: "inventory_entries",
                columns: new[] { "character_id", "item_definition_id", "expires_at" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_entries_item_definition_id",
                schema: "game",
                table: "inventory_entries",
                column: "item_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_entry",
                schema: "game",
                table: "inventory_transactions",
                columns: new[] { "inventory_entry_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_initiated_by",
                schema: "game",
                table: "inventory_transactions",
                column: "initiated_by");

            migrationBuilder.CreateIndex(
                name: "IX_item_definitions_code_version_no",
                schema: "game",
                table: "item_definitions",
                columns: new[] { "code", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_job",
                schema: "game",
                table: "job_runs",
                columns: new[] { "scheduled_job_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_transaction_id",
                schema: "game",
                table: "ledger_entries",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_wallet",
                schema: "game",
                table: "ledger_entries",
                columns: new[] { "wallet_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_initiated_by",
                schema: "game",
                table: "ledger_transactions",
                column: "initiated_by");

            migrationBuilder.CreateIndex(
                name: "IX_line_login_attempts_state_hash",
                schema: "game",
                table: "line_login_attempts",
                column: "state_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_market_offers_active_window",
                schema: "game",
                table: "market_offers",
                columns: new[] { "is_active", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_market_offers_created_by",
                schema: "game",
                table: "market_offers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_market_offers_currency_code",
                schema: "game",
                table: "market_offers",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_market_offers_item_definition_id",
                schema: "game",
                table: "market_offers",
                column: "item_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_created",
                schema: "game",
                table: "media_assets",
                columns: new[] { "owner_user_id", "created_at" },
                filter: "status <> 'deleted'");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_storage_key",
                schema: "game",
                table: "media_assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_unread",
                schema: "game",
                table: "notifications",
                columns: new[] { "user_id", "created_at" },
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_npc_revisions_changed_by",
                schema: "game",
                table: "npc_revisions",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_npc_revisions_npc_id_revision_no",
                schema: "game",
                table: "npc_revisions",
                columns: new[] { "npc_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_npcs_code",
                schema: "game",
                table: "npcs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_npcs_created_by",
                schema: "game",
                table: "npcs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_npcs_portrait_asset_id",
                schema: "game",
                table: "npcs",
                column: "portrait_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_npcs_primary_location_id",
                schema: "game",
                table: "npcs",
                column: "primary_location_id");

            migrationBuilder.CreateIndex(
                name: "IX_npcs_published_by",
                schema: "game",
                table: "npcs",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_offspring_links_child_character_id_parent_type_parent_chara~",
                schema: "game",
                table: "offspring_links",
                columns: new[] { "child_character_id", "parent_type", "parent_character_id", "parent_npc_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_offspring_links_parent_character_id",
                schema: "game",
                table: "offspring_links",
                column: "parent_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "game",
                table: "outbox_messages",
                columns: new[] { "available_at", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_player_portrait_submissions_media_asset_id",
                schema: "game",
                table: "player_portrait_submissions",
                column: "media_asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_portrait_submissions_review_queue",
                schema: "game",
                table: "player_portrait_submissions",
                columns: new[] { "status", "created_at" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_player_portrait_submissions_reviewed_by",
                schema: "game",
                table: "player_portrait_submissions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_player_portrait_submissions_user_id",
                schema: "game",
                table: "player_portrait_submissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pregnancies_audience_request_id",
                schema: "game",
                table: "pregnancies",
                column: "audience_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pregnancies_due",
                schema: "game",
                table: "pregnancies",
                column: "due_at",
                filter: "status = 'ongoing'");

            migrationBuilder.CreateIndex(
                name: "IX_pregnancies_resolved_by",
                schema: "game",
                table: "pregnancies",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ux_pregnancies_one_ongoing_per_mother",
                schema: "game",
                table: "pregnancies",
                column: "mother_character_id",
                unique: true,
                filter: "status = 'ongoing'");

            migrationBuilder.CreateIndex(
                name: "IX_preset_portraits_code",
                schema: "game",
                table: "preset_portraits",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_character_id_idempotency_key",
                schema: "game",
                table: "purchases",
                columns: new[] { "character_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_currency_code",
                schema: "game",
                table: "purchases",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_ledger_transaction_id",
                schema: "game",
                table: "purchases",
                column: "ledger_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_market_offer_id",
                schema: "game",
                table: "purchases",
                column: "market_offer_id");

            migrationBuilder.CreateIndex(
                name: "IX_rank_history_changed_by",
                schema: "game",
                table: "rank_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_rank_history_character",
                schema: "game",
                table: "rank_history",
                columns: new[] { "character_id", "effective_at" });

            migrationBuilder.CreateIndex(
                name: "IX_rank_history_from_rank_id",
                schema: "game",
                table: "rank_history",
                column: "from_rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_rank_history_to_rank_id",
                schema: "game",
                table: "rank_history",
                column: "to_rank_id");

            migrationBuilder.CreateIndex(
                name: "IX_ranks_applies_to_role_display_name",
                schema: "game",
                table: "ranks",
                columns: new[] { "applies_to_role", "display_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ranks_code",
                schema: "game",
                table: "ranks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ranks_role_grade",
                schema: "game",
                table: "ranks",
                columns: new[] { "applies_to_role", "ordinal", "display_name" });

            migrationBuilder.CreateIndex(
                name: "IX_reproduction_control_updated_by",
                schema: "game",
                table: "reproduction_control",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "IX_residences_code",
                schema: "game",
                table: "residences",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_jobs_due",
                schema: "game",
                table: "scheduled_jobs",
                column: "next_run_at",
                filter: "is_enabled = true");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_jobs_job_key",
                schema: "game",
                table: "scheduled_jobs",
                column: "job_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_status_effects_active",
                schema: "game",
                table: "status_effects",
                columns: new[] { "character_id", "effect_code" },
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_active_user",
                schema: "game",
                table: "user_sessions",
                columns: new[] { "user_id", "absolute_expires_at" },
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_token_hash",
                schema: "game",
                table: "user_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_line_user_id",
                schema: "game",
                table: "users",
                column: "line_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallets_character_id_currency_code",
                schema: "game",
                table: "wallets",
                columns: new[] { "character_id", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallets_currency_code",
                schema: "game",
                table: "wallets",
                column: "currency_code");

            migrationBuilder.CreateIndex(
                name: "IX_world_locations_code",
                schema: "game",
                table: "world_locations",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audience_requests_characters_character_id",
                schema: "game",
                table: "audience_requests",
                column: "character_id",
                principalSchema: "game",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_births_characters_child_character_id",
                schema: "game",
                table: "births",
                column: "child_character_id",
                principalSchema: "game",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_births_heir_wait_pool_entries_wait_pool_entry_id",
                schema: "game",
                table: "births",
                column: "wait_pool_entry_id",
                principalSchema: "game",
                principalTable: "heir_wait_pool_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_births_pregnancies_pregnancy_id",
                schema: "game",
                table: "births",
                column: "pregnancy_id",
                principalSchema: "game",
                principalTable: "pregnancies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_character_application_revisions_character_applications_appl~",
                schema: "game",
                table: "character_application_revisions",
                column: "application_id",
                principalSchema: "game",
                principalTable: "character_applications",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_character_applications_created_character",
                schema: "game",
                table: "character_applications",
                column: "created_character_id",
                principalSchema: "game",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_character_applications_users_reviewed_by",
                schema: "game",
                table: "character_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_character_applications_users_user_id",
                schema: "game",
                table: "character_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_characters_users_user_id",
                schema: "game",
                table: "characters");

            migrationBuilder.DropForeignKey(
                name: "FK_media_assets_users_owner_user_id",
                schema: "game",
                table: "media_assets");

            migrationBuilder.DropForeignKey(
                name: "FK_player_portrait_submissions_users_reviewed_by",
                schema: "game",
                table: "player_portrait_submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_player_portrait_submissions_users_user_id",
                schema: "game",
                table: "player_portrait_submissions");

            migrationBuilder.DropForeignKey(
                name: "fk_character_applications_created_character",
                schema: "game",
                table: "character_applications");

            migrationBuilder.DropTable(
                name: "ability_label_definitions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "admin_role_assignments",
                schema: "game");

            migrationBuilder.DropTable(
                name: "announcements",
                schema: "game");

            migrationBuilder.DropTable(
                name: "approval_decisions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "game");

            migrationBuilder.DropTable(
                name: "births",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_application_revisions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_chronicle_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_progress",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_residence_history",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_stats",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_status_history",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_title_assignments",
                schema: "game");

            migrationBuilder.DropTable(
                name: "deaths",
                schema: "game");

            migrationBuilder.DropTable(
                name: "event_participants",
                schema: "game");

            migrationBuilder.DropTable(
                name: "event_post_revisions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "event_results",
                schema: "game");

            migrationBuilder.DropTable(
                name: "external_play_submissions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "game_setting_revisions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "game");

            migrationBuilder.DropTable(
                name: "intrigue_actions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "inventory_transactions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "job_runs",
                schema: "game");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "line_login_attempts",
                schema: "game");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "game");

            migrationBuilder.DropTable(
                name: "npc_revisions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "offspring_links",
                schema: "game");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "game");

            migrationBuilder.DropTable(
                name: "purchases",
                schema: "game");

            migrationBuilder.DropTable(
                name: "rank_history",
                schema: "game");

            migrationBuilder.DropTable(
                name: "reproduction_control",
                schema: "game");

            migrationBuilder.DropTable(
                name: "status_effects",
                schema: "game");

            migrationBuilder.DropTable(
                name: "user_sessions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "world_state",
                schema: "game");

            migrationBuilder.DropTable(
                name: "heir_wait_pool_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "pregnancies",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_title_definitions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "event_posts",
                schema: "game");

            migrationBuilder.DropTable(
                name: "game_settings",
                schema: "game");

            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "game");

            migrationBuilder.DropTable(
                name: "inventory_entries",
                schema: "game");

            migrationBuilder.DropTable(
                name: "scheduled_jobs",
                schema: "game");

            migrationBuilder.DropTable(
                name: "wallets",
                schema: "game");

            migrationBuilder.DropTable(
                name: "npcs",
                schema: "game");

            migrationBuilder.DropTable(
                name: "ledger_transactions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "market_offers",
                schema: "game");

            migrationBuilder.DropTable(
                name: "audience_requests",
                schema: "game");

            migrationBuilder.DropTable(
                name: "event_rooms",
                schema: "game");

            migrationBuilder.DropTable(
                name: "currencies",
                schema: "game");

            migrationBuilder.DropTable(
                name: "item_definitions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "world_locations",
                schema: "game");

            migrationBuilder.DropTable(
                name: "users",
                schema: "game");

            migrationBuilder.DropTable(
                name: "characters",
                schema: "game");

            migrationBuilder.DropTable(
                name: "character_applications",
                schema: "game");

            migrationBuilder.DropTable(
                name: "ranks",
                schema: "game");

            migrationBuilder.DropTable(
                name: "residences",
                schema: "game");

            migrationBuilder.DropTable(
                name: "player_portrait_submissions",
                schema: "game");

            migrationBuilder.DropTable(
                name: "preset_portraits",
                schema: "game");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "game");
        }
    }
}
