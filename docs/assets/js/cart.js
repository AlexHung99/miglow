/* =============================================================================
 * 微光 MI GLOW — 購物車頁
 * ========================================================================== */
(function () {
  "use strict";
  const money = (n) => window.MG.money(n);

  function render() {
    const root = document.getElementById("cartRoot");
    if (!root) return;
    const items = window.MG.cart.get();

    if (!items.length) {
      root.innerHTML = `
        <div class="empty-state">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2"><path d="M3 4h2l2.2 12.2a1.5 1.5 0 0 0 1.5 1.3h8.8a1.5 1.5 0 0 0 1.5-1.2L21 8H6"/><circle cx="9.5" cy="20" r="1.2"/><circle cx="17.5" cy="20" r="1.2"/></svg>
          <h3>購物車是空的</h3>
          <p>還沒有挑選任何商品，去看看微光的雪花酥吧。</p>
          <a href="products.html" class="btn btn--solid">前往選購</a>
        </div>`;
      return;
    }

    const subtotal = window.MG.cart.subtotal();
    const shipping = window.MG.shipping(subtotal);
    const pay = window.MG.payment();
    const threshold = pay.free_shipping_threshold || 0;
    const remain = Math.max(0, threshold - subtotal);
    const pct = Math.min(100, threshold ? (subtotal / threshold) * 100 : 100);

    root.innerHTML = `
      <div class="cart-layout">
        <div>
          <div class="ship-bar">
            <div class="ship-bar__text">${remain > 0
              ? `滿 <b>${money(threshold)}</b> 免運，目前 ${money(subtotal)}，再加 <b>${money(remain)}</b> 即可免運`
              : `🎉 已達免運門檻，本次訂單 <b>免運費</b>`}</div>
            <div class="ship-bar__track"><div class="ship-bar__fill" style="width:${pct}%"></div></div>
          </div>
          <div class="cart-list">
            ${items.map(rowHtml).join("")}
          </div>
          <div class="cart-toolbar">
            <a href="products.html">← 繼續購物</a>
            <a href="#" id="clearCart">清空購物車</a>
          </div>
        </div>
        <aside class="summary">
          <h3>訂單摘要</h3>
          <div class="summary-line"><span>商品小計</span><span>${money(subtotal)}</span></div>
          <div class="summary-line"><span>運費</span><span>${shipping === 0 ? "免運" : money(shipping)}</span></div>
          <div class="summary-line total"><span>應付金額</span><b>${money(subtotal + shipping)}</b></div>
          <a href="checkout.html" class="btn btn--solid btn--block">前往結帳</a>
        </aside>
      </div>`;

    bind(root);
  }

  function rowHtml(it) {
    return `
      <div class="cart-row" data-uid="${it.product_uid}">
        <a class="cart-row__img" href="product.html?slug=${it.slug}"><img src="${it.image_path}" alt="${it.name_zh}"></a>
        <div>
          <a class="cart-row__name" href="product.html?slug=${it.slug}">${it.name_zh}</a>
          <div class="cart-row__price">${money(it.price)} / 盒</div>
          <div class="qty" style="margin-top:10px">
            <button type="button" data-q="-1" aria-label="減少">−</button>
            <input type="number" value="${it.qty}" min="1" inputmode="numeric" data-qty>
            <button type="button" data-q="1" aria-label="增加">＋</button>
          </div>
        </div>
        <div class="cart-row__ctrl">
          <div class="cart-row__subtotal">${money(it.price * it.qty)}</div>
          <button class="cart-remove" data-remove>✕ 移除</button>
        </div>
      </div>`;
  }

  function bind(root) {
    root.querySelectorAll(".cart-row").forEach((row) => {
      const uid = row.dataset.uid;
      const input = row.querySelector("[data-qty]");
      row.querySelectorAll("[data-q]").forEach((b) =>
        b.addEventListener("click", () => {
          window.MG.cart.setQty(uid, (parseInt(input.value, 10) || 1) + parseInt(b.dataset.q, 10));
          render();
        })
      );
      input.addEventListener("change", () => { window.MG.cart.setQty(uid, parseInt(input.value, 10) || 1); render(); });
      row.querySelector("[data-remove]").addEventListener("click", () => { window.MG.cart.remove(uid); render(); });
    });
    const clear = document.getElementById("clearCart");
    if (clear) clear.addEventListener("click", (e) => {
      e.preventDefault();
      if (confirm("確定要清空購物車嗎？")) { window.MG.cart.clear(); render(); }
    });
  }

  document.addEventListener("DOMContentLoaded", render);
})();
