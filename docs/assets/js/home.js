/* =============================================================================
 * 微光 MI GLOW — 首頁邏輯
 * 由 window.MIGLOW_DATA 渲染：Hero 輪播、商品、服務特色、IG。
 * ========================================================================== */
(function () {
  "use strict";
  const D = window.MIGLOW_DATA || {};
  const ICON = window.MIGLOW_ICON || {};
  const TWD = (n) => "NT$" + Number(n).toLocaleString("zh-TW");

  /* ---------- Hero 輪播 ---------- */
  function renderHero() {
    const wrap = document.getElementById("hero");
    if (!wrap) return;
    const banners = (D.banners || []).filter((b) => b.is_active).sort((a, b) => a.sort_order - b.sort_order);
    if (!banners.length) return;

    const slides = banners
      .map(
        (b, i) => `
        <div class="hero__slide ${i === 0 ? "is-active" : ""}" data-index="${i}">
          <div class="container">
            <div class="hero__inner">
              <div class="hero__text">
                <div class="hero__eyebrow">MI GLOW</div>
                <h1 class="hero__title">${b.title}</h1>
                <p class="hero__subtitle">${b.subtitle}</p>
                <a href="${b.link_url}" class="btn btn--solid">${b.link_label || "看更多"}</a>
              </div>
              <div class="hero__media"><img src="${b.image_path}" alt="${b.title}" loading="${i === 0 ? "eager" : "lazy"}" fetchpriority="${i === 0 ? "high" : "auto"}" decoding="async"></div>
            </div>
          </div>
        </div>`
      )
      .join("");

    const dots = banners.map((_, i) => `<button class="hero__dot ${i === 0 ? "is-active" : ""}" data-go="${i}" aria-label="第 ${i + 1} 張"></button>`).join("");

    wrap.innerHTML = `
      <div class="hero__track">${slides}</div>
      ${banners.length > 1 ? `
        <button class="hero__arrow hero__arrow--prev" data-prev aria-label="上一張">${ICON.arrowL || "‹"}</button>
        <button class="hero__arrow hero__arrow--next" data-next aria-label="下一張">${ICON.arrowR || "›"}</button>
        <div class="hero__dots">${dots}</div>` : ""}
    `;

    if (banners.length < 2) return;
    const slideEls = wrap.querySelectorAll(".hero__slide");
    const dotEls = wrap.querySelectorAll(".hero__dot");
    let cur = 0, timer;

    function go(i) {
      cur = (i + slideEls.length) % slideEls.length;
      slideEls.forEach((s, k) => s.classList.toggle("is-active", k === cur));
      dotEls.forEach((d, k) => d.classList.toggle("is-active", k === cur));
    }
    function next() { go(cur + 1); }
    function start() { timer = setInterval(next, 6000); }
    function stop() { clearInterval(timer); }

    wrap.querySelector("[data-next]").addEventListener("click", () => { next(); stop(); start(); });
    wrap.querySelector("[data-prev]").addEventListener("click", () => { go(cur - 1); stop(); start(); });
    dotEls.forEach((d) => d.addEventListener("click", () => { go(+d.dataset.go); stop(); start(); }));
    wrap.addEventListener("mouseenter", stop);
    wrap.addEventListener("mouseleave", start);
    start();
  }

  /* ---------- 商品 ---------- */
  function renderProducts() {
    const grid = document.getElementById("productsGrid");
    if (!grid) return;
    const items = (D.products || []).filter((p) => p.is_active && p.is_featured).sort((a, b) => a.sort_order - b.sort_order);
    grid.innerHTML = items
      .map(
        (p) => `
        <article class="product-card reveal">
          <a class="product-card__media" href="p/${p.slug}.html" aria-label="${p.name_zh}">
            <img src="${p.image_path}" alt="${p.name_zh}" loading="lazy">
          </a>
          <div class="product-card__body">
            <h3 class="product-card__name">${p.name_zh}</h3>
            <div class="product-card__en">${p.name_en}</div>
            <p class="product-card__desc">${p.description}</p>
            <div class="product-card__foot">
              <div class="product-card__price">${TWD(p.price)} <small>/ 盒</small></div>
              <button class="btn btn--sm" data-add="${p.product_uid}">加入購物車</button>
            </div>
          </div>
        </article>`
      )
      .join("");

    grid.querySelectorAll("[data-add]").forEach((btn) =>
      btn.addEventListener("click", () => addToCart(btn.dataset.add, btn))
    );
  }

  /* ---------- 加入購物車（透過共用 MG.cart） ---------- */
  function addToCart(uid, btn) {
    if (!window.MG) return;
    window.MG.cart.add(uid, 1);
    const old = btn.textContent;
    btn.textContent = "已加入 ✓";
    btn.disabled = true;
    setTimeout(() => { btn.textContent = old; btn.disabled = false; }, 1400);
  }

  /* ---------- 服務特色 ---------- */
  function renderFeatures() {
    const wrap = document.getElementById("featuresGrid");
    if (!wrap) return;
    const feats = (D.site && D.site.features) || [];
    wrap.innerHTML = feats
      .map(
        (f) => `
        <div class="feature reveal">
          <div class="feature__icon">${ICON[f.icon] || ""}</div>
          <h3 class="feature__title">${f.title}</h3>
          <p class="feature__desc">${f.desc}</p>
        </div>`
      )
      .join("");
  }

  /* ---------- IG ---------- */
  function renderInstagram() {
    const grid = document.getElementById("igGrid");
    if (!grid) return;
    const imgs = [
      "assets/images/brand/hero.jpg",
      "assets/images/products/p-strawberry.jpg",
      "assets/images/products/p-chocolate.jpg",
      "assets/images/products/p-original.jpg"
    ];
    const link = (D.site && D.site.social && D.site.social.instagram) || "#";
    grid.innerHTML = imgs
      .map((src) => `<a class="ig-item" href="${link}" target="_blank" rel="noopener"><img src="${src}" alt="微光 Instagram" loading="lazy"></a>`)
      .join("");
  }

  function renderAll() {
    renderHero();
    renderProducts();
    renderFeatures();
    renderInstagram();
    // 動態內容渲染完成後，重新觀察新加入的 .reveal 元素（避免商品/特色卡停在不可見狀態）
    if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
  }

  document.addEventListener("DOMContentLoaded", function () {
    // 先讀後台發佈的 products / banners / site（讀不到則沿用 data.js），再渲染
    if (window.MG && MG.loadPublished) {
      MG.loadPublished("products", "data/products.json", function () {
        MG.loadPublished("banners", "data/banners.json", function () {
          MG.loadPublished("site", "data/site.json", renderAll);
        });
      });
    } else {
      renderAll();
    }
  });
})();
