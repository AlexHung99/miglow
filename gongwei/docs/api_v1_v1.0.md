# 《宮闈浮生》HTTP API v1 清單

> 版本：v1.0  
> Base URL：`https://api.<domain>/api/v1`  
> 認證：LINE Login 後的 HttpOnly Session Cookie  
> 管理後台：`https://admin.<domain>/`，ASP.NET Core MVC／Razor Pages on IIS  
> Schema：[schema_v1.0.sql](./schema_v1.0.sql)  
> 位階種子資料：[rank_catalog_v1.0.md](./rank_catalog_v1.0.md)、[seed_rules_v1.0.sql](./seed_rules_v1.0.sql)

---

## 1. 共通約定

本文件表格中的 `/health/live`、`/characters/me` 等 Path 都是相對路徑，正式網址必須加上 Base URL；例如 `GET https://api.<domain>/api/v1/characters/me`。不得同時提供無版本前綴的另一組正式端點。

### 1.1 權限代碼

| 代碼 | 身份／Policy |
|---|---|
| `Public` | 不需登入 |
| `User` | 已登入帳號 |
| `Character` | 有 `active` 角色；特定端點另允許 `waiting_birth`／`paused` |
| `CR` | `character_reviewer` |
| `GM` | `game_master` |
| `EM` | `economy_manager` |
| `MOD` | `moderator` |
| `AUD` | `auditor` 唯讀 |
| `CE` | `content_editor`：故事、章節、節點與事件內容 |
| `CM` | `character_manager`：人物資料、稱號定義與授予 |
| `SCM` | `system_config_manager`：一般遊戲設定；高風險設定仍須覆核 |
| `SA` | `super_admin` |
| `MGR` | 任一啟用中的非唯讀管理角色：CR/GM/EM/MOD/CE/CM/SCM/SA |

多個代碼以 `/` 表示任一符合即可；寫入端點仍需檢查帳號與角色狀態。

`GongWei.Admin` 與 `GongWei.Api` 共用 Application／Domain Use Case。API 清單仍是 HTTP Contract 與權限基準，但 IIS Admin Controller／Razor Page Handler 不應透過 Loopback HTTP 呼叫同機 API；應直接呼叫相同 Application Command／Query，並套用等價 Policy、Validation、Idempotency、Audit 與交易規則。

### 1.2 Header

| Header | 規則 |
|---|---|
| `Cookie` | 玩家 API 使用 `gw_session=<opaque token>`；IIS 管理後台使用獨立 `gw_admin_session`；不得存 JWT 於 Local Storage |
| `X-CSRF-Token` | 玩家 SPA 的 Cookie 寫入 API 必填；管理後台同源表單改用 ASP.NET Core AntiForgery Token |
| `X-Request-Id` | 可由前端送 UUID；未送則 API 產生並回傳 |
| `Idempotency-Key` | 表中標示 `Idem` 的端點必填，長度 16–100 |
| `If-Match` | 更新具 `version` 的 Resource 時傳入 `"<version>"` |

Production 的 GitHub Pages 玩家前台必須綁定已購網域下的 `game.<domain>`，API 使用 `api.<domain>`，兩者保持同一 registrable domain。玩家 SPA 的 `fetch` 必須使用 `credentials: "include"`；API CORS 只允許完整的 Production／Staging Origin 並回 `Access-Control-Allow-Credentials: true`。若仍以 `alexhung99.github.io` 直接呼叫另一網域 API，只可作無登入展示，不作正式 Cookie 登入架構。

### 1.3 列表與時間

- Query：`limit` 預設 20、最大 100；`cursor` 為不透明字串。
- Response：`{ "items": [], "nextCursor": null }`。
- 時間皆為 UTC ISO 8601；日期顯示由前端轉為 Asia/Taipei。
- 管理端列表可另用 `q`、`status`、`role`、`from`、`to`，但單頁仍不得超過 100。
- API 表格中的 `Filters` 代表「共通 Cursor Query 加上該列備註明列的篩選欄位」；正式 OpenAPI 必須逐項宣告型別與允許值，不得實作成任意 Key／Value 或直接拼接 SQL。

### 1.4 標準錯誤

使用 RFC Problem Details 擴充 `code`、`requestId`、`errors`。所有端點共通：

| HTTP | Code | 意義 |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Request 格式、欄位或 Query 不合法 |
| 401 | `AUTH_REQUIRED` / `SESSION_EXPIRED` | 未登入或 Session 過期 |
| 403 | `FORBIDDEN` / `CHARACTER_STATE_FORBIDDEN` | 權限或角色狀態不允許 |
| 404 | `RESOURCE_NOT_FOUND` | 不存在或呼叫者無權得知其存在 |
| 409 | `VERSION_CONFLICT` | `If-Match` 與目前版本不同 |
| 409 | `IDEMPOTENCY_KEY_REUSED` | 相同 Key 搭配不同 Request Body |
| 413 | `PAYLOAD_TOO_LARGE` | 上傳超過端點限制 |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | 上傳格式或 Magic Bytes 不符 |
| 428 | `PRECONDITION_REQUIRED` | 需樂觀鎖的寫入未提供 `If-Match` |
| 429 | `RATE_LIMITED` | 超出速率限制，回傳 `Retry-After` |
| 500 | `INTERNAL_ERROR` | 只公開 Request ID，不公開 Stack Trace |
| 503 | `MAINTENANCE_MODE` | 維護中或必要依賴未就緒 |

錯誤 Response 範例：

```json
{
  "type": "https://api.example/problems/validation-failed",
  "title": "Request validation failed",
  "status": 400,
  "code": "VALIDATION_FAILED",
  "requestId": "req_8C2F",
  "errors": {
    "biography": ["自介至少需要 200 字"]
  }
}
```

成功的單一資源直接回 DTO；列表固定回 `CursorPage<T>`。`201` 必須回 `Location`，`204` 不得帶 Body。具 `version` 的單一資源同時回 `ETag`；敏感寫入成功回傳的 `requestId`、業務 Transaction ID 與 Audit ID 必須可互相追查。

---

## 2. System 與 Authentication

| Method／Path | 權限 | Request | Response | 特殊錯誤／備註 |
|---|---|---|---|---|
| `GET /health/live` | Public | — | `HealthDto` | 只表示程序存活 |
| `GET /health/ready` | Public | — | `HealthDto` | 不回傳 DB 連線字串 |
| `GET /meta` | Public | — | `ApiMetaDto` | API 版本、前端最低版本、維護狀態 |
| `GET /auth/line/start?returnUrl=` | Public | Allowlist URL | `302 LINE` | 設定短效 State/Nonce Cookie |
| `GET /auth/line/callback?code=&state=` | Public | LINE callback | `302 Frontend` | `LINE_AUTH_FAILED`；不得把 Token 放網址 |
| `POST /auth/logout` | User | — | `204` | 撤銷目前 Session |
| `POST /auth/logout-all` | User | `ConfirmDto` | `204` | Idem；撤銷使用者全部 Session |
| `GET /auth/csrf` | User | — | `CsrfTokenDto` | Token 綁定 Session |
| `GET /me` | User | — | `MeDto` | 帳號、目前角色、權限、待辦摘要 |
| `GET /public-settings/support` | Public | — | `PublicSupportSettingDto` | 右上說明按鈕狀態、文字、是否已設定 Creator，以及經 Allowlist 驗證的 URL |
| `GET /me/sessions` | User | — | `SessionSummaryDto[]` | 不回傳 Session Token |
| `DELETE /me/sessions/{sessionId}` | User | — | `204` | 只能刪自己的 Session |
| `PATCH /me/preferences` | User | `UpdatePreferencesRequest` | `PreferencesDto` | `If-Match` |

---

## 3. 角色申請與人物

### 3.1 玩家端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /portraits?role=` | User | Role Query | `PortraitSummaryDto[]` | 只回傳啟用的官方立繪 |
| `POST /portrait-uploads` | User | `multipart/form-data`：`file,role` | `201 PortraitUploadDto` | Idem；JPEG/PNG/WebP、最大 8 MB、至少 600×800；移除 EXIF、重新編碼、惡意檔案掃描 |
| `GET /portrait-uploads/{id}` | User | — | `PortraitUploadDto` | 僅本人；回傳短效預覽 URL 與審核狀態 |
| `GET /media/{assetId}/content?variant=portrait` | User | — | `image/webp` | 受控串流；Owner 可看自己的 Pending，CR/MOD/SA 可審核，Approved 且已連結可見人物者依人物可見性讀取；支援 ETag／Cache-Control，不洩漏 Storage Key |
| `PATCH /portrait-uploads/{id}/crop` | User | `UpdatePortraitCropRequest` | `PortraitUploadDto` | 僅 Pending；`If-Match`；比例座標 0–1 |
| `DELETE /portrait-uploads/{id}` | User | — | `204` | Pending 可撤回；實體檔案延後清除，已有申請引用則 `409` |
| `GET /character-applications/current` | User | — | `CharacterApplicationDto` / `204` | 目前 Draft/審核中申請 |
| `POST /character-applications` | User | `CreateApplicationRequest` | `201 CharacterApplicationDto` | Idem；`OPEN_APPLICATION_EXISTS`、`CURRENT_CHARACTER_EXISTS` |
| `PATCH /character-applications/{id}` | User | `UpdateApplicationRequest` | `CharacterApplicationDto` | 僅 Draft/NeedsRevision；`If-Match` |
| `POST /character-applications/{id}/submit` | User | `SubmitApplicationRequest` | `CharacterApplicationDto` | Idem；保存 Revision Snapshot |
| `POST /character-applications/{id}/cancel` | User | `ReasonRequest` | `CharacterApplicationDto` | Idem；Approved 不可取消 |
| `GET /characters/me` | User | — | `MyCharacterDto` / `204` | WaitingBirth、Paused、Dead 均可查看 |
| `GET /characters/{characterId}/public` | User | — | `PublicCharacterDto` | 依可見性遮蔽資料 |
| `GET /characters/{characterId}/chronicle` | User | `cursor,limit` | `CursorPage<ChronicleEntryDto>` | 只回傳可公開／本人可見紀錄 |
| `GET /characters/me/stats` | Character | — | `CharacterStatsDto` | 包含 Version |
| `GET /characters/me/rank-history` | User | `cursor,limit` | `CursorPage<RankHistoryDto>` | — |
| `GET /characters/me/status-history` | User | `cursor,limit` | `CursorPage<CharacterStatusHistoryDto>` | 管理備註不公開 |
| `POST /characters/me/pause-requests` | Character | `PauseRequest` | `201 PauseRequestDto` | Idem；正式請假申請 |
| `POST /characters/me/resume-requests` | User | `ResumeRequest` | `201 ResumeRequestDto` | 僅 Paused |

### 3.2 角色審核管理端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/character-applications` | CR/AUD/SA | Filters | `CursorPage<ApplicationSummaryDto>` | 審核佇列 |
| `GET /admin/character-applications/{id}` | CR/AUD/SA | — | `AdminApplicationDto` | 含 Revision 與審核歷史 |
| `POST /admin/character-applications/{id}/request-revision` | CR/SA | `ReviewDecisionRequest` | `CharacterApplicationDto` | Idem；Submitted → NeedsRevision |
| `POST /admin/character-applications/{id}/approve` | CR/SA | `ApproveApplicationRequest` | `ApproveApplicationResultDto` | Idem；交易建立 Character/Stats/Wallet/歷史；皇子女另進待生池 |
| `POST /admin/character-applications/{id}/reject` | CR/SA | `ReviewDecisionRequest` | `CharacterApplicationDto` | Idem；必填原因 |
| `GET /admin/portrait-uploads` | CR/MOD/AUD/SA | Filters | `CursorPage<PortraitUploadReviewDto>` | 圖片審核佇列；AUD 唯讀 |
| `POST /admin/portrait-uploads/{id}/approve` | CR/MOD/SA | `PortraitReviewRequest` | `PortraitUploadDto` | Idem；Pending → Approved；寫 Audit 與通知 |
| `POST /admin/portrait-uploads/{id}/reject` | CR/MOD/SA | `PortraitReviewRequest` | `PortraitUploadDto` | Idem；原因必填；已送出的角色申請轉 NeedsRevision |
| `GET /titles?role=` | User | Role Query | `CharacterTitleDefinitionDto[]` | 只回啟用且可公開的稱號定義 |
| `GET /characters/{id}/titles` | User | — | `CharacterTitleDto[]` | 依 Visibility 遮蔽 OwnerOnly／AdminOnly |
| `GET /admin/title-definitions` | CM/AUD/SA | Filters | `CursorPage<CharacterTitleDefinitionDto>` | 含停用與秘密稱號 |
| `POST /admin/title-definitions` | CM/SA | `UpsertTitleDefinitionRequest` | `201 CharacterTitleDefinitionDto` | Idem；Code 不可重複 |
| `GET /admin/title-definitions/{id}` | CM/AUD/SA | — | `CharacterTitleDefinitionDto` | — |
| `PATCH /admin/title-definitions/{id}` | CM/SA | `UpsertTitleDefinitionRequest` | `CharacterTitleDefinitionDto` | `If-Match`；寫 Audit；有 Active Assignment 時不得改成不相容 Role |
| `GET /admin/title-assignments` | CM/AUD/SA | Filters | `CursorPage<CharacterTitleAssignmentDto>` | 可依人物、稱號、Active 篩選 |
| `GET /admin/characters/{id}/titles` | CM/AUD/SA | — | `CharacterTitleAssignmentDto[]` | 含撤回歷程 |
| `POST /admin/characters/{id}/titles` | CM/SA | `GrantCharacterTitleRequest` | `201 CharacterTitleAssignmentDto` | Idem；驗證 Role、Active 與重複授予 |
| `POST /admin/title-assignments/{id}/make-primary` | CM/SA | — | `CharacterTitleAssignmentDto` | Idem；交易取消舊 Primary |
| `POST /admin/title-assignments/{id}/revoke` | CM/SA | `ReasonRequest` | `CharacterTitleAssignmentDto` | Idem；不 Hard Delete |
| `GET /admin/characters` | CR/GM/MOD/AUD/SA | Filters | `CursorPage<AdminCharacterSummaryDto>` | 支援 User、Role、Status 搜尋 |
| `GET /admin/characters/{id}` | CR/GM/MOD/AUD/SA | — | `AdminCharacterDto` | 含內部狀態，不含 LINE Token |
| `GET /admin/users/{userId}/character-history` | MGR/AUD | — | `AdminCharacterHistoryDto[]` | 僅管理端可看同一 LINE 帳號曾使用的在玩、死亡與封存角色；玩家公開 DTO 不回此連結 |
| `PATCH /admin/characters/{id}/profile` | CR/SA | `AdminUpdateCharacterRequest` | `AdminCharacterDto` | `If-Match`；寫 Audit |
| `POST /admin/characters/{id}/pause` | MOD/SA | `ReasonRequest` | `CharacterStateChangeDto` | Idem；寫狀態歷史 |
| `POST /admin/characters/{id}/resume` | MOD/SA | `ReasonRequest` | `CharacterStateChangeDto` | Idem |
| `POST /admin/characters/{id}/suspend` | MOD/SA | `SuspendCharacterRequest` | `CharacterStateChangeDto` | Idem；同步撤銷資格與通知 |
| `POST /admin/characters/{id}/unsuspend` | MOD/SA | `ReasonRequest` | `CharacterStateChangeDto` | Idem |
| `POST /admin/characters/{id}/archive` | SA | `ReasonRequest` | `CharacterStateChangeDto` | Idem；不得封存仍 Active 角色 |
| `POST /admin/characters/{id}/change-rank` | GM/SA | `ChangeRankRequest` | `RankChangeResultDto` | Idem；更新角色及 Rank History |
| `POST /admin/characters/{id}/move-residence` | GM/SA | `MoveResidenceRequest` | `ResidenceChangeResultDto` | Idem；關閉舊入住紀錄 |
| `POST /admin/characters/{id}/adjust-stats` | GM/SA | `AdjustStatsRequest` | `CharacterStatsDto` | Idem；原子更新、Audit |
| `POST /admin/characters/{id}/request-death` | GM/MOD/SA | `DeathRequest` | `202 ApprovalRequestDto` | 高風險；建立雙人覆核，不立即死亡 |

---

## 4. 世界、地圖與內容設定

### 4.1 玩家端

| Method／Path | 權限 | Request | Response | 備註 |
|---|---|---|---|---|
| `GET /world/state` | User | — | `WorldStateDto` | 章節、日期、維護與侍寢狀態 |
| `GET /world/map` | User | — | `WorldMapDto` | 地點及目前角色可用狀態 |
| `GET /world/locations/{id}` | User | — | `WorldLocationDto` | 不符合可見條件回 404 |
| `GET /ranks?role=` | User | Role Query | `RankDto[]` | 僅公開欄位 |
| `GET /residences` | User | — | `ResidenceSummaryDto[]` | 僅啟用居所 |
| `GET /announcements` | Public/User | `audience,cursor` | `CursorPage<AnnouncementDto>` | 未登入只取 Public/All |
| `GET /stories` | User | `status,cursor,limit` | `CursorPage<StoryArcSummaryDto>` | 玩家只看 Published 且已開放內容 |
| `GET /stories/{arcCode}` | User | — | `StoryArcDto` | 含可見章節摘要 |
| `GET /stories/{arcCode}/chapters/{chapterCode}` | User | — | `StoryChapterDto` | 只回已發布節點及本人可走分支 |

### 4.2 管理端

| Method／Path | 權限 | Request | Response | 備註 |
|---|---|---|---|---|
| `PATCH /admin/world/state` | GM/SA | `UpdateWorldStateRequest` | `WorldStateDto` | `If-Match`；重大章切換需 Approval |
| `GET /admin/world/locations` | GM/AUD/SA | Filters | `WorldLocationDto[]` | 含未啟用地點 |
| `POST /admin/world/locations` | GM/SA | `UpsertWorldLocationRequest` | `201 WorldLocationDto` | Idem |
| `PATCH /admin/world/locations/{id}` | GM/SA | `UpsertWorldLocationRequest` | `WorldLocationDto` | `If-Match` |
| `GET /admin/ranks` | GM/AUD/SA | Filters | `RankDto[]` | 含未啟用位階 |
| `POST /admin/ranks` | GM/SA | `UpsertRankRequest` | `201 RankDto` | Idem |
| `PATCH /admin/ranks/{id}` | GM/SA | `UpsertRankRequest` | `RankDto` | `If-Match`；不刪除歷史使用中 Rank |
| `GET /admin/residences` | GM/AUD/SA | Filters | `ResidenceDto[]` | 含容量與未啟用居所 |
| `POST /admin/residences` | GM/SA | `UpsertResidenceRequest` | `201 ResidenceDto` | Idem |
| `PATCH /admin/residences/{id}` | GM/SA | `UpsertResidenceRequest` | `ResidenceDto` | `If-Match` |
| `GET /admin/story-arcs` | CE/GM/AUD/SA | Filters | `CursorPage<AdminStoryArcDto>` | 含 Draft／Review／Archived |
| `POST /admin/story-arcs` | CE/GM/SA | `UpsertStoryArcRequest` | `201 AdminStoryArcDto` | Idem；預設 Draft |
| `GET /admin/story-arcs/{id}` | CE/GM/AUD/SA | — | `AdminStoryArcDto` | 含章節摘要 |
| `PATCH /admin/story-arcs/{id}` | CE/GM/SA | `UpsertStoryArcRequest` | `AdminStoryArcDto` | `If-Match`；建立 Revision |
| `POST /admin/story-arcs/{id}/publish` | CE/GM/SA | `PublishContentRequest` | `AdminStoryArcDto` | Idem；建立不可變 Snapshot |
| `POST /admin/story-arcs/{id}/archive` | CE/GM/SA | `ReasonRequest` | `AdminStoryArcDto` | Idem；已發布內容不 Hard Delete |
| `GET /admin/story-arcs/{id}/revisions` | CE/GM/AUD/SA | `cursor,limit` | `CursorPage<ContentRevisionDto>` | — |
| `GET /admin/story-arcs/{arcId}/chapters` | CE/GM/AUD/SA | Filters | `StoryChapterSummaryDto[]` | — |
| `POST /admin/story-arcs/{arcId}/chapters` | CE/GM/SA | `UpsertStoryChapterRequest` | `201 AdminStoryChapterDto` | Idem；ChapterNo 唯一 |
| `GET /admin/story-chapters/{id}` | CE/GM/AUD/SA | — | `AdminStoryChapterDto` | 含 Nodes 與分支規則 |
| `PATCH /admin/story-chapters/{id}` | CE/GM/SA | `UpsertStoryChapterRequest` | `AdminStoryChapterDto` | `If-Match`；建立 Revision |
| `POST /admin/story-chapters/{id}/publish` | CE/GM/SA | `PublishContentRequest` | `AdminStoryChapterDto` | Idem；需 Published Arc 與唯一 Entry Node |
| `POST /admin/story-chapters/{id}/archive` | CE/GM/SA | `ReasonRequest` | `AdminStoryChapterDto` | Idem |
| `GET /admin/story-chapters/{id}/revisions` | CE/GM/AUD/SA | `cursor,limit` | `CursorPage<ContentRevisionDto>` | — |
| `GET /admin/story-chapters/{id}/preview` | CE/GM/AUD/SA | `characterId?` | `StoryPreviewDto` | 唯讀，以指定人物條件模擬可見分支 |
| `POST /admin/story-chapters/{chapterId}/nodes` | CE/GM/SA | `UpsertStoryNodeRequest` | `201 StoryNodeDto` | Idem；Code 唯一 |
| `PATCH /admin/story-nodes/{id}` | CE/GM/SA | `UpsertStoryNodeRequest` | `StoryNodeDto` | `If-Match`；建立 Revision |
| `DELETE /admin/story-nodes/{id}` | CE/GM/SA | `ReasonRequest` | `204` | 只允許未發布 Chapter；Revision 保留 Snapshot |
| `GET /admin/game-settings` | SCM/GM/AUD/SA | Filters | `GameSettingDto[]` | 不回任何 Secret；含 Draft 差異摘要 |
| `GET /admin/game-settings/{settingKey}` | SCM/GM/AUD/SA | — | `GameSettingDto` | 含 Validation Schema 與 Version |
| `PATCH /admin/game-settings/{settingKey}/draft` | SCM/GM/SA | `UpdateGameSettingDraftRequest` | `GameSettingDto` | `If-Match`；只存草稿，不立即生效 |
| `POST /admin/game-settings/{settingKey}/publish` | SCM/GM/SA | `PublishSettingRequest` | `GameSettingDto` / `202 ApprovalRequestDto` | Idem；High Risk 必須雙人覆核 |
| `GET /admin/game-settings/{settingKey}/revisions` | SCM/GM/AUD/SA | `cursor,limit` | `CursorPage<GameSettingRevisionDto>` | — |
| `POST /admin/game-settings/{settingKey}/restore/{revisionNo}` | SCM/GM/SA | `ReasonRequest` | `GameSettingDto` / `202 ApprovalRequestDto` | Idem；回復也產生新 Revision，不覆寫舊版 |

---

## 5. 事件房與真人互動

### 5.1 玩家端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /events` | User | `status,type,cursor,limit` | `CursorPage<EventSummaryDto>` | 依公開／邀請資格過濾 |
| `GET /events/{id}` | User | — | `EventRoomDto` | 含本人參與資格與截止狀態 |
| `POST /events/{id}/join` | Character | — | `EventParticipantDto` | Idem；`EVENT_FULL`、`EVENT_NOT_OPEN` |
| `POST /events/{id}/leave` | Character | `ReasonRequest?` | `EventParticipantDto` | Idem；事件規則可禁止退出 |
| `GET /events/{id}/participants` | User | `cursor,limit` | `CursorPage<EventParticipantPublicDto>` | — |
| `GET /events/{id}/posts` | User | `cursor,limit` | `CursorPage<EventPostDto>` | 只回 Approved；Cursor 依 `publishedAt,id`，不公開編輯紀錄 |
| `GET /events/{id}/posts/mine` | Character | `status,cursor,limit` | `CursorPage<MyEventPostDto>` | 可看自己的 Draft、審核狀態、退修原因與 Revision |
| `POST /events/{id}/posts` | Character | `SaveEventPostDraftRequest` | `201 MyEventPostDto` | Idem；建立 Draft，可空白自動儲存 |
| `PATCH /events/{id}/posts/{postId}` | Character | `SaveEventPostDraftRequest` | `MyEventPostDto` | 僅本人 Draft/NeedsRevision；`If-Match`；每次明確儲存保存 Revision |
| `POST /events/{id}/posts/{postId}/submit` | Character | `SubmitEventPostRequest` | `MyEventPostDto` | Idem；Draft/NeedsRevision → Submitted；提交後鎖定內容並等待管理員審核 |
| `GET /events/{id}/posts/{postId}/revisions` | Character | — | `EventPostRevisionDto[]` | 僅作者本人；其他玩家看不到編輯歷程 |
| `POST /events/{id}/posts/{postId}/withdraw` | Character | `ReasonRequest?` | `MyEventPostDto` | Idem；僅 Draft 或尚未 Claim 的 Submitted；永久保留歷史 |
| `GET /events/{id}/results` | User | — | `EventResultViewDto` | 結算後才可看；私人結果只給本人 |
| `GET /external-play-submissions` | Character | `cursor,limit` | `CursorPage<ExternalPlaySubmissionDto>` | 只看自己的提交 |
| `POST /external-play-submissions` | Character | `CreateExternalPlaySubmissionRequest` | `201 ExternalPlaySubmissionDto` | Idem；LINE 群內容不自動計分 |
| `PATCH /external-play-submissions/{id}` | Character | `UpdateExternalPlaySubmissionRequest` | `ExternalPlaySubmissionDto` | 僅 Submitted；`If-Match` |
| `POST /external-play-submissions/{id}/cancel` | Character | — | `ExternalPlaySubmissionDto` | Idem |

### 5.2 管理端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/events` | GM/AUD/SA | Filters | `CursorPage<AdminEventSummaryDto>` | 含 Draft/Cancelled |
| `POST /admin/events` | GM/SA | `CreateEventRequest` | `201 AdminEventDto` | Idem；預設 Draft |
| `GET /admin/events/{id}` | GM/AUD/SA | — | `AdminEventDto` | 含規則 Snapshot 與內部結果 |
| `PATCH /admin/events/{id}` | GM/SA | `UpdateEventRequest` | `AdminEventDto` | `If-Match`；Settled 不可改規則 |
| `POST /admin/events/{id}/schedule` | GM/SA | `ScheduleEventRequest` | `AdminEventDto` | Idem；Draft → Scheduled |
| `POST /admin/events/{id}/open` | GM/SA | — | `AdminEventDto` | Idem；可提前開啟 |
| `POST /admin/events/{id}/lock` | GM/SA | `ReasonRequest` | `AdminEventDto` | Idem；停止加入／投稿 |
| `POST /admin/events/{id}/cancel` | GM/SA | `ReasonRequest` | `AdminEventDto` | Idem；取消補償另走 Economy API |
| `POST /admin/events/{id}/participants` | GM/SA | `AddEventParticipantsRequest` | `EventParticipantDto[]` | Idem；邀請或直接加入 |
| `DELETE /admin/events/{id}/participants/{characterId}` | GM/SA | `ReasonRequest` | `204` | 保留 Removed 狀態 |
| `POST /admin/events/{id}/settlements/preview` | GM/SA | `EventSettlementRequest` | `EventSettlementPreviewDto` | 唯讀 Dry Run，不寫資源 |
| `POST /admin/events/{id}/settlements` | GM/SA | `EventSettlementRequest` | `EventSettlementResultDto` | Idem；原子寫 Result/Reward/Ledger/Outbox |
| `POST /admin/events/{id}/posts/{postId}/moderate` | GM/MOD/SA | `ModeratePostRequest` | `EventPostDto` | 不刪除原文 Revision |
| `GET /admin/event-posts` | GM/MOD/AUD/SA | `eventId,status,author,cursor,limit` | `CursorPage<AdminEventPostSummaryDto>` | 投稿審核佇列；可查永久保存的舊稿 |
| `GET /admin/event-posts/{postId}` | GM/MOD/AUD/SA | — | `AdminEventPostDto` | 含完整 Revision、作者及審核紀錄 |
| `POST /admin/event-posts/{postId}/claim` | GM/MOD/SA | — | `AdminEventPostDto` | Idem；Submitted → UnderReview |
| `POST /admin/event-posts/{postId}/request-revision` | GM/MOD/SA | `EventPostReviewRequest` | `AdminEventPostDto` | Idem；退回作者修改，原因必填 |
| `POST /admin/event-posts/{postId}/approve` | GM/MOD/SA | `EventPostReviewRequest` | `EventPostDto` | Idem；UnderReview/Submitted → Approved 並設定 PublishedAt |
| `POST /admin/event-posts/{postId}/reject` | GM/MOD/SA | `EventPostReviewRequest` | `AdminEventPostDto` | Idem；原因必填，不公開但永久保存 |
| `GET /admin/external-play-submissions` | GM/AUD/SA | Filters | `CursorPage<ExternalPlaySubmissionDto>` | 審核佇列 |
| `POST /admin/external-play-submissions/{id}/claim` | GM/SA | — | `ExternalPlaySubmissionDto` | Idem；Submitted → UnderReview |
| `POST /admin/external-play-submissions/{id}/approve` | GM/SA | `ExternalPlayReviewRequest` | `ExternalPlayReviewResultDto` | Idem；獎勵同交易 |
| `POST /admin/external-play-submissions/{id}/reject` | GM/SA | `ExternalPlayReviewRequest` | `ExternalPlaySubmissionDto` | Idem；必填原因 |

---

## 6. 經濟、宮市與庫存

### 6.1 玩家端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /wallets` | Character | — | `WalletDto[]` | 只看目前角色 |
| `GET /wallets/{currencyCode}/ledger` | Character | `cursor,limit` | `CursorPage<LedgerEntryDto>` | — |
| `GET /market/offers` | Character | `category,cursor,limit` | `CursorPage<MarketOfferDto>` | 依資格及期間過濾 |
| `GET /market/offers/{id}` | Character | — | `MarketOfferDetailDto` | 含目前角色限購餘額 |
| `POST /market/purchases` | Character | `CreatePurchaseRequest` | `201 PurchaseResultDto` | Idem；鎖 Offer/Wallet/Inventory；`INSUFFICIENT_FUNDS`、`SOLD_OUT` |
| `GET /inventory` | Character | `category,cursor,limit` | `CursorPage<InventoryEntryDto>` | 預設只回 quantity > 0 |
| `GET /inventory/{entryId}` | Character | — | `InventoryEntryDetailDto` | 只能本人 |
| `POST /inventory/{entryId}/use` | Character | `UseItemRequest` | `ItemUseResultDto` | Idem；鎖庫存，效果與消耗同交易 |
| `GET /inventory/{entryId}/history` | Character | `cursor,limit` | `CursorPage<InventoryTransactionDto>` | — |
| `GET /purchases` | Character | `cursor,limit` | `CursorPage<PurchaseDto>` | — |
| `GET /purchases/{id}` | Character | — | `PurchaseDto` | 只能本人 |

### 6.2 管理端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/currencies` | EM/AUD/SA | — | `CurrencyDto[]` | — |
| `POST /admin/currencies` | EM/SA | `UpsertCurrencyRequest` | `201 CurrencyDto` | Idem |
| `GET /admin/items` | EM/AUD/SA | Filters | `CursorPage<ItemDefinitionDto>` | 含停用版本 |
| `POST /admin/items` | EM/SA | `CreateItemDefinitionRequest` | `201 ItemDefinitionDto` | Idem；新 Code v1 |
| `POST /admin/items/{code}/versions` | EM/SA | `CreateItemVersionRequest` | `201 ItemDefinitionDto` | Idem；不覆寫舊效果 |
| `PATCH /admin/items/{id}/availability` | EM/SA | `SetAvailabilityRequest` | `ItemDefinitionDto` | `If-Match` |
| `GET /admin/market/offers` | EM/AUD/SA | Filters | `CursorPage<AdminMarketOfferDto>` | 含未開始／結束 |
| `POST /admin/market/offers` | EM/SA | `CreateMarketOfferRequest` | `201 AdminMarketOfferDto` | Idem |
| `PATCH /admin/market/offers/{id}` | EM/SA | `UpdateMarketOfferRequest` | `AdminMarketOfferDto` | `If-Match`；已售價格不回寫歷史 Purchase |
| `POST /admin/market/offers/{id}/close` | EM/SA | `ReasonRequest` | `AdminMarketOfferDto` | Idem |
| `GET /admin/characters/{id}/wallets` | EM/AUD/SA | — | `WalletDto[]` | — |
| `GET /admin/characters/{id}/ledger` | EM/AUD/SA | Filters | `CursorPage<LedgerEntryDto>` | — |
| `POST /admin/characters/{id}/economy-adjustments` | MGR | `EconomyAdjustmentRequest` | `EconomyAdjustmentResultDto` | Idem；不設金額雙人覆核；原因必填，原子寫 Ledger 與 Audit，回傳 `auditLogId` |
| `POST /admin/characters/{id}/item-grants` | MGR | `ItemGrantRequest` | `ItemGrantResultDto` | Idem；不設數量雙人覆核；原因必填，原子寫 InventoryTransaction 與 Audit，回傳 `auditLogId` |
| `GET /admin/ledger/transactions/{id}` | EM/AUD/SA | — | `LedgerTransactionDetailDto` | — |
| `POST /admin/ledger/transactions/{id}/corrections` | MGR | `LedgerCorrectionRequest` | `LedgerTransactionDetailDto` | Idem；只能建立反向補正，不能改舊 Entry；原因必填並寫 Audit，不需雙人覆核 |

---

## 7. 關係系統

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /relationships` | Character | `visibility,cursor,limit` | `CursorPage<RelationshipDto>` | 自己可見的 NPC/玩家關係 |
| `GET /relationships/{id}/history` | Character | `cursor,limit` | `CursorPage<RelationshipHistoryDto>` | 依可見性過濾原因 |
| `GET /characters/{id}/relationships/public` | User | `cursor,limit` | `CursorPage<PublicRelationshipDto>` | 只回 Public |
| `POST /admin/relationships/{id}/adjust` | GM/SA | `RelationshipAdjustmentRequest` | `RelationshipDto` | Idem；更新與 History 同交易 |
| `POST /admin/characters/{id}/relationships` | GM/SA | `CreateRelationshipRequest` | `201 RelationshipDto` | Idem |
| `GET /admin/relationships/{id}` | GM/AUD/SA | — | `AdminRelationshipDto` | 含秘密標籤與歷史 |

---

## 8. 侍奉、生育與待生池

### 8.1 玩家端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /reproduction/status` | User | — | `ReproductionStatusDto` | 只公開總數，不公開待生池名單 |
| `GET /reproduction/rules` | User | — | `PublicReproductionRulesDto` | 受孕率、Pregnancy 天數、流產模式的可公開說明與規則版本 |
| `GET /reproduction/me` | User | — | `MyReproductionDto` | 嬪妃看懷孕；皇嗣看待生／出生狀態 |
| `GET /reproduction/audience-eligibility` | Character | `type=meal|bedchamber` | `AudienceEligibilityDto` | 顯示是否可申請及原因 |
| `POST /reproduction/audience-requests` | Character | `CreateAudienceRequest` | `201 AudienceRequestDto` | Idem；Bedchamber 交易鎖 ReproductionControl，無容量回 `HEIR_CAPACITY_EXHAUSTED` |
| `GET /reproduction/audience-requests` | Character | `cursor,limit` | `CursorPage<AudienceRequestDto>` | 只看自己的 |
| `GET /reproduction/pregnancies/{id}` | User | — | `PregnancyDto` | 僅母方、關聯出生角或管理員 |
| `GET /reproduction/offspring` | Character | `cursor,limit` | `CursorPage<OffspringDto>` | 依親子關係與公開規則 |

### 8.2 管理端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/reproduction/overview` | CR/GM/AUD/SA | — | `AdminReproductionOverviewDto` | waiting、reserved、available 必須同一 Snapshot |
| `PATCH /admin/reproduction/control` | GM/SA | `UpdateReproductionControlRequest` | `ReproductionControlDto` | `If-Match`；人工關閉原因必填 |
| `GET /admin/reproduction/wait-pool` | CR/GM/AUD/SA | Filters | `CursorPage<WaitPoolEntryDto>` | 含 Waiting/Drawn/Withdrawn |
| `POST /admin/reproduction/wait-pool/{entryId}/suspend` | CR/GM/SA | `ReasonRequest` | `WaitPoolEntryDto` | Idem；已有保留名額時須拒絕或先處理 Pregnancy |
| `POST /admin/reproduction/wait-pool/{entryId}/restore` | CR/GM/SA | `ReasonRequest` | `WaitPoolEntryDto` | Idem；角色仍須 WaitingBirth |
| `POST /admin/reproduction/wait-pool/{entryId}/withdraw` | CR/GM/SA | `ReasonRequest` | `WaitPoolEntryDto` | Idem；不可撤回已 Drawn |
| `GET /admin/reproduction/audience-requests` | GM/AUD/SA | Filters | `CursorPage<AudienceRequestDto>` | — |
| `POST /admin/reproduction/audience-requests/{id}/resolve` | GM/SA | `ResolveAudienceRequest` | `ResolveAudienceResultDto` | Idem；成功懷孕時原子保留名額 |
| `GET /admin/reproduction/pregnancies` | GM/AUD/SA | Filters | `CursorPage<AdminPregnancyDto>` | — |
| `POST /admin/reproduction/pregnancies/{id}/miscarry` | GM/SA | `PregnancyResolutionRequest` | `PregnancyDto` | Idem；釋放名額、通知同交易 |
| `POST /admin/reproduction/pregnancies/{id}/birth-preview` | GM/SA | — | `BirthPreviewDto` | 不抽號，只回候選數及規則 |
| `POST /admin/reproduction/pregnancies/{id}/draw-birth` | GM/SA | `DrawBirthRequest` | `BirthResultDto` | Idem；鎖控制列/Pregnancy/候選；不得指定角色 |
| `GET /admin/reproduction/births/{id}` | GM/AUD/SA | — | `AdminBirthDto` | 含候選 Hash 與演算法版本 |

`draw-birth` Request **不得**包含 `childCharacterId`、`sex` 或候選名單；正式候選由交易中的資料庫查詢取得。被抽中的 Prince/Princess 本身決定出生性別。

---

## 9. 陰謀、狀態效果與死亡

### 9.1 玩家端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `POST /intrigue/actions` | Character | `CreateIntrigueActionRequest` | `201 IntrigueActionReceiptDto` | Idem；扣除成本與建立秘密行動同交易 |
| `GET /intrigue/actions` | Character | `cursor,limit` | `CursorPage<MyIntrigueActionDto>` | 只回自己可知內容 |
| `GET /intrigue/actions/{id}` | Character | — | `MyIntrigueActionDto` | 不洩漏目標秘密防禦 |
| `GET /characters/me/effects` | Character | — | `StatusEffectDto[]` | 只回 visibility 允許內容 |
| `GET /characters/{id}/death` | User | — | `PublicDeathDto` | 只回公開死因 |

### 9.2 管理端

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/intrigue/actions` | GM/AUD/SA | Filters | `CursorPage<AdminIntrigueActionDto>` | MOD 不預設看到秘密玩法 |
| `POST /admin/intrigue/actions/{id}/resolve` | GM/SA | `ResolveIntrigueActionRequest` | `AdminIntrigueActionDto` | Idem；效果／死亡申請／通知同交易 |
| `POST /admin/characters/{id}/effects` | GM/SA | `CreateStatusEffectRequest` | `201 StatusEffectDto` | Idem |
| `POST /admin/effects/{id}/resolve` | GM/SA | `ResolveStatusEffectRequest` | `StatusEffectDto` | Idem |
| `GET /admin/deaths` | GM/MOD/AUD/SA | Filters | `CursorPage<AdminDeathDto>` | — |
| `GET /admin/deaths/{id}` | GM/MOD/AUD/SA | — | `AdminDeathDto` | 含覆核與秘密來源 |

死亡真正執行由 Approval API 的 `execute` 完成，確保第二人核准後才將 Character、Death、未完成資格及通知一次提交。

---

## 10. 通知與公告

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /notifications` | User | `unreadOnly,cursor,limit` | `CursorPage<NotificationDto>` | — |
| `GET /notifications/unread-count` | User | — | `UnreadCountDto` | 可供 30–60 秒輪詢 |
| `POST /notifications/read` | User | `MarkNotificationsReadRequest` | `MarkReadResultDto` | Idem；最多 100 IDs |
| `POST /notifications/read-all` | User | `ReadAllRequest` | `MarkReadResultDto` | Idem；可指定 before |
| `GET /admin/announcements` | MOD/AUD/SA | Filters | `CursorPage<AdminAnnouncementDto>` | — |
| `POST /admin/announcements` | MOD/SA | `CreateAnnouncementRequest` | `201 AdminAnnouncementDto` | Idem |
| `PATCH /admin/announcements/{id}` | MOD/SA | `UpdateAnnouncementRequest` | `AdminAnnouncementDto` | `If-Match` |
| `POST /admin/announcements/{id}/end` | MOD/SA | `ReasonRequest` | `AdminAnnouncementDto` | Idem |

---

## 11. 管理儀表板、使用者與營運

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/dashboard` | CR/GM/EM/MOD/AUD/SA | — | `AdminDashboardDto` | 只回呼叫者可見模組統計 |
| `GET /admin/users` | MOD/AUD/SA | Filters | `CursorPage<AdminUserSummaryDto>` | LINE User ID 遮罩化；AUD 不看不必要個資 |
| `GET /admin/users/{id}` | MOD/AUD/SA | — | `AdminUserDto` | 不回 Token |
| `POST /admin/users/{id}/suspend` | MOD/SA | `SuspendUserRequest` | `AdminUserDto` | Idem；撤銷所有 Session |
| `POST /admin/users/{id}/unsuspend` | MOD/SA | `ReasonRequest` | `AdminUserDto` | Idem |
| `GET /admin/users/{id}/sessions` | SA | — | `SessionSummaryDto[]` | — |
| `DELETE /admin/users/{id}/sessions` | SA | `ReasonRequest` | `204` | Idem；撤銷全部 Session |
| `GET /admin/admin-roles` | SA/AUD | Filters | `AdminRoleAssignmentDto[]` | — |
| `POST /admin/admin-roles` | SA | `GrantAdminRoleRequest` | `201 ApprovalRequestDto` | SuperAdmin 授權需雙人覆核 |
| `DELETE /admin/admin-roles/{userId}/{role}` | SA | `ReasonRequest` | `204` / `202 ApprovalRequestDto` | 自我移除最後 SA 必須拒絕 |

---

## 12. 雙人覆核、Audit、排程

| Method／Path | 權限 | Request | Response | 特殊錯誤／交易 |
|---|---|---|---|---|
| `GET /admin/approvals` | 對應管理角色/AUD/SA | Filters | `CursorPage<ApprovalRequestDto>` | Requester 可看但不可審自己的案件 |
| `GET /admin/approvals/{id}` | 對應管理角色/AUD/SA | — | `ApprovalRequestDetailDto` | — |
| `POST /admin/approvals/{id}/approve` | 對應管理角色/SA | `ApprovalDecisionRequest` | `ApprovalRequestDetailDto` | Idem；`SELF_APPROVAL_FORBIDDEN` |
| `POST /admin/approvals/{id}/reject` | 對應管理角色/SA | `ApprovalDecisionRequest` | `ApprovalRequestDetailDto` | Idem |
| `POST /admin/approvals/{id}/cancel` | Requester/SA | `ReasonRequest` | `ApprovalRequestDetailDto` | Pending 才可取消 |
| `POST /admin/approvals/{id}/execute` | 對應管理角色/SA | — | `ApprovalExecutionResultDto` | Idem；需 Approved 且未過期；執行 Payload 指定的固定 Handler |
| `GET /admin/audit-logs` | MGR/AUD | `action,actor,targetType,targetId,from,to,cursor,limit` | `CursorPage<AuditLogDto>` | IIS 後台稽核紀錄頁；唯讀、永久保存，不提供更新／刪除 API |
| `GET /admin/audit-logs/{id}` | MGR/AUD | — | `AuditLogDetailDto` | 顯示操作者、管理角色、理由、前後值、時間、Request ID 與來源 IP |
| `GET /admin/jobs` | SA/AUD | Filters | `ScheduledJobDto[]` | — |
| `GET /admin/jobs/{id}/runs` | SA/AUD | `cursor,limit` | `CursorPage<JobRunDto>` | — |
| `PATCH /admin/jobs/{id}` | SA | `UpdateScheduledJobRequest` | `ScheduledJobDto` | `If-Match` |
| `POST /admin/jobs/{id}/run` | SA | `RunJobRequest` | `202 JobRunDto` | Idem；同 Job 已執行時回 `JOB_ALREADY_RUNNING` |
| `POST /admin/jobs/{id}/retry/{runId}` | SA | — | `202 JobRunDto` | Idem；原 Run 必須 Failed |
| `GET /admin/outbox` | SA/AUD | Filters | `CursorPage<OutboxMessageDto>` | Payload 依敏感等級遮蔽 |
| `POST /admin/outbox/{id}/retry` | SA | — | `202 OutboxMessageDto` | Idem |

Approval `execute` 不接受任意 SQL、Type 名稱或自由 Payload；只能依 `actionType` 對應程式碼中已註冊的 Handler，例如 `character.death`、`game_setting.high_risk_publish`、`admin.grant_super_admin`。銀兩調整、道具發放與 Ledger 補正不進入 Approval 流程。

---

## 13. 主要 Request DTO

未列出的 Response DTO 直接由同名 Resource 欄位、公開範圍與 `version` 組成；不得直接序列化 EF Entity。

### 13.1 角色

```json
// CreateApplicationRequest / UpdateApplicationRequest
{
  "role": "consort | prince | princess",
  "familyName": "沈",
  "givenName": "知微",
  "courtesyName": null,
  "birthDateLabel": "永熙七年三月初七",
  "age": 17,
  "appearance": "至少六十字……",
  "biography": "...",
  "personality": "...",
  "strengths": "至少五十字……",
  "weaknesses": "至少五十字……",
  "likes": "至少五十字……",
  "dislikes": "至少五十字……",
  "portraitId": "uuid-or-null",
  "playerPortraitSubmissionId": "uuid-or-null",
  "formData": {}
}
```

規則：Draft 允許欄位不完整並可重複儲存；Submit 時才完整驗證。角色性別不接受獨立輸入，由 `role` 唯一推導：`consort/princess → female`、`prince → male`，避免 Role 與性別不一致。宮妃年齡 15～18；皇嗣姓氏固定「蕭」、年齡固定 0，待生時 `birthDateLabel` 可為 `null`，實際生辰由出生交易寫入。容貌至少 60 字；性格、擅、不擅、喜、不喜各至少 50 字；自介至少 200 字。`portraitId` 與 `playerPortraitSubmissionId` 提交時必須且只能提供一個。後端查立繪或上傳圖的 Owner、Role 與審核狀態；建立正式角色前上傳圖必須 Approved。所有文字移除首尾空白、拒絕 HTML。評分權重為字數 35%、文筆 50%、邏輯 15%。

```json
// UpdatePortraitCropRequest；座標皆相對於原圖，範圍 0–1
{
  "x": 0.08,
  "y": 0.02,
  "width": 0.84,
  "height": 0.96
}
```

```json
// PortraitReviewRequest
{
  "note": "符合人物圖片與社群規範"
}
```

圖片檔不得存入 PostgreSQL。自架版使用持久化 Media Volume，亦可切換 S3-compatible Object Storage；資料庫只保存 `storageKey`、Hash、尺寸、裁切與審核資料。API 以 Magic Bytes 驗證格式，重新編碼為 WebP、移除 EXIF，不直接公開原始檔路徑。拒絕、撤回或隔離檔案由排程在保留期後清除。

```json
// ApproveApplicationRequest
{
  "initialRankId": "uuid",
  "residenceId": "uuid-or-null",
  "scores": {
    "wordCount": 35,
    "writing": 45,
    "logic": 15
  },
  "reviewNote": "資料符合設定"
}
```

`initialRankId` 只能選 `isApplicationOption=true` 且角色類型相符的位號。後端從 Rank Seed 推導體質、容貌、心計、福氣；威望、恩寵與銀兩固定為 0。Request 不接受 `initialStats`、`initialCurrencies` 或 `actionPoints`，避免竄改。遊戲無每日行動點上限。

### 13.2 事件

```json
// CreateEventRequest / UpdateEventRequest
{
  "code": "spring-banquet-001",
  "title": "上巳春宴",
  "summary": "...",
  "bodyMarkdown": "...",
  "eventType": "main",
  "locationId": "uuid",
  "visibility": "public",
  "participantLimit": null,
  "rulesVersion": "event-rules-1",
  "rulesSnapshot": {},
  "opensAt": "2026-08-15T10:00:00Z",
  "deadlineAt": "2026-08-16T14:00:00Z"
}
```

```json
// SaveEventPostDraftRequest
{
  "bodyMarkdown": "可為空白的草稿內容",
  "clientRequestId": "uuid"
}
```

```json
// SubmitEventPostRequest
{
  "expectedVersion": 4,
  "clientRequestId": "uuid"
}
```

```json
// EventPostReviewRequest
{
  "note": "內容符合事件設定與社群規範",
  "clientRequestId": "uuid"
}
```

`request-revision` 與 `reject` 的 `note` 必填；`approve` 可填審核備註。所有決定都保存 Reviewer、時間、狀態前後值及 Audit Log。

```json
// EventSettlementRequest
{
  "expectedEventVersion": 8,
  "publicSummary": "春宴已畢……",
  "characterResults": [
    {
      "characterId": "uuid",
      "outcomeCode": "noticed-clue",
      "publicSummary": "察覺信箋墨色有異",
      "privatePayload": {},
      "rewards": {
        "stats": { "strategy": 2 },
        "currencies": { "silver": 30 },
        "items": []
      }
    }
  ]
}
```

### 13.3 經濟與道具

```json
// CreatePurchaseRequest
{
  "marketOfferId": "uuid",
  "quantity": 1
}
```

前端不得傳 `unitPrice`、`totalPrice` 或 `currencyCode`；Response 才回傳交易時快照。

```json
// UseItemRequest
{
  "quantity": 1,
  "targetCharacterId": "uuid-or-null",
  "context": {}
}
```

```json
// EconomyAdjustmentRequest
{
  "currencyCode": "silver",
  "amount": 300,
  "reasonCode": "service-compensation",
  "reasonText": "事件結算異常補償"
}
```

```json
// ItemGrantRequest
{
  "itemDefinitionId": "uuid",
  "quantity": 2,
  "reasonCode": "event-reward-correction",
  "reasonText": "補發上巳春宴獎勵"
}
```

```json
// LedgerCorrectionRequest
{
  "amount": -300,
  "reasonCode": "duplicate-adjustment-reversal",
  "reasonText": "撤銷重複送出的 req_8C2F"
}
```

`amount` 可正可負但不可為 0；扣除後不得小於 0。`reasonCode` 與具體 `reasonText` 必填。任何管理金額皆直接由一名有權限管理員執行，不設雙人覆核門檻；同一交易必須記錄 `initiatedBy`、Ledger、Audit Log 與 Request ID。

### 13.4 生育

```json
// CreateAudienceRequest
{
  "audienceType": "meal | bedchamber"
}
```

```json
// ResolveAudienceRequest
{
  "decision": "approved | rejected",
  "publicNote": "...",
  "privateNote": "..."
}
```

管理員只能決定核准或拒絕，不能指定是否受孕、成功率、Roll、`dueAt` 或規則版本。核准後 API 必須在交易中重新檢查 `available = waiting - ongoing reservations`，再讀取已發布規則；以 1～100 的密碼學安全 Roll 判斷 `roll <= conceptionRatePercent`。成功時 `dueAt = conceivedAt + pregnancyDurationDays`，預設為 100% 與 10 天；結果、Rate、Roll、期限及規則快照永久保存。

```json
// PregnancyResolutionRequest（流產）
{
  "expectedPregnancyVersion": 3,
  "triggerCode": "severe-poison-event",
  "sourceType": "status_effect",
  "sourceId": "uuid",
  "publicNote": "因劇情事件失去皇嗣",
  "privateReason": "中毒效果超過事件規則門檻且未於期限內解除",
  "clientRequestId": "uuid"
}
```

`privateReason` 至少 5 字，`event_only` 模式下 `sourceType/sourceId` 必須指向已結算事件或符合 Allowlist 的狀態效果；不得只靠管理頁按鈕任意流產。

```json
// DrawBirthRequest
{
  "expectedPregnancyVersion": 3,
  "rulesVersion": "reproduction-1"
}
```

Request 刻意沒有性別與指定人物欄位。

### 13.5 管理與覆核

```json
// ReasonRequest
{ "reason": "具體且可稽核的原因" }
```

```json
// DeathRequest
{
  "causeCode": "poison-fatal",
  "publicCause": "久病不治",
  "privateDetails": {},
  "occurredAt": "2026-08-15T14:00:00Z",
  "reason": "依事件 E-102 結算"
}
```

```json
// ApprovalDecisionRequest
{
  "note": "已覆核事件、狀態效果與玩家紀錄"
}
```

### 13.6 故事、稱號與遊戲設定

```json
// UpsertStoryChapterRequest
{
  "code": "chapter-02",
  "chapterNo": 2,
  "title": "長夜聞鈴",
  "summary": "西六宮深夜傳來銀鈴聲。",
  "opensAt": null,
  "closesAt": null
}
```

```json
// UpsertStoryNodeRequest
{
  "code": "west-corridor-entry",
  "nodeType": "narrative",
  "title": "西側迴廊",
  "bodyMarkdown": "宮門落鎖後……",
  "sortOrder": 10,
  "locationId": "uuid-or-null",
  "linkedEventRoomId": "uuid-or-null",
  "isEntryNode": true,
  "branchRules": {
    "all": [
      { "field": "stats.strategy", "operator": "gte", "value": 60 }
    ]
  }
}
```

`branchRules` 只允許後端註冊的 Field、Operator 與 Value Type，不接受任意 C#、SQL、JavaScript 或反射類型。發布 StoryArc／Chapter 時必須保存完整 `content_revisions.snapshot`；回復舊版會建立新 Revision，不修改舊紀錄。

```json
// UpsertTitleDefinitionRequest
{
  "code": "penglai-honored-guest",
  "displayName": "蓬萊雅客",
  "description": "完成上巳春宴特殊支線",
  "category": "achievement",
  "appliesToRole": null,
  "visibility": "public",
  "styleToken": "title-jade",
  "sortOrder": 20,
  "isActive": true
}
```

```json
// GrantCharacterTitleRequest
{
  "titleDefinitionId": "uuid",
  "isPrimary": false,
  "reason": "完成事件 spring-banquet-001"
}
```

```json
// UpdateGameSettingDraftRequest
{
  "draftValue": {
    "enabled": true,
    "url": "https://buymeacoffee.com/yourname",
    "label": "請我們喝杯咖啡"
  },
  "changeReason": "啟用正式贊助頁連結"
}
```

```json
// PublishSettingRequest / PublishContentRequest
{
  "expectedVersion": 7,
  "changeNote": "第二章開放前設定調整"
}
```

遊戲設定只能修改 Allowlist 中既有的 `settingKey`，並依該列 `validationSchema` 驗證。資料庫密碼、LINE Secret、Session Key、連線字串及檔案路徑不屬於遊戲設定，永遠不得由管理網頁讀寫。`riskLevel=high` 的設定發布與回復只建立 `ApprovalRequest`，第二人核准並 Execute 後才更新 PublishedValue。

---

## 14. 主要 Response DTO

### 14.1 `MeDto`

```json
{
  "user": {
    "id": "uuid",
    "displayName": "Max",
    "status": "active",
    "preferences": {},
    "version": 4
  },
  "characterState": "active",
  "character": {
    "id": "uuid",
    "displayName": "沈知微",
    "role": "consort",
    "status": "active",
    "portraitUrl": "https://game.example/assets/...",
    "rank": { "id": "uuid", "name": "從六品・婕妤" },
    "version": 8
  },
  "adminRoles": [],
  "unreadNotificationCount": 3,
  "pendingActions": []
}
```

### 14.2 `ReproductionStatusDto`

```json
{
  "isOpen": true,
  "closedReason": null,
  "waitingCount": 14,
  "reservedCount": 2,
  "availableCount": 12,
  "asOf": "2026-08-15T14:00:00Z",
  "version": 6
}
```

玩家端不回傳待生角色名單；管理端 `AdminReproductionOverviewDto` 才可連到名單。

### 14.3 `BirthResultDto`

```json
{
  "birthId": "uuid",
  "pregnancyId": "uuid",
  "child": {
    "characterId": "uuid",
    "displayName": "蕭景珩",
    "role": "prince",
    "sex": "male",
    "status": "active"
  },
  "candidateCount": 14,
  "rulesVersion": "reproduction-1",
  "bornAt": "2026-09-15T14:00:00Z",
  "transactionId": "uuid"
}
```

### 14.3A `PublicReproductionRulesDto`

```json
{
  "conceptionRatePercent": 100,
  "pregnancyDurationDays": 10,
  "miscarriageMode": "event_only",
  "miscarriageDescription": "不進行每日隨機流產；只有符合已發布事件規則時才可能觸發。",
  "rulesVersion": "reproduction-1",
  "effectiveAt": "2026-08-15T08:00:00Z"
}
```

### 14.4 `PurchaseResultDto`

```json
{
  "purchaseId": "uuid",
  "marketOfferId": "uuid",
  "quantity": 1,
  "unitPrice": 120,
  "totalPrice": 120,
  "currencyCode": "silver",
  "walletBalance": 1720,
  "inventoryEntry": {
    "id": "uuid",
    "itemCode": "spring-hairpin",
    "quantity": 1,
    "version": 2
  },
  "ledgerTransactionId": "uuid"
}
```

### 14.5 `PublicSupportSettingDto`

```json
{
  "enabled": true,
  "configured": false,
  "url": null,
  "label": "請我們喝杯咖啡",
  "version": 3
}
```

此 DTO 不含付款金額、會員、Webhook 或遊戲帳號對應。`enabled=true,configured=false` 時仍顯示右上說明按鈕，但 Modal 的外部 CTA 停用。URL 未通過 Allowlist 時回 `configured=false,url=null`，不得導向平台首頁。

### 14.6 `EconomyAdjustmentResultDto`

```json
{
  "transactionId": "uuid",
  "auditLogId": 18342,
  "currencyCode": "silver",
  "amount": 300,
  "balanceBefore": 1540,
  "balanceAfter": 1840,
  "reasonCode": "service-compensation",
  "reasonText": "事件結算異常補償",
  "adjustedBy": "uuid",
  "adjustedAt": "2026-08-15T07:42:00Z"
}
```

### 14.7 `AuditLogDetailDto`

```json
{
  "id": 18342,
  "occurredAt": "2026-08-15T07:42:00Z",
  "actorUserId": "uuid",
  "actorRole": "economy_manager",
  "action": "economy.adjust",
  "targetType": "character",
  "targetId": "uuid",
  "beforeData": { "silver": 1540 },
  "afterData": { "silver": 1840 },
  "reason": "事件結算異常補償",
  "requestId": "req_8C2F",
  "ipAddress": "masked-by-policy",
  "metadata": { "transactionId": "uuid" }
}
```

---

## 15. Endpoint 數量與 MVP 凍結規則

本清單定義：

- 公開／帳號／角色 API：13 支。
- 角色、人物圖片、稱號與申請：51 支。
- 世界、故事與遊戲設定：43 支。
- 事件與外部互動：40 支。
- 經濟與庫存：27 支。
- 關係：6 支。
- 生育：21 支。
- 陰謀與死亡：11 支。
- 通知與公告：8 支。
- 管理儀表板、使用者與營運：10 支。
- 雙人覆核、Audit、排程：15 支。

合計 245 個 Method + Path 組合；實作可依 Sprint 分批，但已列出的路徑、狀態碼與 DTO 變更必須透過 OpenAPI Review。MVP 不要求第一天全部上線，`後端規格書_v1.0.md` 會標出 P0/P1/P2 實作順序。
