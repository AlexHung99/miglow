const defaultApiBase = ["localhost", "127.0.0.1"].includes(location.hostname)
  ? "http://localhost:5073"
  : "http://122.116.181.61:5073";
const API_BASE = localStorage.getItem("postoria-api-base") || defaultApiBase;

const app = document.querySelector("#app");
const toast = document.querySelector("#toast");

const state = {
  member: readJson("postoria-member"),
  token: localStorage.getItem("postoria-token") || ""
};

window.addEventListener("hashchange", render);
document.addEventListener("submit", handleSubmit);
document.addEventListener("click", handleClick);

function readJson(key) {
  try {
    return JSON.parse(localStorage.getItem(key) || "null");
  } catch {
    return null;
  }
}

function setSession(member, token, expiresAt) {
  state.member = member;
  state.token = token;
  localStorage.setItem("postoria-member", JSON.stringify(member));
  localStorage.setItem("postoria-token", token);
  localStorage.setItem("postoria-token-expires-at", expiresAt || "");
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add("show");
  window.setTimeout(() => toast.classList.remove("show"), 2600);
}

function setStatus(form, message, isError = false) {
  const status = form.querySelector(".status");
  if (!status) return;
  status.textContent = message;
  status.classList.toggle("error", isError);
}

function setBusy(form, busy) {
  form.querySelectorAll("button, input").forEach(element => {
    element.disabled = busy;
  });
}

async function apiPost(path, payload) {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    throw new Error(data?.message || data?.title || "請稍後再試。");
  }
  return data;
}

function externalUrl(provider) {
  const returnUrl = `${location.origin}${location.pathname}#login-success`;
  return `${API_BASE}/api/external-auth/${provider}/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

function logo() {
  return `
    <span class="logo" aria-hidden="true">
      <span class="logo-star">✦</span>
      <span class="logo-wave"></span>
    </span>
    <p class="wordmark">POSTORIA</p>
  `;
}

function field({ icon, name, type = "text", placeholder, autocomplete = "", required = true }) {
  const password = type === "password";
  return `
    <label class="field">
      <span class="field-icon" aria-hidden="true">${icon}</span>
      <input
        name="${name}"
        type="${type}"
        placeholder="${placeholder}"
        ${autocomplete ? `autocomplete="${autocomplete}"` : ""}
        ${required ? "required" : ""}
      >
      ${password ? `<button class="toggle-password" type="button" aria-label="顯示或隱藏密碼">⊘</button>` : ""}
    </label>
  `;
}

function socialButtons(action) {
  return `
    <div class="divider">或使用以下方式${action}</div>
    <div class="socials">
      <a class="social-button google" href="${externalUrl("google")}" aria-label="使用 Google ${action}">G</a>
      <a class="social-button facebook" href="${externalUrl("facebook")}" aria-label="使用 Facebook ${action}">f</a>
      <a class="social-button apple" href="#login" aria-label="Apple 尚未開放">●</a>
    </div>
  `;
}

function authShell(card, showNotice = true) {
  return `
    <section class="auth-layout">
      <aside class="welcome" aria-label="歡迎訊息">
        <span class="sparkle">✦</span>
        <h1>歡迎回來！</h1>
        <p>登入以繼續探索世界的明信片，<br>收藏美好時刻，分享你的故事。</p>
        <span class="paper-plane" aria-hidden="true"></span>
      </aside>
      <div class="auth-stack">
        ${card}
        ${showNotice ? notice() : ""}
      </div>
    </section>
  `;
}

function notice() {
  return `
    <aside class="notice">
      <span class="notice-icon" aria-hidden="true">♧</span>
      <div>
        <h3>貼心提醒</h3>
        <ul>
          <li>請確認信箱（包含垃圾郵件匣）以收到重設連結信件。</li>
          <li>如未收到信件，可嘗試重新發送或與我們聯繫。</li>
        </ul>
      </div>
      <span class="sparkle" aria-hidden="true">✦</span>
    </aside>
  `;
}

function loginCard() {
  return `
    <article class="auth-card">
      <button class="back-button" type="button" data-back aria-label="返回">‹</button>
      ${logo()}
      <h2>登入</h2>
      <p class="subtitle">還沒有帳號？ <a href="#register">立即註冊</a></p>
      <form class="auth-form" data-form="login">
        ${field({ icon: "✉", name: "email", type: "email", placeholder: "電子郵件地址", autocomplete: "email" })}
        ${field({ icon: "▣", name: "password", type: "password", placeholder: "密碼", autocomplete: "current-password" })}
        <div class="form-row">
          <label class="check"><input name="remember" type="checkbox">記住我</label>
          <a class="text-link" href="#forgot">忘記密碼？</a>
        </div>
        <button class="primary-button" type="submit">登入</button>
        <p class="status"></p>
      </form>
      ${socialButtons("登入")}
      <p class="switch-line">還沒有帳號？ <a class="text-link" href="#register">立即註冊</a></p>
    </article>
  `;
}

function registerCard() {
  return `
    <article class="auth-card">
      <button class="back-button" type="button" data-back aria-label="返回">‹</button>
      ${logo()}
      <h2>註冊會員</h2>
      <p class="subtitle">加入我們，收藏美好時刻，分享世界！</p>
      <form class="auth-form" data-form="register">
        ${field({ icon: "♙", name: "displayName", placeholder: "用戶名稱", autocomplete: "name" })}
        ${field({ icon: "✉", name: "email", type: "email", placeholder: "電子郵件", autocomplete: "email" })}
        ${field({ icon: "▣", name: "password", type: "password", placeholder: "密碼", autocomplete: "new-password" })}
        ${field({ icon: "▣", name: "confirmPassword", type: "password", placeholder: "確認密碼", autocomplete: "new-password" })}
        <label class="check terms">
          <input name="terms" type="checkbox" required>
          我已閱讀並同意 <a class="text-link" href="#register">服務條款</a> 與 <a class="text-link" href="#register">隱私政策</a>
        </label>
        <button class="primary-button" type="submit">註冊</button>
        <p class="status"></p>
      </form>
      ${socialButtons("註冊")}
      <p class="switch-line">已經有帳號？ <a class="text-link" href="#login">立即登入</a></p>
    </article>
  `;
}

function forgotCard(sent = false) {
  if (sent) {
    return `
      <div class="auth-stack">
        <article class="success-card">
          <span class="success-mark" aria-hidden="true">✓</span>
          <div>
            <h2>重設連結已發送！</h2>
            <p>我們已將重設密碼的連結發送至您的電子郵件，請檢查您的信箱。</p>
            <a class="outline-button" href="#login">返回登入</a>
          </div>
        </article>
      </div>
    `;
  }

  return `
    <article class="auth-card">
      <button class="back-button" type="button" data-back aria-label="返回">‹</button>
      ${logo()}
      <h2>忘記密碼？</h2>
      <p class="subtitle">請輸入您的電子郵件，我們將發送重設密碼的連結給您。</p>
      <div class="forgot-visual" aria-hidden="true"><span class="envelope"></span></div>
      <form class="auth-form" data-form="forgot">
        ${field({ icon: "✉", name: "email", type: "email", placeholder: "電子郵件", autocomplete: "email" })}
        <button class="primary-button" type="submit">發送重設連結</button>
        <p class="status"></p>
      </form>
      <p class="switch-line"><a class="text-link" href="#login">返回登入</a></p>
    </article>
  `;
}

function renderLoginSuccess() {
  return `
    <section class="auth-layout">
      <aside class="welcome">
        <span class="sparkle">✦</span>
        <h1>歡迎回來！</h1>
        <p>${state.member?.displayName || state.member?.email || "Postorian"}，祝你今天也收藏到好故事。</p>
      </aside>
      <div class="auth-stack">
        <article class="success-card">
          <span class="success-mark" aria-hidden="true">✓</span>
          <div>
            <h2>登入成功</h2>
            <p>會員 Token 已儲存在瀏覽器，可接續呼叫需要登入的 API。</p>
            <a class="outline-button" href="#login">回到登入頁</a>
          </div>
        </article>
        ${notice()}
      </div>
    </section>
  `;
}

async function handleSubmit(event) {
  const form = event.target.closest("form[data-form]");
  if (!form) return;
  event.preventDefault();

  const values = Object.fromEntries(new FormData(form).entries());
  setBusy(form, true);
  setStatus(form, "");

  try {
    if (form.dataset.form === "login") {
      const result = await apiPost("/api/members/login", {
        email: values.email,
        password: values.password
      });
      setSession(result.member, result.accessToken, result.expiresAt);
      setStatus(form, "登入成功，正在進入會員頁。");
      showToast("登入成功");
      location.hash = "login-success";
    }

    if (form.dataset.form === "register") {
      if (values.password !== values.confirmPassword) {
        throw new Error("兩次輸入的密碼不一致。");
      }
      await apiPost("/api/members/register", {
        email: values.email,
        password: values.password,
        displayName: values.displayName
      });
      setStatus(form, "註冊成功，請使用新帳號登入。");
      showToast("註冊成功");
      location.hash = "login";
    }

    if (form.dataset.form === "forgot") {
      await apiPost("/api/members/forgot-password", { email: values.email });
      sessionStorage.setItem("postoria-reset-sent", "1");
      showToast("重設連結已送出");
      render();
    }
  } catch (error) {
    setStatus(form, error.message, true);
    showToast(error.message);
  } finally {
    setBusy(form, false);
  }
}

function handleClick(event) {
  const toggle = event.target.closest(".toggle-password");
  if (toggle) {
    const input = toggle.closest(".field").querySelector("input");
    input.type = input.type === "password" ? "text" : "password";
    return;
  }

  if (event.target.closest("[data-back]")) {
    history.length > 1 ? history.back() : location.hash = "login";
  }
}

function render() {
  const route = location.hash.replace("#", "") || "login";
  if (route !== "forgot") {
    sessionStorage.removeItem("postoria-reset-sent");
  }

  if (route === "register") {
    app.innerHTML = authShell(registerCard());
  } else if (route === "forgot") {
    const sent = sessionStorage.getItem("postoria-reset-sent") === "1";
    app.innerHTML = authShell(forgotCard(sent), !sent);
  } else if (route === "login-success") {
    app.innerHTML = renderLoginSuccess();
  } else {
    app.innerHTML = authShell(loginCard());
  }

  window.scrollTo({ top: 0, behavior: "smooth" });
}

render();
