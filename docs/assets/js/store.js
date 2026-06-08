/* =============================================================================
 * 微光 MI GLOW — 前端共用 Store / 工具（原型，localStorage）
 * 提供購物車、登入狀態、訂單的存取，集中一處，日後改打 API 只需改這支。
 * 全域命名空間：window.MG
 * ========================================================================== */
(function () {
  "use strict";
  const D = window.MIGLOW_DATA || {};
  const KEY_CART = "miglow_cart";
  const KEY_SESSION = "miglow_session";
  const KEY_TOKEN = "miglow_token";
  const KEY_ORDERS = "miglow_orders";

  /* ---- 後端 API 位址（會員註冊/登入）----
   * 靜態前台（GitHub Pages）需指向 Optimind 後端網址。
   * 部署時把 window.MIGLOW_API_BASE 設成你的 Optimind 網址即可，例如：
   *   <script>window.MIGLOW_API_BASE = "https://your-optimind-host";</script>
   * 預設留空＝同源（僅在從 Optimind 站台直接開啟時可用）。 */
  function apiBase() {
    return String(window.MIGLOW_API_BASE || "").replace(/\/+$/, "");
  }
  function authUrl(method) {
    return apiBase() + "/web/Service/wsMGAUTH.asmx/" + method;
  }
  function apiPost(service, method, payload) {
    return fetch(apiBase() + "/web/Service/" + service + "/" + method, {
      method: "POST",
      headers: { "Content-Type": "application/json; charset=utf-8" },
      body: JSON.stringify(payload)
    })
      .then(function (r) { return r.json(); })
      .then(function (j) {
        var raw = j && j.d != null ? j.d : j;
        return typeof raw === "string" ? JSON.parse(raw) : raw;
      });
  }
  function authPost(method, payload) { return apiPost("wsMGAUTH.asmx", method, payload); }
  function shopPost(method, payload) { return apiPost("wsMGSHOP.asmx", method, payload); }

  function read(key, fallback) {
    try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch (e) { return fallback; }
  }
  function write(key, val) { localStorage.setItem(key, JSON.stringify(val)); }

  const MG = {
    /* ---- 格式化 ---- */
    money(n) { return "NT$" + Number(n || 0).toLocaleString("zh-TW"); },
    qs(name) { return new URLSearchParams(location.search).get(name); },
    dateFmt(s) { return (s || "").replace(/-/g, ".").slice(0, 10); },

    /* ---- 資料查詢 ---- */
    product(uid) { return (D.products || []).find((p) => p.product_uid === uid); },
    productBySlug(slug) { return (D.products || []).find((p) => p.slug === slug); },
    news(slug) { return (D.news || []).find((n) => n.slug === slug); },
    settings() { return D.site || {}; },
    payment() { return (D.site && D.site.payment) || {}; },

    /* ---- 購物車 ---- */
    cart: {
      get() { return read(KEY_CART, []); },
      save(items) { write(KEY_CART, items); if (window.MIGLOW_updateCartBadge) window.MIGLOW_updateCartBadge(); },
      count() { return MG.cart.get().reduce((n, i) => n + (i.qty || 0), 0); },
      subtotal() { return MG.cart.get().reduce((s, i) => s + i.price * i.qty, 0); },
      add(uid, qty) {
        const p = MG.product(uid); if (!p) return;
        const cart = MG.cart.get();
        const line = cart.find((x) => x.product_uid === uid);
        if (line) line.qty += qty || 1;
        else cart.push({ product_uid: uid, slug: p.slug, name_zh: p.name_zh, price: p.price, image_path: p.image_path, qty: qty || 1 });
        MG.cart.save(cart);
      },
      setQty(uid, qty) {
        const cart = MG.cart.get();
        const line = cart.find((x) => x.product_uid === uid);
        if (line) { line.qty = Math.max(1, qty); MG.cart.save(cart); }
      },
      remove(uid) { MG.cart.save(MG.cart.get().filter((x) => x.product_uid !== uid)); },
      clear() { MG.cart.save([]); }
    },

    /* ---- 運費 / 免運 ---- */
    shipping(subtotal) {
      const pay = MG.payment();
      const threshold = pay.free_shipping_threshold || 0;
      const fee = pay.shipping_fee || 0;
      if (!subtotal) return 0;
      return subtotal >= threshold ? 0 : fee;
    },

    /* ---- 登入狀態（後端 token） ---- */
    session: {
      get() { return read(KEY_SESSION, null); },
      login(member, token) { write(KEY_SESSION, member); if (token) localStorage.setItem(KEY_TOKEN, token); },
      token() { return localStorage.getItem(KEY_TOKEN) || ""; },
      logout() { localStorage.removeItem(KEY_SESSION); localStorage.removeItem(KEY_TOKEN); },
      isLoggedIn() { return !!read(KEY_SESSION, null); }
    },

    /* ---- 會員認證 API（打 Optimind wsMGAUTH.asmx）---- */
    auth: {
      register(email, password, display_name, phone) {
        return authPost("Register", { email: email, password: password, display_name: display_name, phone: phone });
      },
      login(email, password) {
        return authPost("Login", { email: email, password: password });
      },
      loginWithGoogle(idToken) {
        return authPost("LoginWithGoogle", { idToken: idToken });
      },
      me(token) { return authPost("Me", { token: token }); },
      logout(token) { return authPost("Logout", { token: token }); },
      requestPasswordReset(email) { return authPost("RequestPasswordReset", { email: email }); },
      resetPassword(token, newPassword) { return authPost("ResetPassword", { token: token, newPassword: newPassword }); }
    },

    /* ---- 訂單（後端 wsMGSHOP，需登入 token）。皆回 Promise ---- */
    orders: {
      myOrders() { return shopPost("GetMyOrders", { token: MG.session.token() }); },
      get(no) { return shopPost("GetOrder", { token: MG.session.token(), orderNo: no }); },
      create(order) { return shopPost("CreateOrder", { token: MG.session.token(), json: JSON.stringify(order) }); },
      reportPayment(no, pay) {
        return shopPost("ReportPayment", { token: MG.session.token(), json: JSON.stringify(Object.assign({ order_no: no }, pay)) });
      }
    }
  };

  /* ---- 輕量提示 Toast ---- */
  MG.toast = function (msg, type) {
    let host = document.getElementById("mg-toast-host");
    if (!host) {
      host = document.createElement("div");
      host.id = "mg-toast-host";
      document.body.appendChild(host);
    }
    const el = document.createElement("div");
    el.className = "mg-toast" + (type === "err" ? " mg-toast--err" : "");
    el.textContent = msg;
    host.appendChild(el);
    requestAnimationFrame(() => el.classList.add("show"));
    setTimeout(() => { el.classList.remove("show"); setTimeout(() => el.remove(), 300); }, 2200);
  };

  /* 訂單狀態顯示對照 */
  MG.ORDER_STATUS = {
    pending: "待付款", reported: "已填後五碼", paid: "已確認付款",
    preparing: "備貨中", shipped: "已出貨", completed: "已完成", cancelled: "已取消"
  };

  /* ---- 讀取後台發佈的 JSON 覆蓋 MIGLOW_DATA（讀不到則沿用 data.js）----
   * 後台「發佈」會把最新內容寫到 data/*.json 並 push 到 GitHub Pages。
   * 例：MG.loadPublished('products', 'data/products.json', renderFn)
   */
  MG.loadPublished = function (key, file, cb) {
    var done = function () { try { cb && cb(); } catch (e) {} };
    if (!window.fetch) { return done(); }
    try {
      fetch(file, { cache: "no-store" })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (json) {
          var ok = json && (Array.isArray(json) ? json.length > 0 : Object.keys(json).length > 0);
          if (ok) { (window.MIGLOW_DATA = window.MIGLOW_DATA || {})[key] = json; }
        })
        .catch(function () {})
        .then(done, done);
    } catch (e) { done(); }
  };

  window.MG = MG;
})();
