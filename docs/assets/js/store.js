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
  const KEY_ORDERS = "miglow_orders";

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

    /* ---- 登入狀態（mock） ---- */
    session: {
      get() { return read(KEY_SESSION, null); },
      login(member) { write(KEY_SESSION, member); },
      logout() { localStorage.removeItem(KEY_SESSION); },
      isLoggedIn() { return !!read(KEY_SESSION, null); }
    },

    /* ---- 訂單（mock） ---- */
    orders: {
      all() { return read(KEY_ORDERS, []); },
      get(no) { return MG.orders.all().find((o) => o.order_no === no); },
      create(order) {
        const list = MG.orders.all();
        const seq = String(list.length + 1).padStart(4, "0");
        const d = new Date();
        const ymd = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, "0")}${String(d.getDate()).padStart(2, "0")}`;
        order.order_no = `MG${ymd}-${seq}`;
        order.created_at = d.toISOString();
        order.order_status = "pending";
        list.unshift(order);
        write(KEY_ORDERS, list);
        return order;
      },
      update(no, patch) {
        const list = MG.orders.all();
        const o = list.find((x) => x.order_no === no);
        if (o) { Object.assign(o, patch); write(KEY_ORDERS, list); }
        return o;
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

  window.MG = MG;
})();
