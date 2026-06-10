/* =============================================================================
 * 微光 MI GLOW — 內容頁：關於我們 / 訂購須知 / 聯絡我們 / FAQ
 * ========================================================================== */
(function () {
  "use strict";
  const D = window.MIGLOW_DATA || {};
  const ICON = window.MIGLOW_ICON || {};

  const MINI = {
    phone: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M5 4h3l1.5 4-2 1.5a12 12 0 0 0 5 5l1.5-2 4 1.5V21c0 .6-.5 1-1.1 1A17 17 0 0 1 4 5.1 1 1 0 0 1 5 4Z"/></svg>',
    mail: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m4 7 8 6 8-6"/></svg>',
    pin: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M12 21s7-6 7-11a7 7 0 1 0-14 0c0 5 7 11 7 11Z"/><circle cx="12" cy="10" r="2.5"/></svg>',
    clock: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3"><circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/></svg>'
  };

  /* ---------------- 關於我們 ---------------- */
  function initAbout() {
    const intro = document.getElementById("aboutIntro");
    if (!intro) return;
    const a = (D.pages && D.pages.about) || {};
    intro.innerHTML = `
      <div class="about__media reveal"><img src="${a.hero_image || ""}" alt="微光 MI GLOW"></div>
      <div class="about__text reveal">
        <h2 class="about__zh">在日常裡，留一點微光</h2>
        <span class="about__en">A Little Glow in Everyday</span>
        <div class="about__body"><p>${a.intro || ""}</p></div>
        <a href="products.html" class="btn">看看商品</a>
      </div>`;

    const story = document.getElementById("aboutStory");
    if (story) story.innerHTML = (a.story || []).map((p) => `<p class="reveal">${p}</p>`).join("");

    const values = document.getElementById("aboutValues");
    if (values) values.innerHTML = (a.values || [])
      .map((v) => `<div class="feature reveal"><div class="feature__icon">${ICON[v.icon] || ""}</div><h3 class="feature__title">${v.title}</h3><p class="feature__desc">${v.desc}</p></div>`)
      .join("");

    if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
  }

  /* ---------------- 訂購須知 ---------------- */
  function initOrdering() {
    const list = document.getElementById("orderingList");
    if (!list) return;
    const secs = (D.pages && D.pages.ordering && D.pages.ordering.sections) || [];
    list.innerHTML = secs
      .map((s, i) => `
        <div class="ordering-item reveal">
          <div class="ordering-item__no">${String(i + 1).padStart(2, "0")}</div>
          <div><h3>${s.title}</h3><p>${s.body}</p></div>
        </div>`)
      .join("");
    if (window.MIGLOW_bindReveal) window.MIGLOW_bindReveal();
  }

  /* ---------------- FAQ 手風琴 ---------------- */
  function initFaq() {
    const wrap = document.getElementById("faqList");
    if (!wrap) return;
    wrap.innerHTML = (D.faq || [])
      .map((f) => `
        <div class="faq-item">
          <button class="faq-q" type="button">${f.q}<span class="mark"></span></button>
          <div class="faq-a"><div class="faq-a__inner">${f.a}</div></div>
        </div>`)
      .join("");
    wrap.querySelectorAll(".faq-q").forEach((q) =>
      q.addEventListener("click", () => {
        const item = q.parentElement;
        const a = item.querySelector(".faq-a");
        const open = item.classList.toggle("is-open");
        a.style.maxHeight = open ? a.scrollHeight + "px" : null;
      })
    );
  }

  /* ---------------- 聯絡我們 ---------------- */
  function initContact() {
    const info = document.getElementById("contactInfo");
    if (!info) return;
    const c = (D.site && D.site.contact) || {};
    const s = (D.site && D.site.social) || {};
    info.innerHTML = `
      <div class="row">${MINI.phone}<div><div class="k">電話</div><div class="v">${c.phone || ""}</div></div></div>
      <div class="row">${MINI.mail}<div><div class="k">Email</div><div class="v">${c.email || ""}</div></div></div>
      <div class="row">${ICON.line || ""}<div><div class="k">LINE</div><div class="v">${c.line_id || ""}</div></div></div>
      <div class="row">${MINI.pin}<div><div class="k">工作室</div><div class="v">${c.address || ""}</div></div></div>
      <div class="row">${MINI.clock}<div><div class="k">營業時間</div><div class="v">${c.hours || ""}</div></div></div>
      <div class="contact-social">
        <a href="${s.instagram || "#"}" target="_blank" rel="noopener" aria-label="Instagram">${ICON.instagram || ""}</a>
        <a href="${s.facebook || "#"}" target="_blank" rel="noopener" aria-label="Facebook">${ICON.facebook || ""}</a>
        <a href="${s.line || "#"}" target="_blank" rel="noopener" aria-label="LINE">${ICON.line || ""}</a>
      </div>`;

    const form = document.getElementById("contactForm");
    const notice = document.getElementById("contactNotice");
    const emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      let ok = true;
      const checks = [
        ["cName", (v) => !!v.trim(), "請輸入姓名"],
        ["cPhone", (v) => !!v.trim(), "請輸入電話"],
        ["cEmail", (v) => emailRe.test(v), "請輸入有效的電子信箱"],
        ["cMsg", (v) => !!v.trim(), "請輸入訊息內容"]
      ];
      checks.forEach(([id, fn, msg]) => {
        const el = document.getElementById(id);
        const err = el.closest(".field").querySelector(".field-error");
        if (!fn(el.value)) { ok = false; el.classList.add("is-invalid"); if (err) err.textContent = msg; }
        else { el.classList.remove("is-invalid"); if (err) err.textContent = ""; }
      });
      if (!ok) { notice.innerHTML = '<div class="notice notice--err">請確認必填欄位</div>'; return; }

      const captcha = (form.querySelector('[name="cf-turnstile-response"]') || {}).value || "";
      if (!captcha) { notice.innerHTML = '<div class="notice notice--err">請先完成「我不是機器人」驗證</div>'; return; }

      const btn = form.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      notice.innerHTML = '<div class="notice">送出中…</div>';
      window.MG.contact.submit({
        name: document.getElementById("cName").value.trim(),
        phone: document.getElementById("cPhone").value.trim(),
        email: document.getElementById("cEmail").value.trim(),
        subject: (document.getElementById("cSubject") ? document.getElementById("cSubject").value : "").trim(),
        message: document.getElementById("cMsg").value.trim(),
        captcha: captcha
      }).then((res) => {
        if (window.turnstile) try { window.turnstile.reset(); } catch (e) {}
        if (res && res.ok) {
          form.reset();
          notice.innerHTML = '<div class="notice notice--ok">已收到您的訊息，我們會盡快與您聯繫！</div>';
        } else {
          notice.innerHTML = '<div class="notice notice--err">' + ((res && res.error) || "送出失敗，請稍後再試") + "</div>";
        }
        if (btn) btn.disabled = false;
        notice.scrollIntoView({ behavior: "smooth", block: "center" });
      }).catch(() => {
        if (window.turnstile) try { window.turnstile.reset(); } catch (e) {}
        notice.innerHTML = '<div class="notice notice--err">無法連線伺服器，請稍後再試，或改用電話／LINE 聯繫。</div>';
        if (btn) btn.disabled = false;
      });
    });
  }

  function initAll() {
    initAbout();
    initOrdering();
    initFaq();
    initContact();
  }

  document.addEventListener("DOMContentLoaded", function () {
    // 先讀後台發佈的 site.json（聯絡/社群），讀不到則沿用 data.js
    if (window.MG && MG.loadPublished) MG.loadPublished("site", "data/site.json", initAll);
    else initAll();
  });
})();
