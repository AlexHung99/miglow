-- 宮闈浮生規則種子資料 v1.1
-- Prerequisite: schema_v1.1.sql
-- Source details and confirmed 400 annual stipend correction: rank_catalog_v1.1.md
-- Idempotent baseline: initial silver/prestige/favor are 0; no global action-point setting exists.

BEGIN;

INSERT INTO game.ability_label_definitions
    (ability_code, min_value, max_value, display_label, sort_order)
VALUES
    ('vitality',0,0,'逝世',0),('vitality',1,99,'病態',1),
    ('vitality',100,199,'嬌弱',2),('vitality',200,399,'無恙',3),
    ('vitality',400,599,'康健',4),('vitality',600,799,'強健',5),
    ('vitality',800,1000,'強韌',6),
    ('strategy',0,0,'未定',0),('strategy',1,99,'單純',1),
    ('strategy',100,199,'直率',2),('strategy',200,399,'世故',3),
    ('strategy',400,599,'善謀',4),('strategy',600,799,'高深',5),
    ('strategy',800,1000,'莫測',6),
    ('appearance',0,0,'未定',0),('appearance',1,99,'醜儀',1),
    ('appearance',100,199,'清秀',2),('appearance',200,399,'端美',3),
    ('appearance',400,599,'花顏',4),('appearance',600,799,'國色',5),
    ('appearance',800,1000,'絕世',6),
    ('luck',0,0,'未定',0),('luck',1,99,'霉運',1),
    ('luck',100,199,'如願',2),('luck',200,399,'如意',3),
    ('luck',400,599,'福澤',4),('luck',600,799,'祥瑞',5),
    ('luck',800,1000,'鴻運',6)
ON CONFLICT (ability_code, min_value) DO UPDATE SET
    max_value=EXCLUDED.max_value,
    display_label=EXCLUDED.display_label,
    sort_order=EXCLUDED.sort_order,
    is_active=true;

INSERT INTO game.world_locations
    (code, display_name, description, image_url, map_x, map_y, access_rules, sort_order, is_active)
VALUES
    ('neiwufu','內務府','玩家可查看管理團隊與執事分工；管理功能仍受後端權限保護。','/gongwei/assets/map-v2/place-neiwufu-v2.webp',50,12,'{"public":true}'::jsonb,10,true),
    ('fengtian','奉天樓','祈福與公開儀典之地。','/gongwei/assets/map-v2/place-fengtian-v2.webp',76,30,'{"public":true}'::jsonb,20,true),
    ('yueshu','閱書院','研讀四書五經並依已發布規則提升心計。','/gongwei/assets/map-v2/place-yueshu-v2.webp',25,34,'{"public":true}'::jsonb,30,true),
    ('taiyi','太醫院','請平安脈、治療與體質相關玩法。','/gongwei/assets/map-v2/place-taiyi-v3.webp',18,66,'{"public":true}'::jsonb,40,true),
    ('guanxian','觀仙台','太液池、御花園與上林苑共享抽籤額度。','/gongwei/assets/map-v2/place-guanxian-v2.webp',52,50,'{"public":true}'::jsonb,50,true),
    ('cangshu','藏書閣','抽取自戲題目並連結事件投稿審核。','/gongwei/assets/map-v2/place-cangshu-v2.webp',80,67,'{"public":true}'::jsonb,60,true),
    ('market','宮市','使用銀兩購買六類宮市道具。','/gongwei/assets/map-v2/place-market-v1.webp',32,83,'{"public":true}'::jsonb,70,true),
    ('npc-archive','宮中人物','查看已發布 NPC 的人物資料與個人故事。','/gongwei/assets/npc-redrawn/lan-ronghua-v4.webp',50,89,'{"public":true}'::jsonb,80,true)
ON CONFLICT (code) DO UPDATE SET
    display_name=EXCLUDED.display_name,
    description=EXCLUDED.description,
    image_url=EXCLUDED.image_url,
    map_x=EXCLUDED.map_x,
    map_y=EXCLUDED.map_y,
    access_rules=EXCLUDED.access_rules,
    sort_order=EXCLUDED.sort_order,
    is_active=EXCLUDED.is_active;

CREATE TEMP TABLE grade_seed (
    grade_code varchar(20) PRIMARY KEY,
    ordinal integer NOT NULL,
    prestige_required bigint NOT NULL,
    source_annual_stipend bigint NOT NULL,
    monthly_stipend bigint NOT NULL
) ON COMMIT DROP;

INSERT INTO grade_seed VALUES
('皇超品',1,15000,1000,83),('側超品',2,12000,800,66),
('正一品',3,10000,700,58),('側一品',4,9000,700,58),('從一品',5,8500,700,58),
('正二品',6,8000,700,58),('側二品',7,7500,700,58),('從二品',8,7000,700,58),
('正三品',9,6500,600,50),('側三品',10,6000,600,50),('從三品',11,5600,600,50),
('正四品',12,5300,600,50),('側四品',13,5000,600,50),('從四品',14,4600,600,50),
('正五品',15,4300,500,41),('側五品',16,4000,500,41),('從五品',17,3600,500,41),
('正六品',18,3300,500,41),('側六品',19,3000,500,41),('從六品',20,2600,500,41),
('正七品',21,2300,400,33),('側七品',22,2000,400,33),('從七品',23,1800,400,33),
('正八品',24,1600,400,33),('側八品',25,1400,400,33),
('從八品',26,1200,300,25),('正九品',27,1000,300,25),('側九品',28,800,300,25),('從九品',29,0,300,25);

CREATE TEMP TABLE title_group_seed (
    applies_to_role varchar(20) NOT NULL,
    grade_code varchar(20) NOT NULL,
    titles text[] NOT NULL
) ON COMMIT DROP;

INSERT INTO title_group_seed VALUES
('consort','皇超品',ARRAY['皇后']),('consort','側超品',ARRAY['皇貴妃']),
('consort','正一品',ARRAY['聖貴妃','御貴妃','尊貴妃','榮貴妃']),
('consort','側一品',ARRAY['淑貴妃','嘉貴妃','賢貴妃','德貴妃']),('consort','從一品',ARRAY['貴妃']),
('consort','正二品',ARRAY['皇夫人','聖夫人','御夫人','尊夫人']),
('consort','側二品',ARRAY['淑夫人','嘉夫人','賢夫人','德夫人']),('consort','從二品',ARRAY['夫人']),
('consort','正三品',ARRAY['皇妃','聖妃','御妃','尊妃']),
('consort','側三品',ARRAY['淑妃','嘉妃','賢妃','德妃']),('consort','從三品',ARRAY['妃']),
('consort','正四品',ARRAY['昭儀','昭容','昭華','昭妤']),('consort','側四品',ARRAY['淑儀','淑容','淑華','淑妤']),
('consort','從四品',ARRAY['德儀','德容','德華','德妤']),('consort','正五品',ARRAY['皇貴嬪','御貴嬪','尊貴嬪','聖貴嬪']),
('consort','側五品',ARRAY['貴嬪']),('consort','從五品',ARRAY['淑嬪','嘉嬪','賢嬪','德嬪']),
('consort','正六品',ARRAY['嬪']),('consort','側六品',ARRAY['容華']),('consort','從六品',ARRAY['婕妤']),
('consort','正七品',ARRAY['皇貴姬','貴姬','尊姬','御姬']),('consort','側七品',ARRAY['姬']),
('consort','從七品',ARRAY['皇貴儀','貴儀','御儀','尊儀']),('consort','正八品',ARRAY['貴人','美人','良人','佳人']),
('consort','側八品',ARRAY['儀婉','佳婉','容婉']),('consort','從八品',ARRAY['常在']),
('consort','正九品',ARRAY['良女']),('consort','側九品',ARRAY['娘子','選侍']),('consort','從九品',ARRAY['答應']),

('prince','皇超品',ARRAY['聖御皇太子']),('prince','側超品',ARRAY['輔鏵皇子']),
('prince','正一品',ARRAY['尊皇子','御皇子','聖皇子','容皇子']),('prince','側一品',ARRAY['書皇子','嘉皇子','咸皇子','德皇子']),
('prince','從一品',ARRAY['華揚皇子']),('prince','正二品',ARRAY['清雁皇子','清譽皇子','清卿皇子']),
('prince','側二品',ARRAY['栖鑾皇子','栖允皇子','栖昭皇子']),('prince','從二品',ARRAY['浯耀皇子']),
('prince','正三品',ARRAY['璟徽皇子','璟靖皇子','璟淶皇子']),('prince','側三品',ARRAY['羅澐皇子','羅詒皇子','羅昭皇子']),
('prince','從三品',ARRAY['濋凌皇子']),('prince','正四品',ARRAY['烯和皇子','烯慶皇子','烯鏵皇子']),
('prince','側四品',ARRAY['恭奕皇子','恭景皇子','恭錫皇子']),('prince','從四品',ARRAY['晼元皇子','晼濚皇子','晼言皇子']),
('prince','正五品',ARRAY['羅霙皇子','鶉洏皇子','檜毅皇子','翊嵦皇子']),('prince','側五品',ARRAY['鶄槤皇子']),
('prince','從五品',ARRAY['樂康皇子','樂僭皇子','樂安皇子','樂衻皇子']),('prince','正六品',ARRAY['余澅皇子']),
('prince','側六品',ARRAY['鵇箹皇子']),('prince','從六品',ARRAY['矜才皇子']),
('prince','正七品',ARRAY['長順皇子','長安皇子','長仕皇子','長諒皇子']),('prince','側七品',ARRAY['宣瀚皇子']),
('prince','從七品',ARRAY['澅禦皇子','澅慶皇子','澅丞皇子','澅染皇子']),('prince','正八品',ARRAY['雁肅皇子','雁德皇子','雁暘皇子','雁南皇子']),
('prince','側八品',ARRAY['琼泧皇子','琼濂皇子','琼澲皇子']),('prince','從八品',ARRAY['伊景皇子']),
('prince','正九品',ARRAY['謙恭皇子']),('prince','側九品',ARRAY['沅桉皇子','沅晉皇子']),('prince','從九品',ARRAY['皇子']),

('princess','皇超品',ARRAY['聖御皇太女']),('princess','側超品',ARRAY['輔華太女']),
('princess','正一品',ARRAY['尊帝姬','御帝姬','聖帝姬','榮帝姬']),('princess','側一品',ARRAY['淑帝姬','嘉帝姬','賢帝姬','德帝姬']),
('princess','從一品',ARRAY['華陽帝姬']),('princess','正二品',ARRAY['清雁帝姬','清玉帝姬','清盈帝姬']),
('princess','側二品',ARRAY['栖鑾帝姬','栖云帝姬','栖昭帝姬']),('princess','從二品',ARRAY['娪月帝姬']),
('princess','正三品',ARRAY['錦徽帝姬','錦曦帝姬','錦羅帝姬']),('princess','側三品',ARRAY['羅云帝姬','羅儀帝姬','羅昭帝姬']),
('princess','從三品',ARRAY['楚綾帝姬']),('princess','正四品',ARRAY['烯和帝姬','烯慶帝姬','烯鏵帝姬']),
('princess','側四品',ARRAY['恭儀帝姬','恭寧帝姬','恭舒帝姬']),('princess','從四品',ARRAY['晼媛帝姬','晼盈帝姬','晼樺帝姬']),
('princess','正五品',ARRAY['碧羅帝姬','碧純帝姬','碧惠帝姬','碧翊帝姬']),('princess','側五品',ARRAY['婧晞帝姬']),
('princess','從五品',ARRAY['樂康帝姬','樂成帝姬','樂寧帝姬','樂晴帝姬']),('princess','正六品',ARRAY['渝嫿帝姬']),
('princess','側六品',ARRAY['姩枂帝姬']),('princess','從六品',ARRAY['㮗嫄帝姬']),
('princess','正七品',ARRAY['長順帝姬','長寧帝姬','長諼帝姬','長傾帝姬']),('princess','側七品',ARRAY['宣嬅帝姬']),
('princess','從七品',ARRAY['嫿愉帝姬','嫿清帝姬','嫿珹帝姬','嫿儀帝姬']),('princess','正八品',ARRAY['雁詩帝姬','雁瑤帝姬','雁璇帝姬','雁琼帝姬']),
('princess','側八品',ARRAY['琼毓帝姬','琼泠帝姬','琼嬅帝姬']),('princess','從八品',ARRAY['瑟紜帝姬']),
('princess','正九品',ARRAY['謙恭帝姬']),('princess','側九品',ARRAY['沅華帝姬','沅絪帝姬']),('princess','從九品',ARRAY['帝姬']);

CREATE TEMP TABLE rank_seed AS
SELECT
    g.applies_to_role,
    g.grade_code,
    s.ordinal,
    s.prestige_required,
    s.source_annual_stipend,
    s.monthly_stipend,
    title AS display_name,
    NULL::integer AS capacity,
    false AS is_lead,
    false AS is_application_option,
    NULL::jsonb AS initial_stats,
    jsonb_build_object('source', 'rank_catalog_v1.1') AS promotion_rules
FROM title_group_seed g
JOIN grade_seed s USING (grade_code)
CROSS JOIN LATERAL unnest(g.titles) AS title;

UPDATE rank_seed SET is_lead = true WHERE display_name IN (
    '皇后','皇貴妃','聖貴妃','淑貴妃','皇夫人','淑夫人','皇妃','淑妃','昭儀','皇貴嬪','淑嬪','皇貴姬','皇貴儀','貴人',
    '聖御皇太子','輔鏵皇子','尊皇子','書皇子','清雁皇子','栖鑾皇子','璟徽皇子','羅澐皇子','烯和皇子','羅霙皇子','樂康皇子','長順皇子','澅禦皇子','雁肅皇子',
    '聖御皇太女','輔華太女','尊帝姬','淑帝姬','清雁帝姬','栖鑾帝姬','錦徽帝姬','羅云帝姬','烯和帝姬','碧羅帝姬','樂康帝姬','長順帝姬','嫿愉帝姬','雁詩帝姬'
);

-- Explicit one-per-title groups.
UPDATE rank_seed SET capacity = 1
WHERE grade_code IN ('皇超品','側超品','正一品','側一品','正二品','側二品','正三品','側三品');
UPDATE rank_seed SET capacity = 2 WHERE applies_to_role='consort' AND display_name='貴妃';
UPDATE rank_seed SET capacity = 3 WHERE display_name IN ('夫人','華揚皇子','浯耀皇子','華陽帝姬','娪月帝姬');
UPDATE rank_seed SET capacity = 4 WHERE display_name IN ('妃','濋凌皇子','楚綾帝姬');
UPDATE rank_seed SET capacity = 1 WHERE display_name IN ('昭儀','烯和皇子','烯和帝姬');

CREATE TEMP TABLE initial_seed (
    applies_to_role varchar(20), display_name text,
    vitality integer, appearance integer, strategy integer, luck integer
) ON COMMIT DROP;
INSERT INTO initial_seed VALUES
('consort','良女',500,400,500,100),('consort','娘子',400,330,400,80),
('consort','選侍',300,280,300,50),('consort','答應',200,250,200,30),
('princess','謙恭帝姬',500,400,500,100),('princess','沅華帝姬',400,330,400,80),
('princess','沅絪帝姬',300,280,300,50),('princess','帝姬',200,250,200,30),
('prince','謙恭皇子',500,400,500,100),('prince','沅桉皇子',400,330,400,80),
('prince','沅晉皇子',300,280,300,50),('prince','皇子',200,250,200,30);

UPDATE rank_seed r
SET is_application_option = true,
    initial_stats = jsonb_build_object('vitality',i.vitality,'appearance',i.appearance,
                                       'strategy',i.strategy,'luck',i.luck,
                                       'prestige',0,'favor',0,'silver',0)
FROM initial_seed i
WHERE r.applies_to_role=i.applies_to_role AND r.display_name=i.display_name;

CREATE TEMP TABLE requirement_seed (
    applies_to_role varchar(20), display_name text,
    vitality integer, appearance integer, strategy integer, luck integer,
    settled_event_count integer, weekly_messages integer, self_play_words integer
) ON COMMIT DROP;

INSERT INTO requirement_seed VALUES
('consort','皇后',1000,1000,1000,900,3,300,500),('consort','皇貴妃',1000,1000,1000,800,2,250,400),
('consort','聖貴妃',1000,1000,900,700,1,250,NULL),('consort','御貴妃',1000,1000,850,600,1,250,NULL),
('consort','尊貴妃',1000,1000,830,550,1,250,NULL),('consort','榮貴妃',1000,1000,800,500,1,250,NULL),
('consort','淑貴妃',1000,1000,750,450,NULL,NULL,NULL),('consort','嘉貴妃',1000,1000,700,400,NULL,NULL,NULL),
('consort','賢貴妃',1000,1000,650,350,NULL,NULL,NULL),('consort','德貴妃',1000,1000,600,300,NULL,NULL,NULL),
('consort','貴妃',1000,1000,550,NULL,NULL,NULL,NULL),
('consort','皇夫人',950,950,500,NULL,NULL,NULL,NULL),('consort','聖夫人',900,900,450,NULL,NULL,NULL,NULL),
('consort','御夫人',850,850,400,NULL,NULL,NULL,NULL),('consort','尊夫人',800,800,350,NULL,NULL,NULL,NULL),
('consort','淑夫人',750,750,NULL,NULL,NULL,NULL,NULL),('consort','嘉夫人',700,700,NULL,NULL,NULL,NULL,NULL),
('consort','賢夫人',650,650,NULL,NULL,NULL,NULL,NULL),('consort','德夫人',600,600,NULL,NULL,NULL,NULL,NULL),
('consort','夫人',550,550,NULL,NULL,NULL,NULL,NULL),('consort','皇妃',500,500,NULL,NULL,NULL,NULL,NULL),
('consort','聖妃',450,450,NULL,NULL,NULL,NULL,NULL),('consort','御妃',400,400,NULL,NULL,NULL,NULL,NULL),
('consort','尊妃',350,350,NULL,NULL,NULL,NULL,NULL),

('prince','聖御皇太子',1000,1000,1000,1000,3,300,500),('prince','輔鏵皇子',1000,1000,1000,1000,2,250,300),
('prince','尊皇子',1000,1000,900,900,NULL,NULL,NULL),('prince','御皇子',1000,1000,850,800,NULL,NULL,NULL),
('prince','聖皇子',1000,1000,800,700,NULL,NULL,NULL),('prince','容皇子',1000,1000,750,600,NULL,NULL,NULL),
('prince','書皇子',1000,1000,700,NULL,NULL,NULL,NULL),('prince','嘉皇子',950,950,650,NULL,NULL,NULL,NULL),
('prince','咸皇子',900,900,600,NULL,NULL,NULL,NULL),('prince','德皇子',850,850,550,NULL,NULL,NULL,NULL),
('prince','華揚皇子',800,800,500,NULL,NULL,NULL,NULL),('prince','清雁皇子',750,750,400,NULL,NULL,NULL,NULL),
('prince','清譽皇子',700,700,350,NULL,NULL,NULL,NULL),('prince','清卿皇子',650,650,300,NULL,NULL,NULL,NULL),
('prince','栖鑾皇子',600,600,NULL,NULL,NULL,NULL,NULL),('prince','栖允皇子',550,550,NULL,NULL,NULL,NULL,NULL),
('prince','栖昭皇子',500,500,NULL,NULL,NULL,NULL,NULL),('prince','浯耀皇子',500,500,NULL,NULL,NULL,NULL,NULL),
('prince','璟徽皇子',450,450,NULL,NULL,NULL,NULL,NULL),('prince','璟靖皇子',450,450,NULL,NULL,NULL,NULL,NULL),
('prince','璟淶皇子',400,400,NULL,NULL,NULL,NULL,NULL),

('princess','聖御皇太女',1000,1000,1000,1000,3,300,500),('princess','輔華太女',1000,1000,1000,1000,2,250,300),
('princess','尊帝姬',1000,1000,900,900,1,250,NULL),('princess','御帝姬',1000,1000,850,800,1,250,NULL),
('princess','聖帝姬',1000,1000,800,700,1,250,NULL),('princess','榮帝姬',1000,1000,750,600,1,250,NULL),
('princess','淑帝姬',1000,1000,700,NULL,NULL,NULL,NULL),('princess','嘉帝姬',950,950,650,NULL,NULL,NULL,NULL),
('princess','賢帝姬',900,900,600,NULL,NULL,NULL,NULL),('princess','德帝姬',850,850,550,NULL,NULL,NULL,NULL),
('princess','華陽帝姬',800,800,500,NULL,NULL,NULL,NULL),('princess','清雁帝姬',750,750,400,NULL,NULL,NULL,NULL),
('princess','清玉帝姬',700,700,350,NULL,NULL,NULL,NULL),('princess','清盈帝姬',650,650,300,NULL,NULL,NULL,NULL),
('princess','栖鑾帝姬',600,600,NULL,NULL,NULL,NULL,NULL),('princess','栖云帝姬',550,550,NULL,NULL,NULL,NULL,NULL),
('princess','栖昭帝姬',500,500,NULL,NULL,NULL,NULL,NULL),('princess','娪月帝姬',450,450,NULL,NULL,NULL,NULL,NULL),
('princess','錦徽帝姬',400,400,NULL,NULL,NULL,NULL,NULL),('princess','錦曦帝姬',350,350,NULL,NULL,NULL,NULL,NULL),
('princess','錦羅帝姬',300,300,NULL,NULL,NULL,NULL,NULL);

UPDATE rank_seed r
SET promotion_rules = jsonb_strip_nulls(jsonb_build_object(
    'source','rank_catalog_v1.1',
    'vitality',q.vitality,'appearance',q.appearance,'strategy',q.strategy,'luck',q.luck,
    'settledEventCount',q.settled_event_count,'weeklyMessages',q.weekly_messages,
    'selfPlayWords',q.self_play_words,
    'requiresAll',true
))
FROM requirement_seed q
WHERE r.applies_to_role=q.applies_to_role AND r.display_name=q.display_name;

UPDATE rank_seed
SET promotion_rules = promotion_rules || '{"requiresNoEmpress":true}'::jsonb
WHERE applies_to_role='consort' AND display_name='皇貴妃';

INSERT INTO game.ranks(
    code, display_name, applies_to_role, grade_code, ordinal, prestige_required,
    monthly_stipend, source_annual_stipend, capacity, is_lead,
    is_application_option, initial_stats, promotion_rules, is_active
)
SELECT
    applies_to_role || '-' || ordinal || '-' || substr(md5(display_name),1,12),
    display_name, applies_to_role, grade_code, ordinal, prestige_required,
    monthly_stipend, source_annual_stipend, capacity, is_lead,
    is_application_option, initial_stats, promotion_rules, true
FROM rank_seed
ON CONFLICT (applies_to_role, display_name) DO UPDATE SET
    grade_code=EXCLUDED.grade_code,
    ordinal=EXCLUDED.ordinal,
    prestige_required=EXCLUDED.prestige_required,
    monthly_stipend=EXCLUDED.monthly_stipend,
    source_annual_stipend=EXCLUDED.source_annual_stipend,
    capacity=EXCLUDED.capacity,
    is_lead=EXCLUDED.is_lead,
    is_application_option=EXCLUDED.is_application_option,
    initial_stats=EXCLUDED.initial_stats,
    promotion_rules=EXCLUDED.promotion_rules,
    is_active=true;

-- Settings require a real actor for provenance. A fresh environment must first run the
-- one-time AdminCli bootstrap documented in README_v1.1.md; never seed a fake admin.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM game.admin_role_assignments a
        WHERE a.role = 'super_admin'
          AND (a.expires_at IS NULL OR a.expires_at > now())
    ) THEN
        RAISE EXCEPTION
            'seed_rules_v1.1 requires an active super_admin; complete the AdminCli bootstrap first'
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

-- Public support link: keep disabled until a creator-specific URL is configured.
-- The API validates https://buymeacoffee.com/<creator> and never stores payment data.
INSERT INTO game.game_settings(
    setting_key, category, description, published_value, draft_value,
    validation_schema, risk_level, is_public, updated_by
)
SELECT
    'support.buy_me_a_coffee', 'support', 'Buy Me a Coffee 外部贊助按鈕設定',
    '{"enabled":false,"url":null,"label":"請我們喝杯咖啡"}'::jsonb, NULL,
    '{"type":"object","required":["enabled","label"],"properties":{"enabled":{"type":"boolean"},"url":{"type":["string","null"],"pattern":"^https://buymeacoffee\\.com/[A-Za-z0-9._-]+/?$"},"label":{"type":"string","minLength":1,"maxLength":30}},"additionalProperties":false}'::jsonb,
    'normal', true, u.id
FROM game.users u
JOIN game.admin_role_assignments a ON a.user_id=u.id AND a.role='super_admin'
ORDER BY a.granted_at
LIMIT 1
ON CONFLICT (setting_key) DO NOTHING;

-- Published reproduction defaults. Publishing this High Risk setting also updates
-- the reproduction_control projection and increments rules_version in one transaction.
INSERT INTO game.game_settings(
    setting_key, category, description, published_value, draft_value,
    validation_schema, risk_level, is_public, updated_by
)
SELECT
    'reproduction.rules', 'reproduction', '侍寢受孕、妊娠天數與流產模式',
    '{"conceptionRatePercent":100,"pregnancyDurationDays":10,"miscarriageMode":"event_only","miscarriageRules":{"baseRatePercent":0}}'::jsonb,
    NULL,
    '{"type":"object","required":["conceptionRatePercent","pregnancyDurationDays","miscarriageMode","miscarriageRules"],"properties":{"conceptionRatePercent":{"type":"integer","minimum":0,"maximum":100},"pregnancyDurationDays":{"type":"integer","minimum":1,"maximum":365},"miscarriageMode":{"type":"string","enum":["disabled","event_only","threshold","daily_probability"]},"miscarriageRules":{"type":"object"}},"additionalProperties":false}'::jsonb,
    'high', true, u.id
FROM game.users u
JOIN game.admin_role_assignments a ON a.user_id=u.id AND a.role='super_admin'
ORDER BY a.granted_at
LIMIT 1
ON CONFLICT (setting_key) DO NOTHING;

COMMIT;
