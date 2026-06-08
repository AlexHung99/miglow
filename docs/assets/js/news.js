/* =============================================================================
 * 微光 MI GLOW — 最新消息列表 + 詳細
 * ========================================================================== */
(function () {
  "use strict";
  const D = window.MIGLOW_DATA || {};
  const date = (s) => window.MG.dateFmt(s);

  function card(n) {
    return `
      <a class="news-card reveal" href="n/${n.slug}.html">
        <div class="news-card__media"><img src="${n.cover_image_path}" alt="${n.title}" loading="lazy"></div>
        <div class="news-card__body">
          <div class="news-card__cat">${n.category}</div>
          <h3 class="news-card__title">${n.title}</h3>
          <p class="news-card__excerpt">${n.excerpt}</p>
          <div class="news-card__date">${date(n.published_at)}</div>
        </div>
      </a>`;
  }

  /* ---------------- 列表 ---------------- */
  function initList() {
    const list = document.getElementById("newsList");
    if (!list) return;
    const all = (D.news || []).slice().sort((a, b) => (a.published_at < b.published_at ? 1 : -1));
    const cats = ["全部", ...Array.from(new Set(all.map((n) => n.category)))];

    const filter = document.getElementById("newsFilter");
    filter.innerHTML = cats
      .map((c, i) => `<button class="filter-chip ${i === 0 ? "is-active" : ""}" data-cat="${c}">${c}</button>`)
      .join("");

    function render(cat) {
      const items = cat === "全部" ? all : all.filter((n) => n.category === cat);
      list.innerHTML = items.map(card).join("");
      if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
    }
    filter.querySelectorAll("[data-cat]").forEach((chip) =>
      chip.addEventListener("click", () => {
        filter.querySelectorAll(".filter-chip").forEach((c) => c.classList.remove("is-active"));
        chip.classList.add("is-active");
        render(chip.dataset.cat);
      })
    );
    render("全部");
  }

  /* ---------------- 詳細 ---------------- */
  function initDetail() {
    const wrap = document.getElementById("article");
    if (!wrap) return;
    const n = window.MG.news(window.MG.qs("slug")) || (D.news || [])[0];
    if (!n) { wrap.innerHTML = '<p class="text-center">找不到文章。</p>'; return; }

    // SEO（原型於前端設定；未來由後端輸出於 HTML <head>）
    document.title = (n.seo_title || n.title) + "｜微光 MI GLOW";
    setMeta("name", "description", n.seo_description || n.excerpt);
    setMeta("property", "og:title", n.title);
    setMeta("property", "og:description", n.seo_description || n.excerpt);
    setMeta("property", "og:image", n.cover_image_path);
    const bc = document.getElementById("bcTitle"); if (bc) bc.textContent = n.title;

    wrap.innerHTML = `
      <div class="article__head">
        <div class="article__cat">${n.category}</div>
        <h1 class="article__title">${n.title}</h1>
        <div class="article__date">${date(n.published_at)}</div>
      </div>
      <div class="article__cover"><img src="${n.cover_image_path}" alt="${n.title}"></div>
      <div class="article__body">${n.content_html}</div>`;
  }

  function setMeta(attr, key, val) {
    let el = document.querySelector(`meta[${attr}="${key}"]`);
    if (!el) { el = document.createElement("meta"); el.setAttribute(attr, key); document.head.appendChild(el); }
    el.setAttribute("content", val || "");
  }

  function initAll() {
    initList();
    initDetail();
  }

  document.addEventListener("DOMContentLoaded", function () {
    // 先讀後台發佈的 news.json（讀不到則沿用 data.js），再渲染
    if (window.MG && MG.loadPublished) MG.loadPublished("news", "data/news.json", initAll);
    else initAll();
  });
})();
