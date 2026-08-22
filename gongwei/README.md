# 宮闈浮生 GongWeiFuSheng — 後端

依 **v1.1 交付包**實作的 ASP.NET Core 後端。

- 正式前台：`https://miglow.vip/gongwei/`
- 正式 API：`https://gongwei-api.miglow.vip/api/v1`
- 正式管理後台：`https://gongwei-admin.miglow.vip/`

## 文件優先順序（README_v1.1 §1）

衝突時依序判定，**v1.0 只供追溯，不得用來產生新的 Migration 或 OpenAPI**：

| # | 文件 | 位置 |
|---|---|---|
| 1 | 後端規格書 v1.1 | `docs/v1.1/backend_spec_v1.1.md` |
| 2 | HTTP API v1.1（234 支端點，前端依據） | `docs/v1.1/api_v1_v1.1.md` |
| 3 | PostgreSQL Schema v1.1（60 張表） | `db/authoritative/v1.1/schema_v1.1.sql` |
| 4 | LINE Login 與 Web Session v1.1 | `docs/v1.1/line_login_v1.1.md` |
| 5 | 位階、晉降與俸祿 v1.1 | `docs/v1.1/rank_catalog_v1.1.md` |
| 6 | 規則種子資料 v1.1 | `db/authoritative/v1.1/seed_rules_v1.1.sql` |
| 7 | NPC 種子資料 v1.1 | **未隨交付包提供** |

v1.0 的舊副本保留在 `docs/`（未加 `v1.1/`）與 `db/authoritative/schema_v1.0.sql`，僅供追溯。

## v1.0 → v1.1 的結構變動

仍是 60 張表，但換掉 6 張：

| 移除 | 新增 |
|---|---|
| `story_arcs`／`story_chapters`／`story_nodes` | `npcs`／`npc_revisions`（NPC CMS） |
| `relationships`／`relationship_history` | `ability_label_definitions`（體質 570 →「康健」）／`character_progress` |
| `content_revisions` | `character_chronicle_entries`（統一歷程）／`line_login_attempts` |

另有三張表改欄位：`world_state.chapter_code` → `era_code`；`admin_role_assignments` 加
公開執事欄位（`is_public`／`public_display_name`／`public_title`／`public_duty`／
`sort_order`／`updated_at`／`version`）；`users` 加 `last_seen_at`。

主線故事與人物關係分數功能已取消，地圖內容改由地點、NPC 與地點事件構成。

---

## 目前狀態（2026-08-16）

| 層 | 狀態 | 驗證方式 |
|---|---|---|
| 權威 SQL + Seed + 文件 | ✅ v1.1 已納入 | 原樣複製，未經改寫 |
| `GongWei.Domain` | ✅ v1.1 | 建置通過 |
| `GongWei.Infrastructure`（EF 模型 + Migration） | ✅ v1.1 | 建置通過；**60/60 表且全部欄位與 schema_v1.1.sql 相符**；11/11 函式一致；57 個 trigger；`has-pending-model-changes` clean |
| `GongWei.Application`（P0 use case） | ✅ 建置通過 | 領域規則仍為 v1.0 語意，尚未接上新的 NPC／歷程／能力標籤 |
| `GongWei.Api` | ❌ **無法建置** | 需依 v1.1 的 234 支端點重寫 |
| `GongWei.Admin` | ❌ **無法建置** | 需依規格書 §2.4 重寫路由 |
| `GongWei.Worker` | ❌ **無法建置** | 需移除 action-point-reset、加日曆與月俸 |
| `tests/` | ⚠️ 仍為舊斷言 | 需改寫 |

**解決方案目前無法整包建置。** `dotnet build src/GongWei.Infrastructure` 可驗證已完成的部分。

---

## 已完成的 v1.0 對齊重點

以下都是 v0.8 版本做錯、現已修正的項目：

| 項目 | v0.8 做法（錯） | v1.0 現況（正確） |
|---|---|---|
| 四能力 | charm/intellect/artistry/stamina 0–100 | **vitality/appearance/strategy/luck 0–1000**（體質、容貌、心計、福氣） |
| 資源 | favor + reputation | **prestige 威望（bigint ≥0）+ favor 恩寵**；初始皆為 0 |
| 行動點 | `action_points` 欄位 + 每日重置 job + 設定 | **完全移除**。每日行動不限次數（§0.2、§6.12） |
| `ranks` 唯一鍵 | `(role, ordinal)` | **`(applies_to_role, display_name)`** —— 同品級可有多個位號 |
| `ranks` 欄位 | 只有 code/ordinal/stipend | 加 `grade_code`、`prestige_required`、`source_annual_stipend`、`capacity`、`is_lead`、`is_application_option`、`initial_stats`、`promotion_rules` |
| 建角欄位 | 6 個欄位 | 加 `sex`（由 role 推導）、`courtesy_name`、`birth_date_label`、`age`、`strengths`、`weaknesses`、`likes`、`dislikes`，並含各欄字數下限 |
| 年齡規則 | 無 | 宮妃 15–18；皇嗣 `age=0` 且姓「蕭」 |
| 建角初始能力 | 審核員自由填 | **由 `ranks.initial_stats` 推導**，Request 不接受 stats |
| 事件投稿 | 送出即公開 | **draft → submitted → under_review → approved**，只有 approved 才有 `published_at` 進 feed |
| 侍寢語意 | 管理員核准時 roll 0–99 對門檻 35 | **成功率＝受孕率，預設 100%**；管理員只送 approved/rejected，後端 roll **1–100** |
| 懷孕欄位 | 無 roll 紀錄 | 加 `conception_rate_percent`、`conception_roll`，永久保存 |
| 流產 | 只收 reason 字串 | **`miscarriage_mode` 預設 `event_only`**，需 `triggerCode` + 可驗證的 `sourceType/sourceId` + ≥5 字理由 |
| 經濟調整 | 有金額門檻 + 雙人覆核 | **不設門檻、不走覆核**，改為 `reasonCode` + `reasonText`（≥5 字）必填，回傳 `auditLogId` |
| `version` / `updated_at` | 應用程式手動 `Touch()` | **由 `tr_*_touch` trigger 維護**，EF 映射為 database-generated 並讀回 |
| 錯誤碼 | 自訂一套 | 對齊 API 清單 §1.4：`AUTH_REQUIRED`、`RESOURCE_NOT_FOUND`、`PRECONDITION_REQUIRED`(428)、`PAYLOAD_TOO_LARGE`(413)、`UNSUPPORTED_MEDIA_TYPE`(415)、`MAINTENANCE_MODE`(503) 等 |
| Problem Details | 無欄位級錯誤 | 加 `errors` 物件（例：`{"biography":["自介至少需要 200 字"]}`） |

---

## 資料庫

```bash
dotnet build src/GongWei.Infrastructure
```

Migration 分兩支，對應規格書 §13.1：

| Migration | 內容 |
|---|---|
| `InitialSchemaV1` | 由 EF 模型產生：60 張表、欄位、型別、長度、PK/FK、CHECK、Partial Unique Index |
| `SqlHardeningV1` | EF 表達不了的：`touch_updated_at`／`reject_mutation`／`reject_deletion` 與 9 個跨表驗證函式、58 個 trigger、兩個 singleton 控制列與基礎貨幣 |

已比對 `db/generated/schema_from_migrations.sql` 與權威 SQL：

- **表 60/60 完全相同**
- **函式 12/12 完全相同**
- `character_stats`、`ranks`、`character_applications`、`event_posts`、`pregnancies`、
  `world_state` 逐欄比對相符
- Trigger：權威檔以 `DO` 迴圈建立，migration 展開為 58 個具名 trigger
  （32 touch + 16 immutable + 1 no-delete + 9 驗證）
- `dotnet ef migrations has-pending-model-changes` 回報 clean

### 建立開發資料庫

**由你本人執行**（腳本互動式詢問密碼，不寫入 repo）：

```bash
pwsh ./deploy/db/setup-database.ps1 -Port 5433
```

> 注意：本機 PostgreSQL 18 監聽 **5433**，不是 5432。腳本目前仍指向舊的
> `db/seed_reference.sql`，需改為 `db/authoritative/seed_rules_v1.0.sql`——這在
> 待辦清單裡。

---

## 已實作的關鍵交易

| 規格 | 實作位置 |
|---|---|
| §6.1 核准角色申請（能力由位號推導） | `CharacterApplicationService.ApproveAsync` |
| §6.2 侍寢核准與受孕判定 | `ReproductionService.ResolveAudienceAsync` |
| §6.3 流產（event_only 來源驗證） | `ReproductionService.MiscarryAsync` |
| §6.4 出生抽取（CSPRNG + 候選 hash） | `ReproductionService.DrawBirthAsync` |
| §6.5 購買 | `EconomyService.PurchaseAsync` |
| §6.7 永久死亡（含死亡後處理表） | `ApprovalService.ExecuteCharacterDeathAsync` |
| §6.8 圖片上傳與審核 | `PortraitService` + `ImageSharpPortraitProcessor` |
| §6.10 事件投稿草稿與審核 | `EventPostService` |
| §6.11 管理員銀兩調整與道具發放 | `EconomyService.AdjustCurrencyAsync` / `GrantItemAsync` / `CorrectLedgerAsync` |

生育流程一律照 `reproduction_control(1)` → `pregnancy` → `wait_pool_entry` →
`character` 取鎖；可鎖定的表列在 `DbLocks.LockableTables` 白名單。

---

## 待辦（依優先順序）

1. **`GongWei.Api` 重寫** —— 前端已在開發，這項最急。路徑要對齊 API 清單：
   `/portrait-uploads`、`/character-applications`、`/characters/me`、
   `/media/{assetId}/content`、`/market/purchases`、`/reproduction/audience-requests`
   等，並補上 `errors` 欄位與 §1.4 全部錯誤碼。
2. **`GongWei.Admin` 重寫** —— 依規格書 §2.4 的 14 條後台路由與對應 Policy。
3. **`GongWei.Worker`** —— 移除 `action-point-reset`；加宮廷日換日（Asia/Taipei
   1:1，由 anchor 計算）與 `monthly-stipend`（每宮廷月 1 日 00:05，
   `floor(年俸/12)`，第 12 月補餘數，同角色同宮廷年月唯一）。
4. **Seed 切換** —— `deploy/db/setup-database.ps1` 改指向
   `db/authoritative/seed_rules_v1.0.sql`（87 個位號）。
5. **測試改寫** —— Domain 測試改用新能力與規則；Postgres 測試比對 v1.0 enum 與 trigger。
6. **尚未實作的 v1.0 功能** —— Buy Me a Coffee 設定（§6.13）、故事編輯與發布、
   稱號授予、GameSetting 草稿／發布／回復（這三項在 v1.0 屬 P0）、
   `ApprovalService` 除 `character.death` 外的 7 個 handler。
