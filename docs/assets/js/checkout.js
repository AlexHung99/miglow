/* =============================================================================
 * 微光 MI GLOW — 結帳 + 訂單成立(匯款後五碼)
 * ========================================================================== */
(function () {
  "use strict";
  const money = (n) => window.MG.money(n);
  const emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  /* =============== 結帳頁 =============== */
  function initCheckout() {
    const root = document.getElementById("checkoutRoot");
    if (!root) return;

    if (!window.MG.session.isLoggedIn()) {
      root.innerHTML = `
        <div class="empty-state">
          <h3>請先登入會員</h3>
          <p>登入後即可結帳，並在會員中心追蹤訂單狀態。</p>
          <a href="login.html?redirect=checkout.html" class="btn btn--solid">前往登入</a>
        </div>`;
      return;
    }

    const items = window.MG.cart.get();

    if (!items.length) {
      root.innerHTML = `
        <div class="empty-state">
          <h3>購物車是空的</h3>
          <p>請先選購商品再進行結帳。</p>
          <a href="products.html" class="btn btn--solid">前往選購</a>
        </div>`;
      return;
    }

    const subtotal = window.MG.cart.subtotal();
    const shipping = window.MG.shipping(subtotal);
    const total = subtotal + shipping;
    const me = window.MG.session.get() || {};

    root.innerHTML = `
      <form id="checkoutForm" novalidate>
        <div class="checkout-layout">
          <div>
            <div class="checkout-block">
              <h3>訂購人資料</h3>
              <div class="form">
                <div class="form-row">
                  ${field("oName", "訂購人姓名", "text", true, me.display_name)}
                  ${field("oPhone", "訂購人電話", "tel", true, me.phone)}
                </div>
                ${field("oEmail", "訂購人 Email", "email", true, me.email)}
              </div>
            </div>
            <div class="checkout-block">
              <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;margin-bottom:6px">
                <h3 style="margin:0">收件資料</h3>
                <button type="button" id="sameAsOrderer" class="btn btn--ghost" style="padding:6px 16px;font-size:13px;line-height:1.4">同訂購人</button>
              </div>
              <div class="form">
                <div class="form-row">
                  ${field("rName", "收件人姓名", "text", true)}
                  ${field("rPhone", "收件人電話", "tel", true)}
                </div>
                ${field("rAddr", "收件地址", "text", true)}
                <div class="field">
                  <label for="ship">配送方式</label>
                  <select class="select" id="ship">
                    <option value="宅配常溫">宅配常溫</option>
                    <option value="宅配低溫">宅配低溫（冷藏）</option>
                    <option value="7-11 店到店">7-11 店到店</option>
                  </select>
                </div>
                <div class="field">
                  <label for="note">訂單備註</label>
                  <textarea class="textarea" id="note" placeholder="例如：指定到貨日、卡片留言…"></textarea>
                </div>
              </div>
            </div>
          </div>

          <aside class="summary">
            <h3>訂單明細</h3>
            <div class="mini-items">
              ${items.map((it) => `
                <div class="mini-item">
                  <img src="${it.image_path}" alt="${it.name_zh}">
                  <div>${it.name_zh}<div class="q">x ${it.qty}</div></div>
                  <div>${money(it.price * it.qty)}</div>
                </div>`).join("")}
            </div>
            <div class="summary-line"><span>商品小計</span><span>${money(subtotal)}</span></div>
            <div class="summary-line"><span>運費</span><span>${shipping === 0 ? "免運" : money(shipping)}</span></div>
            <div class="summary-line total"><span>應付金額</span><b>${money(total)}</b></div>
            <div id="checkoutNotice"></div>
            <button class="btn btn--solid btn--block" type="submit">送出訂單</button>
            <p class="field-hint" style="text-align:center;margin-top:12px;">送出後將顯示匯款帳號，採銀行轉帳付款。</p>
          </aside>
        </div>
      </form>`;

    const sameBtn = document.getElementById("sameAsOrderer");
    if (sameBtn) {
      sameBtn.addEventListener("click", () => {
        const set = (id, v) => {
          const el = document.getElementById(id);
          if (!el) return;
          el.value = v;
          el.classList.remove("is-invalid");
          const err = el.closest(".field") && el.closest(".field").querySelector(".field-error");
          if (err) err.textContent = "";
        };
        set("rName", val("oName"));
        set("rPhone", val("oPhone"));
        if (window.MG.toast) window.MG.toast("已帶入訂購人資料");
      });
    }

    document.getElementById("checkoutForm").addEventListener("submit", (e) => {
      e.preventDefault();
      const rules = [
        ["oName", (v) => !!v.trim(), "請輸入訂購人姓名"],
        ["oPhone", (v) => !!v.trim(), "請輸入訂購人電話"],
        ["oEmail", (v) => emailRe.test(v), "請輸入有效 Email"],
        ["rName", (v) => !!v.trim(), "請輸入收件人姓名"],
        ["rPhone", (v) => !!v.trim(), "請輸入收件人電話"],
        ["rAddr", (v) => !!v.trim(), "請輸入收件地址"]
      ];
      if (!validate(rules)) {
        document.getElementById("checkoutNotice").innerHTML = '<div class="notice notice--err" style="margin-bottom:14px">請確認必填欄位</div>';
        return;
      }
      const notice = document.getElementById("checkoutNotice");
      const btn = e.target.querySelector('[type="submit"]');
      if (btn) btn.disabled = true;
      if (notice) notice.innerHTML = '<div class="notice" style="margin-bottom:14px">送出中…</div>';
      window.MG.orders.create({
        orderer: { name: val("oName"), phone: val("oPhone"), email: val("oEmail") },
        receiver: { name: val("rName"), phone: val("rPhone"), address: val("rAddr") },
        shipping_method: val("ship"),
        note: val("note"),
        items: items
      }).then((res) => {
        if (!res || !res.ok) {
          if (notice) notice.innerHTML = '<div class="notice notice--err" style="margin-bottom:14px">' + ((res && res.error) || "下單失敗") + "</div>";
          if (btn) btn.disabled = false;
          return;
        }
        window.MG.cart.clear();
        location.href = "order-complete.html?no=" + encodeURIComponent(res.order_no);
      }).catch((err) => {
        const reason = err && err.message ? "：" + err.message : "，請稍後再試。";
        if (notice) notice.innerHTML = '<div class="notice notice--err" style="margin-bottom:14px">送出訂單失敗' + reason + '</div>';
        if (btn) btn.disabled = false;
      });
    });
  }

  /* =============== 訂單成立 / 後五碼 =============== */
  function notFoundHtml() {
    return `<div class="empty-state"><h3>找不到訂單</h3><p>訂單可能已失效，或連結不正確。</p><a href="products.html" class="btn btn--solid">繼續購物</a></div>`;
  }

  function initOrderDone() {
    const root = document.getElementById("orderDoneRoot");
    if (!root) return;
    const no = window.MG.qs("no");
    if (!no) { root.innerHTML = notFoundHtml(); return; }
    if (!window.MG.session.isLoggedIn()) {
      root.innerHTML = `<div class="empty-state"><h3>請先登入</h3><p>登入後即可查看此訂單。</p><a href="login.html" class="btn btn--solid">前往登入</a></div>`;
      return;
    }
    window.MG.orders.get(no).then((res) => {
      if (!res || !res.ok) { root.innerHTML = notFoundHtml(); return; }
      renderOrder(root, res.order, res.items);
    }).catch(() => { root.innerHTML = notFoundHtml(); });
  }

  function renderOrder(root, order, items) {
    const pay = window.MG.payment();
    const reported = order.order_status !== "pending";

    root.innerHTML = `
      <div class="order-done">
        <div class="order-done__icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M5 13l4 4L19 7"/></svg></div>
        <h1>訂單已成立</h1>
        <p class="lead">感謝您的訂購！訂單編號 <b>${order.order_no}</b>，<br>請於下方完成銀行匯款並回填帳號後五碼。</p>

        <div class="bank-card">
          <h3>匯款資訊</h3>
          <dl>
            <div class="bank-line"><dt>應付金額</dt><dd class="big">${money(order.total)}</dd></div>
            <div class="bank-line"><dt>銀行</dt><dd>${pay.bank_name || ""}</dd></div>
            <div class="bank-line"><dt>帳號</dt><dd>${pay.bank_account || ""}<button class="copy-btn" type="button" data-copy="${pay.bank_account || ""}">複製</button></dd></div>
            <div class="bank-line"><dt>戶名</dt><dd>${pay.account_name || ""}</dd></div>
            <div class="bank-line"><dt>匯款期限</dt><dd>訂單成立後 3 日內</dd></div>
          </dl>
        </div>

        <div id="payArea"></div>
      </div>`;

    root.querySelectorAll("[data-copy]").forEach((b) =>
      b.addEventListener("click", () => {
        navigator.clipboard && navigator.clipboard.writeText(b.dataset.copy);
        window.MG.toast("已複製帳號");
      })
    );

    const payArea = document.getElementById("payArea");
    if (reported) {
      payArea.innerHTML = `
        <div class="notice notice--ok" style="text-align:left">
          ✓ 我們已收到您的匯款資訊（帳號後五碼 ${order.pay_account_last5}），將盡快為您確認款項並安排出貨。
        </div>
        <div style="margin-top:22px"><a href="member.html" class="btn btn--ghost">查看我的訂單</a></div>`;
      return;
    }

    payArea.innerHTML = `
      <div class="bank-card" style="background:var(--card)">
        <h3>回填匯款資訊</h3>
        <form id="payForm" class="form" novalidate>
          <div class="field"><label>訂單編號</label><input class="input" value="${order.order_no}" readonly></div>
          <div class="form-row">
            ${field("last5", "匯款帳號後五碼", "text", true)}
            ${field("payAmt", "匯款金額", "number", true, order.total)}
          </div>
          <div class="form-row">
            ${field("payDate", "匯款日期", "date", true)}
            ${field("payNote", "備註", "text", false)}
          </div>
          <div id="payNotice"></div>
          <button class="btn btn--solid btn--block" type="submit">送出匯款資訊</button>
        </form>
      </div>`;

    document.getElementById("payForm").addEventListener("submit", (e) => {
      e.preventDefault();
      const rules = [
        ["last5", (v) => /^\d{5}$/.test(v.trim()), "請輸入 5 位數字後五碼"],
        ["payAmt", (v) => Number(v) > 0, "請輸入匯款金額"],
        ["payDate", (v) => !!v, "請選擇匯款日期"]
      ];
      if (!validate(rules)) {
        document.getElementById("payNotice").innerHTML = '<div class="notice notice--err">請確認欄位填寫正確</div>';
        return;
      }
      const pbtn = e.target.querySelector('[type="submit"]');
      if (pbtn) pbtn.disabled = true;
      window.MG.orders.reportPayment(order.order_no, {
        account_last5: val("last5"), paid_amount: Number(val("payAmt")), paid_date: val("payDate"), payer_note: val("payNote")
      }).then((res) => {
        if (!res || !res.ok) {
          document.getElementById("payNotice").innerHTML = '<div class="notice notice--err">' + ((res && res.error) || "送出失敗") + "</div>";
          if (pbtn) pbtn.disabled = false;
          return;
        }
        window.MG.toast("已送出匯款資訊");
        window.MG.orders.get(order.order_no).then((r2) => { if (r2 && r2.ok) renderOrder(root, r2.order, r2.items); });
      }).catch((err) => {
        const reason = err && err.message ? "：" + err.message : "，請稍後再試。";
        document.getElementById("payNotice").innerHTML = '<div class="notice notice--err">送出失敗' + reason + '</div>';
        if (pbtn) pbtn.disabled = false;
      });
    });
  }

  /* =============== 共用 =============== */
  function field(id, label, type, req, value) {
    return `
      <div class="field">
        <label for="${id}">${label}${req ? '<span class="req">*</span>' : ""}</label>
        <input class="input" id="${id}" type="${type}" value="${value != null ? value : ""}">
        <div class="field-error"></div>
      </div>`;
  }
  function val(id) { const el = document.getElementById(id); return el ? el.value : ""; }
  function validate(rules) {
    let ok = true;
    rules.forEach(([id, fn, msg]) => {
      const el = document.getElementById(id);
      if (!el) return;
      const err = el.closest(".field") && el.closest(".field").querySelector(".field-error");
      if (!fn(el.value)) { ok = false; el.classList.add("is-invalid"); if (err) err.textContent = msg; }
      else { el.classList.remove("is-invalid"); if (err) err.textContent = ""; }
    });
    return ok;
  }

  document.addEventListener("DOMContentLoaded", function () {
    initCheckout();
    initOrderDone();
  });
})();
