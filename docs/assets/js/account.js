/* =============================================================================
 * 微光 MI GLOW — 會員：登入 / 註冊 / 忘記密碼 / 會員中心
 * 登入/註冊改打 Optimind 後端 wsMGAUTH.asmx（MG.auth）；登入狀態+token 存於 localStorage。
 * 忘記密碼仍為原型（待接 Email 重設）。
 * ========================================================================== */
(function () {
  "use strict";
  const emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  const EYE = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/></svg>';
  const EYE_OFF = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M4 4l16 16M9.9 5.2A10 10 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.6M6.3 6.3A17 17 0 0 0 2 12s3.5 7 10 7a10 10 0 0 0 3.5-.6M9.5 9.5a3 3 0 0 0 4.2 4.2"/></svg>';

  function read(k, d) { try { return JSON.parse(localStorage.getItem(k)) ?? d; } catch (e) { return d; } }
  function members() { return read("miglow_members", []); }
  function val(id) { const e = document.getElementById(id); return e ? e.value : ""; }
  function validate(rules) {
    let ok = true;
    rules.forEach(([id, fn, msg]) => {
      const el = document.getElementById(id); if (!el) return;
      const f = el.closest(".field"); const err = f && f.querySelector(".field-error");
      if (!fn(el.value)) { ok = false; el.classList.add("is-invalid"); if (err) err.textContent = msg; }
      else { el.classList.remove("is-invalid"); if (err) err.textContent = ""; }
    });
    return ok;
  }
  function notice(html) { const n = document.getElementById("authNotice"); if (n) n.innerHTML = html; }

  /* 密碼顯示切換 */
  function initPasswordToggles() {
    document.querySelectorAll("[data-toggle]").forEach((btn) => {
      btn.innerHTML = EYE;
      btn.addEventListener("click", () => {
        const input = document.getElementById(btn.dataset.toggle);
        const show = input.type === "password";
        input.type = show ? "text" : "password";
        btn.innerHTML = show ? EYE_OFF : EYE;
      });
    });
  }

  /* ---------------- 登入 ---------------- */
  function initLogin() {
    const form = document.getElementById("loginForm");
    if (!form) return;
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      if (!validate([
        ["email", (v) => emailRe.test(v), "請輸入有效的電子郵件"],
        ["password", (v) => v.length >= 6, "密碼至少 6 碼"]
      ])) return;

      const btn = form.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      notice('<div class="notice">登入中…</div>');
      window.MG.auth.login(val("email").trim(), val("password"))
        .then((res) => {
          if (!res || !res.ok) {
            notice('<div class="notice notice--err">' + ((res && res.error) || "登入失敗") + "</div>");
            if (btn) btn.disabled = false;
            return;
          }
          window.MG.session.login(res.member, res.token);
          location.href = "member.html";
        })
        .catch(() => {
          notice('<div class="notice notice--err">無法連線到伺服器，請稍後再試。</div>');
          if (btn) btn.disabled = false;
        });
    });
  }

  /* ---------------- 註冊 ---------------- */
  function initRegister() {
    const form = document.getElementById("registerForm");
    if (!form) return;
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      const agree = document.getElementById("agree");
      let ok = validate([
        ["name", (v) => !!v.trim(), "請輸入姓名"],
        ["phone", (v) => /^\d{8,}$/.test(v.replace(/\D/g, "")), "請輸入有效手機號碼"],
        ["email", (v) => emailRe.test(v), "請輸入有效的電子郵件"],
        ["password", (v) => v.length >= 6, "密碼至少 6 碼"],
        ["password2", (v) => v === val("password"), "兩次密碼不一致"]
      ]);
      if (!agree.checked) { ok = false; notice('<div class="notice notice--err">請先同意隱私權政策與服務條款。</div>'); }
      if (!ok) return;

      const btn = form.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      notice('<div class="notice">註冊中…</div>');
      window.MG.auth.register(val("email").trim(), val("password"), val("name").trim(), val("phone").trim())
        .then((res) => {
          if (!res || !res.ok) {
            notice('<div class="notice notice--err">' + ((res && res.error) || "註冊失敗") + "</div>");
            if (btn) btn.disabled = false;
            return;
          }
          window.MG.session.login(res.member, res.token);
          location.href = "member.html";
        })
        .catch(() => {
          notice('<div class="notice notice--err">無法連線到伺服器，請稍後再試。</div>');
          if (btn) btn.disabled = false;
        });
    });
  }

  /* ---------------- 忘記密碼 ---------------- */
  function initForgot() {
    const form = document.getElementById("forgotForm");
    if (!form) return;
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      if (!validate([["email", (v) => emailRe.test(v), "請輸入有效的電子郵件"]])) return;
      const btn = form.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      notice('<div class="notice">寄送中…</div>');
      window.MG.auth.requestPasswordReset(val("email").trim())
        .then(() => {
          form.reset();
          notice('<div class="notice notice--ok">若此信箱已註冊，重設密碼連結已寄出，請至信箱查收（含垃圾信匣）。</div>');
          if (btn) btn.disabled = false;
        })
        .catch(() => {
          notice('<div class="notice notice--err">無法連線到伺服器，請稍後再試。</div>');
          if (btn) btn.disabled = false;
        });
    });
  }

  /* ---------------- 重設密碼（reset-password.html）---------------- */
  function initReset() {
    const form = document.getElementById("resetForm");
    if (!form) return;
    const token = new URLSearchParams(location.search).get("token") || "";
    if (!token) {
      notice('<div class="notice notice--err">連結無效，請重新申請忘記密碼。</div>');
      form.style.display = "none";
      return;
    }
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      if (!validate([
        ["password", (v) => v.length >= 6, "密碼至少 6 碼"],
        ["password2", (v) => v === val("password"), "兩次密碼不一致"]
      ])) return;
      const btn = form.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      notice('<div class="notice">處理中…</div>');
      window.MG.auth.resetPassword(token, val("password"))
        .then((res) => {
          if (!res || !res.ok) {
            notice('<div class="notice notice--err">' + ((res && res.error) || "重設失敗") + "</div>");
            if (btn) btn.disabled = false;
            return;
          }
          form.style.display = "none";
          notice('<div class="notice notice--ok">密碼已重設！請用新密碼登入。<br><a href="login.html" class="btn btn--solid" style="margin-top:10px">前往登入</a></div>');
        })
        .catch(() => {
          notice('<div class="notice notice--err">無法連線到伺服器，請稍後再試。</div>');
          if (btn) btn.disabled = false;
        });
    });
  }

  /* ---------------- 會員中心 ---------------- */
  function initMember() {
    const root = document.getElementById("memberRoot");
    if (!root) return;
    const me = window.MG.session.get();

    if (!me) {
      root.innerHTML = `
        <div class="empty-state">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 3.6-6.5 8-6.5S20 17 20 21"/></svg>
          <h3>請先登入</h3>
          <p>登入後即可查看會員資料與訂單。</p>
          <a href="login.html" class="btn btn--solid">前往登入</a>
        </div>`;
      return;
    }

    const ST = window.MG.ORDER_STATUS;
    const money = (n) => window.MG.money(n);
    function ordersTable(orders) {
      if (!orders || !orders.length) {
        return `<div class="empty-state" style="padding:40px 0"><p>目前還沒有訂單。</p><a href="products.html" class="btn btn--ghost">去逛逛</a></div>`;
      }
      return `<table class="order-table">
          <thead><tr><th>訂單編號</th><th>日期</th><th>金額</th><th>狀態</th><th></th></tr></thead>
          <tbody>${orders.map((o) => `
            <tr>
              <td data-label="訂單編號">${o.order_no}</td>
              <td data-label="日期">${window.MG.dateFmt(o.created_at)}</td>
              <td data-label="金額">${money(o.total)}</td>
              <td data-label="狀態"><span class="status-tag" data-s="${o.order_status}">${ST[o.order_status] || o.order_status}</span></td>
              <td data-label=""><a href="order-complete.html?no=${o.order_no}" style="color:var(--accent)">查看</a></td>
            </tr>`).join("")}
          </tbody>
         </table>`;
    }
    const ordersHtml = '<div id="memberOrders" style="color:#999;padding:20px 0">載入中…</div>';

    root.innerHTML = `
      <div class="account-layout">
        <nav class="account-nav">
          <a href="#" class="is-active" data-tab="profile">${window.MIGLOW_ICON.member}會員資料</a>
          <a href="#" data-tab="orders"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 9h8M8 13h8M8 17h5"/></svg>我的訂單</a>
          <a href="#" data-tab="logout"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M15 12H4m0 0 3.5-3.5M4 12l3.5 3.5M14 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4"/></svg>登出</a>
        </nav>
        <div>
          <div class="account-card" data-panel="profile">
            <h2>會員資料</h2>
            <div class="info-grid">
              <div class="info-cell"><div class="k">姓名</div><div class="v">${me.display_name || "—"}</div></div>
              <div class="info-cell"><div class="k">電子郵件</div><div class="v">${me.email || "—"}</div></div>
              <div class="info-cell"><div class="k">手機</div><div class="v">${me.phone || "—"}</div></div>
              <div class="info-cell"><div class="k">會員等級</div><div class="v">一般會員</div></div>
            </div>
          </div>
          <div class="account-card" data-panel="orders" hidden>
            <h2>我的訂單</h2>
            ${ordersHtml}
          </div>
        </div>
      </div>`;

    root.querySelectorAll("[data-tab]").forEach((a) =>
      a.addEventListener("click", (e) => {
        e.preventDefault();
        const tab = a.dataset.tab;
        if (tab === "logout") {
          const t = window.MG.session.token();
          if (window.MG.auth && t) { try { window.MG.auth.logout(t); } catch (err) {} }
          window.MG.session.logout();
          window.MG.toast("已登出");
          setTimeout(() => (location.href = "index.html"), 600);
          return;
        }
        root.querySelectorAll("[data-tab]").forEach((x) => x.classList.toggle("is-active", x === a));
        root.querySelectorAll("[data-panel]").forEach((p) => (p.hidden = p.dataset.panel !== tab));
      })
    );

    // 非同步載入「我的訂單」（後端 wsMGSHOP）
    window.MG.orders.myOrders()
      .then((res) => {
        const box = document.getElementById("memberOrders");
        if (!box) return;
        if (res && res.ok) box.outerHTML = ordersTable(res.orders);
        else box.innerHTML = '<p style="color:#c0392b">訂單載入失敗：' + ((res && res.error) || "") + "</p>";
      })
      .catch(() => {
        const box = document.getElementById("memberOrders");
        if (box) box.innerHTML = '<p style="color:#c0392b">無法連線伺服器</p>';
      });
  }

  /* ---------------- Google 登入 ---------------- */
  function handleGoogleCredential(response) {
    if (!response || !response.credential) return;
    notice('<div class="notice">Google 驗證中…</div>');
    window.MG.auth.loginWithGoogle(response.credential)
      .then((res) => {
        if (!res || !res.ok) { notice('<div class="notice notice--err">' + ((res && res.error) || "Google 登入失敗") + "</div>"); return; }
        window.MG.session.login(res.member, res.token);
        location.href = "member.html";
      })
      .catch(() => { notice('<div class="notice notice--err">無法連線到伺服器，請稍後再試。</div>'); });
  }

  function initGoogleSignin() {
    const containers = document.querySelectorAll(".google-signin");
    if (!containers.length) return;
    const cid = window.MIGLOW_GOOGLE_CLIENT_ID || "";
    if (!cid) return; // 未設定 Client ID 就不顯示 Google 按鈕
    let tries = 0;
    (function ready() {
      if (window.google && google.accounts && google.accounts.id) {
        google.accounts.id.initialize({ client_id: cid, callback: handleGoogleCredential });
        containers.forEach((c) =>
          google.accounts.id.renderButton(c, { theme: "outline", size: "large", text: "continue_with", shape: "pill", width: 280 })
        );
      } else if (tries++ < 40) {
        setTimeout(ready, 150);
      }
    })();
  }

  document.addEventListener("DOMContentLoaded", function () {
    initPasswordToggles();
    initLogin();
    initRegister();
    initForgot();
    initReset();
    initMember();
    initGoogleSignin();
  });
})();
