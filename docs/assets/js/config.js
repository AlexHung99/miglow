/* =============================================================================
 * 微光 MI GLOW — 前台執行環境設定
 *
 * MIGLOW_API_BASE：Optimind 後端網址（會員註冊/登入用）。
 *   本機測試（IIS）例：  "http://localhost/TPMIS"
 *   正式部署：           "https://api.miglow.vip"
 *   留空 ""＝同源（僅在直接從 Optimind 站台開啟前台時可用）。
 *   注意：結尾不要加斜線，也不要加 /web/Service。
 *
 * MIGLOW_GOOGLE_CLIENT_ID：Google 登入用的 OAuth Client ID（Web）。
 *   到 Google Cloud Console 建立後填入；留空則不顯示 Google 按鈕。
 * ========================================================================== */
window.MIGLOW_API_BASE = "https://api.miglow.vip";
window.MIGLOW_GOOGLE_CLIENT_ID = "505425736885-3pc2h7prs1q7cn8a0l12hnbb8n80c8he.apps.googleusercontent.com";
