# 部署順序

伺服器：`<origin-ip>`，IIS + PostgreSQL 18（port **5433**）
API 對外：`https://gongwei-api.miglow.vip`（Cloudflare 橘雲代理）

前置：安裝 [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)（.NET 10）與 PowerShell 7。

## 一次性設定

```powershell
# 1. 資料庫（已完成，重建時才需要）
.\db\setup-database.ps1

# 2. Cloudflare Origin Certificate
#    先在 Cloudflare 後台：SSL/TLS -> Origin Server -> Create Certificate
#    Hostnames 填 *.miglow.vip 與 miglow.vip，Key format 選 PEM
.\iis\import-origin-certificate.ps1 -CertificatePath .\origin.pem -PrivateKeyPath .\origin.key

# 3. IIS 站台、應用程式集區、資料目錄、443 繫結
.\iis\install-sites.ps1 -CertificateThumbprint <上一步印出的 thumbprint>
```

`install-sites.ps1` 會建立 `C:\GongWeiData\keys` 並把權限收到只有 `GongWeiApiPool` 和
Administrators 可讀。那是 Data Protection 金鑰環，負責封裝 LINE 登入的 nonce 與 PKCE
verifier；放在發行資料夾裡或用預設的 per-process 金鑰，App Pool 一回收就會讓所有進行中的
登入失敗（`line_login_v1.1` §8.4）。

## 每次發行

```powershell
.\iis\publish.ps1
```

`GongWei.Admin` 目前無法編譯（任務 #15），所以預設只發行 API。修好後加 `-IncludeAdmin`。

## 設定值與密鑰

```powershell
.\iis\set-app-settings.ps1
```

會逐項隱藏輸入，寫進 IIS 的 `applicationHost.config`，不落地成檔案。連線字串若不是指向
`gongwei` 資料庫會直接拒絕 —— 同一個 PostgreSQL 執行個體上還有 optilogin、ttsp、payment
等資料庫，指錯不是可以復原的錯誤。

需要輪替 LINE Channel Secret 時再跑一次：

```powershell
.\iis\set-app-settings.ps1 -SkipConnectionString
```

## 驗證

```powershell
.\iis\verify-endpoint.ps1 -OriginAddress <origin-ip>
```

分開檢查 origin 與 Cloudflare 邊緣，因為兩邊壞掉的樣子不一樣：

| origin | 邊緣 | 意思 |
|---|---|---|
| TLS 失敗 | 525 | 這個主機名稱沒有 HTTPS 繫結或沒有憑證 |
| TLS 失敗 | 502 | 繫結在，但應用程式沒跑起來 |
| TLS 正常 | 525 | Cloudflare 不信任該憑證，檢查 SSL/TLS 模式 |
| 都正常 | 200 | 完成 |

origin 那一段是直接對 IP 建 TLS 連線並帶正確的 SNI，不經過 DNS，所以能真的分辨是
IIS 沒繫結還是 Cloudflare 沒接上。

全綠之後把 Cloudflare SSL/TLS 模式設為 **Full (strict)**。

健康檢查端點在 **`/api/v1/health/live`** 與 **`/api/v1/health/ready`**，不是無版本前綴的
`/health/*` —— `api_v1_v1.1` §1 規定表格中的路徑都相對於 Base URL，並且明文禁止另外提供
一組無版本前綴的端點。監控設定請用含前綴的網址。

`/api/v1/health/ready` 會逐項列出失敗原因，例如：

```json
{"status":"Unhealthy","checks":[
  {"name":"database","status":"Healthy"},
  {"name":"line-login-config","status":"Unhealthy",
   "description":"Not configured: LineLogin:ChannelSecret. ..."},
  {"name":"data-protection","status":"Healthy"}]}
```

`data-protection` 那項會實際做一次封裝／解封往返，因為金鑰環寫不進去的症狀最難查：
登入一直正常，直到處理程序回收，然後所有進行中的嘗試同時失敗且看不出原因。

## 資料庫初始化（`implementation_bootstrap_v1.1` §7）

順序是固定的，因為 seed 需要一個 super admin，而 super admin 需要 `game.users` 裡先有一列，
那又需要有人真的用 LINE 登入過一次：

```powershell
# 1. 站長用瀏覽器走一次 https://gongwei-api.miglow.vip/api/v1/auth/line/start?returnUrl=...
# 2. 取得該帳號的 LINE sub 後：
dotnet tools\GongWei.AdminCli\GongWei.AdminCli.dll grant-super-admin `
    --line-user-id '<LINE_SUB>' --reason 'initial production bootstrap'

# 3. 灌規則與 NPC
psql -h 127.0.0.1 -p 5433 -U gongwei_app -d gongwei -f db\authoritative\v1.1\seed_rules_v1.1.sql
psql -h 127.0.0.1 -p 5433 -U gongwei_app -d gongwei -f db\authoritative\v1.1\seed_npcs_v1.1.sql

# 4. 驗收
dotnet tools\GongWei.AdminCli\GongWei.AdminCli.dll verify-database
```

## 背景服務

```powershell
.\worker\install-service.ps1
```

`GongWei.Worker` 目前無法編譯（任務 #16），修好前先不要裝。

## 指令碼相容性

`verify-endpoint.ps1`、`install-sites.ps1`、`publish.ps1`、`set-app-settings.ps1`
在 Windows PowerShell 5.1 下可執行。`import-origin-certificate.ps1` 需要 PowerShell 7
（`X509Certificate2.CreateFromPem` 是 .NET 5 之後才有的）。
