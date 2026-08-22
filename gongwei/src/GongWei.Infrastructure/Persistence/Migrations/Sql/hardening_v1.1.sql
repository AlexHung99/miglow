-- =====================================================================
--  Hardening for schema v1.1 — everything the EF model cannot express.
--
--  Copied verbatim from db/authoritative/v1.1/schema_v1.1.sql. Embedded as a
--  resource and executed by SqlHardeningV11 so the two can be diffed directly
--  instead of being retyped into C# string literals each time.
--
--  Contents:
--    * touch_updated_at        bumps updated_at + version on UPDATE
--    * reject_mutation         append-only guard (UPDATE and DELETE)
--    * reject_deletion         no-delete guard for event text
--    * 8 cross-table validation functions and their triggers
--    * 32 touch triggers, 16 append-only triggers, 1 no-delete trigger
--    * the two singleton control rows and the base currency
-- =====================================================================

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

-- ---------------------------------------------------------------------
-- Cross-table integrity checks for the highest-risk rules
-- ---------------------------------------------------------------------

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
    ELSIF NEW.player_portrait_submission_id IS NOT NULL THEN
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
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_births_validate_selection
    BEFORE INSERT ON game.births
    FOR EACH ROW EXECUTE FUNCTION game.validate_birth_selection();

-- ---------------------------------------------------------------------
-- Append-only tables. Permanent retention; no purge job or delete API.
-- ---------------------------------------------------------------------

DO $$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'audit_logs', 'ledger_entries', 'inventory_transactions',
        'character_application_revisions', 'character_status_history', 'rank_history',
        'game_setting_revisions', 'npc_revisions', 'character_chronicle_entries',
        'event_post_revisions', 'event_results', 'births',
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

-- Event text may change status but can never be deleted.
CREATE TRIGGER tr_event_posts_no_delete
    BEFORE DELETE ON game.event_posts
    FOR EACH ROW EXECUTE FUNCTION game.reject_deletion();

-- ---------------------------------------------------------------------
-- Automatic updated_at/version. Tables intentionally excluded are append-only.
-- ---------------------------------------------------------------------

DO $$
DECLARE
    table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'users', 'admin_role_assignments', 'preset_portraits', 'media_assets', 'player_portrait_submissions',
        'character_applications', 'ranks', 'character_title_definitions', 'residences',
        'characters', 'character_title_assignments', 'character_stats',
        'ability_label_definitions', 'character_progress',
        'world_state', 'game_settings', 'world_locations', 'npcs', 'event_rooms', 'event_posts',
        'external_play_submissions', 'wallets', 'inventory_entries', 'market_offers',
        'reproduction_control', 'heir_wait_pool_entries', 'pregnancies',
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

COMMENT ON TABLE game.audit_logs IS 'Append-only. Permanent retention; no purge job or delete API.';
COMMENT ON TABLE game.event_posts IS 'Drafts and submitted/approved event text are retained permanently.';
COMMENT ON TABLE game.event_post_revisions IS 'Append-only event text history retained permanently; not public.';
COMMENT ON TABLE game.deaths IS 'Permanent character death record; visible through admin history only where linkage is private.';

-- ---------------------------------------------------------------------
-- Singleton control rows and the base currency
-- ---------------------------------------------------------------------

INSERT INTO game.reproduction_control(singleton_id, is_open)
VALUES (1, true)
ON CONFLICT (singleton_id) DO NOTHING;

INSERT INTO game.world_state(singleton_id, era_code, display_year, season, day_label)
VALUES (1, 'yongxi-07', '永熙七年', 'spring', '三月初七')
ON CONFLICT (singleton_id) DO NOTHING;

INSERT INTO game.currencies(code, display_name)
VALUES ('silver', '銀兩')
ON CONFLICT (code) DO NOTHING;
