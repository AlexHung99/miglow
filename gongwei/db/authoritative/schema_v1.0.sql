-- 宮闈浮生 PostgreSQL schema v1.0
-- Target: PostgreSQL 17/18
-- v1.0 keeps the validated 60-table v0.9 structure; the contract cleanup adds no table.
-- Application timestamps are UTC timestamptz. IDs may be supplied as UUIDv7 by the API;
-- gen_random_uuid() remains the database fallback.

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS game;

CREATE OR REPLACE FUNCTION game.touch_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = now();
    NEW.version = OLD.version + 1;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION game.reject_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION '% is append-only', TG_TABLE_NAME USING ERRCODE = '55000';
END;
$$;

CREATE OR REPLACE FUNCTION game.reject_deletion()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION '% does not allow deletion', TG_TABLE_NAME USING ERRCODE = '55000';
END;
$$;

-- ============================================================
-- Identity, session, admin RBAC
-- ============================================================

CREATE TABLE game.users (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    line_user_id        text NOT NULL UNIQUE,
    display_name        varchar(80) NOT NULL,
    avatar_url          text,
    locale              varchar(16) NOT NULL DEFAULT 'zh-TW',
    status              varchar(20) NOT NULL DEFAULT 'active'
                        CHECK (status IN ('active', 'suspended', 'deleted')),
    terms_accepted_at   timestamptz,
    privacy_accepted_at timestamptz,
    last_login_at       timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (char_length(btrim(line_user_id)) BETWEEN 1 AND 255),
    CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 80)
);

CREATE TABLE game.user_sessions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid NOT NULL REFERENCES game.users(id) ON DELETE CASCADE,
    token_hash          bytea NOT NULL UNIQUE,
    csrf_secret_hash    bytea NOT NULL,
    ip_address          inet,
    user_agent          varchar(512),
    created_at          timestamptz NOT NULL DEFAULT now(),
    last_seen_at        timestamptz NOT NULL DEFAULT now(),
    idle_expires_at     timestamptz NOT NULL,
    absolute_expires_at timestamptz NOT NULL,
    revoked_at          timestamptz,
    revoke_reason       varchar(200),
    CHECK (idle_expires_at <= absolute_expires_at),
    CHECK (absolute_expires_at > created_at)
);

CREATE INDEX ix_user_sessions_active_user
    ON game.user_sessions(user_id, absolute_expires_at DESC)
    WHERE revoked_at IS NULL;

CREATE TABLE game.admin_role_assignments (
    user_id       uuid NOT NULL REFERENCES game.users(id) ON DELETE CASCADE,
    role          varchar(40) NOT NULL
                  CHECK (role IN ('super_admin', 'character_reviewer', 'game_master',
                                  'economy_manager', 'moderator', 'auditor',
                                  'content_editor', 'character_manager',
                                  'system_config_manager')),
    granted_by    uuid REFERENCES game.users(id) ON DELETE SET NULL,
    granted_at    timestamptz NOT NULL DEFAULT now(),
    expires_at    timestamptz,
    PRIMARY KEY (user_id, role),
    CHECK (expires_at IS NULL OR expires_at > granted_at)
);

-- ============================================================
-- Character application and character master data
-- ============================================================

CREATE TABLE game.preset_portraits (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(80) NOT NULL UNIQUE,
    role            varchar(20) NOT NULL CHECK (role IN ('consort', 'prince', 'princess')),
    display_name    varchar(80) NOT NULL,
    asset_url       text NOT NULL,
    thumbnail_url   text,
    sort_order      integer NOT NULL DEFAULT 0,
    is_active       boolean NOT NULL DEFAULT true,
    metadata        jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

-- Uploaded files live on a persistent media volume or S3-compatible object storage.
-- PostgreSQL stores only ownership, integrity, dimensions and moderation metadata.
CREATE TABLE game.media_assets (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_user_id       uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    storage_key         text NOT NULL UNIQUE,
    original_file_name  varchar(255) NOT NULL,
    original_mime_type  varchar(100) NOT NULL
                        CHECK (original_mime_type IN ('image/jpeg', 'image/png', 'image/webp')),
    stored_mime_type    varchar(30) CHECK (stored_mime_type IN ('image/webp', 'image/jpeg')),
    byte_size           bigint NOT NULL CHECK (byte_size BETWEEN 1 AND 8388608),
    width               integer NOT NULL CHECK (width >= 600),
    height              integer NOT NULL CHECK (height >= 800),
    sha256              char(64) NOT NULL,
    status              varchar(20) NOT NULL DEFAULT 'uploaded'
                        CHECK (status IN ('uploaded', 'processing', 'ready', 'quarantined', 'deleted')),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (char_length(btrim(storage_key)) BETWEEN 1 AND 1024)
);

CREATE INDEX ix_media_assets_owner_created
    ON game.media_assets(owner_user_id, created_at DESC)
    WHERE status <> 'deleted';

CREATE TABLE game.player_portrait_submissions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    media_asset_id      uuid NOT NULL UNIQUE REFERENCES game.media_assets(id) ON DELETE RESTRICT,
    role                varchar(20) NOT NULL CHECK (role IN ('consort', 'prince', 'princess')),
    crop_x              numeric(6,5) NOT NULL DEFAULT 0 CHECK (crop_x BETWEEN 0 AND 1),
    crop_y              numeric(6,5) NOT NULL DEFAULT 0 CHECK (crop_y BETWEEN 0 AND 1),
    crop_width          numeric(6,5) NOT NULL DEFAULT 1 CHECK (crop_width > 0 AND crop_width <= 1),
    crop_height         numeric(6,5) NOT NULL DEFAULT 1 CHECK (crop_height > 0 AND crop_height <= 1),
    status              varchar(20) NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'approved', 'rejected', 'withdrawn')),
    reviewed_by         uuid REFERENCES game.users(id) ON DELETE SET NULL,
    reviewed_at         timestamptz,
    review_note         varchar(1000),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (crop_x + crop_width <= 1.00001),
    CHECK (crop_y + crop_height <= 1.00001),
    CHECK ((status IN ('approved', 'rejected') AND reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)
           OR status IN ('pending', 'withdrawn'))
);

CREATE INDEX ix_player_portrait_submissions_review_queue
    ON game.player_portrait_submissions(status, created_at)
    WHERE status = 'pending';

CREATE TABLE game.character_applications (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    role                varchar(20) NOT NULL CHECK (role IN ('consort', 'prince', 'princess')),
    sex                 varchar(10) NOT NULL CHECK (sex IN ('female', 'male')),
    family_name         varchar(20) NOT NULL DEFAULT '',
    given_name          varchar(30) NOT NULL DEFAULT '',
    courtesy_name       varchar(30),
    birth_date_label    varchar(30),
    age                 smallint,
    appearance          varchar(3000) NOT NULL DEFAULT '',
    biography           varchar(2000) NOT NULL DEFAULT '',
    personality         varchar(1000) NOT NULL DEFAULT '',
    strengths           varchar(1000) NOT NULL DEFAULT '',
    weaknesses          varchar(1000) NOT NULL DEFAULT '',
    likes               varchar(1000) NOT NULL DEFAULT '',
    dislikes            varchar(1000) NOT NULL DEFAULT '',
    portrait_id         uuid REFERENCES game.preset_portraits(id) ON DELETE RESTRICT,
    player_portrait_submission_id uuid REFERENCES game.player_portrait_submissions(id) ON DELETE RESTRICT,
    status              varchar(30) NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'submitted', 'needs_revision',
                                         'approved', 'rejected', 'cancelled')),
    form_data           jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(form_data) = 'object'),
    submitted_at        timestamptz,
    reviewed_at         timestamptz,
    reviewed_by         uuid REFERENCES game.users(id) ON DELETE SET NULL,
    review_note         varchar(2000),
    created_character_id uuid,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((role = 'prince' AND sex = 'male') OR
           (role IN ('consort', 'princess') AND sex = 'female')),
    CHECK (status = 'draft' OR char_length(btrim(given_name)) BETWEEN 1 AND 30),
    CHECK (status = 'draft' OR ((portrait_id IS NOT NULL)::integer +
           (player_portrait_submission_id IS NOT NULL)::integer = 1)),
    CHECK (status = 'draft' OR char_length(appearance) >= 60),
    CHECK (status = 'draft' OR char_length(personality) >= 50),
    CHECK (status = 'draft' OR char_length(strengths) >= 50),
    CHECK (status = 'draft' OR char_length(weaknesses) >= 50),
    CHECK (status = 'draft' OR char_length(likes) >= 50),
    CHECK (status = 'draft' OR char_length(dislikes) >= 50),
    CHECK (status = 'draft' OR char_length(biography) >= 200),
    CHECK (status = 'draft' OR
           (role = 'consort' AND age BETWEEN 15 AND 18 AND char_length(btrim(family_name)) > 0) OR
           (role IN ('prince', 'princess') AND age = 0 AND family_name = '蕭')),
    CHECK ((status = 'draft' AND submitted_at IS NULL) OR status <> 'draft'),
    CHECK ((status = 'approved' AND reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL) OR
           status <> 'approved')
);

CREATE UNIQUE INDEX ux_character_applications_one_open_per_user
    ON game.character_applications(user_id)
    WHERE status IN ('draft', 'submitted', 'needs_revision');

CREATE INDEX ix_character_applications_review_queue
    ON game.character_applications(status, submitted_at)
    WHERE status IN ('submitted', 'needs_revision');

CREATE TABLE game.character_application_revisions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id  uuid NOT NULL REFERENCES game.character_applications(id) ON DELETE CASCADE,
    revision_no     integer NOT NULL CHECK (revision_no > 0),
    snapshot        jsonb NOT NULL CHECK (jsonb_typeof(snapshot) = 'object'),
    changed_by      uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    change_reason   varchar(500),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (application_id, revision_no)
);

CREATE TABLE game.ranks (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code              varchar(50) NOT NULL UNIQUE,
    display_name      varchar(80) NOT NULL,
    applies_to_role   varchar(20) NOT NULL CHECK (applies_to_role IN ('consort', 'prince', 'princess')),
    grade_code        varchar(20) NOT NULL,
    ordinal           integer NOT NULL CHECK (ordinal >= 0),
    prestige_required bigint NOT NULL DEFAULT 0 CHECK (prestige_required >= 0),
    monthly_stipend   bigint NOT NULL DEFAULT 0 CHECK (monthly_stipend >= 0),
    source_annual_stipend bigint NOT NULL DEFAULT 0 CHECK (source_annual_stipend >= 0),
    capacity          integer CHECK (capacity IS NULL OR capacity > 0),
    is_lead           boolean NOT NULL DEFAULT false,
    is_application_option boolean NOT NULL DEFAULT false,
    initial_stats     jsonb CHECK (initial_stats IS NULL OR jsonb_typeof(initial_stats) = 'object'),
    promotion_rules   jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(promotion_rules) = 'object'),
    is_active         boolean NOT NULL DEFAULT true,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    version           bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (applies_to_role, display_name)
);

CREATE INDEX ix_ranks_role_grade ON game.ranks(applies_to_role, ordinal, display_name);

CREATE TABLE game.character_title_definitions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code                varchar(80) NOT NULL UNIQUE,
    display_name        varchar(100) NOT NULL,
    description         varchar(1000) NOT NULL DEFAULT '',
    category            varchar(30) NOT NULL
                        CHECK (category IN ('rank', 'achievement', 'story', 'honorary', 'secret')),
    applies_to_role     varchar(20) CHECK (applies_to_role IN ('consort', 'prince', 'princess')),
    visibility          varchar(20) NOT NULL DEFAULT 'public'
                        CHECK (visibility IN ('public', 'owner_only', 'admin_only')),
    style_token         varchar(50),
    sort_order          integer NOT NULL DEFAULT 0,
    is_active           boolean NOT NULL DEFAULT true,
    created_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 100)
);

CREATE TABLE game.residences (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(50) NOT NULL UNIQUE,
    display_name    varchar(80) NOT NULL,
    description     varchar(1000) NOT NULL DEFAULT '',
    map_x           numeric(5,2) CHECK (map_x BETWEEN 0 AND 100),
    map_y           numeric(5,2) CHECK (map_y BETWEEN 0 AND 100),
    capacity        integer CHECK (capacity IS NULL OR capacity > 0),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE TABLE game.characters (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    source_application_id uuid NOT NULL UNIQUE REFERENCES game.character_applications(id) ON DELETE RESTRICT,
    role                varchar(20) NOT NULL CHECK (role IN ('consort', 'prince', 'princess')),
    sex                 varchar(10) NOT NULL CHECK (sex IN ('female', 'male')),
    family_name         varchar(20),
    given_name          varchar(30) NOT NULL,
    courtesy_name       varchar(30),
    birth_date_label    varchar(30),
    age_at_creation     smallint NOT NULL,
    appearance          varchar(3000) NOT NULL,
    biography           varchar(2000) NOT NULL DEFAULT '',
    personality         varchar(1000) NOT NULL DEFAULT '',
    strengths           varchar(1000) NOT NULL,
    weaknesses          varchar(1000) NOT NULL,
    likes               varchar(1000) NOT NULL,
    dislikes            varchar(1000) NOT NULL,
    portrait_id         uuid REFERENCES game.preset_portraits(id) ON DELETE RESTRICT,
    player_portrait_submission_id uuid REFERENCES game.player_portrait_submissions(id) ON DELETE RESTRICT,
    rank_id             uuid REFERENCES game.ranks(id) ON DELETE RESTRICT,
    residence_id        uuid REFERENCES game.residences(id) ON DELETE SET NULL,
    status              varchar(30) NOT NULL
                        CHECK (status IN ('waiting_birth', 'active', 'paused',
                                         'dead', 'suspended', 'archived')),
    pause_reason        varchar(500),
    activated_at        timestamptz,
    died_at             timestamptz,
    archived_at         timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((role = 'prince' AND sex = 'male') OR
           (role IN ('consort', 'princess') AND sex = 'female')),
    CHECK ((portrait_id IS NOT NULL)::integer +
           (player_portrait_submission_id IS NOT NULL)::integer = 1),
    CHECK ((status = 'waiting_birth' AND role IN ('prince', 'princess')) OR status <> 'waiting_birth'),
    CHECK ((status = 'dead' AND died_at IS NOT NULL) OR status <> 'dead'),
    CHECK ((status = 'archived' AND archived_at IS NOT NULL) OR status <> 'archived')
);

ALTER TABLE game.character_applications
    ADD CONSTRAINT fk_character_applications_created_character
    FOREIGN KEY (created_character_id) REFERENCES game.characters(id) ON DELETE SET NULL;

-- One LINE user may retain dead/archive history, but only one current playable/waiting character.
CREATE UNIQUE INDEX ux_characters_one_current_per_user
    ON game.characters(user_id)
    WHERE status IN ('waiting_birth', 'active', 'paused', 'suspended');

CREATE INDEX ix_characters_status_role ON game.characters(status, role);
CREATE INDEX ix_characters_public_name ON game.characters(family_name, given_name);

CREATE TABLE game.character_title_assignments (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id        uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    title_definition_id uuid NOT NULL REFERENCES game.character_title_definitions(id) ON DELETE RESTRICT,
    is_primary          boolean NOT NULL DEFAULT false,
    granted_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    granted_at          timestamptz NOT NULL DEFAULT now(),
    grant_reason        varchar(1000) NOT NULL,
    revoked_by          uuid REFERENCES game.users(id) ON DELETE SET NULL,
    revoked_at          timestamptz,
    revoke_reason       varchar(1000),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((revoked_at IS NULL AND revoked_by IS NULL AND revoke_reason IS NULL) OR
           (revoked_at IS NOT NULL AND revoked_by IS NOT NULL AND revoke_reason IS NOT NULL))
);

CREATE UNIQUE INDEX ux_character_title_assignments_active
    ON game.character_title_assignments(character_id, title_definition_id)
    WHERE revoked_at IS NULL;

CREATE UNIQUE INDEX ux_character_title_assignments_one_primary
    ON game.character_title_assignments(character_id)
    WHERE revoked_at IS NULL AND is_primary = true;

CREATE TABLE game.character_stats (
    character_id    uuid PRIMARY KEY REFERENCES game.characters(id) ON DELETE CASCADE,
    vitality        smallint NOT NULL DEFAULT 0 CHECK (vitality BETWEEN 0 AND 1000),
    appearance      smallint NOT NULL DEFAULT 0 CHECK (appearance BETWEEN 0 AND 1000),
    strategy        smallint NOT NULL DEFAULT 0 CHECK (strategy BETWEEN 0 AND 1000),
    luck            smallint NOT NULL DEFAULT 0 CHECK (luck BETWEEN 0 AND 1000),
    prestige        bigint NOT NULL DEFAULT 0 CHECK (prestige >= 0),
    favor           integer NOT NULL DEFAULT 0 CHECK (favor BETWEEN -1000 AND 1000),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE TABLE game.character_status_history (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    from_status     varchar(30),
    to_status       varchar(30) NOT NULL,
    reason_code     varchar(80) NOT NULL,
    reason_text     varchar(1000),
    changed_by      uuid REFERENCES game.users(id) ON DELETE SET NULL,
    request_id      varchar(80),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_character_status_history_character
    ON game.character_status_history(character_id, created_at DESC);

CREATE TABLE game.rank_history (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    from_rank_id    uuid REFERENCES game.ranks(id) ON DELETE RESTRICT,
    to_rank_id      uuid NOT NULL REFERENCES game.ranks(id) ON DELETE RESTRICT,
    reason_code     varchar(80) NOT NULL,
    reason_text     varchar(1000),
    changed_by      uuid REFERENCES game.users(id) ON DELETE SET NULL,
    effective_at    timestamptz NOT NULL DEFAULT now(),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_rank_history_character ON game.rank_history(character_id, effective_at DESC);

CREATE TABLE game.character_residence_history (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    residence_id    uuid NOT NULL REFERENCES game.residences(id) ON DELETE RESTRICT,
    moved_in_at     timestamptz NOT NULL,
    moved_out_at    timestamptz,
    reason          varchar(500),
    changed_by      uuid REFERENCES game.users(id) ON DELETE SET NULL,
    CHECK (moved_out_at IS NULL OR moved_out_at >= moved_in_at)
);

CREATE UNIQUE INDEX ux_character_residence_current
    ON game.character_residence_history(character_id)
    WHERE moved_out_at IS NULL;

-- ============================================================
-- World and content
-- ============================================================

CREATE TABLE game.world_state (
    singleton_id        smallint PRIMARY KEY DEFAULT 1 CHECK (singleton_id = 1),
    chapter_code        varchar(50) NOT NULL,
    display_year        varchar(30) NOT NULL,
    season              varchar(20) NOT NULL CHECK (season IN ('spring', 'summer', 'autumn', 'winter')),
    day_label           varchar(30) NOT NULL,
    calendar_mode       varchar(20) NOT NULL DEFAULT 'realtime_1to1'
                        CHECK (calendar_mode = 'realtime_1to1'),
    calendar_timezone   varchar(50) NOT NULL DEFAULT 'Asia/Taipei',
    calendar_anchor_real_date date NOT NULL DEFAULT CURRENT_DATE,
    calendar_anchor_game_date date NOT NULL DEFAULT CURRENT_DATE,
    reproduction_open   boolean NOT NULL DEFAULT true,
    maintenance_mode    boolean NOT NULL DEFAULT false,
    config              jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(config) = 'object'),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE TABLE game.game_settings (
    setting_key         varchar(120) PRIMARY KEY,
    category            varchar(40) NOT NULL,
    description         varchar(1000) NOT NULL DEFAULT '',
    published_value     jsonb NOT NULL,
    draft_value         jsonb,
    validation_schema   jsonb NOT NULL CHECK (jsonb_typeof(validation_schema) = 'object'),
    risk_level          varchar(20) NOT NULL DEFAULT 'normal'
                        CHECK (risk_level IN ('normal', 'high')),
    is_public           boolean NOT NULL DEFAULT false,
    updated_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    published_by        uuid REFERENCES game.users(id) ON DELETE SET NULL,
    published_at        timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (char_length(btrim(setting_key)) BETWEEN 3 AND 120),
    CHECK (published_at IS NULL OR published_by IS NOT NULL)
);

CREATE TABLE game.game_setting_revisions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    setting_key         varchar(120) NOT NULL REFERENCES game.game_settings(setting_key) ON DELETE RESTRICT,
    revision_no         integer NOT NULL CHECK (revision_no > 0),
    previous_value      jsonb,
    published_value     jsonb NOT NULL,
    change_reason       varchar(1000) NOT NULL,
    approval_request_id uuid,
    changed_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    changed_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (setting_key, revision_no)
);

CREATE TABLE game.world_locations (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(50) NOT NULL UNIQUE,
    display_name    varchar(80) NOT NULL,
    description     varchar(1500) NOT NULL DEFAULT '',
    image_url       text,
    map_x           numeric(5,2) NOT NULL CHECK (map_x BETWEEN 0 AND 100),
    map_y           numeric(5,2) NOT NULL CHECK (map_y BETWEEN 0 AND 100),
    access_rules    jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(access_rules) = 'object'),
    sort_order      integer NOT NULL DEFAULT 0,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

-- ============================================================
-- Events and external role-play submissions
-- ============================================================

CREATE TABLE game.event_rooms (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code                varchar(80) NOT NULL UNIQUE,
    title               varchar(150) NOT NULL,
    summary             varchar(1000) NOT NULL DEFAULT '',
    body_markdown       text NOT NULL DEFAULT '',
    event_type          varchar(30) NOT NULL CHECK (event_type IN ('main', 'social', 'investigation',
                                                                  'limited', 'private', 'admin')),
    status              varchar(20) NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'scheduled', 'open', 'locked', 'settled', 'cancelled')),
    location_id         uuid REFERENCES game.world_locations(id) ON DELETE SET NULL,
    visibility          varchar(20) NOT NULL DEFAULT 'public'
                        CHECK (visibility IN ('public', 'invited', 'private')),
    participant_limit   integer CHECK (participant_limit IS NULL OR participant_limit > 0),
    rules_version       varchar(40) NOT NULL,
    rules_snapshot      jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(rules_snapshot) = 'object'),
    opens_at            timestamptz,
    deadline_at         timestamptz,
    settled_at          timestamptz,
    created_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (deadline_at IS NULL OR opens_at IS NULL OR deadline_at > opens_at),
    CHECK ((status = 'settled' AND settled_at IS NOT NULL) OR status <> 'settled')
);

CREATE INDEX ix_event_rooms_player_list ON game.event_rooms(status, opens_at DESC, deadline_at);

CREATE TABLE game.story_arcs (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code                varchar(80) NOT NULL UNIQUE,
    title               varchar(150) NOT NULL,
    synopsis            varchar(3000) NOT NULL DEFAULT '',
    status              varchar(20) NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'review', 'published', 'archived')),
    sort_order          integer NOT NULL DEFAULT 0,
    created_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    published_by        uuid REFERENCES game.users(id) ON DELETE SET NULL,
    published_at        timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((status = 'published' AND published_by IS NOT NULL AND published_at IS NOT NULL)
           OR status <> 'published')
);

CREATE TABLE game.story_chapters (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    story_arc_id        uuid NOT NULL REFERENCES game.story_arcs(id) ON DELETE RESTRICT,
    code                varchar(80) NOT NULL,
    chapter_no          integer NOT NULL CHECK (chapter_no > 0),
    title               varchar(150) NOT NULL,
    summary             varchar(3000) NOT NULL DEFAULT '',
    status              varchar(20) NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'review', 'scheduled', 'published', 'archived')),
    opens_at            timestamptz,
    closes_at           timestamptz,
    created_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    published_by        uuid REFERENCES game.users(id) ON DELETE SET NULL,
    published_at        timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (story_arc_id, code),
    UNIQUE (story_arc_id, chapter_no),
    CHECK (closes_at IS NULL OR opens_at IS NULL OR closes_at > opens_at),
    CHECK ((status = 'published' AND published_by IS NOT NULL AND published_at IS NOT NULL)
           OR status <> 'published')
);

CREATE INDEX ix_story_chapters_player_list
    ON game.story_chapters(status, opens_at, chapter_no);

CREATE TABLE game.story_nodes (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    story_chapter_id    uuid NOT NULL REFERENCES game.story_chapters(id) ON DELETE RESTRICT,
    code                varchar(80) NOT NULL,
    node_type           varchar(20) NOT NULL
                        CHECK (node_type IN ('narrative', 'choice', 'condition', 'event', 'ending')),
    title               varchar(150) NOT NULL,
    body_markdown       text NOT NULL DEFAULT '',
    sort_order          integer NOT NULL DEFAULT 0,
    location_id         uuid REFERENCES game.world_locations(id) ON DELETE SET NULL,
    linked_event_room_id uuid REFERENCES game.event_rooms(id) ON DELETE SET NULL,
    branch_rules        jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(branch_rules) = 'object'),
    is_entry_node       boolean NOT NULL DEFAULT false,
    created_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (story_chapter_id, code),
    CHECK (char_length(body_markdown) <= 50000)
);

CREATE UNIQUE INDEX ux_story_nodes_one_entry
    ON game.story_nodes(story_chapter_id)
    WHERE is_entry_node = true;

CREATE TABLE game.content_revisions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    resource_type       varchar(30) NOT NULL
                        CHECK (resource_type IN ('story_arc', 'story_chapter', 'story_node')),
    resource_id         uuid NOT NULL,
    revision_no         integer NOT NULL CHECK (revision_no > 0),
    snapshot            jsonb NOT NULL CHECK (jsonb_typeof(snapshot) = 'object'),
    change_kind         varchar(20) NOT NULL
                        CHECK (change_kind IN ('create', 'edit', 'publish', 'archive', 'restore')),
    change_note         varchar(1000),
    changed_by          uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    changed_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (resource_type, resource_id, revision_no)
);

CREATE TABLE game.event_participants (
    event_room_id   uuid NOT NULL REFERENCES game.event_rooms(id) ON DELETE CASCADE,
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    participant_role varchar(40) NOT NULL DEFAULT 'participant',
    status          varchar(20) NOT NULL DEFAULT 'joined'
                    CHECK (status IN ('invited', 'joined', 'left', 'removed', 'completed')),
    joined_at       timestamptz,
    completed_at    timestamptz,
    metadata        jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object'),
    PRIMARY KEY (event_room_id, character_id)
);

CREATE INDEX ix_event_participants_character ON game.event_participants(character_id, status);

CREATE TABLE game.event_posts (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    event_room_id   uuid NOT NULL REFERENCES game.event_rooms(id) ON DELETE RESTRICT,
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    body_markdown   text NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft', 'submitted', 'under_review', 'approved',
                                      'needs_revision', 'rejected', 'withdrawn', 'moderated')),
    client_request_id varchar(80),
    created_at      timestamptz NOT NULL DEFAULT now(),
    submitted_at    timestamptz,
    reviewed_at     timestamptz,
    reviewed_by     uuid REFERENCES game.users(id) ON DELETE SET NULL,
    review_note     varchar(1000),
    published_at    timestamptz,
    edited_at       timestamptz,
    moderated_by    uuid REFERENCES game.users(id) ON DELETE SET NULL,
    moderation_note varchar(500),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (char_length(body_markdown) <= 10000),
    CHECK (status = 'draft' OR char_length(btrim(body_markdown)) > 0),
    CHECK (status NOT IN ('submitted', 'under_review', 'approved', 'rejected', 'needs_revision')
           OR submitted_at IS NOT NULL),
    CHECK (status <> 'approved' OR
           (reviewed_at IS NOT NULL AND reviewed_by IS NOT NULL AND published_at IS NOT NULL)),
    UNIQUE (event_room_id, character_id, client_request_id)
);

CREATE INDEX ix_event_posts_room_feed ON game.event_posts(event_room_id, published_at, id)
    WHERE status = 'approved';
CREATE INDEX ix_event_posts_review_queue ON game.event_posts(status, submitted_at)
    WHERE status IN ('submitted', 'under_review');

CREATE TABLE game.event_post_revisions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    event_post_id   uuid NOT NULL REFERENCES game.event_posts(id) ON DELETE RESTRICT,
    revision_no     integer NOT NULL CHECK (revision_no > 0),
    body_markdown   text NOT NULL,
    revision_kind   varchar(20) NOT NULL DEFAULT 'draft_save'
                    CHECK (revision_kind IN ('draft_save', 'submit', 'revision_request',
                                             'approval', 'moderation')),
    changed_by      uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (event_post_id, revision_no)
);

CREATE TABLE game.event_results (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    event_room_id   uuid NOT NULL REFERENCES game.event_rooms(id) ON DELETE CASCADE,
    character_id    uuid REFERENCES game.characters(id) ON DELETE RESTRICT,
    outcome_code    varchar(80) NOT NULL,
    public_summary  varchar(2000) NOT NULL,
    private_payload jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(private_payload) = 'object'),
    rewards_payload jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(rewards_payload) = 'object'),
    rules_version   varchar(40) NOT NULL,
    settled_by      uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE NULLS NOT DISTINCT (event_room_id, character_id)
);

CREATE TABLE game.external_play_submissions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    submitted_by_character_id uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    source_type     varchar(20) NOT NULL CHECK (source_type IN ('line_group', 'other')),
    occurred_at     timestamptz NOT NULL,
    summary         varchar(4000) NOT NULL,
    evidence_urls   jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(evidence_urls) = 'array'),
    involved_character_ids jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(involved_character_ids) = 'array'),
    status          varchar(30) NOT NULL DEFAULT 'submitted'
                    CHECK (status IN ('submitted', 'under_review', 'approved', 'rejected', 'cancelled')),
    review_note     varchar(1000),
    reviewed_by     uuid REFERENCES game.users(id) ON DELETE SET NULL,
    reviewed_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (char_length(btrim(summary)) BETWEEN 1 AND 4000)
);

CREATE INDEX ix_external_play_review_queue
    ON game.external_play_submissions(status, created_at)
    WHERE status IN ('submitted', 'under_review');

-- ============================================================
-- Economy, inventory, market
-- ============================================================

CREATE TABLE game.currencies (
    code            varchar(30) PRIMARY KEY,
    display_name    varchar(50) NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE game.wallets (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    currency_code   varchar(30) NOT NULL REFERENCES game.currencies(code) ON DELETE RESTRICT,
    balance         bigint NOT NULL DEFAULT 0 CHECK (balance >= 0),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (character_id, currency_code)
);

CREATE TABLE game.ledger_transactions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_type varchar(40) NOT NULL
                    CHECK (transaction_type IN ('stipend', 'purchase', 'reward', 'item_use',
                                                'admin_grant', 'admin_correction', 'refund')),
    reference_type  varchar(60),
    reference_id    uuid,
    reason_code     varchar(80) NOT NULL,
    reason_text     varchar(1000),
    initiated_by    uuid REFERENCES game.users(id) ON DELETE SET NULL,
    request_id      varchar(80),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE game.ledger_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id  uuid NOT NULL REFERENCES game.ledger_transactions(id) ON DELETE RESTRICT,
    wallet_id       uuid NOT NULL REFERENCES game.wallets(id) ON DELETE RESTRICT,
    amount          bigint NOT NULL CHECK (amount <> 0),
    balance_after   bigint NOT NULL CHECK (balance_after >= 0),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_ledger_entries_wallet ON game.ledger_entries(wallet_id, created_at DESC, id DESC);

CREATE TABLE game.item_definitions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code            varchar(80) NOT NULL,
    version_no      integer NOT NULL DEFAULT 1 CHECK (version_no > 0),
    display_name    varchar(100) NOT NULL,
    description     varchar(1500) NOT NULL DEFAULT '',
    category        varchar(30) NOT NULL CHECK (category IN ('clothing', 'medicine', 'poison',
                                                             'gift', 'quest', 'material', 'other')),
    image_url       text,
    stack_limit     integer NOT NULL DEFAULT 999 CHECK (stack_limit > 0),
    is_consumable   boolean NOT NULL DEFAULT false,
    effect_payload  jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(effect_payload) = 'object'),
    usage_rules     jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(usage_rules) = 'object'),
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (code, version_no)
);

CREATE TABLE game.inventory_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    item_definition_id uuid NOT NULL REFERENCES game.item_definitions(id) ON DELETE RESTRICT,
    quantity        integer NOT NULL CHECK (quantity >= 0),
    expires_at      timestamptz,
    acquired_at     timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE NULLS NOT DISTINCT (character_id, item_definition_id, expires_at)
);

CREATE INDEX ix_inventory_entries_character_available
    ON game.inventory_entries(character_id, item_definition_id)
    WHERE quantity > 0;

CREATE TABLE game.inventory_transactions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    inventory_entry_id uuid NOT NULL REFERENCES game.inventory_entries(id) ON DELETE RESTRICT,
    transaction_type varchar(30) NOT NULL
                    CHECK (transaction_type IN ('purchase', 'reward', 'use', 'expire',
                                                'admin_grant', 'admin_correction', 'refund')),
    quantity_delta  integer NOT NULL CHECK (quantity_delta <> 0),
    quantity_after  integer NOT NULL CHECK (quantity_after >= 0),
    effect_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(effect_snapshot) = 'object'),
    reference_type  varchar(60),
    reference_id    uuid,
    initiated_by    uuid REFERENCES game.users(id) ON DELETE SET NULL,
    reason_code     varchar(80),
    reason_text     varchar(1000),
    request_id      varchar(80),
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_inventory_transactions_entry
    ON game.inventory_transactions(inventory_entry_id, created_at DESC);

CREATE TABLE game.market_offers (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    item_definition_id uuid NOT NULL REFERENCES game.item_definitions(id) ON DELETE RESTRICT,
    currency_code   varchar(30) NOT NULL REFERENCES game.currencies(code) ON DELETE RESTRICT,
    unit_price      bigint NOT NULL CHECK (unit_price >= 0),
    stock_total     integer CHECK (stock_total IS NULL OR stock_total >= 0),
    stock_sold      integer NOT NULL DEFAULT 0 CHECK (stock_sold >= 0),
    per_character_limit integer CHECK (per_character_limit IS NULL OR per_character_limit > 0),
    starts_at       timestamptz,
    ends_at         timestamptz,
    eligibility_rules jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(eligibility_rules) = 'object'),
    is_active       boolean NOT NULL DEFAULT true,
    created_by      uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (stock_total IS NULL OR stock_sold <= stock_total),
    CHECK (ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at)
);

CREATE INDEX ix_market_offers_active_window ON game.market_offers(is_active, starts_at, ends_at);

CREATE TABLE game.purchases (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    market_offer_id uuid NOT NULL REFERENCES game.market_offers(id) ON DELETE RESTRICT,
    quantity        integer NOT NULL CHECK (quantity > 0),
    unit_price      bigint NOT NULL CHECK (unit_price >= 0),
    total_price     bigint NOT NULL CHECK (total_price >= 0),
    currency_code   varchar(30) NOT NULL REFERENCES game.currencies(code) ON DELETE RESTRICT,
    ledger_transaction_id uuid NOT NULL UNIQUE REFERENCES game.ledger_transactions(id) ON DELETE RESTRICT,
    idempotency_key varchar(100) NOT NULL,
    purchased_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (character_id, idempotency_key),
    CHECK (total_price = unit_price * quantity)
);

-- ============================================================
-- Relationships
-- ============================================================

CREATE TABLE game.relationships (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    source_character_id uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    target_character_id uuid REFERENCES game.characters(id) ON DELETE RESTRICT,
    target_npc_code     varchar(80),
    relationship_type   varchar(40) NOT NULL,
    score               integer NOT NULL DEFAULT 0 CHECK (score BETWEEN -1000 AND 1000),
    visibility          varchar(20) NOT NULL DEFAULT 'private'
                        CHECK (visibility IN ('private', 'mutual', 'public', 'admin_only')),
    tags                jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(tags) = 'array'),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((target_character_id IS NOT NULL)::integer + (target_npc_code IS NOT NULL)::integer = 1),
    CHECK (target_character_id IS NULL OR target_character_id <> source_character_id),
    UNIQUE NULLS NOT DISTINCT (source_character_id, target_character_id, target_npc_code, relationship_type)
);

CREATE INDEX ix_relationships_source ON game.relationships(source_character_id, visibility);

CREATE TABLE game.relationship_history (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    relationship_id uuid NOT NULL REFERENCES game.relationships(id) ON DELETE RESTRICT,
    score_before    integer NOT NULL,
    score_delta     integer NOT NULL CHECK (score_delta <> 0),
    score_after     integer NOT NULL,
    reason_code     varchar(80) NOT NULL,
    reference_type  varchar(60),
    reference_id    uuid,
    changed_by      uuid REFERENCES game.users(id) ON DELETE SET NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CHECK (score_after = score_before + score_delta),
    CHECK (score_after BETWEEN -1000 AND 1000)
);

-- ============================================================
-- Reproduction and heir waiting pool
-- ============================================================

CREATE TABLE game.reproduction_control (
    singleton_id    smallint PRIMARY KEY DEFAULT 1 CHECK (singleton_id = 1),
    is_open         boolean NOT NULL DEFAULT true,
    closed_reason   varchar(500),
    conception_rate_percent smallint NOT NULL DEFAULT 100
                    CHECK (conception_rate_percent BETWEEN 0 AND 100),
    pregnancy_duration_days smallint NOT NULL DEFAULT 10
                    CHECK (pregnancy_duration_days BETWEEN 1 AND 365),
    miscarriage_mode varchar(30) NOT NULL DEFAULT 'event_only'
                    CHECK (miscarriage_mode IN ('disabled', 'event_only',
                                                'threshold', 'daily_probability')),
    miscarriage_rules jsonb NOT NULL DEFAULT '{"baseRatePercent":0}'::jsonb
                    CHECK (jsonb_typeof(miscarriage_rules) = 'object'),
    rules_version   varchar(40) NOT NULL DEFAULT 'reproduction-1',
    updated_by      uuid REFERENCES game.users(id) ON DELETE SET NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE TABLE game.heir_wait_pool_entries (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    status          varchar(20) NOT NULL DEFAULT 'waiting'
                    CHECK (status IN ('waiting', 'drawn', 'withdrawn', 'suspended')),
    entered_at      timestamptz NOT NULL DEFAULT now(),
    resolved_at     timestamptz,
    resolved_reason varchar(500),
    created_by      uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK ((status = 'waiting' AND resolved_at IS NULL) OR
           (status <> 'waiting' AND resolved_at IS NOT NULL))
);

CREATE UNIQUE INDEX ux_heir_wait_pool_one_waiting_per_character
    ON game.heir_wait_pool_entries(character_id)
    WHERE status = 'waiting';
CREATE INDEX ix_heir_wait_pool_draw_candidates
    ON game.heir_wait_pool_entries(entered_at, id)
    WHERE status = 'waiting';

CREATE TABLE game.audience_requests (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    audience_type   varchar(20) NOT NULL CHECK (audience_type IN ('meal', 'bedchamber')),
    status          varchar(20) NOT NULL DEFAULT 'submitted'
                    CHECK (status IN ('submitted', 'approved', 'rejected', 'resolved', 'cancelled')),
    qualification_snapshot jsonb NOT NULL CHECK (jsonb_typeof(qualification_snapshot) = 'object'),
    requested_at    timestamptz NOT NULL DEFAULT now(),
    resolved_at     timestamptz,
    result_code     varchar(80),
    result_payload  jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(result_payload) = 'object'),
    idempotency_key varchar(100) NOT NULL,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    UNIQUE (character_id, idempotency_key),
    CHECK ((status IN ('resolved', 'rejected', 'cancelled') AND resolved_at IS NOT NULL) OR
           status IN ('submitted', 'approved'))
);

CREATE INDEX ix_audience_requests_character ON game.audience_requests(character_id, requested_at DESC);

CREATE TABLE game.pregnancies (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    mother_character_id uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    audience_request_id uuid NOT NULL UNIQUE REFERENCES game.audience_requests(id) ON DELETE RESTRICT,
    status          varchar(20) NOT NULL DEFAULT 'ongoing'
                    CHECK (status IN ('ongoing', 'miscarried', 'completed', 'cancelled')),
    conceived_at    timestamptz NOT NULL,
    due_at          timestamptz NOT NULL,
    conception_rate_percent smallint NOT NULL
                    CHECK (conception_rate_percent BETWEEN 0 AND 100),
    conception_roll smallint NOT NULL CHECK (conception_roll BETWEEN 1 AND 100),
    slot_reserved_at timestamptz NOT NULL,
    slot_released_at timestamptz,
    rules_version   varchar(40) NOT NULL,
    rules_snapshot  jsonb NOT NULL CHECK (jsonb_typeof(rules_snapshot) = 'object'),
    resolved_by     uuid REFERENCES game.users(id) ON DELETE SET NULL,
    resolution_code varchar(80),
    resolution_reason varchar(1000),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (due_at > conceived_at),
    CHECK (slot_reserved_at >= conceived_at),
    CHECK ((status = 'ongoing' AND slot_released_at IS NULL) OR
           (status <> 'ongoing' AND slot_released_at IS NOT NULL)),
    CHECK (status <> 'miscarried' OR
           (resolution_code IS NOT NULL AND char_length(btrim(resolution_reason)) >= 5))
);

CREATE UNIQUE INDEX ux_pregnancies_one_ongoing_per_mother
    ON game.pregnancies(mother_character_id)
    WHERE status = 'ongoing';
CREATE INDEX ix_pregnancies_due ON game.pregnancies(due_at) WHERE status = 'ongoing';

CREATE TABLE game.births (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    pregnancy_id        uuid NOT NULL UNIQUE REFERENCES game.pregnancies(id) ON DELETE RESTRICT,
    wait_pool_entry_id  uuid NOT NULL UNIQUE REFERENCES game.heir_wait_pool_entries(id) ON DELETE RESTRICT,
    child_character_id  uuid NOT NULL UNIQUE REFERENCES game.characters(id) ON DELETE RESTRICT,
    candidate_count     integer NOT NULL CHECK (candidate_count > 0),
    candidate_set_hash  varchar(128) NOT NULL,
    random_algorithm    varchar(80) NOT NULL,
    random_proof_hash   varchar(128) NOT NULL,
    rules_version       varchar(40) NOT NULL,
    drawn_by            uuid REFERENCES game.users(id) ON DELETE SET NULL,
    born_at             timestamptz NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE game.offspring_links (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    child_character_id  uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    parent_type         varchar(20) NOT NULL CHECK (parent_type IN ('mother', 'father')),
    parent_character_id uuid REFERENCES game.characters(id) ON DELETE RESTRICT,
    parent_npc_code     varchar(80),
    is_public           boolean NOT NULL DEFAULT true,
    created_at          timestamptz NOT NULL DEFAULT now(),
    CHECK ((parent_character_id IS NOT NULL)::integer + (parent_npc_code IS NOT NULL)::integer = 1),
    UNIQUE NULLS NOT DISTINCT (child_character_id, parent_type, parent_character_id, parent_npc_code)
);

-- ============================================================
-- Intrigue, effects, death
-- ============================================================

CREATE TABLE game.intrigue_actions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_character_id  uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    target_character_id uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    action_type         varchar(30) NOT NULL CHECK (action_type IN ('poison', 'investigate', 'countermeasure')),
    status              varchar(20) NOT NULL DEFAULT 'submitted'
                        CHECK (status IN ('submitted', 'processing', 'resolved', 'failed', 'cancelled')),
    input_payload       jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(input_payload) = 'object'),
    secret_result       jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(secret_result) = 'object'),
    public_result       jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(public_result) = 'object'),
    rules_version       varchar(40) NOT NULL,
    idempotency_key     varchar(100) NOT NULL,
    submitted_at        timestamptz NOT NULL DEFAULT now(),
    resolve_after       timestamptz,
    resolved_at         timestamptz,
    updated_at          timestamptz NOT NULL DEFAULT now(),
    version             bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (actor_character_id <> target_character_id),
    UNIQUE (actor_character_id, idempotency_key)
);

CREATE INDEX ix_intrigue_actions_pending ON game.intrigue_actions(resolve_after)
    WHERE status IN ('submitted', 'processing');

CREATE TABLE game.status_effects (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id    uuid NOT NULL REFERENCES game.characters(id) ON DELETE RESTRICT,
    effect_code     varchar(80) NOT NULL,
    visibility      varchar(20) NOT NULL DEFAULT 'private'
                    CHECK (visibility IN ('private', 'public', 'admin_only')),
    severity        smallint NOT NULL DEFAULT 1 CHECK (severity BETWEEN 1 AND 10),
    payload         jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(payload) = 'object'),
    starts_at       timestamptz NOT NULL,
    expires_at      timestamptz,
    resolved_at     timestamptz,
    source_type     varchar(60),
    source_id       uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CHECK (expires_at IS NULL OR expires_at > starts_at),
    CHECK (resolved_at IS NULL OR resolved_at >= starts_at)
);

CREATE INDEX ix_status_effects_active ON game.status_effects(character_id, effect_code)
    WHERE resolved_at IS NULL;

CREATE TABLE game.deaths (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    character_id        uuid NOT NULL UNIQUE REFERENCES game.characters(id) ON DELETE RESTRICT,
    cause_code          varchar(80) NOT NULL,
    public_cause        varchar(1000) NOT NULL,
    private_details     jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(private_details) = 'object'),
    source_type         varchar(60),
    source_id           uuid,
    occurred_at         timestamptz NOT NULL,
    ruled_by            uuid REFERENCES game.users(id) ON DELETE SET NULL,
    approval_request_id uuid,
    created_at          timestamptz NOT NULL DEFAULT now()
);

-- ============================================================
-- Notification and announcement
-- ============================================================

CREATE TABLE game.notifications (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES game.users(id) ON DELETE CASCADE,
    notification_type varchar(60) NOT NULL,
    title           varchar(150) NOT NULL,
    body            varchar(2000) NOT NULL,
    route           varchar(300),
    payload         jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(payload) = 'object'),
    created_at      timestamptz NOT NULL DEFAULT now(),
    read_at         timestamptz,
    expires_at      timestamptz
);

CREATE INDEX ix_notifications_user_unread ON game.notifications(user_id, created_at DESC)
    WHERE read_at IS NULL;

CREATE TABLE game.announcements (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    title           varchar(150) NOT NULL,
    body_markdown   text NOT NULL,
    severity        varchar(20) NOT NULL DEFAULT 'info'
                    CHECK (severity IN ('info', 'warning', 'critical')),
    audience        varchar(20) NOT NULL DEFAULT 'all'
                    CHECK (audience IN ('all', 'players', 'admins')),
    starts_at       timestamptz NOT NULL,
    ends_at         timestamptz,
    published_by    uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (ends_at IS NULL OR ends_at > starts_at)
);

CREATE INDEX ix_announcements_active ON game.announcements(starts_at, ends_at);

-- ============================================================
-- Approval, audit, idempotency, outbox, jobs
-- ============================================================

CREATE TABLE game.approval_requests (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    action_type     varchar(80) NOT NULL,
    target_type     varchar(60) NOT NULL,
    target_id       uuid,
    payload         jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
    reason          varchar(1000) NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'approved', 'rejected', 'expired', 'executed', 'cancelled')),
    requested_by    uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    requested_at    timestamptz NOT NULL DEFAULT now(),
    expires_at      timestamptz NOT NULL,
    resolved_at     timestamptz,
    executed_at     timestamptz,
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    CHECK (expires_at > requested_at)
);

CREATE INDEX ix_approval_requests_pending ON game.approval_requests(requested_at)
    WHERE status = 'pending';

ALTER TABLE game.deaths
    ADD CONSTRAINT fk_deaths_approval_request
    FOREIGN KEY (approval_request_id) REFERENCES game.approval_requests(id) ON DELETE SET NULL;

ALTER TABLE game.game_setting_revisions
    ADD CONSTRAINT fk_game_setting_revisions_approval_request
    FOREIGN KEY (approval_request_id) REFERENCES game.approval_requests(id) ON DELETE SET NULL;

CREATE TABLE game.approval_decisions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_request_id uuid NOT NULL REFERENCES game.approval_requests(id) ON DELETE CASCADE,
    reviewer_id         uuid NOT NULL REFERENCES game.users(id) ON DELETE RESTRICT,
    decision            varchar(20) NOT NULL CHECK (decision IN ('approve', 'reject')),
    note                varchar(1000),
    decided_at          timestamptz NOT NULL DEFAULT now(),
    UNIQUE (approval_request_id, reviewer_id)
);

CREATE TABLE game.audit_logs (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    occurred_at     timestamptz NOT NULL DEFAULT now(),
    actor_user_id   uuid REFERENCES game.users(id) ON DELETE SET NULL,
    actor_role      varchar(40),
    action          varchar(100) NOT NULL,
    target_type     varchar(60),
    target_id       uuid,
    before_data     jsonb,
    after_data      jsonb,
    reason          varchar(1000),
    request_id      varchar(80),
    ip_address      inet,
    user_agent      varchar(512),
    metadata        jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(metadata) = 'object')
);

COMMENT ON TABLE game.audit_logs IS 'Append-only. Permanent retention; no purge job or delete API.';
COMMENT ON TABLE game.event_posts IS 'Drafts and submitted/approved event text are retained permanently.';
COMMENT ON TABLE game.event_post_revisions IS 'Append-only event text history retained permanently; not public.';
COMMENT ON TABLE game.deaths IS 'Permanent character death record; visible through admin history only where linkage is private.';

CREATE TRIGGER tr_event_posts_no_delete
    BEFORE DELETE ON game.event_posts
    FOR EACH ROW EXECUTE FUNCTION game.reject_deletion();

CREATE INDEX ix_audit_logs_target ON game.audit_logs(target_type, target_id, occurred_at DESC);
CREATE INDEX ix_audit_logs_actor ON game.audit_logs(actor_user_id, occurred_at DESC);

CREATE TRIGGER tr_audit_logs_immutable
    BEFORE UPDATE OR DELETE ON game.audit_logs
    FOR EACH ROW EXECUTE FUNCTION game.reject_mutation();

CREATE TRIGGER tr_ledger_entries_immutable
    BEFORE UPDATE OR DELETE ON game.ledger_entries
    FOR EACH ROW EXECUTE FUNCTION game.reject_mutation();

CREATE TRIGGER tr_inventory_transactions_immutable
    BEFORE UPDATE OR DELETE ON game.inventory_transactions
    FOR EACH ROW EXECUTE FUNCTION game.reject_mutation();

DO $$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'character_application_revisions', 'character_status_history', 'rank_history',
        'game_setting_revisions', 'content_revisions',
        'event_post_revisions', 'event_results', 'relationship_history', 'births',
        'offspring_links', 'deaths', 'approval_decisions', 'job_runs'
    ]
    LOOP
        EXECUTE format(
            'CREATE TRIGGER tr_%I_immutable BEFORE UPDATE OR DELETE ON game.%I FOR EACH ROW EXECUTE FUNCTION game.reject_mutation()',
            table_name, table_name
        );
    END LOOP;
END;
$$;

CREATE TABLE game.idempotency_records (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES game.users(id) ON DELETE CASCADE,
    http_method     varchar(10) NOT NULL,
    request_path    varchar(300) NOT NULL,
    idempotency_key varchar(100) NOT NULL,
    request_hash    varchar(128) NOT NULL,
    status          varchar(20) NOT NULL DEFAULT 'processing'
                    CHECK (status IN ('processing', 'completed', 'failed')),
    response_status integer CHECK (response_status BETWEEN 100 AND 599),
    response_body   jsonb,
    created_at      timestamptz NOT NULL DEFAULT now(),
    completed_at    timestamptz,
    expires_at      timestamptz NOT NULL,
    UNIQUE (user_id, http_method, request_path, idempotency_key),
    CHECK (expires_at > created_at)
);

CREATE INDEX ix_idempotency_records_expiry ON game.idempotency_records(expires_at);

CREATE TABLE game.outbox_messages (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    topic           varchar(100) NOT NULL,
    aggregate_type  varchar(60) NOT NULL,
    aggregate_id    uuid NOT NULL,
    payload         jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
    occurred_at     timestamptz NOT NULL DEFAULT now(),
    available_at    timestamptz NOT NULL DEFAULT now(),
    processed_at    timestamptz,
    attempt_count   integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    last_error      varchar(2000)
);

CREATE INDEX ix_outbox_messages_pending ON game.outbox_messages(available_at, occurred_at)
    WHERE processed_at IS NULL;

CREATE TABLE game.scheduled_jobs (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    job_key         varchar(100) NOT NULL UNIQUE,
    job_type        varchar(80) NOT NULL,
    cron_expression varchar(100),
    payload         jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(payload) = 'object'),
    is_enabled      boolean NOT NULL DEFAULT true,
    next_run_at     timestamptz,
    locked_by       varchar(100),
    locked_until    timestamptz,
    last_run_at     timestamptz,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    version         bigint NOT NULL DEFAULT 1 CHECK (version > 0)
);

CREATE INDEX ix_scheduled_jobs_due ON game.scheduled_jobs(next_run_at)
    WHERE is_enabled = true;

CREATE TABLE game.job_runs (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    scheduled_job_id uuid NOT NULL REFERENCES game.scheduled_jobs(id) ON DELETE CASCADE,
    status          varchar(20) NOT NULL CHECK (status IN ('running', 'succeeded', 'failed', 'cancelled')),
    started_at      timestamptz NOT NULL DEFAULT now(),
    finished_at     timestamptz,
    attempt_no      integer NOT NULL DEFAULT 1 CHECK (attempt_no > 0),
    result_payload  jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(result_payload) = 'object'),
    error_message   varchar(4000),
    worker_id       varchar(100) NOT NULL,
    CHECK ((status = 'running' AND finished_at IS NULL) OR
           (status <> 'running' AND finished_at IS NOT NULL))
);

CREATE INDEX ix_job_runs_job ON game.job_runs(scheduled_job_id, started_at DESC);

-- ============================================================
-- Cross-table integrity checks for the highest-risk rules
-- ============================================================

CREATE OR REPLACE FUNCTION game.validate_title_definition_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_incompatible_count integer;
BEGIN
    IF NEW.applies_to_role IS DISTINCT FROM OLD.applies_to_role AND NEW.applies_to_role IS NOT NULL THEN
        SELECT count(*) INTO v_incompatible_count
        FROM game.character_title_assignments assignment
        JOIN game.characters character ON character.id = assignment.character_id
        WHERE assignment.title_definition_id = NEW.id
          AND assignment.revoked_at IS NULL
          AND character.role <> NEW.applies_to_role;

        IF v_incompatible_count > 0 THEN
            RAISE EXCEPTION 'Title role cannot invalidate active assignments' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_character_title_definitions_validate_update
    BEFORE UPDATE OF applies_to_role ON game.character_title_definitions
    FOR EACH ROW EXECUTE FUNCTION game.validate_title_definition_update();

CREATE OR REPLACE FUNCTION game.validate_character_title_assignment()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_character_role varchar(20);
    v_title_role varchar(20);
    v_title_active boolean;
BEGIN
    SELECT role INTO v_character_role
    FROM game.characters
    WHERE id = NEW.character_id;

    SELECT applies_to_role, is_active INTO v_title_role, v_title_active
    FROM game.character_title_definitions
    WHERE id = NEW.title_definition_id;

    IF v_title_active IS DISTINCT FROM true OR
       (v_title_role IS NOT NULL AND v_title_role IS DISTINCT FROM v_character_role) THEN
        RAISE EXCEPTION 'Assigned title must be active and match character role' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_character_title_assignments_validate
    BEFORE INSERT OR UPDATE OF character_id, title_definition_id ON game.character_title_assignments
    FOR EACH ROW EXECUTE FUNCTION game.validate_character_title_assignment();

CREATE OR REPLACE FUNCTION game.validate_story_chapter_publish()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_arc_status varchar(20);
    v_entry_count integer;
    v_validate boolean := false;
BEGIN
    IF TG_OP = 'INSERT' THEN
        v_validate := true;
    ELSIF OLD.status IS DISTINCT FROM 'published' THEN
        v_validate := true;
    END IF;

    IF NEW.status = 'published' AND v_validate THEN
        SELECT status INTO v_arc_status FROM game.story_arcs WHERE id = NEW.story_arc_id;
        SELECT count(*) INTO v_entry_count
        FROM game.story_nodes
        WHERE story_chapter_id = NEW.id AND is_entry_node = true;

        IF v_arc_status IS DISTINCT FROM 'published' OR v_entry_count <> 1 THEN
            RAISE EXCEPTION 'Published chapter requires a published arc and exactly one entry node' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_story_chapters_validate_publish
    BEFORE INSERT OR UPDATE OF status ON game.story_chapters
    FOR EACH ROW EXECUTE FUNCTION game.validate_story_chapter_publish();

CREATE OR REPLACE FUNCTION game.validate_player_portrait_asset()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_asset_owner uuid;
    v_asset_status varchar(20);
BEGIN
    SELECT owner_user_id, status INTO v_asset_owner, v_asset_status
    FROM game.media_assets
    WHERE id = NEW.media_asset_id;

    IF v_asset_owner IS DISTINCT FROM NEW.user_id OR v_asset_status IS DISTINCT FROM 'ready' THEN
        RAISE EXCEPTION 'Portrait media must be ready and belong to the submitting user' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_player_portrait_submissions_validate_asset
    BEFORE INSERT OR UPDATE OF user_id, media_asset_id ON game.player_portrait_submissions
    FOR EACH ROW EXECUTE FUNCTION game.validate_player_portrait_asset();

CREATE OR REPLACE FUNCTION game.validate_application_portrait()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_portrait_role varchar(20);
    v_portrait_active boolean;
    v_upload_role varchar(20);
    v_upload_user_id uuid;
    v_upload_status varchar(20);
BEGIN
    IF NEW.portrait_id IS NOT NULL THEN
        SELECT role, is_active INTO v_portrait_role, v_portrait_active
        FROM game.preset_portraits
        WHERE id = NEW.portrait_id;

        IF v_portrait_role IS DISTINCT FROM NEW.role OR v_portrait_active IS DISTINCT FROM true THEN
            RAISE EXCEPTION 'Application preset portrait must be active and match role' USING ERRCODE = '23514';
        END IF;
    ELSE
        SELECT role, user_id, status INTO v_upload_role, v_upload_user_id, v_upload_status
        FROM game.player_portrait_submissions
        WHERE id = NEW.player_portrait_submission_id;

        IF v_upload_role IS DISTINCT FROM NEW.role OR
           v_upload_user_id IS DISTINCT FROM NEW.user_id OR
           v_upload_status NOT IN ('pending', 'approved') THEN
            RAISE EXCEPTION 'Application uploaded portrait must belong to user, match role and be reviewable' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_character_applications_validate_portrait
    BEFORE INSERT OR UPDATE OF role, portrait_id, player_portrait_submission_id, user_id ON game.character_applications
    FOR EACH ROW EXECUTE FUNCTION game.validate_application_portrait();

CREATE OR REPLACE FUNCTION game.validate_character_master_data()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_portrait_role varchar(20);
    v_upload_role varchar(20);
    v_upload_user_id uuid;
    v_upload_status varchar(20);
    v_rank_role varchar(20);
BEGIN
    IF NEW.portrait_id IS NOT NULL THEN
        SELECT role INTO v_portrait_role
        FROM game.preset_portraits
        WHERE id = NEW.portrait_id;

        IF v_portrait_role IS DISTINCT FROM NEW.role THEN
            RAISE EXCEPTION 'Character preset portrait must match role' USING ERRCODE = '23514';
        END IF;
    ELSE
        SELECT role, user_id, status INTO v_upload_role, v_upload_user_id, v_upload_status
        FROM game.player_portrait_submissions
        WHERE id = NEW.player_portrait_submission_id;

        IF v_upload_role IS DISTINCT FROM NEW.role OR
           v_upload_user_id IS DISTINCT FROM NEW.user_id OR
           v_upload_status IS DISTINCT FROM 'approved' THEN
            RAISE EXCEPTION 'Character uploaded portrait must be approved, belong to user and match role' USING ERRCODE = '23514';
        END IF;
    END IF;

    IF NEW.rank_id IS NOT NULL THEN
        SELECT applies_to_role INTO v_rank_role
        FROM game.ranks
        WHERE id = NEW.rank_id;
        IF v_rank_role IS DISTINCT FROM NEW.role THEN
            RAISE EXCEPTION 'Character rank must match role' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_characters_validate_master_data
    BEFORE INSERT OR UPDATE OF role, portrait_id, player_portrait_submission_id, user_id, rank_id ON game.characters
    FOR EACH ROW EXECUTE FUNCTION game.validate_character_master_data();

CREATE OR REPLACE FUNCTION game.validate_wait_pool_character()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_role varchar(20);
    v_status varchar(30);
BEGIN
    SELECT role, status INTO v_role, v_status
    FROM game.characters
    WHERE id = NEW.character_id;

    IF v_role NOT IN ('prince', 'princess') THEN
        RAISE EXCEPTION 'Only prince/princess can enter heir wait pool' USING ERRCODE = '23514';
    END IF;
    IF NEW.status = 'waiting' AND v_status <> 'waiting_birth' THEN
        RAISE EXCEPTION 'Waiting pool character must have waiting_birth status' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_heir_wait_pool_validate
    BEFORE INSERT OR UPDATE OF character_id, status ON game.heir_wait_pool_entries
    FOR EACH ROW EXECUTE FUNCTION game.validate_wait_pool_character();

CREATE OR REPLACE FUNCTION game.validate_pregnancy_mother()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_role varchar(20);
    v_status varchar(30);
BEGIN
    SELECT role, status INTO v_role, v_status
    FROM game.characters
    WHERE id = NEW.mother_character_id;

    IF v_role <> 'consort' OR v_status <> 'active' THEN
        RAISE EXCEPTION 'Pregnancy mother must be an active consort' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_pregnancies_validate_mother
    BEFORE INSERT OR UPDATE OF mother_character_id ON game.pregnancies
    FOR EACH ROW EXECUTE FUNCTION game.validate_pregnancy_mother();

CREATE OR REPLACE FUNCTION game.validate_birth_selection()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_pool_character uuid;
    v_pool_status varchar(20);
BEGIN
    SELECT character_id, status INTO v_pool_character, v_pool_status
    FROM game.heir_wait_pool_entries
    WHERE id = NEW.wait_pool_entry_id;

    IF v_pool_character IS DISTINCT FROM NEW.child_character_id THEN
        RAISE EXCEPTION 'Birth child must match wait pool character' USING ERRCODE = '23514';
    END IF;
    IF v_pool_status <> 'waiting' THEN
        RAISE EXCEPTION 'Birth can only select a waiting pool entry' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_births_validate_selection
    BEFORE INSERT ON game.births
    FOR EACH ROW EXECUTE FUNCTION game.validate_birth_selection();

-- Automatic updated_at/version triggers. Tables intentionally excluded are append-only.
DO $$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'users', 'preset_portraits', 'media_assets', 'player_portrait_submissions',
        'character_applications', 'ranks', 'character_title_definitions', 'residences',
        'characters', 'character_title_assignments', 'character_stats',
        'world_state', 'game_settings', 'world_locations', 'event_rooms', 'event_posts',
        'story_arcs', 'story_chapters', 'story_nodes',
        'external_play_submissions', 'wallets', 'inventory_entries', 'market_offers',
        'relationships', 'reproduction_control', 'heir_wait_pool_entries', 'pregnancies',
        'audience_requests', 'intrigue_actions', 'approval_requests',
        'announcements', 'scheduled_jobs'
    ]
    LOOP
        EXECUTE format(
            'CREATE TRIGGER tr_%I_touch BEFORE UPDATE ON game.%I FOR EACH ROW EXECUTE FUNCTION game.touch_updated_at()',
            table_name, table_name
        );
    END LOOP;
END;
$$;

INSERT INTO game.reproduction_control(singleton_id, is_open)
VALUES (1, true)
ON CONFLICT (singleton_id) DO NOTHING;

INSERT INTO game.world_state(singleton_id, chapter_code, display_year, season, day_label)
VALUES (1, 'chapter-01', '永熙七年', 'spring', '三月初七')
ON CONFLICT (singleton_id) DO NOTHING;

INSERT INTO game.currencies(code, display_name)
VALUES ('silver', '銀兩')
ON CONFLICT (code) DO NOTHING;

COMMIT;
