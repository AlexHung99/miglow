-- 宮闈浮生 NPC 初始內容 v1.1
-- Prerequisite: schema_v1.1.sql, seed_rules_v1.1.sql and an active super_admin.
-- Source: frontend/src/data.ts and the reviewed v1.0 game specification.
-- This bootstrap seed inserts missing NPC codes only. It never overwrites later CMS edits.

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM game.admin_role_assignments
        WHERE role = 'super_admin' AND (expires_at IS NULL OR expires_at > now())
    ) THEN
        RAISE EXCEPTION
            'seed_npcs_v1.1 requires an active super_admin; complete the AdminCli bootstrap first'
            USING ERRCODE = 'P0001';
    END IF;
END;
$$;

CREATE TEMP TABLE npc_seed (
    code varchar(80) PRIMARY KEY,
    display_name varchar(100) NOT NULL,
    title varchar(100) NOT NULL,
    sex varchar(10) NOT NULL,
    summary varchar(1500) NOT NULL,
    story_markdown text NOT NULL,
    portrait_url text NOT NULL,
    public_profile jsonb NOT NULL,
    sort_order integer NOT NULL
) ON COMMIT DROP;

INSERT INTO npc_seed VALUES
(
    'yuzhao-emperor','蕭漌辞','渝昭帝','male',
    '先帝崩逝後以謀略平定朝局、登基為帝；外示仁和，內心深沉，對錦歸情意尤深。',
    $s1$先帝崩逝而儲位未定，諸皇子相爭，朝局動盪。漌辞尚未弱冠便懂得避鋒藏智，聯合朝臣與宗室平定亂局，先立太子，後登帝位。

幼年宮禁森嚴，使他對自由懷有近乎執著的嚮往。微服時遇見不拘禮俗的唐錦歸，彷彿看見自己從未擁有的人生，遂將她迎入東宮。

即位後，他以仁政安天下、以恩賞收攝政之權；表面溫潤，決斷卻從不遲疑。後宮諸人之中，錦歸仍是最能使他暫忘帝王束縛的人。$s1$,
    '/gongwei/assets/npc-redrawn/yuzhao-emperor-v2.webp',
    '{"courtesy":"靖潯","personality":"溫潤如玉、心思深沉","skilled":"謀略、詩詞","unskilled":"書法","likes":"溫順謙和","dislikes":"束縛","rankHistory":"二皇子 → 渝昭帝"}'::jsonb,10
),
(
    'chengzhao-empress-dowager','梁怜卿','珹昭皇太后','female',
    '曾與帝王恩愛甚篤，痛失皇嗣後轉而追求權勢，以縝密謀略掌握後宮。',
    $s2$怜卿由太子妃正位中宮，早年與帝王相敬如賓。新人漸多後，恩情日薄；沅誠六年，她唯一的嫡子又因寵妃設計而未能保住。

喪子使她徹底不再相信情誼。她開始收攬人心、衡量利害，把所有能用之人納入棋局，最終在後宮建立足以與帝王分庭抗禮的權勢。

她常說唯有權力能夠自保，卻也因此再無可以共語之人。夜深獨坐時，她仍會想起那個原本無意爭權的自己。$s2$,
    '/gongwei/assets/npc-redrawn/chengzhao-empress-dowager-v2.webp',
    '{"courtesy":"知微","personality":"位高權重、心機深沉","skilled":"擅權、偽裝","unskilled":"詩書","likes":"財權、丹青","dislikes":"目無尊卑者","rankHistory":"太子妃 → 皇后 → 皇太后"}'::jsonb,20
),
(
    'jinhui-taifei','君疏鳶','瑾惠太妃','female',
    '早年因盛寵而驕，歷經貶位與禁足後性情漸斂，後將心思寄於琴棋書畫。',
    $s3$疏鳶以顯赫家世與絕世姿容入宮，初封貴嬪，很快因盛寵晉為妃。恩寵使她目中無人，甚至為嫉妒設計傷害皇后的皇嗣。

事情敗露後，皇帝念舊只褫奪封號、降為昭容並禁足三月。漫長禁足讓她第一次看清，自己雖握有高位，卻已失盡人心。

復封御妃後，她不再像從前般爭鋒。帝寵漸淡，她反而在琴聲、詩書與花木之間得到安穩；成為太妃後，她把昔日榮辱都收進沉默，只留下旁人對美貌、謀略與自毀的不同評說。$s3$,
    '/gongwei/assets/npc-redrawn/jinhui-taifei-v5.webp',
    '{"courtesy":"映嫿","personality":"溫柔婉約、不問世事","skilled":"琴藝、詩書","unskilled":"騎射、冷嘲熱諷","likes":"蒔花","dislikes":"踰矩","rankHistory":"琦貴嬪 → 琦妃 → 君昭容 → 儀御妃 → 瑾惠太妃","portraitDirection":"黑紅配色、自然成年女性形象，不刻意高齡化"}'::jsonb,30
),
(
    'jia-fei','虞綰今','嘉妃','female',
    '家世顯赫且行止端方，曾深受太子寵愛；錦歸入宮後逐漸失寵，表面淡然，野心仍未泯滅。',
    $s4$綰今出身高門，以側福晉身份進入東宮，最初與太子恩愛有加，被視為端方賢淑的良配。

唐錦歸入宮後，太子的心逐漸偏向新人。綰今曾試圖挽回，卻發現家世與禮法都敵不過君王的一念。

漌辞登基後，她被封嘉妃，也學會以詩書自遣。旁人以為她已放下，她卻只說自己的野心從未消失，只是時機尚未到來。$s4$,
    '/gongwei/assets/npc-redrawn/jia-fei-v2.webp',
    '{"courtesy":"歸意","personality":"進退有度、平易近人","skilled":"琵琶、舞藝","unskilled":"書法、笛","likes":"荷花、音律","dislikes":"虛情假意","rankHistory":"側福晉 → 嘉妃","stats":{"vitality":810,"strategy":481,"luck":584,"appearance":463}}'::jsonb,40
),
(
    'yi-fei','南司韞','禕妃','female',
    '出身百年簪纓世族，禮儀無懈可擊；因昔日婚事與馥錦決裂，入宮後孤高自持。',
    $s5$司韞出身百年簪纓世族，自幼與陸馥錦同窗，情同手足。及笄時，她原將嫁給青梅竹馬靳氏，婚事卻被馥錦暗中破壞。

不知靳家真實圖謀的司韞，只認定摯友背叛了自己。兩人一同入東宮後，她以冷言與欺辱報復，拒絕馥錦所有修好的可能。

太子即位後，她憑家世封妃，既不爭寵，也不親近旁人。她像一座不可攀的孤峰，以詩書自娛，卻始終放不下那場失去婚姻與摯友的舊恨。$s5$,
    '/gongwei/assets/npc-redrawn/yi-fei-v2.webp',
    '{"courtesy":"袼姝","personality":"清冷孤傲、高貴自信","skilled":"撫琴","unskilled":"飲酒","likes":"吟詩、遊玩","dislikes":"無理取鬧者、甜食","rankHistory":"側福晉 → 禕妃","stats":{"vitality":399,"strategy":756,"luck":621,"appearance":560}}'::jsonb,50
),
(
    'jinsheng-deyu','唐錦歸','錦笙德妤','female',
    '出身寒微而姿容出眾，深得渝昭帝寵愛；在宮中積極鞏固母家與自身地位。',
    $s6$錦歸在郊外偶遇微服的太子，以不受宮規束縛的靈動吸引了他。進入東宮後，她很快明白恩寵就是立足之本，遂與原本得寵的虞綰今暗中相爭。

漌辞登基後本想封她為貴妃，卻因她家世寒微遭群臣反對，只能先抬舉唐氏母家。

黎氏叛亂時，唐家領兵平亂有功，她終於名正言順晉為德妤。地位雖穩，她對出身的不足仍耿耿於懷，持續扶持宗族，為自己打造更牢固的根基。$s6$,
    '/gongwei/assets/npc-redrawn/jinsheng-deyu-v2.webp',
    '{"courtesy":"嶠兮","personality":"嬌矜嫵媚、城府深沉","skilled":"調香、釀酒","unskilled":"鑑玉","likes":"書法、冰嬉","dislikes":"烹飪","rankHistory":"格格 → 錦笙御貴嬪 → 錦笙德妤","stats":{"vitality":380,"strategy":700,"luck":300,"appearance":850}}'::jsonb,60
),
(
    'lan-ronghua','陸馥錦','嵐容華','female',
    '為保護摯友而暗中破壞一場危險婚事，卻因此被南司韞視為仇敵；入宮後承受誤解，始終沒有說出真相。',
    $s7$馥錦與南司韞自幼相交，被稱為佳友雙璧。司韞將嫁靳氏時，馥錦意外得知靳家只圖南家嫁妝，事後還準備以惡名休妻。

她不願摯友受辱，便暗中設局阻斷婚事，卻沒有把真相說出口。司韞因此認定她毀去良緣；兩人又同時被選入東宮，昔日情誼自此變成冷眼與報復。

馥錦默默承受司韞的怨恨，也逐漸明白沉默同樣會傷人。她不爭寵、不示弱，把未能說出的歉意藏在戲曲與花木之中；嵐容華的溫婉，並非軟弱，而是在兩難之後仍選擇保有善意。$s7$,
    '/gongwei/assets/npc-redrawn/lan-ronghua-v4.webp',
    '{"courtesy":"卿弦","personality":"溫婉沉靜、獨立自強","skilled":"唱戲、蒔花","unskilled":"謀略","likes":"清淨","dislikes":"張揚跋扈、表裡不一","rankHistory":"侍妾 → 嵐容華","portraitDirection":"黑金暖光原創重繪","stats":{"vitality":658,"strategy":369,"luck":452,"appearance":580}}'::jsonb,70
),
(
    'li-liangren','黎栖璇','黎良人','female',
    '入東宮後始終隱忍自守；母家叛亂使其遭貶與禁足，此後在失勢與孤寂中求存。',
    $s8$栖璇與虞綰今同入東宮，自知家世與容貌都難在眾人中占先，便選擇退讓，不爭不搶地保存自己。

漌辞即位後，她因多年相伴封為淑容。嘉珹二年黎氏叛亂，母家之罪使她被褫奪位號、降為良人並禁足一年。

禁足期滿，她已失去恩寵與倚仗，昔日相識也多半疏遠。她仍以畫與女紅維持清醒，明白後宮無權者如卒，卻不願用卑劣手段換回位置。$s8$,
    '/gongwei/assets/npc-redrawn/li-liangren-v4.webp',
    '{"courtesy":"碧禾","personality":"清醒通透、機敏聰慧","skilled":"畫技、女紅","unskilled":"馬術","likes":"知恩圖報","dislikes":"卑鄙小人","rankHistory":"侍妾 → 穎淑容 → 黎良人","portraitDirection":"青藍銀飾原創重繪","stats":{"vitality":599,"strategy":564,"luck":721,"appearance":700}}'::jsonb,80
);

WITH actor AS (
    SELECT u.id
    FROM game.users u
    JOIN game.admin_role_assignments a ON a.user_id = u.id
    WHERE a.role = 'super_admin'
      AND (a.expires_at IS NULL OR a.expires_at > now())
    ORDER BY a.granted_at, u.id
    LIMIT 1
), location AS (
    SELECT id FROM game.world_locations WHERE code = 'npc-archive'
)
INSERT INTO game.npcs (
    code, display_name, title, sex, summary, story_markdown, public_profile,
    portrait_url, primary_location_id, status, sort_order,
    created_by, published_by, published_at
)
SELECT
    s.code, s.display_name, s.title, s.sex, s.summary, s.story_markdown,
    s.public_profile, s.portrait_url, l.id, 'published', s.sort_order,
    a.id, a.id, now()
FROM npc_seed s
CROSS JOIN actor a
CROSS JOIN location l
ON CONFLICT (code) DO NOTHING;

-- Create the immutable initial publish revision only for NPCs that do not yet have one.
WITH actor AS (
    SELECT u.id
    FROM game.users u
    JOIN game.admin_role_assignments a ON a.user_id = u.id
    WHERE a.role = 'super_admin'
      AND (a.expires_at IS NULL OR a.expires_at > now())
    ORDER BY a.granted_at, u.id
    LIMIT 1
)
INSERT INTO game.npc_revisions (
    npc_id, revision_no, snapshot, change_kind, change_note, changed_by
)
SELECT
    n.id,
    1,
    jsonb_build_object(
        'code', n.code,
        'displayName', n.display_name,
        'title', n.title,
        'sex', n.sex,
        'summary', n.summary,
        'storyMarkdown', n.story_markdown,
        'publicProfile', n.public_profile,
        'portraitUrl', n.portrait_url,
        'status', n.status,
        'sortOrder', n.sort_order
    ),
    'publish',
    'v1.1 initial NPC content import',
    a.id
FROM game.npcs n
JOIN npc_seed s ON s.code = n.code
CROSS JOIN actor a
WHERE NOT EXISTS (
    SELECT 1 FROM game.npc_revisions r WHERE r.npc_id = n.id
);

COMMIT;
