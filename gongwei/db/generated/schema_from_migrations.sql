DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
        CREATE SCHEMA game;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS game.__ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
            CREATE SCHEMA game;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'game') THEN
            CREATE SCHEMA game;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE EXTENSION IF NOT EXISTS pgcrypto;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.ability_label_definitions (
        ability_code text NOT NULL,
        min_value smallint NOT NULL,
        max_value smallint NOT NULL,
        display_label character varying(30) NOT NULL,
        description character varying(500) NOT NULL DEFAULT '',
        sort_order integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_ability_label_definitions" PRIMARY KEY (ability_code, min_value),
        CONSTRAINT ck_ald_ability_code CHECK (ability_code IN ('vitality', 'appearance', 'strategy', 'luck')),
        CONSTRAINT ck_ald_max CHECK (max_value BETWEEN 0 AND 1000),
        CONSTRAINT ck_ald_min CHECK (min_value BETWEEN 0 AND 1000),
        CONSTRAINT ck_ald_range CHECK (min_value <= max_value),
        CONSTRAINT ck_ald_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.currencies (
        code character varying(30) NOT NULL,
        display_name character varying(50) NOT NULL,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_currencies" PRIMARY KEY (code)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.item_definitions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(80) NOT NULL,
        version_no integer NOT NULL DEFAULT 1,
        display_name character varying(100) NOT NULL,
        description character varying(1500) NOT NULL DEFAULT '',
        category text NOT NULL,
        image_url text,
        stack_limit integer NOT NULL DEFAULT 999,
        is_consumable boolean NOT NULL DEFAULT FALSE,
        effect_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        usage_rules jsonb NOT NULL DEFAULT ('{}'::jsonb),
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_item_definitions" PRIMARY KEY (id),
        CONSTRAINT ck_id_category CHECK (category IN ('clothing', 'medicine', 'poison', 'gift', 'quest', 'material', 'other')),
        CONSTRAINT ck_id_effect_payload CHECK (jsonb_typeof(effect_payload) = 'object'),
        CONSTRAINT ck_id_stack_limit CHECK (stack_limit > 0),
        CONSTRAINT ck_id_usage_rules CHECK (jsonb_typeof(usage_rules) = 'object'),
        CONSTRAINT ck_id_version_no CHECK (version_no > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.line_login_attempts (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        state_hash bytea NOT NULL,
        nonce_hash bytea NOT NULL,
        protected_payload bytea NOT NULL,
        return_url character varying(500) NOT NULL,
        ip_address inet,
        user_agent character varying(512),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        expires_at timestamp with time zone NOT NULL,
        consumed_at timestamp with time zone,
        failure_code character varying(80),
        CONSTRAINT "PK_line_login_attempts" PRIMARY KEY (id),
        CONSTRAINT ck_lla_consumed CHECK (consumed_at IS NULL OR consumed_at >= created_at),
        CONSTRAINT ck_lla_expiry CHECK (expires_at > created_at),
        CONSTRAINT ck_lla_return_url CHECK (return_url LIKE 'https://miglow.vip/gongwei/%' OR return_url = 'https://miglow.vip/gongwei/')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.outbox_messages (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        topic character varying(100) NOT NULL,
        aggregate_type character varying(60) NOT NULL,
        aggregate_id uuid NOT NULL,
        payload jsonb NOT NULL,
        occurred_at timestamp with time zone NOT NULL DEFAULT (now()),
        available_at timestamp with time zone NOT NULL DEFAULT (now()),
        processed_at timestamp with time zone,
        attempt_count integer NOT NULL DEFAULT 0,
        last_error character varying(2000),
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY (id),
        CONSTRAINT ck_outbox_attempts CHECK (attempt_count >= 0),
        CONSTRAINT ck_outbox_payload CHECK (jsonb_typeof(payload) = 'object')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.preset_portraits (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(80) NOT NULL,
        role text NOT NULL,
        display_name character varying(80) NOT NULL,
        asset_url text NOT NULL,
        thumbnail_url text,
        sort_order integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_preset_portraits" PRIMARY KEY (id),
        CONSTRAINT ck_preset_portraits_metadata CHECK (jsonb_typeof(metadata) = 'object'),
        CONSTRAINT ck_preset_portraits_role CHECK (role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_preset_portraits_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.ranks (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(50) NOT NULL,
        display_name character varying(80) NOT NULL,
        applies_to_role text NOT NULL,
        grade_code character varying(20) NOT NULL,
        ordinal integer NOT NULL,
        prestige_required bigint NOT NULL DEFAULT 0,
        monthly_stipend bigint NOT NULL DEFAULT 0,
        source_annual_stipend bigint NOT NULL DEFAULT 0,
        capacity integer,
        is_lead boolean NOT NULL DEFAULT FALSE,
        is_application_option boolean NOT NULL DEFAULT FALSE,
        initial_stats jsonb,
        promotion_rules jsonb NOT NULL DEFAULT ('{}'::jsonb),
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_ranks" PRIMARY KEY (id),
        CONSTRAINT ck_ranks_annual_stipend CHECK (source_annual_stipend >= 0),
        CONSTRAINT ck_ranks_capacity CHECK (capacity IS NULL OR capacity > 0),
        CONSTRAINT ck_ranks_initial_stats CHECK (initial_stats IS NULL OR jsonb_typeof(initial_stats) = 'object'),
        CONSTRAINT ck_ranks_monthly_stipend CHECK (monthly_stipend >= 0),
        CONSTRAINT ck_ranks_ordinal CHECK (ordinal >= 0),
        CONSTRAINT ck_ranks_prestige CHECK (prestige_required >= 0),
        CONSTRAINT ck_ranks_promotion_rules CHECK (jsonb_typeof(promotion_rules) = 'object'),
        CONSTRAINT ck_ranks_role CHECK (applies_to_role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_ranks_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.residences (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(50) NOT NULL,
        display_name character varying(80) NOT NULL,
        description character varying(1000) NOT NULL DEFAULT '',
        map_x numeric(5,2),
        map_y numeric(5,2),
        capacity integer,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_residences" PRIMARY KEY (id),
        CONSTRAINT ck_residences_capacity CHECK (capacity IS NULL OR capacity > 0),
        CONSTRAINT ck_residences_map_x CHECK (map_x IS NULL OR map_x BETWEEN 0 AND 100),
        CONSTRAINT ck_residences_map_y CHECK (map_y IS NULL OR map_y BETWEEN 0 AND 100),
        CONSTRAINT ck_residences_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.scheduled_jobs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        job_key character varying(100) NOT NULL,
        job_type character varying(80) NOT NULL,
        cron_expression character varying(100),
        payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        is_enabled boolean NOT NULL DEFAULT TRUE,
        next_run_at timestamp with time zone,
        locked_by character varying(100),
        locked_until timestamp with time zone,
        last_run_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_scheduled_jobs" PRIMARY KEY (id),
        CONSTRAINT ck_sj_payload CHECK (jsonb_typeof(payload) = 'object'),
        CONSTRAINT ck_sj_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.users (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        line_user_id text NOT NULL,
        display_name character varying(80) NOT NULL,
        avatar_url text,
        locale character varying(16) NOT NULL DEFAULT 'zh-TW',
        status text NOT NULL DEFAULT 'active',
        terms_accepted_at timestamp with time zone,
        privacy_accepted_at timestamp with time zone,
        last_login_at timestamp with time zone,
        last_seen_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_users" PRIMARY KEY (id),
        CONSTRAINT ck_users_display_name_len CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 80),
        CONSTRAINT ck_users_line_user_id_len CHECK (char_length(btrim(line_user_id)) BETWEEN 1 AND 255),
        CONSTRAINT ck_users_status CHECK (status IN ('active', 'suspended', 'deleted')),
        CONSTRAINT ck_users_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.world_locations (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(50) NOT NULL,
        display_name character varying(80) NOT NULL,
        description character varying(1500) NOT NULL DEFAULT '',
        image_url text,
        map_x numeric(5,2) NOT NULL,
        map_y numeric(5,2) NOT NULL,
        access_rules jsonb NOT NULL DEFAULT ('{}'::jsonb),
        sort_order integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_world_locations" PRIMARY KEY (id),
        CONSTRAINT ck_wl_access_rules CHECK (jsonb_typeof(access_rules) = 'object'),
        CONSTRAINT ck_wl_map_x CHECK (map_x BETWEEN 0 AND 100),
        CONSTRAINT ck_wl_map_y CHECK (map_y BETWEEN 0 AND 100),
        CONSTRAINT ck_wl_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.world_state (
        singleton_id smallint NOT NULL DEFAULT 1,
        era_code character varying(50) NOT NULL,
        display_year character varying(30) NOT NULL,
        season text NOT NULL,
        day_label character varying(30) NOT NULL,
        calendar_mode character varying(20) NOT NULL DEFAULT 'realtime_1to1',
        calendar_timezone character varying(50) NOT NULL DEFAULT 'Asia/Taipei',
        calendar_anchor_real_date date NOT NULL DEFAULT (CURRENT_DATE),
        calendar_anchor_game_date date NOT NULL DEFAULT (CURRENT_DATE),
        reproduction_open boolean NOT NULL DEFAULT TRUE,
        maintenance_mode boolean NOT NULL DEFAULT FALSE,
        config jsonb NOT NULL DEFAULT ('{}'::jsonb),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_world_state" PRIMARY KEY (singleton_id),
        CONSTRAINT ck_world_state_calendar_mode CHECK (calendar_mode = 'realtime_1to1'),
        CONSTRAINT ck_world_state_config CHECK (jsonb_typeof(config) = 'object'),
        CONSTRAINT ck_world_state_season CHECK (season IN ('spring', 'summer', 'autumn', 'winter')),
        CONSTRAINT ck_world_state_singleton CHECK (singleton_id = 1),
        CONSTRAINT ck_world_state_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.job_runs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        scheduled_job_id uuid NOT NULL,
        status text NOT NULL,
        started_at timestamp with time zone NOT NULL DEFAULT (now()),
        finished_at timestamp with time zone,
        attempt_no integer NOT NULL DEFAULT 1,
        result_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        error_message character varying(4000),
        worker_id character varying(100) NOT NULL,
        CONSTRAINT "PK_job_runs" PRIMARY KEY (id),
        CONSTRAINT ck_jr_attempt_no CHECK (attempt_no > 0),
        CONSTRAINT ck_jr_finished_pair CHECK ((status = 'running' AND finished_at IS NULL) OR (status <> 'running' AND finished_at IS NOT NULL)),
        CONSTRAINT ck_jr_result_payload CHECK (jsonb_typeof(result_payload) = 'object'),
        CONSTRAINT ck_jr_status CHECK (status IN ('running', 'succeeded', 'failed', 'cancelled')),
        CONSTRAINT "FK_job_runs_scheduled_jobs_scheduled_job_id" FOREIGN KEY (scheduled_job_id) REFERENCES game.scheduled_jobs (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.admin_role_assignments (
        user_id uuid NOT NULL,
        role text NOT NULL,
        granted_by uuid,
        granted_at timestamp with time zone NOT NULL DEFAULT (now()),
        expires_at timestamp with time zone,
        public_display_name character varying(80),
        public_title character varying(80),
        public_duty character varying(500),
        is_public boolean NOT NULL DEFAULT FALSE,
        sort_order integer NOT NULL DEFAULT 0,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_admin_role_assignments" PRIMARY KEY (user_id, role),
        CONSTRAINT ck_admin_role_assignments_expiry CHECK (expires_at IS NULL OR expires_at > granted_at),
        CONSTRAINT ck_admin_role_assignments_public CHECK (is_public = false OR public_display_name IS NOT NULL),
        CONSTRAINT ck_admin_role_assignments_role CHECK (role IN ('super_admin', 'character_reviewer', 'game_master', 'economy_manager', 'moderator', 'auditor', 'content_editor', 'character_manager', 'system_config_manager')),
        CONSTRAINT ck_admin_role_assignments_version CHECK (version > 0),
        CONSTRAINT "FK_admin_role_assignments_users_granted_by" FOREIGN KEY (granted_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_admin_role_assignments_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.announcements (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        title character varying(150) NOT NULL,
        body_markdown text NOT NULL,
        severity text NOT NULL DEFAULT 'info',
        audience text NOT NULL DEFAULT 'all',
        starts_at timestamp with time zone NOT NULL,
        ends_at timestamp with time zone,
        published_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_announcements" PRIMARY KEY (id),
        CONSTRAINT ck_ann_audience CHECK (audience IN ('all', 'players', 'admins')),
        CONSTRAINT ck_ann_severity CHECK (severity IN ('info', 'warning', 'critical')),
        CONSTRAINT ck_ann_version CHECK (version > 0),
        CONSTRAINT ck_ann_window CHECK (ends_at IS NULL OR ends_at > starts_at),
        CONSTRAINT "FK_announcements_users_published_by" FOREIGN KEY (published_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.approval_requests (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        action_type character varying(80) NOT NULL,
        target_type character varying(60) NOT NULL,
        target_id uuid,
        payload jsonb NOT NULL,
        reason character varying(1000) NOT NULL,
        status text NOT NULL DEFAULT 'pending',
        requested_by uuid NOT NULL,
        requested_at timestamp with time zone NOT NULL DEFAULT (now()),
        expires_at timestamp with time zone NOT NULL,
        resolved_at timestamp with time zone,
        executed_at timestamp with time zone,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_approval_requests" PRIMARY KEY (id),
        CONSTRAINT ck_apr_expiry CHECK (expires_at > requested_at),
        CONSTRAINT ck_apr_payload CHECK (jsonb_typeof(payload) = 'object'),
        CONSTRAINT ck_apr_status CHECK (status IN ('pending', 'approved', 'rejected', 'expired', 'executed', 'cancelled')),
        CONSTRAINT ck_apr_version CHECK (version > 0),
        CONSTRAINT "FK_approval_requests_users_requested_by" FOREIGN KEY (requested_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.audit_logs (
        id bigint GENERATED ALWAYS AS IDENTITY,
        occurred_at timestamp with time zone NOT NULL DEFAULT (now()),
        actor_user_id uuid,
        actor_role character varying(40),
        action character varying(100) NOT NULL,
        target_type character varying(60),
        target_id uuid,
        before_data jsonb,
        after_data jsonb,
        reason character varying(1000),
        request_id character varying(80),
        ip_address inet,
        user_agent character varying(512),
        metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
        CONSTRAINT "PK_audit_logs" PRIMARY KEY (id),
        CONSTRAINT ck_audit_metadata CHECK (jsonb_typeof(metadata) = 'object'),
        CONSTRAINT "FK_audit_logs_users_actor_user_id" FOREIGN KEY (actor_user_id) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_title_definitions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(80) NOT NULL,
        display_name character varying(100) NOT NULL,
        description character varying(1000) NOT NULL DEFAULT '',
        category text NOT NULL,
        applies_to_role text,
        visibility text NOT NULL DEFAULT 'public',
        style_token character varying(50),
        sort_order integer NOT NULL DEFAULT 0,
        is_active boolean NOT NULL DEFAULT TRUE,
        created_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_character_title_definitions" PRIMARY KEY (id),
        CONSTRAINT ck_ctd_category CHECK (category IN ('rank', 'achievement', 'story', 'honorary', 'secret')),
        CONSTRAINT ck_ctd_display_name_len CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 100),
        CONSTRAINT ck_ctd_role CHECK (applies_to_role IS NULL OR applies_to_role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_ctd_version CHECK (version > 0),
        CONSTRAINT ck_ctd_visibility CHECK (visibility IN ('public', 'owner_only', 'admin_only')),
        CONSTRAINT "FK_character_title_definitions_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.game_settings (
        setting_key character varying(120) NOT NULL,
        category character varying(40) NOT NULL,
        description character varying(1000) NOT NULL DEFAULT '',
        published_value jsonb NOT NULL,
        draft_value jsonb,
        validation_schema jsonb NOT NULL,
        risk_level text NOT NULL DEFAULT 'normal',
        is_public boolean NOT NULL DEFAULT FALSE,
        updated_by uuid NOT NULL,
        published_by uuid,
        published_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_game_settings" PRIMARY KEY (setting_key),
        CONSTRAINT ck_gs_key_len CHECK (char_length(btrim(setting_key)) BETWEEN 3 AND 120),
        CONSTRAINT ck_gs_published_by CHECK (published_at IS NULL OR published_by IS NOT NULL),
        CONSTRAINT ck_gs_risk_level CHECK (risk_level IN ('normal', 'high')),
        CONSTRAINT ck_gs_validation_schema CHECK (jsonb_typeof(validation_schema) = 'object'),
        CONSTRAINT ck_gs_version CHECK (version > 0),
        CONSTRAINT "FK_game_settings_users_published_by" FOREIGN KEY (published_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_game_settings_users_updated_by" FOREIGN KEY (updated_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.idempotency_records (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        http_method character varying(10) NOT NULL,
        request_path character varying(300) NOT NULL,
        idempotency_key character varying(100) NOT NULL,
        request_hash character varying(128) NOT NULL,
        status text NOT NULL DEFAULT 'processing',
        response_status integer,
        response_body jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        completed_at timestamp with time zone,
        expires_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_idempotency_records" PRIMARY KEY (id),
        CONSTRAINT ck_idem_expiry CHECK (expires_at > created_at),
        CONSTRAINT ck_idem_response_status CHECK (response_status IS NULL OR response_status BETWEEN 100 AND 599),
        CONSTRAINT ck_idem_status CHECK (status IN ('processing', 'completed', 'failed')),
        CONSTRAINT "FK_idempotency_records_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.ledger_transactions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        transaction_type text NOT NULL,
        reference_type character varying(60),
        reference_id uuid,
        reason_code character varying(80) NOT NULL,
        reason_text character varying(1000),
        initiated_by uuid,
        request_id character varying(80),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_ledger_transactions" PRIMARY KEY (id),
        CONSTRAINT ck_lt_type CHECK (transaction_type IN ('stipend', 'purchase', 'reward', 'item_use', 'admin_grant', 'admin_correction', 'refund')),
        CONSTRAINT "FK_ledger_transactions_users_initiated_by" FOREIGN KEY (initiated_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.market_offers (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        item_definition_id uuid NOT NULL,
        currency_code character varying(30) NOT NULL,
        unit_price bigint NOT NULL,
        stock_total integer,
        stock_sold integer NOT NULL DEFAULT 0,
        per_character_limit integer,
        starts_at timestamp with time zone,
        ends_at timestamp with time zone,
        eligibility_rules jsonb NOT NULL DEFAULT ('{}'::jsonb),
        is_active boolean NOT NULL DEFAULT TRUE,
        created_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_market_offers" PRIMARY KEY (id),
        CONSTRAINT ck_mo_eligibility CHECK (jsonb_typeof(eligibility_rules) = 'object'),
        CONSTRAINT ck_mo_limit CHECK (per_character_limit IS NULL OR per_character_limit > 0),
        CONSTRAINT ck_mo_sold_within_total CHECK (stock_total IS NULL OR stock_sold <= stock_total),
        CONSTRAINT ck_mo_stock_sold CHECK (stock_sold >= 0),
        CONSTRAINT ck_mo_stock_total CHECK (stock_total IS NULL OR stock_total >= 0),
        CONSTRAINT ck_mo_unit_price CHECK (unit_price >= 0),
        CONSTRAINT ck_mo_version CHECK (version > 0),
        CONSTRAINT ck_mo_window CHECK (ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at),
        CONSTRAINT "FK_market_offers_currencies_currency_code" FOREIGN KEY (currency_code) REFERENCES game.currencies (code) ON DELETE RESTRICT,
        CONSTRAINT "FK_market_offers_item_definitions_item_definition_id" FOREIGN KEY (item_definition_id) REFERENCES game.item_definitions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_market_offers_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.media_assets (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        owner_user_id uuid NOT NULL,
        storage_key text NOT NULL,
        original_file_name character varying(255) NOT NULL,
        original_mime_type character varying(100) NOT NULL,
        stored_mime_type character varying(30),
        byte_size bigint NOT NULL,
        width integer NOT NULL,
        height integer NOT NULL,
        sha256 character(64) NOT NULL,
        status text NOT NULL DEFAULT 'uploaded',
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_media_assets" PRIMARY KEY (id),
        CONSTRAINT ck_media_assets_byte_size CHECK (byte_size BETWEEN 1 AND 8388608),
        CONSTRAINT ck_media_assets_height CHECK (height >= 800),
        CONSTRAINT ck_media_assets_original_mime CHECK (original_mime_type IN ('image/jpeg', 'image/png', 'image/webp')),
        CONSTRAINT ck_media_assets_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$'),
        CONSTRAINT ck_media_assets_status CHECK (status IN ('uploaded', 'processing', 'ready', 'quarantined', 'deleted')),
        CONSTRAINT ck_media_assets_storage_key_len CHECK (char_length(btrim(storage_key)) BETWEEN 1 AND 1024),
        CONSTRAINT ck_media_assets_stored_mime CHECK (stored_mime_type IS NULL OR stored_mime_type IN ('image/webp', 'image/jpeg')),
        CONSTRAINT ck_media_assets_version CHECK (version > 0),
        CONSTRAINT ck_media_assets_width CHECK (width >= 600),
        CONSTRAINT "FK_media_assets_users_owner_user_id" FOREIGN KEY (owner_user_id) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.notifications (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        notification_type character varying(60) NOT NULL,
        title character varying(150) NOT NULL,
        body character varying(2000) NOT NULL,
        route character varying(300),
        payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        read_at timestamp with time zone,
        expires_at timestamp with time zone,
        CONSTRAINT "PK_notifications" PRIMARY KEY (id),
        CONSTRAINT ck_notif_payload CHECK (jsonb_typeof(payload) = 'object'),
        CONSTRAINT "FK_notifications_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.reproduction_control (
        singleton_id smallint NOT NULL DEFAULT 1,
        is_open boolean NOT NULL DEFAULT TRUE,
        closed_reason character varying(500),
        conception_rate_percent smallint NOT NULL DEFAULT 100,
        pregnancy_duration_days smallint NOT NULL DEFAULT 10,
        miscarriage_mode text NOT NULL DEFAULT 'event_only',
        miscarriage_rules jsonb NOT NULL DEFAULT ('{"baseRatePercent":0}'::jsonb),
        rules_version character varying(40) NOT NULL DEFAULT 'reproduction-1',
        updated_by uuid,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_reproduction_control" PRIMARY KEY (singleton_id),
        CONSTRAINT ck_rc_conception_rate CHECK (conception_rate_percent BETWEEN 0 AND 100),
        CONSTRAINT ck_rc_duration CHECK (pregnancy_duration_days BETWEEN 1 AND 365),
        CONSTRAINT ck_rc_miscarriage_mode CHECK (miscarriage_mode IN ('disabled', 'event_only', 'threshold', 'daily_probability')),
        CONSTRAINT ck_rc_miscarriage_rules CHECK (jsonb_typeof(miscarriage_rules) = 'object'),
        CONSTRAINT ck_rc_singleton CHECK (singleton_id = 1),
        CONSTRAINT ck_rc_version CHECK (version > 0),
        CONSTRAINT "FK_reproduction_control_users_updated_by" FOREIGN KEY (updated_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.user_sessions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        token_hash bytea NOT NULL,
        csrf_secret_hash bytea NOT NULL,
        ip_address inet,
        user_agent character varying(512),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        last_seen_at timestamp with time zone NOT NULL DEFAULT (now()),
        idle_expires_at timestamp with time zone NOT NULL,
        absolute_expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        revoke_reason character varying(200),
        CONSTRAINT "PK_user_sessions" PRIMARY KEY (id),
        CONSTRAINT ck_user_sessions_absolute_after_created CHECK (absolute_expires_at > created_at),
        CONSTRAINT ck_user_sessions_expiry_order CHECK (idle_expires_at <= absolute_expires_at),
        CONSTRAINT "FK_user_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.event_rooms (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(80) NOT NULL,
        title character varying(150) NOT NULL,
        summary character varying(1000) NOT NULL DEFAULT '',
        body_markdown text NOT NULL DEFAULT '',
        event_type text NOT NULL,
        status text NOT NULL DEFAULT 'draft',
        location_id uuid,
        visibility text NOT NULL DEFAULT 'public',
        participant_limit integer,
        rules_version character varying(40) NOT NULL,
        rules_snapshot jsonb NOT NULL DEFAULT ('{}'::jsonb),
        opens_at timestamp with time zone,
        deadline_at timestamp with time zone,
        settled_at timestamp with time zone,
        created_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_event_rooms" PRIMARY KEY (id),
        CONSTRAINT ck_er_event_type CHECK (event_type IN ('main', 'social', 'investigation', 'limited', 'private', 'admin')),
        CONSTRAINT ck_er_participant_limit CHECK (participant_limit IS NULL OR participant_limit > 0),
        CONSTRAINT ck_er_rules_snapshot CHECK (jsonb_typeof(rules_snapshot) = 'object'),
        CONSTRAINT ck_er_settled_at CHECK ((status = 'settled' AND settled_at IS NOT NULL) OR status <> 'settled'),
        CONSTRAINT ck_er_status CHECK (status IN ('draft', 'scheduled', 'open', 'locked', 'settled', 'cancelled')),
        CONSTRAINT ck_er_version CHECK (version > 0),
        CONSTRAINT ck_er_visibility CHECK (visibility IN ('public', 'invited', 'private')),
        CONSTRAINT ck_er_window CHECK (deadline_at IS NULL OR opens_at IS NULL OR deadline_at > opens_at),
        CONSTRAINT "FK_event_rooms_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_rooms_world_locations_location_id" FOREIGN KEY (location_id) REFERENCES game.world_locations (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.approval_decisions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        approval_request_id uuid NOT NULL,
        reviewer_id uuid NOT NULL,
        decision text NOT NULL,
        note character varying(1000),
        decided_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_approval_decisions" PRIMARY KEY (id),
        CONSTRAINT ck_apd_decision CHECK (decision IN ('approve', 'reject')),
        CONSTRAINT "FK_approval_decisions_approval_requests_approval_request_id" FOREIGN KEY (approval_request_id) REFERENCES game.approval_requests (id) ON DELETE CASCADE,
        CONSTRAINT "FK_approval_decisions_users_reviewer_id" FOREIGN KEY (reviewer_id) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.game_setting_revisions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        setting_key character varying(120) NOT NULL,
        revision_no integer NOT NULL,
        previous_value jsonb,
        published_value jsonb NOT NULL,
        change_reason character varying(1000) NOT NULL,
        approval_request_id uuid,
        changed_by uuid NOT NULL,
        changed_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_game_setting_revisions" PRIMARY KEY (id),
        CONSTRAINT ck_gsr_revision_no CHECK (revision_no > 0),
        CONSTRAINT "FK_game_setting_revisions_game_settings_setting_key" FOREIGN KEY (setting_key) REFERENCES game.game_settings (setting_key) ON DELETE RESTRICT,
        CONSTRAINT "FK_game_setting_revisions_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE RESTRICT,
        CONSTRAINT fk_game_setting_revisions_approval_request FOREIGN KEY (approval_request_id) REFERENCES game.approval_requests (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.npcs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        code character varying(80) NOT NULL,
        display_name character varying(100) NOT NULL,
        title character varying(100) NOT NULL DEFAULT '',
        sex text,
        summary character varying(1500) NOT NULL DEFAULT '',
        story_markdown text NOT NULL DEFAULT '',
        public_profile jsonb NOT NULL DEFAULT ('{}'::jsonb),
        portrait_asset_id uuid,
        portrait_url text,
        primary_location_id uuid,
        status text NOT NULL DEFAULT 'draft',
        sort_order integer NOT NULL DEFAULT 0,
        created_by uuid NOT NULL,
        published_by uuid,
        published_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_npcs" PRIMARY KEY (id),
        CONSTRAINT ck_npc_display_name_len CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 100),
        CONSTRAINT ck_npc_portrait_present CHECK (portrait_asset_id IS NOT NULL OR portrait_url IS NOT NULL),
        CONSTRAINT ck_npc_public_profile CHECK (jsonb_typeof(public_profile) = 'object'),
        CONSTRAINT ck_npc_published_pair CHECK ((status = 'published' AND published_by IS NOT NULL AND published_at IS NOT NULL) OR status <> 'published'),
        CONSTRAINT ck_npc_sex CHECK (sex IN ('female', 'male', 'unknown')),
        CONSTRAINT ck_npc_status CHECK (status IN ('draft', 'review', 'published', 'archived')),
        CONSTRAINT ck_npc_story_len CHECK (char_length(story_markdown) <= 50000),
        CONSTRAINT ck_npc_version CHECK (version > 0),
        CONSTRAINT "FK_npcs_media_assets_portrait_asset_id" FOREIGN KEY (portrait_asset_id) REFERENCES game.media_assets (id) ON DELETE SET NULL,
        CONSTRAINT "FK_npcs_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_npcs_users_published_by" FOREIGN KEY (published_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_npcs_world_locations_primary_location_id" FOREIGN KEY (primary_location_id) REFERENCES game.world_locations (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.player_portrait_submissions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        media_asset_id uuid NOT NULL,
        role text NOT NULL,
        crop_x numeric(6,5) NOT NULL DEFAULT 0.0,
        crop_y numeric(6,5) NOT NULL DEFAULT 0.0,
        crop_width numeric(6,5) NOT NULL DEFAULT 1.0,
        crop_height numeric(6,5) NOT NULL DEFAULT 1.0,
        status text NOT NULL DEFAULT 'pending',
        reviewed_by uuid,
        reviewed_at timestamp with time zone,
        review_note character varying(1000),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_player_portrait_submissions" PRIMARY KEY (id),
        CONSTRAINT ck_pps_crop_height CHECK (crop_height > 0 AND crop_height <= 1),
        CONSTRAINT ck_pps_crop_width CHECK (crop_width > 0 AND crop_width <= 1),
        CONSTRAINT ck_pps_crop_x CHECK (crop_x BETWEEN 0 AND 1),
        CONSTRAINT ck_pps_crop_x_bounds CHECK (crop_x + crop_width <= 1.00001),
        CONSTRAINT ck_pps_crop_y CHECK (crop_y BETWEEN 0 AND 1),
        CONSTRAINT ck_pps_crop_y_bounds CHECK (crop_y + crop_height <= 1.00001),
        CONSTRAINT ck_pps_reviewed_pair CHECK ((status IN ('approved', 'rejected') AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL) OR status IN ('pending', 'withdrawn')),
        CONSTRAINT ck_pps_role CHECK (role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_pps_status CHECK (status IN ('pending', 'approved', 'rejected', 'withdrawn')),
        CONSTRAINT ck_pps_version CHECK (version > 0),
        CONSTRAINT "FK_player_portrait_submissions_media_assets_media_asset_id" FOREIGN KEY (media_asset_id) REFERENCES game.media_assets (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_player_portrait_submissions_users_reviewed_by" FOREIGN KEY (reviewed_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_player_portrait_submissions_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.npc_revisions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        npc_id uuid NOT NULL,
        revision_no integer NOT NULL,
        snapshot jsonb NOT NULL,
        change_kind text NOT NULL,
        change_note character varying(1000),
        changed_by uuid NOT NULL,
        changed_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_npc_revisions" PRIMARY KEY (id),
        CONSTRAINT ck_npcrev_change_kind CHECK (change_kind IN ('create', 'edit', 'publish', 'archive', 'restore')),
        CONSTRAINT ck_npcrev_revision_no CHECK (revision_no > 0),
        CONSTRAINT ck_npcrev_snapshot CHECK (jsonb_typeof(snapshot) = 'object'),
        CONSTRAINT "FK_npc_revisions_npcs_npc_id" FOREIGN KEY (npc_id) REFERENCES game.npcs (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_npc_revisions_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.audience_requests (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        audience_type text NOT NULL,
        status text NOT NULL DEFAULT 'submitted',
        qualification_snapshot jsonb NOT NULL,
        requested_at timestamp with time zone NOT NULL DEFAULT (now()),
        resolved_at timestamp with time zone,
        result_code character varying(80),
        result_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        idempotency_key character varying(100) NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_audience_requests" PRIMARY KEY (id),
        CONSTRAINT ck_ar_qualification CHECK (jsonb_typeof(qualification_snapshot) = 'object'),
        CONSTRAINT ck_ar_resolved_pair CHECK ((status IN ('resolved', 'rejected', 'cancelled') AND resolved_at IS NOT NULL) OR status IN ('submitted', 'approved')),
        CONSTRAINT ck_ar_result_payload CHECK (jsonb_typeof(result_payload) = 'object'),
        CONSTRAINT ck_ar_status CHECK (status IN ('submitted', 'approved', 'rejected', 'resolved', 'cancelled')),
        CONSTRAINT ck_ar_type CHECK (audience_type IN ('meal', 'bedchamber')),
        CONSTRAINT ck_ar_version CHECK (version > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.births (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        pregnancy_id uuid NOT NULL,
        wait_pool_entry_id uuid NOT NULL,
        child_character_id uuid NOT NULL,
        candidate_count integer NOT NULL,
        candidate_set_hash character varying(128) NOT NULL,
        random_algorithm character varying(80) NOT NULL,
        random_proof_hash character varying(128) NOT NULL,
        rules_version character varying(40) NOT NULL,
        drawn_by uuid,
        born_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_births" PRIMARY KEY (id),
        CONSTRAINT ck_births_candidate_count CHECK (candidate_count > 0),
        CONSTRAINT "FK_births_users_drawn_by" FOREIGN KEY (drawn_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_application_revisions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        application_id uuid NOT NULL,
        revision_no integer NOT NULL,
        snapshot jsonb NOT NULL,
        changed_by uuid NOT NULL,
        change_reason character varying(500),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_character_application_revisions" PRIMARY KEY (id),
        CONSTRAINT ck_car_revision_no CHECK (revision_no > 0),
        CONSTRAINT ck_car_snapshot CHECK (jsonb_typeof(snapshot) = 'object'),
        CONSTRAINT "FK_character_application_revisions_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_applications (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        role text NOT NULL,
        sex text NOT NULL,
        family_name character varying(20) NOT NULL DEFAULT '',
        given_name character varying(30) NOT NULL DEFAULT '',
        courtesy_name character varying(30),
        birth_date_label character varying(30),
        age smallint,
        appearance character varying(3000) NOT NULL DEFAULT '',
        biography character varying(2000) NOT NULL DEFAULT '',
        personality character varying(1000) NOT NULL DEFAULT '',
        strengths character varying(1000) NOT NULL DEFAULT '',
        weaknesses character varying(1000) NOT NULL DEFAULT '',
        likes character varying(1000) NOT NULL DEFAULT '',
        dislikes character varying(1000) NOT NULL DEFAULT '',
        portrait_id uuid,
        player_portrait_submission_id uuid,
        status text NOT NULL DEFAULT 'draft',
        form_data jsonb NOT NULL DEFAULT ('{}'::jsonb),
        submitted_at timestamp with time zone,
        reviewed_at timestamp with time zone,
        reviewed_by uuid,
        review_note character varying(2000),
        created_character_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_character_applications" PRIMARY KEY (id),
        CONSTRAINT ck_ca_age_and_family CHECK (status = 'draft' OR (role = 'consort' AND age BETWEEN 15 AND 18 AND char_length(btrim(family_name)) > 0) OR (role IN ('prince', 'princess') AND age = 0 AND family_name = '蕭')),
        CONSTRAINT ck_ca_appearance_len CHECK (status = 'draft' OR char_length(appearance) >= 60),
        CONSTRAINT ck_ca_approved_reviewed CHECK ((status = 'approved' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL) OR status <> 'approved'),
        CONSTRAINT ck_ca_biography_len CHECK (status = 'draft' OR char_length(biography) >= 200),
        CONSTRAINT ck_ca_dislikes_len CHECK (status = 'draft' OR char_length(dislikes) >= 50),
        CONSTRAINT ck_ca_draft_not_submitted CHECK ((status = 'draft' AND submitted_at IS NULL) OR status <> 'draft'),
        CONSTRAINT ck_ca_form_data CHECK (jsonb_typeof(form_data) = 'object'),
        CONSTRAINT ck_ca_given_name CHECK (status = 'draft' OR char_length(btrim(given_name)) BETWEEN 1 AND 30),
        CONSTRAINT ck_ca_likes_len CHECK (status = 'draft' OR char_length(likes) >= 50),
        CONSTRAINT ck_ca_personality_len CHECK (status = 'draft' OR char_length(personality) >= 50),
        CONSTRAINT ck_ca_portrait_xor CHECK (status = 'draft' OR ((portrait_id IS NOT NULL)::integer + (player_portrait_submission_id IS NOT NULL)::integer = 1)),
        CONSTRAINT ck_ca_role CHECK (role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_ca_role_sex CHECK ((role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')),
        CONSTRAINT ck_ca_sex CHECK (sex IN ('female', 'male')),
        CONSTRAINT ck_ca_status CHECK (status IN ('draft', 'submitted', 'needs_revision', 'approved', 'rejected', 'cancelled')),
        CONSTRAINT ck_ca_strengths_len CHECK (status = 'draft' OR char_length(strengths) >= 50),
        CONSTRAINT ck_ca_version CHECK (version > 0),
        CONSTRAINT ck_ca_weaknesses_len CHECK (status = 'draft' OR char_length(weaknesses) >= 50),
        CONSTRAINT "FK_character_applications_player_portrait_submissions_player_p~" FOREIGN KEY (player_portrait_submission_id) REFERENCES game.player_portrait_submissions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_applications_preset_portraits_portrait_id" FOREIGN KEY (portrait_id) REFERENCES game.preset_portraits (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_applications_users_reviewed_by" FOREIGN KEY (reviewed_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_character_applications_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.characters (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        source_application_id uuid NOT NULL,
        role text NOT NULL,
        sex text NOT NULL,
        family_name character varying(20),
        given_name character varying(30) NOT NULL,
        courtesy_name character varying(30),
        birth_date_label character varying(30),
        age_at_creation smallint NOT NULL,
        appearance character varying(3000) NOT NULL,
        biography character varying(2000) NOT NULL DEFAULT '',
        personality character varying(1000) NOT NULL DEFAULT '',
        strengths character varying(1000) NOT NULL,
        weaknesses character varying(1000) NOT NULL,
        likes character varying(1000) NOT NULL,
        dislikes character varying(1000) NOT NULL,
        portrait_id uuid,
        player_portrait_submission_id uuid,
        rank_id uuid,
        residence_id uuid,
        status text NOT NULL,
        pause_reason character varying(500),
        activated_at timestamp with time zone,
        died_at timestamp with time zone,
        archived_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_characters" PRIMARY KEY (id),
        CONSTRAINT ck_characters_archived_at CHECK ((status = 'archived' AND archived_at IS NOT NULL) OR status <> 'archived'),
        CONSTRAINT ck_characters_dead_at CHECK ((status = 'dead' AND died_at IS NOT NULL) OR status <> 'dead'),
        CONSTRAINT ck_characters_portrait_xor CHECK ((portrait_id IS NOT NULL)::integer + (player_portrait_submission_id IS NOT NULL)::integer = 1),
        CONSTRAINT ck_characters_role CHECK (role IN ('consort', 'prince', 'princess')),
        CONSTRAINT ck_characters_role_sex CHECK ((role = 'prince' AND sex = 'male') OR (role IN ('consort', 'princess') AND sex = 'female')),
        CONSTRAINT ck_characters_sex CHECK (sex IN ('female', 'male')),
        CONSTRAINT ck_characters_status CHECK (status IN ('waiting_birth', 'active', 'paused', 'dead', 'suspended', 'archived')),
        CONSTRAINT ck_characters_version CHECK (version > 0),
        CONSTRAINT ck_characters_waiting_birth_role CHECK ((status = 'waiting_birth' AND role IN ('prince', 'princess')) OR status <> 'waiting_birth'),
        CONSTRAINT "FK_characters_character_applications_source_application_id" FOREIGN KEY (source_application_id) REFERENCES game.character_applications (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_characters_player_portrait_submissions_player_portrait_subm~" FOREIGN KEY (player_portrait_submission_id) REFERENCES game.player_portrait_submissions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_characters_preset_portraits_portrait_id" FOREIGN KEY (portrait_id) REFERENCES game.preset_portraits (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_characters_ranks_rank_id" FOREIGN KEY (rank_id) REFERENCES game.ranks (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_characters_residences_residence_id" FOREIGN KEY (residence_id) REFERENCES game.residences (id) ON DELETE SET NULL,
        CONSTRAINT "FK_characters_users_user_id" FOREIGN KEY (user_id) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_chronicle_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        entry_type text NOT NULL,
        visibility text NOT NULL DEFAULT 'public',
        title character varying(150) NOT NULL,
        detail character varying(3000) NOT NULL DEFAULT '',
        location_id uuid,
        source_type character varying(60) NOT NULL,
        source_id uuid,
        stat_changes jsonb NOT NULL DEFAULT ('[]'::jsonb),
        resource_changes jsonb NOT NULL DEFAULT ('[]'::jsonb),
        happened_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        created_by uuid,
        request_id character varying(80),
        metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
        CONSTRAINT "PK_character_chronicle_entries" PRIMARY KEY (id),
        CONSTRAINT ck_cce_entry_type CHECK (entry_type IN ('event', 'economy', 'inventory', 'rank', 'status', 'reproduction', 'intrigue', 'admin', 'system')),
        CONSTRAINT ck_cce_metadata CHECK (jsonb_typeof(metadata) = 'object'),
        CONSTRAINT ck_cce_resource_changes CHECK (jsonb_typeof(resource_changes) = 'array'),
        CONSTRAINT ck_cce_stat_changes CHECK (jsonb_typeof(stat_changes) = 'array'),
        CONSTRAINT ck_cce_visibility CHECK (visibility IN ('public', 'owner_only', 'admin_only')),
        CONSTRAINT "FK_character_chronicle_entries_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_chronicle_entries_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_character_chronicle_entries_world_locations_location_id" FOREIGN KEY (location_id) REFERENCES game.world_locations (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_progress (
        character_id uuid NOT NULL,
        settled_event_count bigint NOT NULL DEFAULT 0,
        approved_event_post_count bigint NOT NULL DEFAULT 0,
        approved_external_play_count bigint NOT NULL DEFAULT 0,
        self_play_word_count bigint NOT NULL DEFAULT 0,
        week_start_date date NOT NULL,
        weekly_message_count integer NOT NULL DEFAULT 0,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_character_progress" PRIMARY KEY (character_id),
        CONSTRAINT ck_cp_approved_external CHECK (approved_external_play_count >= 0),
        CONSTRAINT ck_cp_approved_posts CHECK (approved_event_post_count >= 0),
        CONSTRAINT ck_cp_self_play_words CHECK (self_play_word_count >= 0),
        CONSTRAINT ck_cp_settled_events CHECK (settled_event_count >= 0),
        CONSTRAINT ck_cp_version CHECK (version > 0),
        CONSTRAINT ck_cp_weekly_messages CHECK (weekly_message_count >= 0),
        CONSTRAINT "FK_character_progress_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_residence_history (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        residence_id uuid NOT NULL,
        moved_in_at timestamp with time zone NOT NULL,
        moved_out_at timestamp with time zone,
        reason character varying(500),
        changed_by uuid,
        CONSTRAINT "PK_character_residence_history" PRIMARY KEY (id),
        CONSTRAINT ck_crh_order CHECK (moved_out_at IS NULL OR moved_out_at >= moved_in_at),
        CONSTRAINT "FK_character_residence_history_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_residence_history_residences_residence_id" FOREIGN KEY (residence_id) REFERENCES game.residences (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_residence_history_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_stats (
        character_id uuid NOT NULL,
        vitality smallint NOT NULL DEFAULT 0,
        appearance smallint NOT NULL DEFAULT 0,
        strategy smallint NOT NULL DEFAULT 0,
        luck smallint NOT NULL DEFAULT 0,
        prestige bigint NOT NULL DEFAULT 0,
        favor integer NOT NULL DEFAULT 0,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_character_stats" PRIMARY KEY (character_id),
        CONSTRAINT ck_cs_appearance CHECK (appearance BETWEEN 0 AND 1000),
        CONSTRAINT ck_cs_favor CHECK (favor BETWEEN -1000 AND 1000),
        CONSTRAINT ck_cs_luck CHECK (luck BETWEEN 0 AND 1000),
        CONSTRAINT ck_cs_prestige CHECK (prestige >= 0),
        CONSTRAINT ck_cs_strategy CHECK (strategy BETWEEN 0 AND 1000),
        CONSTRAINT ck_cs_version CHECK (version > 0),
        CONSTRAINT ck_cs_vitality CHECK (vitality BETWEEN 0 AND 1000),
        CONSTRAINT "FK_character_stats_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_status_history (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        from_status text,
        to_status text NOT NULL,
        reason_code character varying(80) NOT NULL,
        reason_text character varying(1000),
        changed_by uuid,
        request_id character varying(80),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_character_status_history" PRIMARY KEY (id),
        CONSTRAINT "FK_character_status_history_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_status_history_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.character_title_assignments (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        title_definition_id uuid NOT NULL,
        is_primary boolean NOT NULL DEFAULT FALSE,
        granted_by uuid NOT NULL,
        granted_at timestamp with time zone NOT NULL DEFAULT (now()),
        grant_reason character varying(1000) NOT NULL,
        revoked_by uuid,
        revoked_at timestamp with time zone,
        revoke_reason character varying(1000),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_character_title_assignments" PRIMARY KEY (id),
        CONSTRAINT ck_cta_revoked_triple CHECK ((revoked_at IS NULL AND revoked_by IS NULL AND revoke_reason IS NULL) OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revoke_reason IS NOT NULL)),
        CONSTRAINT ck_cta_version CHECK (version > 0),
        CONSTRAINT "FK_character_title_assignments_character_title_definitions_tit~" FOREIGN KEY (title_definition_id) REFERENCES game.character_title_definitions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_title_assignments_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_title_assignments_users_granted_by" FOREIGN KEY (granted_by) REFERENCES game.users (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_character_title_assignments_users_revoked_by" FOREIGN KEY (revoked_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.deaths (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        cause_code character varying(80) NOT NULL,
        public_cause character varying(1000) NOT NULL,
        private_details jsonb NOT NULL DEFAULT ('{}'::jsonb),
        source_type character varying(60),
        source_id uuid,
        occurred_at timestamp with time zone NOT NULL,
        ruled_by uuid,
        approval_request_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_deaths" PRIMARY KEY (id),
        CONSTRAINT ck_deaths_private_details CHECK (jsonb_typeof(private_details) = 'object'),
        CONSTRAINT "FK_deaths_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_deaths_users_ruled_by" FOREIGN KEY (ruled_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT fk_deaths_approval_request FOREIGN KEY (approval_request_id) REFERENCES game.approval_requests (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.event_participants (
        event_room_id uuid NOT NULL,
        character_id uuid NOT NULL,
        participant_role character varying(40) NOT NULL DEFAULT 'participant',
        status text NOT NULL DEFAULT 'joined',
        joined_at timestamp with time zone,
        completed_at timestamp with time zone,
        metadata jsonb NOT NULL DEFAULT ('{}'::jsonb),
        CONSTRAINT "PK_event_participants" PRIMARY KEY (event_room_id, character_id),
        CONSTRAINT ck_ep_metadata CHECK (jsonb_typeof(metadata) = 'object'),
        CONSTRAINT ck_ep_status CHECK (status IN ('invited', 'joined', 'left', 'removed', 'completed')),
        CONSTRAINT "FK_event_participants_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_participants_event_rooms_event_room_id" FOREIGN KEY (event_room_id) REFERENCES game.event_rooms (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.event_posts (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        event_room_id uuid NOT NULL,
        character_id uuid NOT NULL,
        body_markdown text NOT NULL,
        status text NOT NULL DEFAULT 'draft',
        client_request_id character varying(80),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        submitted_at timestamp with time zone,
        reviewed_at timestamp with time zone,
        reviewed_by uuid,
        review_note character varying(1000),
        published_at timestamp with time zone,
        edited_at timestamp with time zone,
        moderated_by uuid,
        moderation_note character varying(500),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_event_posts" PRIMARY KEY (id),
        CONSTRAINT ck_epost_approved_published CHECK (status <> 'approved' OR (reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL AND published_at IS NOT NULL)),
        CONSTRAINT ck_epost_body_len CHECK (char_length(body_markdown) <= 10000),
        CONSTRAINT ck_epost_body_not_blank CHECK (status = 'draft' OR char_length(btrim(body_markdown)) > 0),
        CONSTRAINT ck_epost_status CHECK (status IN ('draft', 'submitted', 'under_review', 'approved', 'needs_revision', 'rejected', 'withdrawn', 'moderated')),
        CONSTRAINT ck_epost_submitted_at CHECK (status NOT IN ('submitted', 'under_review', 'approved', 'rejected', 'needs_revision') OR submitted_at IS NOT NULL),
        CONSTRAINT ck_epost_version CHECK (version > 0),
        CONSTRAINT "FK_event_posts_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_posts_event_rooms_event_room_id" FOREIGN KEY (event_room_id) REFERENCES game.event_rooms (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_posts_users_moderated_by" FOREIGN KEY (moderated_by) REFERENCES game.users (id) ON DELETE SET NULL,
        CONSTRAINT "FK_event_posts_users_reviewed_by" FOREIGN KEY (reviewed_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.event_results (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        event_room_id uuid NOT NULL,
        character_id uuid,
        outcome_code character varying(80) NOT NULL,
        public_summary character varying(2000) NOT NULL,
        private_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        rewards_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        rules_version character varying(40) NOT NULL,
        settled_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_event_results" PRIMARY KEY (id),
        CONSTRAINT ck_eres_private_payload CHECK (jsonb_typeof(private_payload) = 'object'),
        CONSTRAINT ck_eres_rewards_payload CHECK (jsonb_typeof(rewards_payload) = 'object'),
        CONSTRAINT "FK_event_results_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_results_event_rooms_event_room_id" FOREIGN KEY (event_room_id) REFERENCES game.event_rooms (id) ON DELETE CASCADE,
        CONSTRAINT "FK_event_results_users_settled_by" FOREIGN KEY (settled_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.external_play_submissions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        submitted_by_character_id uuid NOT NULL,
        source_type text NOT NULL DEFAULT 'line_group',
        occurred_at timestamp with time zone NOT NULL,
        summary character varying(4000) NOT NULL,
        evidence_urls jsonb NOT NULL DEFAULT ('[]'::jsonb),
        involved_character_ids jsonb NOT NULL DEFAULT ('[]'::jsonb),
        status text NOT NULL DEFAULT 'submitted',
        review_note character varying(1000),
        reviewed_by uuid,
        reviewed_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_external_play_submissions" PRIMARY KEY (id),
        CONSTRAINT ck_eps_evidence_urls CHECK (jsonb_typeof(evidence_urls) = 'array'),
        CONSTRAINT ck_eps_involved CHECK (jsonb_typeof(involved_character_ids) = 'array'),
        CONSTRAINT ck_eps_source_type CHECK (source_type IN ('line_group', 'other')),
        CONSTRAINT ck_eps_status CHECK (status IN ('submitted', 'under_review', 'approved', 'rejected', 'cancelled')),
        CONSTRAINT ck_eps_summary_len CHECK (char_length(btrim(summary)) BETWEEN 1 AND 4000),
        CONSTRAINT ck_eps_version CHECK (version > 0),
        CONSTRAINT "FK_external_play_submissions_characters_submitted_by_character~" FOREIGN KEY (submitted_by_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_external_play_submissions_users_reviewed_by" FOREIGN KEY (reviewed_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.heir_wait_pool_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        status text NOT NULL DEFAULT 'waiting',
        entered_at timestamp with time zone NOT NULL DEFAULT (now()),
        resolved_at timestamp with time zone,
        resolved_reason character varying(500),
        created_by uuid NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_heir_wait_pool_entries" PRIMARY KEY (id),
        CONSTRAINT ck_hwp_resolved_pair CHECK ((status = 'waiting' AND resolved_at IS NULL) OR (status <> 'waiting' AND resolved_at IS NOT NULL)),
        CONSTRAINT ck_hwp_status CHECK (status IN ('waiting', 'drawn', 'withdrawn', 'suspended')),
        CONSTRAINT ck_hwp_version CHECK (version > 0),
        CONSTRAINT "FK_heir_wait_pool_entries_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_heir_wait_pool_entries_users_created_by" FOREIGN KEY (created_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.intrigue_actions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        actor_character_id uuid NOT NULL,
        target_character_id uuid NOT NULL,
        action_type text NOT NULL,
        status text NOT NULL DEFAULT 'submitted',
        input_payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        secret_result jsonb NOT NULL DEFAULT ('{}'::jsonb),
        public_result jsonb NOT NULL DEFAULT ('{}'::jsonb),
        rules_version character varying(40) NOT NULL,
        idempotency_key character varying(100) NOT NULL,
        submitted_at timestamp with time zone NOT NULL DEFAULT (now()),
        resolve_after timestamp with time zone,
        resolved_at timestamp with time zone,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_intrigue_actions" PRIMARY KEY (id),
        CONSTRAINT ck_ia_action_type CHECK (action_type IN ('poison', 'investigate', 'countermeasure')),
        CONSTRAINT ck_ia_input_payload CHECK (jsonb_typeof(input_payload) = 'object'),
        CONSTRAINT ck_ia_not_self CHECK (actor_character_id <> target_character_id),
        CONSTRAINT ck_ia_public_result CHECK (jsonb_typeof(public_result) = 'object'),
        CONSTRAINT ck_ia_secret_result CHECK (jsonb_typeof(secret_result) = 'object'),
        CONSTRAINT ck_ia_status CHECK (status IN ('submitted', 'processing', 'resolved', 'failed', 'cancelled')),
        CONSTRAINT ck_ia_version CHECK (version > 0),
        CONSTRAINT "FK_intrigue_actions_characters_actor_character_id" FOREIGN KEY (actor_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_intrigue_actions_characters_target_character_id" FOREIGN KEY (target_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.inventory_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        item_definition_id uuid NOT NULL,
        quantity integer NOT NULL,
        expires_at timestamp with time zone,
        acquired_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_inventory_entries" PRIMARY KEY (id),
        CONSTRAINT ck_ie_quantity CHECK (quantity >= 0),
        CONSTRAINT ck_ie_version CHECK (version > 0),
        CONSTRAINT "FK_inventory_entries_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_inventory_entries_item_definitions_item_definition_id" FOREIGN KEY (item_definition_id) REFERENCES game.item_definitions (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.offspring_links (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        child_character_id uuid NOT NULL,
        parent_type text NOT NULL,
        parent_character_id uuid,
        parent_npc_code character varying(80),
        is_public boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_offspring_links" PRIMARY KEY (id),
        CONSTRAINT ck_ol_parent_type CHECK (parent_type IN ('mother', 'father')),
        CONSTRAINT ck_ol_parent_xor CHECK ((parent_character_id IS NOT NULL)::integer + (parent_npc_code IS NOT NULL)::integer = 1),
        CONSTRAINT "FK_offspring_links_characters_child_character_id" FOREIGN KEY (child_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_offspring_links_characters_parent_character_id" FOREIGN KEY (parent_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.pregnancies (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        mother_character_id uuid NOT NULL,
        audience_request_id uuid NOT NULL,
        status text NOT NULL DEFAULT 'ongoing',
        conceived_at timestamp with time zone NOT NULL,
        due_at timestamp with time zone NOT NULL,
        conception_rate_percent smallint NOT NULL,
        conception_roll smallint NOT NULL,
        slot_reserved_at timestamp with time zone NOT NULL,
        slot_released_at timestamp with time zone,
        rules_version character varying(40) NOT NULL,
        rules_snapshot jsonb NOT NULL,
        resolved_by uuid,
        resolution_code character varying(80),
        resolution_reason character varying(1000),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_pregnancies" PRIMARY KEY (id),
        CONSTRAINT ck_preg_due CHECK (due_at > conceived_at),
        CONSTRAINT ck_preg_miscarriage_reason CHECK (status <> 'miscarried' OR (resolution_code IS NOT NULL AND char_length(btrim(resolution_reason)) >= 5)),
        CONSTRAINT ck_preg_rate CHECK (conception_rate_percent BETWEEN 0 AND 100),
        CONSTRAINT ck_preg_roll CHECK (conception_roll BETWEEN 1 AND 100),
        CONSTRAINT ck_preg_rules_snapshot CHECK (jsonb_typeof(rules_snapshot) = 'object'),
        CONSTRAINT ck_preg_slot_release CHECK ((status = 'ongoing' AND slot_released_at IS NULL) OR (status <> 'ongoing' AND slot_released_at IS NOT NULL)),
        CONSTRAINT ck_preg_slot_reserved CHECK (slot_reserved_at >= conceived_at),
        CONSTRAINT ck_preg_status CHECK (status IN ('ongoing', 'miscarried', 'completed', 'cancelled')),
        CONSTRAINT ck_preg_version CHECK (version > 0),
        CONSTRAINT "FK_pregnancies_audience_requests_audience_request_id" FOREIGN KEY (audience_request_id) REFERENCES game.audience_requests (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_pregnancies_characters_mother_character_id" FOREIGN KEY (mother_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_pregnancies_users_resolved_by" FOREIGN KEY (resolved_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.purchases (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        market_offer_id uuid NOT NULL,
        quantity integer NOT NULL,
        unit_price bigint NOT NULL,
        total_price bigint NOT NULL,
        currency_code character varying(30) NOT NULL,
        ledger_transaction_id uuid NOT NULL,
        idempotency_key character varying(100) NOT NULL,
        purchased_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_purchases" PRIMARY KEY (id),
        CONSTRAINT ck_pur_quantity CHECK (quantity > 0),
        CONSTRAINT ck_pur_total_matches CHECK (total_price = unit_price * quantity),
        CONSTRAINT ck_pur_total_price CHECK (total_price >= 0),
        CONSTRAINT ck_pur_unit_price CHECK (unit_price >= 0),
        CONSTRAINT "FK_purchases_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_purchases_currencies_currency_code" FOREIGN KEY (currency_code) REFERENCES game.currencies (code) ON DELETE RESTRICT,
        CONSTRAINT "FK_purchases_ledger_transactions_ledger_transaction_id" FOREIGN KEY (ledger_transaction_id) REFERENCES game.ledger_transactions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_purchases_market_offers_market_offer_id" FOREIGN KEY (market_offer_id) REFERENCES game.market_offers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.rank_history (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        from_rank_id uuid,
        to_rank_id uuid NOT NULL,
        reason_code character varying(80) NOT NULL,
        reason_text character varying(1000),
        changed_by uuid,
        effective_at timestamp with time zone NOT NULL DEFAULT (now()),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_rank_history" PRIMARY KEY (id),
        CONSTRAINT "FK_rank_history_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_rank_history_ranks_from_rank_id" FOREIGN KEY (from_rank_id) REFERENCES game.ranks (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_rank_history_ranks_to_rank_id" FOREIGN KEY (to_rank_id) REFERENCES game.ranks (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_rank_history_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.status_effects (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        effect_code character varying(80) NOT NULL,
        visibility text NOT NULL DEFAULT 'private',
        severity smallint NOT NULL DEFAULT 1,
        payload jsonb NOT NULL DEFAULT ('{}'::jsonb),
        starts_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone,
        resolved_at timestamp with time zone,
        source_type character varying(60),
        source_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_status_effects" PRIMARY KEY (id),
        CONSTRAINT ck_se_expiry CHECK (expires_at IS NULL OR expires_at > starts_at),
        CONSTRAINT ck_se_payload CHECK (jsonb_typeof(payload) = 'object'),
        CONSTRAINT ck_se_resolved CHECK (resolved_at IS NULL OR resolved_at >= starts_at),
        CONSTRAINT ck_se_severity CHECK (severity BETWEEN 1 AND 10),
        CONSTRAINT ck_se_visibility CHECK (visibility IN ('private', 'public', 'admin_only')),
        CONSTRAINT "FK_status_effects_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.wallets (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        character_id uuid NOT NULL,
        currency_code character varying(30) NOT NULL,
        balance bigint NOT NULL DEFAULT 0,
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        version bigint NOT NULL DEFAULT 1,
        CONSTRAINT "PK_wallets" PRIMARY KEY (id),
        CONSTRAINT ck_wallets_balance CHECK (balance >= 0),
        CONSTRAINT ck_wallets_version CHECK (version > 0),
        CONSTRAINT "FK_wallets_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_wallets_currencies_currency_code" FOREIGN KEY (currency_code) REFERENCES game.currencies (code) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.event_post_revisions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        event_post_id uuid NOT NULL,
        revision_no integer NOT NULL,
        body_markdown text NOT NULL,
        revision_kind text NOT NULL DEFAULT 'draft_save',
        changed_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_event_post_revisions" PRIMARY KEY (id),
        CONSTRAINT ck_epr_revision_kind CHECK (revision_kind IN ('draft_save', 'submit', 'revision_request', 'approval', 'moderation')),
        CONSTRAINT ck_epr_revision_no CHECK (revision_no > 0),
        CONSTRAINT "FK_event_post_revisions_event_posts_event_post_id" FOREIGN KEY (event_post_id) REFERENCES game.event_posts (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_event_post_revisions_users_changed_by" FOREIGN KEY (changed_by) REFERENCES game.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.inventory_transactions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        inventory_entry_id uuid NOT NULL,
        transaction_type text NOT NULL,
        quantity_delta integer NOT NULL,
        quantity_after integer NOT NULL,
        effect_snapshot jsonb NOT NULL DEFAULT ('{}'::jsonb),
        reference_type character varying(60),
        reference_id uuid,
        initiated_by uuid,
        reason_code character varying(80),
        reason_text character varying(1000),
        request_id character varying(80),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_inventory_transactions" PRIMARY KEY (id),
        CONSTRAINT ck_it_after CHECK (quantity_after >= 0),
        CONSTRAINT ck_it_delta CHECK (quantity_delta <> 0),
        CONSTRAINT ck_it_effect_snapshot CHECK (jsonb_typeof(effect_snapshot) = 'object'),
        CONSTRAINT ck_it_type CHECK (transaction_type IN ('purchase', 'reward', 'use', 'expire', 'admin_grant', 'admin_correction', 'refund')),
        CONSTRAINT "FK_inventory_transactions_inventory_entries_inventory_entry_id" FOREIGN KEY (inventory_entry_id) REFERENCES game.inventory_entries (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_inventory_transactions_users_initiated_by" FOREIGN KEY (initiated_by) REFERENCES game.users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE TABLE game.ledger_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        transaction_id uuid NOT NULL,
        wallet_id uuid NOT NULL,
        amount bigint NOT NULL,
        balance_after bigint NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_ledger_entries" PRIMARY KEY (id),
        CONSTRAINT ck_le_amount CHECK (amount <> 0),
        CONSTRAINT ck_le_balance_after CHECK (balance_after >= 0),
        CONSTRAINT "FK_ledger_entries_ledger_transactions_transaction_id" FOREIGN KEY (transaction_id) REFERENCES game.ledger_transactions (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_ledger_entries_wallets_wallet_id" FOREIGN KEY (wallet_id) REFERENCES game.wallets (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_ability_label_definitions_ability_code_display_label" ON game.ability_label_definitions (ability_code, display_label);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_admin_role_assignments_granted_by" ON game.admin_role_assignments (granted_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_announcements_active ON game.announcements (starts_at, ends_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_announcements_published_by" ON game.announcements (published_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_approval_decisions_approval_request_id_reviewer_id" ON game.approval_decisions (approval_request_id, reviewer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_approval_decisions_reviewer_id" ON game.approval_decisions (reviewer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_approval_requests_pending ON game.approval_requests (requested_at) WHERE status = 'pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_approval_requests_requested_by" ON game.approval_requests (requested_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_audience_requests_character ON game.audience_requests (character_id, requested_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_audience_requests_character_id_idempotency_key" ON game.audience_requests (character_id, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_audit_logs_actor ON game.audit_logs (actor_user_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_audit_logs_target ON game.audit_logs (target_type, target_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_births_child_character_id" ON game.births (child_character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_births_drawn_by" ON game.births (drawn_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_births_pregnancy_id" ON game.births (pregnancy_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_births_wait_pool_entry_id" ON game.births (wait_pool_entry_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_character_application_revisions_application_id_revision_no" ON game.character_application_revisions (application_id, revision_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_application_revisions_changed_by" ON game.character_application_revisions (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_applications_created_character_id" ON game.character_applications (created_character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_applications_player_portrait_submission_id" ON game.character_applications (player_portrait_submission_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_applications_portrait_id" ON game.character_applications (portrait_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_character_applications_review_queue ON game.character_applications (status, submitted_at) WHERE status IN ('submitted', 'needs_revision');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_applications_reviewed_by" ON game.character_applications (reviewed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_character_applications_one_open_per_user ON game.character_applications (user_id) WHERE status IN ('draft', 'submitted', 'needs_revision');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_character_chronicle_character ON game.character_chronicle_entries (character_id, happened_at, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_chronicle_entries_created_by" ON game.character_chronicle_entries (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_chronicle_entries_location_id" ON game.character_chronicle_entries (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_character_chronicle_source ON game.character_chronicle_entries (source_type, source_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_residence_history_changed_by" ON game.character_residence_history (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_residence_history_residence_id" ON game.character_residence_history (residence_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_character_residence_current ON game.character_residence_history (character_id) WHERE moved_out_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_status_history_changed_by" ON game.character_status_history (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_character_status_history_character ON game.character_status_history (character_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_title_assignments_granted_by" ON game.character_title_assignments (granted_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_title_assignments_revoked_by" ON game.character_title_assignments (revoked_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_title_assignments_title_definition_id" ON game.character_title_assignments (title_definition_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_character_title_assignments_active ON game.character_title_assignments (character_id, title_definition_id) WHERE revoked_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_character_title_assignments_one_primary ON game.character_title_assignments (character_id) WHERE revoked_at IS NULL AND is_primary = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_character_title_definitions_code" ON game.character_title_definitions (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_character_title_definitions_created_by" ON game.character_title_definitions (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_characters_player_portrait_submission_id" ON game.characters (player_portrait_submission_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_characters_portrait_id" ON game.characters (portrait_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_characters_public_name ON game.characters (family_name, given_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_characters_rank_id" ON game.characters (rank_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_characters_residence_id" ON game.characters (residence_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_characters_source_application_id" ON game.characters (source_application_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_characters_status_role ON game.characters (status, role);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_characters_one_current_per_user ON game.characters (user_id) WHERE status IN ('waiting_birth', 'active', 'paused', 'suspended');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_deaths_approval_request_id" ON game.deaths (approval_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_deaths_character_id" ON game.deaths (character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_deaths_ruled_by" ON game.deaths (ruled_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_event_participants_character ON game.event_participants (character_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_post_revisions_changed_by" ON game.event_post_revisions (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_event_post_revisions_event_post_id_revision_no" ON game.event_post_revisions (event_post_id, revision_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_posts_character_id" ON game.event_posts (character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_event_posts_event_room_id_character_id_client_request_id" ON game.event_posts (event_room_id, character_id, client_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_posts_moderated_by" ON game.event_posts (moderated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_event_posts_review_queue ON game.event_posts (status, submitted_at) WHERE status IN ('submitted', 'under_review');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_posts_reviewed_by" ON game.event_posts (reviewed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_event_posts_room_feed ON game.event_posts (event_room_id, published_at, id) WHERE status = 'approved';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_results_character_id" ON game.event_results (character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_event_results_event_room_id_character_id" ON game.event_results (event_room_id, character_id) NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_results_settled_by" ON game.event_results (settled_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_event_rooms_code" ON game.event_rooms (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_rooms_created_by" ON game.event_rooms (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_event_rooms_location_id" ON game.event_rooms (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_event_rooms_player_list ON game.event_rooms (status, opens_at, deadline_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_external_play_review_queue ON game.external_play_submissions (status, created_at) WHERE status IN ('submitted', 'under_review');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_external_play_submissions_reviewed_by" ON game.external_play_submissions (reviewed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_external_play_submissions_submitted_by_character_id" ON game.external_play_submissions (submitted_by_character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_game_setting_revisions_approval_request_id" ON game.game_setting_revisions (approval_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_game_setting_revisions_changed_by" ON game.game_setting_revisions (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_game_setting_revisions_setting_key_revision_no" ON game.game_setting_revisions (setting_key, revision_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_game_settings_published_by" ON game.game_settings (published_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_game_settings_updated_by" ON game.game_settings (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_heir_wait_pool_draw_candidates ON game.heir_wait_pool_entries (entered_at, id) WHERE status = 'waiting';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_heir_wait_pool_entries_created_by" ON game.heir_wait_pool_entries (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_heir_wait_pool_one_waiting_per_character ON game.heir_wait_pool_entries (character_id) WHERE status = 'waiting';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_idempotency_records_expiry ON game.idempotency_records (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_idempotency_records_user_id_http_method_request_path_idempo~" ON game.idempotency_records (user_id, http_method, request_path, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_intrigue_actions_actor_character_id_idempotency_key" ON game.intrigue_actions (actor_character_id, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_intrigue_actions_pending ON game.intrigue_actions (resolve_after) WHERE status IN ('submitted', 'processing');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_intrigue_actions_target_character_id" ON game.intrigue_actions (target_character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_inventory_entries_character_available ON game.inventory_entries (character_id, item_definition_id) WHERE quantity > 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_inventory_entries_character_id_item_definition_id_expires_at" ON game.inventory_entries (character_id, item_definition_id, expires_at) NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_inventory_entries_item_definition_id" ON game.inventory_entries (item_definition_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_inventory_transactions_entry ON game.inventory_transactions (inventory_entry_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_inventory_transactions_initiated_by" ON game.inventory_transactions (initiated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_item_definitions_code_version_no" ON game.item_definitions (code, version_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_job_runs_job ON game.job_runs (scheduled_job_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_ledger_entries_transaction_id" ON game.ledger_entries (transaction_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_ledger_entries_wallet ON game.ledger_entries (wallet_id, created_at, id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_ledger_transactions_initiated_by" ON game.ledger_transactions (initiated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_line_login_attempts_state_hash" ON game.line_login_attempts (state_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_market_offers_active_window ON game.market_offers (is_active, starts_at, ends_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_market_offers_created_by" ON game.market_offers (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_market_offers_currency_code" ON game.market_offers (currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_market_offers_item_definition_id" ON game.market_offers (item_definition_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_media_assets_owner_created ON game.media_assets (owner_user_id, created_at) WHERE status <> 'deleted';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_media_assets_storage_key" ON game.media_assets (storage_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_notifications_user_unread ON game.notifications (user_id, created_at) WHERE read_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_npc_revisions_changed_by" ON game.npc_revisions (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_npc_revisions_npc_id_revision_no" ON game.npc_revisions (npc_id, revision_no);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_npcs_code" ON game.npcs (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_npcs_created_by" ON game.npcs (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_npcs_portrait_asset_id" ON game.npcs (portrait_asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_npcs_primary_location_id" ON game.npcs (primary_location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_npcs_published_by" ON game.npcs (published_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_offspring_links_child_character_id_parent_type_parent_chara~" ON game.offspring_links (child_character_id, parent_type, parent_character_id, parent_npc_code) NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_offspring_links_parent_character_id" ON game.offspring_links (parent_character_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_outbox_messages_pending ON game.outbox_messages (available_at, occurred_at) WHERE processed_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_player_portrait_submissions_media_asset_id" ON game.player_portrait_submissions (media_asset_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_player_portrait_submissions_review_queue ON game.player_portrait_submissions (status, created_at) WHERE status = 'pending';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_player_portrait_submissions_reviewed_by" ON game.player_portrait_submissions (reviewed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_player_portrait_submissions_user_id" ON game.player_portrait_submissions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_pregnancies_audience_request_id" ON game.pregnancies (audience_request_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_pregnancies_due ON game.pregnancies (due_at) WHERE status = 'ongoing';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_pregnancies_resolved_by" ON game.pregnancies (resolved_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX ux_pregnancies_one_ongoing_per_mother ON game.pregnancies (mother_character_id) WHERE status = 'ongoing';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_preset_portraits_code" ON game.preset_portraits (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_purchases_character_id_idempotency_key" ON game.purchases (character_id, idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_purchases_currency_code" ON game.purchases (currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_purchases_ledger_transaction_id" ON game.purchases (ledger_transaction_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_purchases_market_offer_id" ON game.purchases (market_offer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_rank_history_changed_by" ON game.rank_history (changed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_rank_history_character ON game.rank_history (character_id, effective_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_rank_history_from_rank_id" ON game.rank_history (from_rank_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_rank_history_to_rank_id" ON game.rank_history (to_rank_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_ranks_applies_to_role_display_name" ON game.ranks (applies_to_role, display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_ranks_code" ON game.ranks (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_ranks_role_grade ON game.ranks (applies_to_role, ordinal, display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_reproduction_control_updated_by" ON game.reproduction_control (updated_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_residences_code" ON game.residences (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_scheduled_jobs_due ON game.scheduled_jobs (next_run_at) WHERE is_enabled = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_scheduled_jobs_job_key" ON game.scheduled_jobs (job_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_status_effects_active ON game.status_effects (character_id, effect_code) WHERE resolved_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX ix_user_sessions_active_user ON game.user_sessions (user_id, absolute_expires_at) WHERE revoked_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_user_sessions_token_hash" ON game.user_sessions (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_users_line_user_id" ON game.users (line_user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_wallets_character_id_currency_code" ON game.wallets (character_id, currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE INDEX "IX_wallets_currency_code" ON game.wallets (currency_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    CREATE UNIQUE INDEX "IX_world_locations_code" ON game.world_locations (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.audience_requests ADD CONSTRAINT "FK_audience_requests_characters_character_id" FOREIGN KEY (character_id) REFERENCES game.characters (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.births ADD CONSTRAINT "FK_births_characters_child_character_id" FOREIGN KEY (child_character_id) REFERENCES game.characters (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.births ADD CONSTRAINT "FK_births_heir_wait_pool_entries_wait_pool_entry_id" FOREIGN KEY (wait_pool_entry_id) REFERENCES game.heir_wait_pool_entries (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.births ADD CONSTRAINT "FK_births_pregnancies_pregnancy_id" FOREIGN KEY (pregnancy_id) REFERENCES game.pregnancies (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.character_application_revisions ADD CONSTRAINT "FK_character_application_revisions_character_applications_appl~" FOREIGN KEY (application_id) REFERENCES game.character_applications (id) ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    ALTER TABLE game.character_applications ADD CONSTRAINT fk_character_applications_created_character FOREIGN KEY (created_character_id) REFERENCES game.characters (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM game.__ef_migrations_history WHERE "MigrationId" = '20260816182807_InitialSchemaV11') THEN
    INSERT INTO game.__ef_migrations_history ("MigrationId", "ProductVersion")
    VALUES ('20260816182807_InitialSchemaV11', '10.0.4');
    END IF;
END $EF$;
COMMIT;

