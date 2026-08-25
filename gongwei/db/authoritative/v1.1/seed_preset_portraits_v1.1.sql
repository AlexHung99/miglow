-- Official preset portraits for the character application picker.
-- Idempotent: only inserts missing codes and never overwrites CMS changes.

BEGIN;

INSERT INTO game.preset_portraits
    (id, code, role, display_name, asset_url, thumbnail_url, sort_order, is_active, metadata)
VALUES
    ('0df50c9c-4dc8-4b20-b55a-35db7f32ec02', 'official-consort-02', 'consort', '青玉蘭',
     'https://miglow.vip/gongwei/assets/portrait-consort-2.webp',
     'https://miglow.vip/gongwei/assets/portrait-consort-2.webp', 20, true,
     '{"palette":"celadon_ivory","source":"official_generated"}'::jsonb),
    ('c98260ce-6154-48ec-97c0-ec7fb2bcab03', 'official-consort-03', 'consort', '暮荷',
     'https://miglow.vip/gongwei/assets/portrait-consort-3.webp',
     'https://miglow.vip/gongwei/assets/portrait-consort-3.webp', 30, true,
     '{"palette":"plum_blue_gray","source":"official_generated"}'::jsonb),
    ('7d80c427-e941-42ca-8cbe-bb130f4a9402', 'official-prince-02', 'prince', '雲嶺',
     'https://miglow.vip/gongwei/assets/portrait-prince-2.webp',
     'https://miglow.vip/gongwei/assets/portrait-prince-2.webp', 20, true,
     '{"palette":"moon_white_mist_blue","source":"official_generated"}'::jsonb),
    ('e3b2965c-d7c9-4501-a8ca-abcf4d7e0f03', 'official-prince-03', 'prince', '丹闕',
     'https://miglow.vip/gongwei/assets/portrait-prince-3.webp',
     'https://miglow.vip/gongwei/assets/portrait-prince-3.webp', 30, true,
     '{"palette":"burgundy_ink_gold","source":"official_generated"}'::jsonb),
    ('92b09ddf-a7f8-4a69-9302-ace594042302', 'official-princess-02', 'princess', '霽梅',
     'https://miglow.vip/gongwei/assets/portrait-princess-2.webp',
     'https://miglow.vip/gongwei/assets/portrait-princess-2.webp', 20, true,
     '{"palette":"sky_blue_lavender","source":"official_generated"}'::jsonb),
    ('47e0929f-4139-44e4-bfab-83dc04652203', 'official-princess-03', 'princess', '春棠',
     'https://miglow.vip/gongwei/assets/portrait-princess-3.webp',
     'https://miglow.vip/gongwei/assets/portrait-princess-3.webp', 30, true,
     '{"palette":"apricot_yellow_green","source":"official_generated"}'::jsonb)
ON CONFLICT (code) DO NOTHING;

COMMIT;
