/* =============================================================================
 * MI GLOW 微光 — 前端資料來源 (原型階段)
 * -----------------------------------------------------------------------------
 * 這支檔案是「暫時的資料來源」。前端所有畫面都從 window.MIGLOW_DATA 取資料，
 * 不直接寫死在 HTML，方便日後接後台。
 *
 * 上線後台後：本檔內容會由後台 API 提供，回傳的 JSON 結構與 /data/*.json 相同。
 *   - banners  ← GET /api/banners      (對應資料表 b_miglow_banner)
 *   - products ← GET /api/products      (對應資料表 b_miglow_product)
 *   - site     ← GET /api/site-settings (對應資料表 b_miglow_site_setting)
 * 屆時只要把本檔換成 fetch()，畫面不需重寫。
 * ========================================================================== */
window.MIGLOW_DATA = {
  /* 網站全域設定 ------------------------------------------------------------ */
  site: {
    brand: {
      name_zh: "微光",
      name_en: "MI GLOW",
      slogan: "微光・甜在心裡的幸福",
      tagline: "在日常裡，留一點微光的甜"
    },
    contact: {
      phone: "0900-000-000",
      email: "hello@miglow.com",
      line_id: "@miglow",
      address: "台北市・微光手作甜點工作室",
      hours: "週二至週六 11:00 – 19:00"
    },
    social: {
      instagram: "https://www.instagram.com/miglow.cw",
      facebook: "https://www.facebook.com/miglow",
      line: "https://line.me/R/ti/p/@miglow"
    },
    // 銀行匯款資訊（結帳頁／訂單成立頁使用，後台可編修）
    payment: {
      bank_name: "國泰世華銀行 (013)",
      bank_account: "0000-0000-00000",
      account_name: "微光手作甜點有限公司",
      free_shipping_threshold: 1200,
      shipping_fee: 160
    },
    // 全站服務特色列（首頁與 Footer 共用）
    features: [
      { icon: "leaf",    title: "嚴選食材", desc: "精選天然原料，純粹不馬虎" },
      { icon: "hand",    title: "手工製作", desc: "小批次手作，溫度與心意同在" },
      { icon: "gift",    title: "精美包裝", desc: "質感禮盒，傳遞每一份心意" },
      { icon: "truck",   title: "快速出貨", desc: "新鮮現做，下單後盡快寄出" }
    ],
    copyright: "© 2026 微光 MI GLOW. All rights reserved."
  },

  /* 首頁輪播 Banner ─ 對應 b_miglow_banner -------------------------------- */
  banners: [
    {
      banner_uid: "b1",
      title: "微光・甜在心裡的幸福",
      subtitle: "雪花酥裡，藏著一點微光的溫柔。\n小批次手作，給日常一份剛剛好的甜。",
      image_path: "assets/images/brand/hero.jpg",
      link_url: "products.html",
      link_label: "選購商品",
      sort_order: 1,
      is_active: true
    },
    {
      banner_uid: "b2",
      title: "用心包裝，傳遞每一份心意",
      subtitle: "質感禮盒，無論送禮或自享，\n都是恰到好處的微光。",
      image_path: "assets/images/brand/bags.jpg",
      link_url: "about.html",
      link_label: "認識微光",
      sort_order: 2,
      is_active: true
    },
    {
      banner_uid: "b3",
      title: "三種口味・三種幸福",
      subtitle: "原味、草莓、巧克力，\n每一口都是純手作的細緻。",
      image_path: "assets/images/products/p-strawberry.jpg",
      link_url: "products.html",
      link_label: "看更多",
      sort_order: 3,
      is_active: true
    }
  ],

  /* 商品 ─ 對應 b_miglow_product ------------------------------------------ */
  products: [
    {
      product_uid: "p-strawberry",
      slug: "strawberry",
      name_zh: "草莓雪花酥",
      name_en: "Strawberry",
      subtitle: "Strawberry Snowflake Crisp",
      description: "酸甜草莓乾與手工棉花糖交織，奶香中帶著果香，是少女心般的柔軟滋味。",
      price: 280,
      image_path: "assets/images/products/p-strawberry.jpg",
      category: "snowflake",
      sort_order: 1,
      is_active: true,
      is_featured: true
    },
    {
      product_uid: "p-chocolate",
      slug: "chocolate",
      name_zh: "巧克力雪花酥",
      name_en: "Chocolate",
      subtitle: "Chocolate Snowflake Crisp",
      description: "微苦可可與堅果的厚實口感，濃郁不甜膩，大人系的療癒選擇。",
      price: 300,
      image_path: "assets/images/products/p-chocolate.jpg",
      category: "snowflake",
      sort_order: 2,
      is_active: true,
      is_featured: true
    },
    {
      product_uid: "p-original",
      slug: "original",
      name_zh: "原味雪花酥",
      name_en: "Original",
      subtitle: "Original Snowflake Crisp",
      description: "最純粹的奶香與蔓越莓、堅果的平衡，經典耐吃，微光的起點滋味。",
      price: 260,
      image_path: "assets/images/products/p-original.jpg",
      category: "snowflake",
      sort_order: 3,
      is_active: true,
      is_featured: true
    }
  ],

  /* 最新消息 ─ 對應 b_miglow_news --------------------------------------- */
  news: [
    {
      news_uid: "n1",
      slug: "snowflake-crisp-story",
      category: "品牌故事",
      title: "為什麼叫「微光」？關於一塊雪花酥的起點",
      cover_image_path: "assets/images/brand/hero.jpg",
      excerpt: "微光，取自日常裡那一點剛剛好的光。我們想做的，是讓人安靜地感到幸福的甜點。",
      published_at: "2026-05-20",
      seo_title: "微光 MI GLOW 品牌故事｜手作雪花酥的起點",
      seo_description: "認識微光 MI GLOW 的品牌起點，為什麼我們堅持小批次手作雪花酥。",
      content_html: "<p>微光，取自日常裡那一點剛剛好的光。創立微光的初衷很單純——我們相信幸福不必喧嘩，像一塊手作雪花酥，安靜地甜進心裡。</p><p>從一個小小的家庭廚房開始，我們嘗試了上百次配方，只為了找到奶香與果乾、堅果之間最剛好的平衡。每一批都親手製作、親手包裝，因為我們相信，溫度是吃得出來的。</p><blockquote>「願每個忙碌的日子裡，都留得下一點微光的甜。」</blockquote><p>謝謝每一位選擇微光的你，讓這份小小的心意，得以繼續發光。</p>"
    },
    {
      news_uid: "n2",
      slug: "how-to-keep-fresh",
      category: "保存須知",
      title: "雪花酥怎麼保存最好吃？三個小撇步",
      cover_image_path: "assets/images/products/p-original.jpg",
      excerpt: "手工雪花酥不含防腐劑，正確保存才能留住酥軟口感。分享三個保存小撇步。",
      published_at: "2026-05-08",
      seo_title: "雪花酥保存方式｜微光 MI GLOW",
      seo_description: "手工雪花酥的正確保存方式與保存期限，三個小撇步留住最佳口感。",
      content_html: "<p>手工雪花酥不添加防腐劑，建議收到後盡快享用，並注意以下三點：</p><ul><li><strong>避免陽光直射與高溫</strong>：請放置於陰涼乾燥處，避免棉花糖軟化。</li><li><strong>密封保存</strong>：開封後請以密封袋或保鮮盒收納，減少受潮。</li><li><strong>冷藏更佳</strong>：夏季建議冷藏，享用前回溫 5–10 分鐘，口感最酥。</li></ul><p>常溫保存期限約 14 天，請依包裝標示為準。</p>"
    },
    {
      news_uid: "n3",
      slug: "gift-packaging",
      category: "活動公告",
      title: "節慶禮盒預購開跑・把微光送給在乎的人",
      cover_image_path: "assets/images/brand/bags.jpg",
      excerpt: "質感禮盒，無論送禮或自享都剛剛好。本季節慶禮盒開放預購，數量有限。",
      published_at: "2026-04-25",
      seo_title: "節慶禮盒預購｜微光 MI GLOW",
      seo_description: "微光 MI GLOW 節慶禮盒開放預購，質感包裝，送禮自用兩相宜。",
      content_html: "<p>每逢節慶，總想把最好的留給最在乎的人。微光本季推出限定節慶禮盒，以柔和米白與緞帶包裝，內含綜合口味雪花酥。</p><p>預購期間享早鳥優惠，數量有限，售完為止。歡迎於商品頁選購，或來訊洽詢企業大量訂購。</p>"
    },
    {
      news_uid: "n4",
      slug: "strawberry-season",
      category: "新品上市",
      title: "草莓季限定・酸甜上市",
      cover_image_path: "assets/images/products/p-strawberry.jpg",
      excerpt: "嚴選當季草莓乾，與手工棉花糖交織出柔軟酸甜，草莓雪花酥season限定登場。",
      published_at: "2026-04-10",
      seo_title: "草莓雪花酥季節限定｜微光 MI GLOW",
      seo_description: "草莓季限定，嚴選草莓乾手作雪花酥，酸甜柔軟登場。",
      content_html: "<p>等待了一整年，草莓季終於來了。我們嚴選酸甜飽滿的草莓乾，與手工棉花糖、堅果一同交織，做成這款季節限定的草莓雪花酥。</p><p>奶香中帶著果香，是少女心般的柔軟滋味。數量有限，喜歡草莓的你別錯過。</p>"
    }
  ],

  /* 內容頁 ─ 對應 b_miglow_page ----------------------------------------- */
  pages: {
    about: {
      hero_image: "assets/images/brand/bags.jpg",
      intro: "微光，取自日常裡那一點剛剛好的光。我們相信幸福不必喧嘩，像一塊手作雪花酥，安靜地甜進心裡。",
      story: [
        "微光 MI GLOW 始於一間小小的家庭廚房。我們相信，真正的好味道來自於耐心與細節——於是從配方、食材到包裝，每一個環節都親手把關。",
        "我們選擇小批次手作，不追求量產的快，只願每一塊雪花酥都保有剛出爐的酥軟與奶香。無論是送給自己，或送給在乎的人，都是恰到好處的溫柔。"
      ],
      values: [
        { icon: "leaf",  title: "嚴選食材", desc: "精選天然奶源、堅果與果乾，純粹不馬虎。" },
        { icon: "hand",  title: "手工製作", desc: "小批次親手製作，溫度與心意同在。" },
        { icon: "gift",  title: "精美包裝", desc: "柔和質感禮盒，傳遞每一份心意。" },
        { icon: "truck", title: "新鮮直送", desc: "新鮮現做，下單後盡快為你寄出。" }
      ]
    },
    ordering: {
      sections: [
        { title: "訂購方式", body: "可於本網站線上選購加入購物車後結帳，或透過 Instagram、LINE 私訊洽詢訂購。" },
        { title: "付款方式", body: "目前採銀行匯款／轉帳。訂單成立後系統將顯示匯款帳號，完成轉帳後請於訂單頁回填帳號後五碼，我們確認款項後安排出貨。" },
        { title: "出貨時間", body: "手工製作需要備貨時間，款項確認後約 2–4 個工作天出貨，遇例假日與檔期順延。" },
        { title: "運送方式", body: "以宅配常溫／低溫寄送。單筆訂單滿 NT$1,200 免運，未達門檻酌收運費 NT$160。" },
        { title: "保存方式", body: "請置於陰涼乾燥處避免陽光直射；夏季與開封後建議冷藏，享用前回溫風味更佳。" },
        { title: "保存期限", body: "常溫保存約 14 天，實際請依包裝標示為準。本產品為手工製作，不含防腐劑，建議盡早享用。" },
        { title: "注意事項", body: "本產品含奶、堅果、麩質等成分，對上述食材過敏者請斟酌食用。" },
        { title: "退換貨說明", body: "食品衛生考量，商品一經售出、非瑕疵恕不退換。若收到商品有瑕疵或運送破損，請於收貨 24 小時內拍照來訊，我們將盡快為您處理。" },
        { title: "大量訂購／企業訂單", body: "歡迎婚禮小物、彌月、企業送禮等大量訂購，可客製包裝與卡片，請透過聯絡我們或 LINE 洽詢。" }
      ]
    }
  },

  /* 常見問題 ------------------------------------------------------------- */
  faq: [
    { q: "雪花酥可以保存多久？", a: "常溫約 14 天，請依包裝標示為準。手工製作不含防腐劑，建議盡早享用，夏季建議冷藏。" },
    { q: "有哪些口味？", a: "目前有原味、草莓、巧克力三種口味，季節限定口味會於最新消息公告。" },
    { q: "如何付款？", a: "採銀行匯款／轉帳。訂單成立後顯示匯款帳號，轉帳後於訂單頁回填帳號後五碼即可。" },
    { q: "多久會出貨？", a: "款項確認後約 2–4 個工作天出貨，遇例假日與檔期順延。" },
    { q: "可以客製禮盒嗎？", a: "可以，歡迎婚禮、彌月、企業送禮等大量或客製訂購，請透過聯絡我們洽詢。" },
    { q: "產品含有哪些過敏原？", a: "含奶、堅果、麩質等成分，對上述食材過敏者請斟酌食用。" }
  ]
};
