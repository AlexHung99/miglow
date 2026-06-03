# 微光 MI GLOW 網站

手作甜點品牌「微光 MI GLOW」官方網站。視覺方向：奶油米白、柔和光影、大量留白、襯線中文字。

目前進度：**前端全頁面已完成**（資料驅動、RWD 三斷點驗證通過，含完整購物→結帳→匯款後五碼與會員流程，皆為前端 mock）。後台與資料庫已先行規劃，待前端定稿後開發。

---

## 目錄結構

```
miglow/
├─ docs/                      ← GitHub Pages 發布根目錄（前端）
│  ├─ index.html              首頁
│  ├─ products.html           商品列表
│  ├─ product.html            商品詳細（?slug=）
│  ├─ about.html              關於我們
│  ├─ news.html / news-detail.html   最新消息列表 / 詳細（?slug=，含 SEO meta）
│  ├─ ordering.html           訂購須知 + FAQ
│  ├─ contact.html            聯絡我們（表單 + 聯絡資訊）
│  ├─ cart.html               購物車
│  ├─ checkout.html           結帳
│  ├─ order-complete.html     訂單成立 + 匯款後五碼回填
│  ├─ login.html / register.html / forgot-password.html   會員登入 / 註冊 / 忘記密碼
│  ├─ member.html             會員中心（資料 + 訂單查詢）
│  ├─ .nojekyll               關閉 Jekyll 處理
│  ├─ assets/
│  │  ├─ css/
│  │  │  ├─ style.css         設計系統 / 全站樣式（首頁與共用，含 RWD）
│  │  │  └─ pages.css         內頁元件樣式（商品/購物車/結帳/會員/消息/聯絡，含 RWD）
│  │  ├─ js/
│  │  │  ├─ data.js           前端資料來源（原型；未來改為 API fetch）
│  │  │  ├─ components.js      共用元件：導覽列 / 頁尾 / 行動選單 / 購物車徽章 / 進場動畫
│  │  │  ├─ store.js          共用 Store：購物車 / 登入狀態 / 訂單 / 格式化 / Toast
│  │  │  ├─ home.js           首頁：輪播 / 商品 / 特色 / IG
│  │  │  ├─ products.js       商品列表 + 詳細
│  │  │  ├─ content.js        關於 / 訂購須知 / 聯絡 / FAQ
│  │  │  ├─ news.js           最新消息列表 + 詳細
│  │  │  ├─ cart.js           購物車
│  │  │  ├─ checkout.js       結帳 + 訂單成立(後五碼)
│  │  │  └─ account.js        會員：登入 / 註冊 / 忘記密碼 / 會員中心
│  │  └─ images/
│  │     ├─ brand/            hero.png、bags.png
│  │     └─ products/         p-strawberry.png、p-chocolate.png、p-original.png
│  └─ data/                   API 合約樣本（site/banners/products/news .json）
├─ brand_design/              品牌設計稿
└─ web_spec/                  規格書
   ├─ MI_GLOW_Web軟體規格書.md
   ├─ MI_GLOW_DB規劃.md          ← 資料庫先行規劃（b_miglow_*）
   ├─ MI_GLOW_後台與API規劃.md    ← 後台功能 + 前台 API 合約
   ├─ db_spec.md
   └─ Optimind 後台功能規格書.md
```

---

## 本機預覽

頁面採 `<script>` 載入資料，可直接以 `file://` 開 `docs/index.html`。
若要完整模擬（含相對路徑與未來 fetch），用任一靜態伺服器：

```powershell
# 本專案已附 PowerShell 靜態伺服器
powershell -ExecutionPolicy Bypass -File .claude/serve.ps1 -Port 8080 -Root docs
# 開 http://localhost:8080/
```

---

## 發布到 GitHub Pages

1. 將本資料夾初始化為 git 並推上 GitHub：
   ```bash
   git init && git add . && git commit -m "init miglow site"
   git branch -M main
   git remote add origin <your-repo-url>
   git push -u origin main
   ```
2. GitHub repo → **Settings → Pages**：
   - Source：`Deploy from a branch`
   - Branch：`main` / 資料夾 `/docs`
3. 數分鐘後即可於 `https://<account>.github.io/<repo>/` 瀏覽。

> `docs/.nojekyll` 已加入，避免 GitHub Pages 用 Jekyll 處理靜態資源。

---

## 內容怎麼改（給維護者 / 後期美工）

所有文字與商品資料集中在 **`docs/assets/js/data.js`**，不寫死在 HTML：

- 改 Banner 文案 / 連結：`MIGLOW_DATA.banners`
- 改商品名稱 / 價格 / 介紹：`MIGLOW_DATA.products`
- 改品牌 / 聯絡 / 社群 / 匯款 / 滿額免運：`MIGLOW_DATA.site`

**換圖**：直接覆蓋同名檔即可，無需改程式：

| 用途 | 檔案 |
| --- | --- |
| 首頁主視覺 | `docs/assets/images/brand/hero.png` |
| 關於我們 / 品牌 | `docs/assets/images/brand/bags.png` |
| 草莓雪花酥 | `docs/assets/images/products/p-strawberry.png` |
| 巧克力雪花酥 | `docs/assets/images/products/p-chocolate.png` |
| 原味雪花酥 | `docs/assets/images/products/p-original.png` |

> 原型商品圖為品牌設計稿切圖，後期由美工以正式去背 / 情境照直接替換同名檔。

---

## 與未來後台的銜接

前端已做成資料驅動。上線後台時：

1. 把 `data.js` 改為向 API `fetch`（端點見 `web_spec/MI_GLOW_後台與API規劃.md` B 節）。
2. 購物車 `addToCart()` 由 `localStorage` 改打 `/api/cart`。
3. 圖片改用後台上傳回傳的 R2 公開 URL（需版本化）。

`data/*.json` 即為 API 應回傳的結構樣本（欄位 = 資料表欄位 = 前端欄位，三者一致）。
```
banners.json  → GET /api/banners   → b_miglow_banner
products.json → GET /api/products  → b_miglow_product
site.json     → GET /api/site-settings → b_miglow_site_setting
```
