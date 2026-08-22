# 宮闈浮生後端開發交付包 v1.1

> 定稿日期：2026-08-16  
> 適用：ASP.NET Core 10／.NET 10 LTS、IIS、PostgreSQL 17 或 18  
> 正式前台：`https://miglow.vip/gongwei/`  
> 正式 API：`https://gongwei-api.miglow.vip/api/v1`  
> 正式管理後台：`https://gongwei-admin.miglow.vip/`

LINE Developers Console 已登錄正式 Callback；Channel 目前維持 `Developing`，待後端登入整合測試完成再切換 `Published`。

## 1. 文件優先順序

發生內容衝突時，後端開發依下列順序判定：

1. [後端規格書_v1.1.md](../後端規格書_v1.1.md)：系統邊界、非功能需求、交易、權限與驗收標準。
2. [implementation_bootstrap_v1.1.md](./implementation_bootstrap_v1.1.md)：後端不存在時，從零建立 Api／Admin／Worker／AdminCli、CI、#20～#22 的開工規格。
3. [api_v1_v1.1.md](./api_v1_v1.1.md)：HTTP Method、Path、DTO、狀態碼及錯誤碼。
4. [schema_v1.1.sql](./schema_v1.1.sql)：資料表、FK、Check、Unique、Index、Trigger 與不可變資料規則。
5. [line_login_v1.1.md](./line_login_v1.1.md)：LINE Login、Cookie Session、CSRF、CORS 與正式環境設定。
6. [rank_catalog_v1.1.md](./rank_catalog_v1.1.md)：完整位階、門檻、初始能力與俸祿。
7. [seed_rules_v1.1.sql](./seed_rules_v1.1.sql)：能力標籤、地點、位階及已確認規則的 Seed。
8. [seed_npcs_v1.1.sql](./seed_npcs_v1.1.sql)：現有 8 位 NPC 的初始已發布內容與立繪路徑；只補缺少 Code，不覆蓋 CMS 編輯。

舊版 v1.0 只供追溯，不得用來產生新的 Migration 或 OpenAPI。企劃原始文件若與 v1.1 的已確認修正衝突，以 v1.1 為準；未知規則必須維持可設定，不可自行臆測。

## 2. 交付內容摘要

- 60 張 PostgreSQL Table，包含 LINE 登入、防重放、角色、玩家歷程、NPC CMS、事件、宮市、庫存、繁衍、死亡、審核、Audit、Outbox 與排程。
- 234 個 Method + Path API Contract；OpenAPI 應由實作自動產生並以 Snapshot Test 防止未審核破壞性修改。
- 一個 LINE 帳號只允許一個存活角色；死亡後可重新填單，但舊角色及死亡資料永久保留且只允許管理員查看同一帳號的跨世角色鏈結。
- 無全域行動點；宮廷日曆以 `Asia/Taipei` 現實日 1:1 推進。
- 主線故事與人物關係分數功能已取消；地圖內容由地點、NPC 與地點事件構成。
- 人物頁的數值標籤由資料表判定，例如體質 570 必須回傳「康健」。

## 3. 建議建置順序

1. 若後端尚不存在，必須依 `implementation_bootstrap_v1.1.md` 自行建立完整 Solution、`GongWei.Api`、`GongWei.Admin`、`GongWei.Worker`、共用層、`GongWei.AdminCli` 與測試專案；不得因專案不存在而把任務標成阻塞。
2. 以 `schema_v1.1.sql` 建立第一版 Migration；不得在 Production 直接把整份 SQL 當成每次啟動腳本重跑。
3. 建立 LINE Login、Session、CSRF、CORS 與 RBAC；完成下方首次管理員 Bootstrap。
4. 執行 `seed_rules_v1.1.sql`，再執行 `seed_npcs_v1.1.sql`；以整合測試驗證兩份 Seed 可重跑。
5. 完成角色申請／審核、人物頁、玩家清單、數值標籤及統一歷程。
6. 完成 NPC、事件投稿／審核／結算、經濟帳本、宮市與庫存。
7. 完成侍寢／懷孕／出生、死亡重建角色、管理調整及永久 Audit。
8. 完成 Worker、Outbox、備份還原演練、負載測試、OpenAPI Snapshot 及部署 Runbook。

## 4. 全新環境 Bootstrap

`seed_rules_v1.1.sql` 內部分 `game_settings` 需要 `created_by`，因此全新資料庫必須先建立第一位超級管理員。`GongWei.AdminCli` 是後端交付的一部分，必須由開發者依 `implementation_bootstrap_v1.1.md` 建立，不是外部前置專案。Production 不允許手動修改角色表或留下通用預設密碼。

1. LINE Developers 將站長帳號列為 Channel Admin 或 Tester；Channel 尚在 Developing 時只有這些帳號可登入。
2. 套用 Schema，啟動 API，站長由 LINE Login 登入一次，讓系統正常建立 `game.users`。
3. 在伺服器本機執行一次性管理指令：

   ```powershell
   dotnet GongWei.AdminCli.dll grant-super-admin --line-user-id <LINE_SUB> --reason "initial production bootstrap"
   ```

4. 指令必須要求互動確認、只允許本機／部署身分執行、以交易新增 `admin_role_assignments`，同時寫入 `audit_logs`；成功後不得輸出完整 LINE Sub。
5. 依序執行 `seed_rules_v1.1.sql`、`seed_npcs_v1.1.sql`。若尚未有 `super_admin`，Seeder 必須直接失敗並顯示可操作的錯誤，不可靜默略過設定。
6. 以管理後台確認角色與公開執事資料，再將 LINE Channel 切換 Published。

開發環境可另提供 `bootstrap-admin` User Secret，但不得放入 Git、`appsettings.json`、SQL Seed 或 CI Log。

## 5. Migration 與 Seed 規則

- EF Core Migration 是正式版本來源；`schema_v1.1.sql` 是 Initial Migration 的可審查基準。
- Migration 在單獨部署步驟執行，不由每個 IIS Instance 啟動時競速執行。
- Schema 與 Seed 使用專用 Migration DB Role；Runtime Role 不得擁有 `DROP TABLE`、`ALTER ROLE` 或建立 Extension 的權限。
- `seed_rules_v1.1.sql` 可重跑；規則更新只能改可變 Master Data，不得覆寫 Ledger、角色歷史、事件結果、死亡、Revision 或 Audit。
- `seed_npcs_v1.1.sql` 只插入缺少的 NPC Code 與其第一版 Revision；既有 NPC 即使內容不同也不得由部署 Seed 覆寫，必須走 CMS 發布／回復流程。
- 每次 Release 必須記錄 App Version、Git Commit、Migration ID、執行人、開始／完成時間與結果。
- 回復資料庫前先停止寫入，確認備份完整，還原後跑 Schema／Ledger／Audit 一致性檢查。

## 6. 必要環境設定

| Key | Production 值／來源 |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__GameDb` | IIS Secret／受控環境變數 |
| `LINE_LOGIN_CHANNEL_ID` | `2011123657` |
| `LINE_LOGIN_CHANNEL_SECRET` | LINE Console Secret；不可進 Git 或 Log |
| `LINE_LOGIN_CALLBACK_URL` | `https://gongwei-api.miglow.vip/api/v1/auth/line/callback` |
| `PLAYER_FRONTEND_BASE_URL` | `https://miglow.vip/gongwei/` |
| `CORS_ALLOWED_ORIGINS__0` | `https://miglow.vip` |
| `TIME_ZONE_ID` | Windows：`Taipei Standard Time`；IANA 邏輯值：`Asia/Taipei` |
| `MEDIA_STORAGE_ROOT` | IIS Web Root 以外的持久化目錄或 S3-compatible Bucket |
| `DATA_PROTECTION_KEY_PATH` | 多 Instance 共用、ACL 限制且有備份的 Key Ring |

Secret 不得回傳於 `/health`、OpenAPI、錯誤頁、Audit Payload 或管理後台頁面。

## 7. 上線前必要驗收

- LINE Login 成功、拒絕、State 重放、Nonce 錯誤、Code 過期、Return URL Open Redirect、Developing／Published 狀態均有測試。
- `users.line_user_id` 唯一；同帳號無法同時擁有兩個存活角色。
- 體質 570 的 API DTO 回傳 `label: "康健"`；所有能力值 0、邊界值、1000 都有測試。
- 事件／道具造成的每筆能力或銀兩變化，同一交易寫入結果、Ledger、Stats、Chronicle、Audit／Outbox；重試不重複結算。
- 玩家可看自己今日與歷史歷程；公開玩家查詢不可揭露 LINE Sub、死亡跨世鏈結、內部備註、IP 或 Audit。
- 管理員所有數值／道具調整都要求理由，並能於 Audit Web 查詢。
- NPC 未發布版本不會出現在玩家 API；發布、回復與封存均保留 Revision。
- P95 目標、200 人登入尖峰、排程單例、備份與還原演練通過。

## 8. 尚未定案但已可設定

- 侍寢成功率預設 100%。
- 懷孕時長預設 10 個宮廷日。
- 流產策略預設 `event_only`；觸發事件、機率、保護條件及補償尚未定案。
- Buy Me a Coffee 只是前端外部連結，不建立付款、Webhook、遊戲獎勵或玩家綁定 API。

上述規則不得 Hard-code；正式調整必須透過有 Revision、Reason、Audit 的管理流程。
