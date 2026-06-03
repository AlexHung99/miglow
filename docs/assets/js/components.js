/* =============================================================================
 * 微光 MI GLOW — 共用元件（Header / Footer / 行動選單 / 購物車徽章）
 * 每個頁面只要：
 *   <header id="site-header"></header> ... <footer id="site-footer"></footer>
 *   <body data-page="home"> 並載入本檔，導覽列與頁尾即自動產生。
 * 導覽 / 品牌 / 社群等內容取自 window.MIGLOW_DATA，方便日後由後台供給。
 * ========================================================================== */
(function () {
  "use strict";

  /* 確認瀏覽器支援後才啟用淡入動畫；否則 CSS 維持內容可見（永不卡白畫面）。 */
  if ("IntersectionObserver" in window) document.documentElement.classList.add("js-anim");

  /* ---- SVG 圖示集（細線條風格） ---- */
  const ICON = {
    member: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 3.6-6.5 8-6.5S20 17 20 21"/></svg>',
    cart:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M3 4h2l2.2 12.2a1.5 1.5 0 0 0 1.5 1.3h8.8a1.5 1.5 0 0 0 1.5-1.2L21 8H6"/><circle cx="9.5" cy="20" r="1.2"/><circle cx="17.5" cy="20" r="1.2"/></svg>',
    instagram: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="3" y="3" width="18" height="18" rx="5"/><circle cx="12" cy="12" r="4"/><circle cx="17.3" cy="6.7" r="1" fill="currentColor" stroke="none"/></svg>',
    facebook:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="3" y="3" width="18" height="18" rx="5"/><path d="M14.8 8.3H13.3c-.8 0-1.3.5-1.3 1.3V21M9.8 13h4.4"/></svg>',
    line: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M21 10.5c0-4.1-4-7.5-9-7.5S3 6.4 3 10.5c0 3.7 3.2 6.8 7.5 7.4.9.1.8.6.7 1.1l-.2 1.1c-.1.5.3.9.9.6 2.4-1.2 5-3 6.7-5.1 1-1.3 1.7-2.9 1.7-5.1Z"/></svg>',
    arrowL: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4"><path d="M15 5l-7 7 7 7"/></svg>',
    arrowR: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4"><path d="M9 5l7 7-7 7"/></svg>',
    leaf:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><path d="M5 19C5 11 11 5 20 5c0 9-6 15-14 15-1.5 0-3-.4-3-.4S4 13 12 10"/></svg>',
    hand:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><path d="M8 11V5.5a1.5 1.5 0 0 1 3 0V10m0-1V4.5a1.5 1.5 0 0 1 3 0V10m0-.5a1.5 1.5 0 0 1 3 0V14c0 4-2.5 6.5-6 6.5S8 18.5 7 16l-2-3.5a1.4 1.4 0 0 1 2.3-1.6L8 12"/></svg>',
    gift:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><rect x="4" y="9" width="16" height="11" rx="1"/><path d="M3 9h18M12 9v11M12 9S9 4 7 5.5 9 9 12 9Zm0 0s3-5 5-3.5S15 9 12 9Z"/></svg>',
    truck: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><path d="M3 6h11v9H3zM14 9h4l3 3v3h-7"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/></svg>'
  };
  window.MIGLOW_ICON = ICON;

  /* ---- 導覽項目 ---- */
  const NAV = [
    { label: "首頁",   href: "index.html",    page: "home" },
    { label: "商品介紹", href: "products.html", page: "products" },
    { label: "關於我們", href: "about.html",    page: "about" },
    { label: "最新消息", href: "news.html",     page: "news" },
    { label: "訂購須知", href: "ordering.html", page: "ordering" },
    { label: "聯絡我們", href: "contact.html",  page: "contact" }
  ];

  const D = window.MIGLOW_DATA || {};
  const brand = (D.site && D.site.brand) || { name_zh: "微光", name_en: "MI GLOW" };
  const social = (D.site && D.site.social) || {};

  /* ---- 登入狀態（直接讀 localStorage，header 早於 store.js 也可用） ---- */
  function isLoggedIn() {
    try { return !!JSON.parse(localStorage.getItem("miglow_session")); } catch (e) { return false; }
  }
  const memberHref = () => (isLoggedIn() ? "member.html" : "login.html");

  /* ---- 購物車徽章 ---- */
  function getCartCount() {
    try {
      const cart = JSON.parse(localStorage.getItem("miglow_cart") || "[]");
      return cart.reduce((n, it) => n + (it.qty || 0), 0);
    } catch (e) { return 0; }
  }
  function updateCartBadge() {
    const n = getCartCount();
    document.querySelectorAll(".nav__cart-count").forEach((el) => {
      el.textContent = n; el.dataset.count = n;
    });
  }
  window.MIGLOW_updateCartBadge = updateCartBadge;

  /* ---- Header ---- */
  function renderHeader(active) {
    const links = NAV.map(
      (n) => `<a href="${n.href}" class="${n.page === active ? "is-active" : ""}">${n.label}</a>`
    ).join("");

    return `
    <div class="container nav">
      <a href="index.html" class="nav__logo" aria-label="${brand.name_zh} ${brand.name_en}">
        <span class="zh">${brand.name_zh}</span>
        <span class="divider"></span>
        <span class="en">${brand.name_en}</span>
      </a>
      <nav class="nav__menu">${links}</nav>
      <div class="nav__actions">
        <a href="${memberHref()}" class="nav__icon nav__icon--member" aria-label="會員">${ICON.member}</a>
        <a href="cart.html" class="nav__icon" aria-label="購物車">
          ${ICON.cart}<span class="nav__cart-count" data-count="0">0</span>
        </a>
        <button class="nav__toggle" id="navToggle" aria-label="開啟選單"><span></span><span></span><span></span></button>
      </div>
    </div>`;
  }

  /* ---- 行動抽屜 ---- */
  function renderDrawer(active) {
    const links = NAV.map(
      (n) => `<a href="${n.href}" class="${n.page === active ? "is-active" : ""}">${n.label}</a>`
    ).join("");
    return `
      <div class="nav-drawer__backdrop" data-close></div>
      <aside class="nav-drawer__panel">
        <div class="nav-drawer__head">
          <span class="nav__logo"><span class="zh">${brand.name_zh}</span></span>
          <button class="nav-drawer__close" data-close aria-label="關閉選單">&times;</button>
        </div>
        <nav class="nav-drawer__menu">${links}</nav>
        <div class="nav-drawer__foot">
          <a href="${memberHref()}" class="btn btn--ghost btn--block">${isLoggedIn() ? "會員中心" : "會員登入 / 註冊"}</a>
        </div>
      </aside>`;
  }

  /* ---- Footer ---- */
  function renderFooter() {
    const feats = (D.site && D.site.features) || [];
    const featRow = feats
      .map((f) => `<div class="footer-feature">${ICON[f.icon] || ""}<span>${f.title}</span></div>`)
      .join("");
    const copyright = (D.site && D.site.copyright) || "© 2026 微光 MI GLOW";
    const contact = (D.site && D.site.contact) || {};

    return `
    <div class="container">
      <div class="footer-features">${featRow}</div>
      <div class="footer-main">
        <div class="footer-brand">
          <div class="zh">${brand.name_zh}</div>
          <div class="en">${brand.name_en}</div>
          <p>在日常裡，留一點微光的甜。小批次手作雪花酥，給你與在乎的人剛剛好的幸福。</p>
          <div class="footer-social">
            <a href="${social.instagram || "#"}" target="_blank" rel="noopener" aria-label="Instagram">${ICON.instagram}</a>
            <a href="${social.facebook || "#"}" target="_blank" rel="noopener" aria-label="Facebook">${ICON.facebook}</a>
            <a href="${social.line || "#"}" target="_blank" rel="noopener" aria-label="LINE">${ICON.line}</a>
          </div>
        </div>
        <div class="footer-col">
          <h4>網站導覽</h4>
          <ul>${NAV.map((n) => `<li><a href="${n.href}">${n.label}</a></li>`).join("")}</ul>
        </div>
        <div class="footer-col">
          <h4>聯絡我們</h4>
          <ul>
            <li><span>${contact.phone || ""}</span></li>
            <li><span>${contact.email || ""}</span></li>
            <li><span>${contact.hours || ""}</span></li>
            <li><span>${contact.address || ""}</span></li>
          </ul>
        </div>
      </div>
    </div>
    <div class="footer-bottom">${copyright}</div>`;
  }

  /* ---- 互動 ---- */
  function bindNav() {
    const toggle = document.getElementById("navToggle");
    if (toggle) toggle.addEventListener("click", () => document.body.classList.toggle("menu-open"));
    document.querySelectorAll("[data-close]").forEach((el) =>
      el.addEventListener("click", () => document.body.classList.remove("menu-open"))
    );
    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") document.body.classList.remove("menu-open");
    });
  }

  /* ---- 進場動畫（IntersectionObserver） ----
   * 可重複呼叫：動態渲染（如商品卡）後需再呼叫一次，
   * 才能觀察到「渲染當下還不存在」的 .reveal 元素，避免其永遠停在 opacity:0。
   */
  let _io = null;
  function bindReveal() {
    const els = document.querySelectorAll(".reveal:not(.is-visible):not(.reveal-observed)");
    if (!els.length) return;
    if (!("IntersectionObserver" in window)) {
      els.forEach((el) => el.classList.add("is-visible"));
      return;
    }
    if (!_io) {
      _io = new IntersectionObserver(
        (entries) => entries.forEach((en) => {
          if (en.isIntersecting) { en.target.classList.add("is-visible"); _io.unobserve(en.target); }
        }),
        { threshold: 0.12 }
      );
    }
    els.forEach((el) => {
      el.classList.add("reveal-observed");
      // 已位於視窗內或已被捲動過的元素 → 立即顯示，避免錨點跳轉 / 快速捲動時卡在不可見；
      // 真正在下方的元素才交給 observer，捲到時才淡入。
      if (el.getBoundingClientRect().top < window.innerHeight * 0.95) {
        el.classList.add("is-visible");
      } else {
        _io.observe(el);
      }
    });
  }
  window.MIGLOW_bindReveal = bindReveal;

  /* ---- 初始化 ---- */
  document.addEventListener("DOMContentLoaded", function () {
    const active = document.body.dataset.page || "";
    const header = document.getElementById("site-header");
    const footer = document.getElementById("site-footer");
    if (header) header.innerHTML = renderHeader(active);
    if (footer) footer.innerHTML = renderFooter();

    const drawer = document.createElement("div");
    drawer.className = "nav-drawer";
    drawer.innerHTML = renderDrawer(active);
    document.body.appendChild(drawer);

    bindNav();
    updateCartBadge();
    bindReveal();
  });
})();
