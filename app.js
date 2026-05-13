const API_BASE = location.origin;

const products = [
  {
    id: "strawberry",
    name: "草莓雪花酥",
    en: "STRAWBERRY",
    price: 280,
    unit: "150g",
    image: "assets/strawberry.png",
    detail: "assets/detail-strawberry.png",
    description: "酸甜草莓搭配奶香，清新不膩口，每一口都能嚐到幸福的滋味。"
  },
  {
    id: "chocolate",
    name: "巧克力雪花酥",
    en: "CHOCOLATE",
    price: 280,
    unit: "150g",
    image: "assets/chocolate.png",
    detail: "assets/chocolate.png",
    description: "濃郁可可香氣，苦甜交織，越嚼越香，甜而不膩。"
  },
  {
    id: "original",
    name: "原味雪花酥",
    en: "ORIGINAL",
    price: 260,
    unit: "150g",
    image: "assets/original.png",
    detail: "assets/original.png",
    description: "經典原味，奶香濃郁，簡單純粹的美味。"
  }
];

const state = {
  cart: JSON.parse(localStorage.getItem("miglow-cart") || "[]"),
  member: JSON.parse(localStorage.getItem("miglow-member") || "null"),
  token: localStorage.getItem("miglow-token") || ""
};

const app = document.querySelector("#app");
const toast = document.querySelector("#toast");
const nav = document.querySelector(".nav");

document.querySelector(".menu-button").addEventListener("click", () => nav.classList.toggle("open"));
window.addEventListener("hashchange", render);
document.addEventListener("click", handleGlobalClick);
document.addEventListener("submit", handleSubmit);

function money(value) {
  return `NT$ ${value.toLocaleString("zh-TW")}`;
}

function saveCart() {
  localStorage.setItem("miglow-cart", JSON.stringify(state.cart));
  updateCartCount();
}

function updateCartCount() {
  document.querySelector("#cart-count").textContent = state.cart.reduce((sum, item) => sum + item.qty, 0);
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add("show");
  window.setTimeout(() => toast.classList.remove("show"), 2200);
}

function setActiveNav(page) {
  document.querySelectorAll(".nav a").forEach(link => {
    link.classList.toggle("active", link.getAttribute("href") === `#${page}`);
  });
  nav.classList.remove("open");
}

function productById(id) {
  return products.find(product => product.id === id) || products[0];
}

function addToCart(id, qty = 1) {
  const product = productById(id);
  const found = state.cart.find(item => item.id === product.id);
  if (found) found.qty += qty;
  else state.cart.push({ id: product.id, qty });
  saveCart();
  showToast(`${product.name} 已加入購物車`);
}

function cartSubtotal() {
  return state.cart.reduce((sum, item) => sum + productById(item.id).price * item.qty, 0);
}

function hero(title, subtitle = "", narrow = true) {
  return `
    <section class="hero ${narrow ? "narrow" : ""}">
      <div class="hero-copy">
        <h1>${title}</h1>
        ${subtitle ? `<p class="eyebrow">${subtitle}</p>` : ""}
        <div class="rule"></div>
      </div>
    </section>
  `;
}

function productCards() {
  return `
    <div class="product-grid">
      ${products.map(product => `
        <article class="product-card">
          <img src="${product.image}" alt="${product.name}">
          <div class="product-body">
            <h3>${product.name}</h3>
            <p class="en">${product.en}</p>
            <p>${product.description}</p>
            <p class="price">${money(product.price)} <small>/ ${product.unit}</small></p>
            <div class="product-actions">
              <a class="button ghost" href="#product-${product.id}">查看商品</a>
              <button class="button" data-add-cart="${product.id}" type="button">加入購物車</button>
            </div>
          </div>
        </article>
      `).join("")}
    </div>
  `;
}

function renderHome() {
  app.innerHTML = `
    <section class="hero">
      <div class="hero-copy">
        <h1>微光・甜在心裡的幸福</h1>
        <div class="rule"></div>
        <p>手工製作的雪花酥，嚴選食材，層層堆疊出幸福的好滋味。簡單純粹，只想帶給你最真實的感動。</p>
        <a class="button" href="#products">立即選購 →</a>
      </div>
    </section>
    <section class="section">
      <div class="section-title"><h2><span>•</span>雪花酥系列<span>•</span></h2><p>三種口味・三種幸福</p></div>
      ${productCards()}
    </section>
  `;
}

function renderProducts() {
  app.innerHTML = `
    ${hero("商品介紹", "三種口味・三種幸福")}
    <section class="section">${productCards()}</section>
  `;
}

function renderProductDetail(id) {
  const product = productById(id);
  app.innerHTML = `
    ${hero("商品介紹", "雪花酥系列")}
    <section class="section">
      <div class="detail-layout">
        <div class="detail-image"><img src="${product.detail}" alt="${product.name}"></div>
        <div>
          <p class="eyebrow">雪花酥系列</p>
          <h2>${product.name}</h2>
          <p class="en">${product.en}</p>
          <p class="section-lead">${product.description}</p>
          <p class="price">${money(product.price)} <small>/ ${product.unit}</small></p>
          <div class="field">
            <span>數量</span>
            <div class="qty" data-qty-control>
              <button type="button" data-qty-minus>−</button>
              <span data-qty-value>1</span>
              <button type="button" data-qty-plus>＋</button>
            </div>
          </div>
          <p>
            <button class="button" type="button" data-detail-add="${product.id}">加入購物車</button>
            <button class="button ghost" type="button">♡ 加入喜愛</button>
          </p>
          <div class="feature-row">
            <span>真材實料</span>
            <span>奶香濃郁</span>
            <span>手工製作</span>
          </div>
        </div>
      </div>
    </section>
  `;
}

function renderAbout() {
  app.innerHTML = `
    ${hero("關於 微光", "甜在心裡的幸福，從微光開始")}
    <section class="section">
      <div class="about-layout">
        <img src="assets/about-story.png" alt="手工製作雪花酥">
        <div>
          <p class="eyebrow">品牌故事</p>
          <h2>一份想分享的心意</h2>
          <p class="section-lead">微光誕生於一個平凡卻溫暖的廚房。每一塊雪花酥，都是我們想與你分享的微小美好。</p>
        </div>
      </div>
    </section>
    <section class="section">
      <div class="section-title"><h2><span>•</span>製作流程<span>•</span></h2></div>
      <img class="panel" src="assets/process.png" alt="製作流程">
    </section>
  `;
}

function renderGuide() {
  const items = [
    ["01", "訂購方式", "官網線上訂購，加入購物車後依步驟完成結帳。"],
    ["02", "付款方式", "信用卡、ATM 轉帳、超商代碼繳費。"],
    ["03", "出貨時間", "現貨商品下單後 2-3 個工作天內出貨。"],
    ["04", "運送方式", "宅配到府，訂單滿 NT$1,500 享免運。"],
    ["05", "保存方式", "請置於陰涼乾燥處，建議 25°C 以下保存。"],
    ["06", "保存期限", "常溫保存 21 天，實際效期請參考包裝標示。"],
    ["07", "注意事項", "含乳製品、堅果與麩質，不適合相關過敏者食用。"],
    ["08", "退換貨說明", "食品因衛生考量，除商品瑕疵不接受個人因素退換。"]
  ];
  app.innerHTML = `
    ${hero("訂購須知", "購買前，請先詳閱以下說明")}
    <section class="section">
      <div class="info-grid">
        ${items.map(([num, title, text]) => `
          <article class="panel info-card">
            <div class="info-icon">${num}</div>
            <div><h3>${title}</h3><p>${text}</p></div>
          </article>
        `).join("")}
      </div>
    </section>
  `;
}

function renderContact() {
  app.innerHTML = `
    ${hero("聯絡我們", "有任何問題，歡迎與我們聯繫")}
    <section class="section">
      <div class="contact-layout">
        <aside class="panel contact-list">
          <article><div class="info-icon">☎</div><div><h3>電話</h3><p>(02) 1234-5678<br>週一至週五 10:00 - 18:00</p></div></article>
          <article><div class="info-icon">✉</div><div><h3>電子信箱</h3><p>info@miglow.com<br>我們將於 1-2 個工作天內回覆</p></div></article>
          <article><div class="info-icon">◎</div><div><h3>LINE 官方帳號</h3><p>@miglow</p></div></article>
        </aside>
        <form class="panel contact-form" data-form="contact">
          <h2>聯絡表單</h2>
          <div class="form-grid">
            <label class="field"><span>您的姓名</span><input required name="name" placeholder="請輸入姓名"></label>
            <label class="field"><span>聯絡電話</span><input required name="phone" placeholder="請輸入電話"></label>
          </div>
          <label class="field"><span>電子信箱</span><input required type="email" name="email" placeholder="請輸入電子信箱"></label>
          <label class="field"><span>主旨</span><select name="subject"><option>商品問題</option><option>訂單問題</option><option>大量訂購</option></select></label>
          <label class="field"><span>您的訊息</span><textarea required name="message" placeholder="請輸入想詢問的內容..."></textarea></label>
          <button class="button full" type="submit">送出訊息</button>
        </form>
      </div>
    </section>
  `;
}

function authShell(mode) {
  const isLogin = mode === "login";
  const isForgot = mode === "forgot";
  const isRegister = mode === "register";
  return `
    <section class="auth-shell">
      <div class="auth-brand">
        <div>
          <div class="brand-mark">微 光</div>
          <div class="brand-sub">MI GLOW</div>
          <div class="rule" style="margin-inline:auto"></div>
          <p class="eyebrow">甜在心裡的幸福，從微光開始</p>
        </div>
      </div>
      <div class="auth-card">
        <h2>${isLogin ? "會員登入" : isForgot ? "忘記密碼" : "會員註冊"}</h2>
        <p class="section-lead">${isForgot ? "請輸入您註冊的電子郵件地址，我們將產生重設密碼 token。" : ""}</p>
        <form data-form="${mode}">
          ${isRegister ? `
            <div class="form-grid">
              <label class="field"><span>姓名</span><input name="displayName" required placeholder="請輸入您的姓名"></label>
              <label class="field"><span>手機號碼</span><input name="phone" placeholder="請輸入手機號碼"></label>
            </div>
          ` : ""}
          <label class="field"><span>電子郵件</span><input name="email" type="email" required placeholder="請輸入電子郵件"></label>
          ${!isForgot ? `<label class="field"><span>密碼</span><input name="password" type="password" required minlength="8" placeholder="請輸入密碼"></label>` : ""}
          ${isRegister ? `<label class="field"><span>再次輸入密碼</span><input name="confirmPassword" type="password" required minlength="8" placeholder="請再次輸入密碼"></label>` : ""}
          <button class="button full" type="submit">${isLogin ? "登入" : isForgot ? "送出重設連結" : "立即註冊"}</button>
        </form>
        <p class="form-note">
          ${isLogin ? `還沒有帳號？ <a href="#register">立即註冊</a>　<a href="#forgot">忘記密碼？</a>` : isForgot ? `記得密碼了？ <a href="#login">返回登入</a>` : `已有帳號？ <a href="#login">立即登入</a>`}
        </p>
      </div>
    </section>
  `;
}

function renderCart() {
  const subtotal = cartSubtotal();
  const shipping = subtotal >= 1200 || subtotal === 0 ? 0 : 100;
  const discount = subtotal >= 1200 ? 100 : 0;
  const total = Math.max(0, subtotal + shipping - discount);
  app.innerHTML = `
    ${hero("購物車", "首頁　>　購物車")}
    <section class="section">
      <div class="cart-layout">
        <div class="panel">
          <button class="button ghost" type="button" data-clear-cart>清空購物車</button>
          ${state.cart.length ? state.cart.map(item => {
            const product = productById(item.id);
            return `
              <article class="cart-item">
                <img src="${product.image}" alt="${product.name}">
                <div>
                  <h3>${product.name}</h3>
                  <p>${product.description}</p>
                  <p class="price">${money(product.price)}</p>
                </div>
                <div class="qty">
                  <button type="button" data-cart-minus="${item.id}">−</button>
                  <span>${item.qty}</span>
                  <button type="button" data-cart-plus="${item.id}">＋</button>
                </div>
              </article>
            `;
          }).join("") : `<div class="empty">購物車目前是空的，去挑一盒微光吧。</div>`}
        </div>
        <aside class="panel">
          <h2>訂單摘要</h2>
          <div class="summary-line"><span>商品小計</span><b>${money(subtotal)}</b></div>
          <div class="summary-line"><span>運費</span><b>${money(shipping)}</b></div>
          <div class="summary-line"><span>滿額優惠</span><b>- ${money(discount)}</b></div>
          <hr>
          <div class="summary-line summary-total"><span>應付金額</span><b>${money(total)}</b></div>
          <button class="button full" type="button" data-checkout>前往結帳</button>
          <a class="button ghost full" href="#products">繼續購物</a>
        </aside>
      </div>
    </section>
  `;
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
    throw new Error(data?.message || data?.title || "API 呼叫失敗");
  }
  return data;
}

async function handleSubmit(event) {
  const form = event.target.closest("form");
  if (!form) return;
  event.preventDefault();
  const type = form.dataset.form;
  const values = Object.fromEntries(new FormData(form).entries());

  try {
    if (type === "login") {
      const result = await apiPost("/api/members/login", { email: values.email, password: values.password });
      state.member = result.member;
      state.token = result.accessToken;
      localStorage.setItem("miglow-member", JSON.stringify(state.member));
      localStorage.setItem("miglow-token", state.token);
      showToast("登入成功，歡迎回來");
      location.hash = "products";
    }
    if (type === "register") {
      if (values.password !== values.confirmPassword) throw new Error("兩次輸入的密碼不一致");
      await apiPost("/api/members/register", {
        email: values.email,
        password: values.password,
        displayName: values.displayName
      });
      showToast("註冊成功，請登入");
      location.hash = "login";
    }
    if (type === "forgot") {
      const result = await apiPost("/api/members/forgot-password", { email: values.email });
      showToast(result.resetToken ? `重設 token：${result.resetToken.slice(0, 12)}...` : result.message);
    }
    if (type === "contact") {
      showToast("訊息已送出，我們會盡快回覆");
      form.reset();
    }
  } catch (error) {
    showToast(error.message);
  }
}

function handleGlobalClick(event) {
  const target = event.target;
  const addId = target.closest("[data-add-cart]")?.dataset.addCart;
  if (addId) addToCart(addId);

  const detailAdd = target.closest("[data-detail-add]")?.dataset.detailAdd;
  if (detailAdd) {
    const qty = Number(document.querySelector("[data-qty-value]")?.textContent || "1");
    addToCart(detailAdd, qty);
  }

  if (target.closest("[data-qty-plus]")) {
    const value = document.querySelector("[data-qty-value]");
    value.textContent = Number(value.textContent) + 1;
  }
  if (target.closest("[data-qty-minus]")) {
    const value = document.querySelector("[data-qty-value]");
    value.textContent = Math.max(1, Number(value.textContent) - 1);
  }

  const plus = target.closest("[data-cart-plus]")?.dataset.cartPlus;
  if (plus) {
    state.cart.find(item => item.id === plus).qty += 1;
    saveCart();
    renderCart();
  }
  const minus = target.closest("[data-cart-minus]")?.dataset.cartMinus;
  if (minus) {
    const found = state.cart.find(item => item.id === minus);
    found.qty -= 1;
    state.cart = state.cart.filter(item => item.qty > 0);
    saveCart();
    renderCart();
  }
  if (target.closest("[data-clear-cart]")) {
    state.cart = [];
    saveCart();
    renderCart();
  }
  if (target.closest("[data-checkout]")) {
    showToast(state.member ? "已記錄購物車，可進入結帳流程" : "請先登入會員再結帳");
    if (!state.member) location.hash = "login";
  }
}

function render() {
  const route = location.hash.replace("#", "") || "home";
  const [page, id] = route.startsWith("product-") ? ["product", route.replace("product-", "")] : [route, ""];
  setActiveNav(["product", "products"].includes(page) ? "products" : page);

  if (page === "home") renderHome();
  else if (page === "products") renderProducts();
  else if (page === "product") renderProductDetail(id);
  else if (page === "about") renderAbout();
  else if (page === "guide") renderGuide();
  else if (page === "contact") renderContact();
  else if (page === "login") app.innerHTML = authShell("login");
  else if (page === "register") app.innerHTML = authShell("register");
  else if (page === "forgot") app.innerHTML = authShell("forgot");
  else if (page === "cart") renderCart();
  else renderHome();
  window.scrollTo({ top: 0, behavior: "smooth" });
  updateCartCount();
}

render();
