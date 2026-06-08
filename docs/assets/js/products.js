/* =============================================================================
 * 微光 MI GLOW — 商品列表 + 商品詳細
 * 同一支檔案，依頁面存在的容器決定行為。
 * ========================================================================== */
(function () {
  "use strict";
  const D = window.MIGLOW_DATA || {};
  const money = (n) => window.MG.money(n);

  function productCard(p) {
    return `
      <article class="product-card reveal">
        <a class="product-card__media" href="p/${p.slug}.html" aria-label="${p.name_zh}">
          <img src="${p.image_path}" alt="${p.name_zh}" loading="lazy">
        </a>
        <div class="product-card__body">
          <h3 class="product-card__name">${p.name_zh}</h3>
          <div class="product-card__en">${p.name_en}</div>
          <p class="product-card__desc">${p.description}</p>
          <div class="product-card__foot">
            <div class="product-card__price">${money(p.price)} <small>/ 盒</small></div>
            <button class="btn btn--sm" data-add="${p.product_uid}">加入購物車</button>
          </div>
        </div>
      </article>`;
  }

  function bindAddButtons(scope) {
    scope.querySelectorAll("[data-add]").forEach((btn) =>
      btn.addEventListener("click", () => {
        window.MG.cart.add(btn.dataset.add, 1);
        window.MG.toast("已加入購物車");
      })
    );
  }

  /* ---------------- 列表頁 ---------------- */
  function initList() {
    const list = document.getElementById("productList");
    if (!list) return;
    const filter = document.getElementById("productFilter");
    const products = (D.products || []).filter((p) => p.is_active).sort((a, b) => a.sort_order - b.sort_order);

    const cats = [{ key: "all", label: "全部" }, { key: "snowflake", label: "雪花酥系列" }];
    filter.innerHTML = cats
      .map((c) => `<button class="filter-chip ${c.key === "all" ? "is-active" : ""}" data-cat="${c.key}">${c.label}</button>`)
      .join("");

    function render(cat) {
      const items = cat === "all" ? products : products.filter((p) => p.category === cat);
      list.innerHTML = items.map(productCard).join("");
      bindAddButtons(list);
      if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
    }
    filter.querySelectorAll("[data-cat]").forEach((chip) =>
      chip.addEventListener("click", () => {
        filter.querySelectorAll(".filter-chip").forEach((c) => c.classList.remove("is-active"));
        chip.classList.add("is-active");
        render(chip.dataset.cat);
      })
    );
    render("all");
  }

  /* ---------------- 詳細頁 ---------------- */
  function initDetail() {
    const wrap = document.getElementById("productDetail");
    if (!wrap) return;
    const slug = window.MG.qs("slug");
    const p = window.MG.productBySlug(slug) || (D.products || [])[0];
    if (!p) { wrap.innerHTML = '<p class="text-center">找不到商品。</p>'; return; }

    document.title = `${p.name_zh}｜微光 MI GLOW`;
    const bc = document.getElementById("bcName"); if (bc) bc.textContent = p.name_zh;

    // 圖庫：主圖 + 品牌情境圖（原型；未來由 b_miglow_product_image 提供多圖）
    const gallery = [p.image_path, "assets/images/brand/hero.png"];

    wrap.innerHTML = `
      <div class="product-detail">
        <div class="pd-gallery">
          <div class="pd-main"><img id="pdMain" src="${gallery[0]}" alt="${p.name_zh}"></div>
          <div class="pd-thumbs">
            ${gallery.map((g, i) => `<div class="pd-thumb ${i === 0 ? "is-active" : ""}" data-src="${g}"><img src="${g}" alt=""></div>`).join("")}
          </div>
        </div>
        <div class="pd-info">
          <div class="eyebrow">${p.name_en}</div>
          <h1>${p.name_zh}</h1>
          <div class="en-name">${p.subtitle || ""}</div>
          <div class="pd-price">${money(p.price)} <small>/ 盒</small></div>
          <div class="pd-desc">${p.description}</div>
          <dl class="pd-meta">
            <div><dt>類型</dt><dd>雪花酥系列</dd></div>
            <div><dt>內容量</dt><dd>約 12 入 / 盒</dd></div>
            <div><dt>保存</dt><dd>常溫約 14 天，建議冷藏</dd></div>
          </dl>
          <div class="pd-actions">
            <div class="qty">
              <button type="button" data-q="-1" aria-label="減少">−</button>
              <input id="pdQty" type="number" value="1" min="1" inputmode="numeric">
              <button type="button" data-q="1" aria-label="增加">＋</button>
            </div>
            <button class="btn btn--solid" id="pdAdd">加入購物車</button>
            <button class="btn btn--ghost" id="pdFav" aria-label="加入喜愛">♡ 加入喜愛</button>
          </div>
        </div>
      </div>`;

    // 縮圖切換
    wrap.querySelectorAll(".pd-thumb").forEach((t) =>
      t.addEventListener("click", () => {
        wrap.querySelectorAll(".pd-thumb").forEach((x) => x.classList.remove("is-active"));
        t.classList.add("is-active");
        document.getElementById("pdMain").src = t.dataset.src;
      })
    );
    // 數量
    const qtyEl = document.getElementById("pdQty");
    wrap.querySelectorAll("[data-q]").forEach((b) =>
      b.addEventListener("click", () => {
        qtyEl.value = Math.max(1, (parseInt(qtyEl.value, 10) || 1) + parseInt(b.dataset.q, 10));
      })
    );
    // 加入購物車
    document.getElementById("pdAdd").addEventListener("click", () => {
      window.MG.cart.add(p.product_uid, Math.max(1, parseInt(qtyEl.value, 10) || 1));
      window.MG.toast("已加入購物車");
    });
    // 加入喜愛（mock）
    document.getElementById("pdFav").addEventListener("click", (e) => {
      const on = e.currentTarget.classList.toggle("is-fav");
      e.currentTarget.innerHTML = on ? "♥ 已收藏" : "♡ 加入喜愛";
      window.MG.toast(on ? "已加入喜愛" : "已移除喜愛");
    });

    // 其他口味
    const related = (D.products || []).filter((x) => x.is_active && x.product_uid !== p.product_uid);
    if (related.length) {
      document.getElementById("relatedSection").hidden = false;
      const grid = document.getElementById("relatedGrid");
      grid.innerHTML = related.map(productCard).join("");
      bindAddButtons(grid);
      if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    function go() { initList(); initDetail(); }
    // 先讀後台發佈的 products.json（讀不到則沿用 data.js），再渲染
    if (window.MG && MG.loadPublished) MG.loadPublished("products", "data/products.json", go);
    else go();
  });
})();
