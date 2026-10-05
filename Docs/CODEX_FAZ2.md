# ChatGPT/Codex — FAZ 2 Görev Listesi

**Proje:** HAREKÂT. Türk askeri temalı, 10 kişilik timlerle oynanan FPP Tim Battle Royale.
- Unity oyununu Claude ajanları yazıyor.
- Backend, dağıtım ve Unity entegrasyonunu Cursor yapıyor (bkz. `Docs/CURSOR_FAZ2.md`).

**Altyapı kararları (kesin):**
- Sunucular **Windows Server**. **Linux yok.**
- Veritabanı **MSSQL (SQL Server)**.
- Web **düz HTML/CSS/JS** (framework yok).
- Backend IIS üzerinde (ASP.NET Core).

**Faz 1'de yaptıkların:** Web portalı v1, GDD (21 bölüm), denge hesaplayıcı, harita paftaları, yerelleştirme, pazarlama. Hepsi kalıyor; Faz 2 bunların üzerine kurulur.

Aşağıdaki **8 görev birbirinden bağımsız**. Her birini ayrı bir Codex görevinde aynı anda başlatabilirsin.
Başlatma cümlesi: *"Docs/CODEX_FAZ2.md içindeki GÖREV C2-X'i baştan sona uygula."*

## ORTAK KURALLAR
- Her görev **yalnızca kendi klasörüne** yazar (her görevde belirtilmiş).
- Şu klasörlere **asla yazma**: `Assets/`, `Packages/`, `ProjectSettings/`, `Backend/`, `Deploy/`, `.github/`, `Tools/LoadTest/`, `Tools/DiscordBot/`, `Tools/UnityVerify/`, `Docs/`. Okumak serbest.
- Koordinasyon dosyası: `Docs/DURUM.md`. Görev başında oku. Kendi ilerlemeni `Design/CODEX_DURUM.md` dosyasına yaz (tarih, saat, yapılan, kalan).
- Git: yalnızca kendi klasörlerini `git add` ile ekle (`git add -A` kullanma). Dosya değiştiren git komutlarını çalıştırma: checkout, reset, clean, stash.
- Arayüz dili Türkçe; İngilizce ikinci dil. Gerçek dünyadan örgüt ya da grup adı kullanma; kurgu "harekât tatbikatı", Mavi/Kırmızı kuvvetler.
- Kaynak veriler (salt okunur):
  - `Assets/_Project/Scripts/Application/Catalogs/*.cs` (silahlar, eşyalar, rütbeler, teçhizat)
  - `Assets/_Project/Scripts/Core/Domain/**`
  - `Assets/_Project/Scripts/Infrastructure/World/WorldTypes.cs` ve `MapLayout*.cs`
  - `Backend/Harekat.Api` (endpoint'ler), `Backend/ClientSdk` (DTO'lar)
  - `Docs/CONTRACTS.md`, `Design/GDD/*`
- **DURMA KURALI:** Ana iş bitince durma. Görevin "UZATMA" listesine geç; o da bitince test, erişilebilirlik, performans ve dokümantasyonu derinleştir. Her adımda build, lint ve testleri çalıştır.

---

## GÖREV C2-1 — Web Portalı v2: Backend Entegrasyonu ve Yönetici Paneli · `Web/`
Teknoloji aynı kalır: düz HTML/CSS/JS, ES modülleri. Mevcut `scripts/lint|test|build` düzeni korunur.
1. **API katmanı:** `Web/js/api/`
   - Backend endpoint'lerinin tamamı için tipli JSDoc'lu istemci: `Backend/Harekat.Api` ve `Backend/ClientSdk` DTO'larından üret, elle senkron tut.
   - JWT saklama ve yenileme; 401'de oturumu kapatma.
   - Hata ve yeniden deneme; çevrimdışı mock modu korunur.
2. **Sayfalar:**
   - **Giriş/kayıt** (form doğrulama).
   - **Profil:** rütbe nişanı, XP çubuğu, kariyer, son maçlar, en iyi silah.
   - **Tim:**
     - Tim kur, davet kodu, üyeler (10 kişi), hazır durumu.
     - Gerçek zamanlı güncelleme: backend'de SignalR varsa `@microsoft/signalr` kullanma, düz WebSocket ya da long-polling ile yaz.
   - **Eşleştirme durumu:** sıradaki süre, tahmini bekleme.
   - **Sıralamalar:** sezon filtresi, sayfalama.
   - **Maç detayı:** tim sıralaması, öldürme akışı, Kuzgun Vadisi haritası üzerinde (SVG) iniş ve ölüm noktaları.
   - **Başarımlar.**
3. **Yönetici paneli** (rol tabanlı; menü yetkiye göre görünür):
   - Oyuncu arama.
   - Yasaklama ve susturma.
   - Rapor kuyruğu.
   - **Windows sunucu filosu:** sunucu listesi, CPU/RAM, aktif maç, port.
   - Telemetri hile şüphelileri (`Backend/Harekat.Telemetry` uçları).
   - Sistem sağlığı.
4. **IIS dağıtımı:**
   - `Web/web.config`: statik dosya, SPA yönlendirme, `/api` için URL Rewrite + ARR reverse proxy kuralları, güvenlik başlıkları (CSP, HSTS), sıkıştırma, önbellek.
   - `Web/README.md`'ye IIS kurulum adımları.
5. **Kalite:**
   - i18n (TR/EN, `Web/i18n/*.json`).
   - Erişilebilirlik: klavye ile gezinme, ARIA, kontrast.
   - Responsive tasarım.
   - Lighthouse 90+.
   - `scripts/test.mjs` ile birim testleri (DOM'suz modüller) ve sözleşme testi: endpoint listesi ile backend route'larının karşılaştırılması.

**UZATMA:**
- PWA push bildirimi altyapısı (tim daveti).
- Koyu ve açık tema.
- Oyuncu karşılaştırma sayfası.
- Sezon arşivi.
- Haber/blog sistemi (Markdown'dan statik üretim).

## GÖREV C2-2 — Oyuncu Kılavuzu ve Wiki (statik site) · `Wiki/`
- Düz HTML/CSS/JS. Bir Node betiği (`Wiki/build.mjs`) şunları okuyup statik sayfalar üretir:
  - `Design/GDD/*.md`
  - Katalog C# dosyaları: silahlar, eşyalar, rütbeler, teçhizat
  - `Design/Maps/*`
  - Markdown → HTML çevirici kendin yaz ya da bağımlılıksız küçük bir tane kullan.
- **Sayfalar:**
  - Her silah için ayrı sayfa: hasar, şarjör, RPM, mermi tipi, menzil düşüşü, öldürme süresi tablosu (`Tools/BalanceCalc` çıktısından), kullanım ipuçları.
  - Her rütbe için nişan, XP eşiği, açıklama.
  - Her tim görevi için başlangıç teçhizatı ve taktik rolü.
  - Her lokasyon için pafta kesiti ve taktik notlar.
  - Kontroller (tuş haritası), komuta zinciri, intikal (T-70 / Kirpi), topçu desteği, harekât alanı fazları.
  - "Yeni başlayanlar" rehberi.
- Arama: istemci tarafında, önceden üretilmiş indeks JSON'u ile.
- IIS için `Wiki/web.config`.
- `npm run build` ile `Wiki/dist`. Testler: kırık link ve eksik sayfa kontrolü.

**UZATMA:**
- TR/EN.
- Silah karşılaştırma aracı (iki silah yan yana).
- Etkileşimli Kuzgun Vadisi haritası (SVG, katman aç/kapa).
- Yama notu arşivi.

## GÖREV C2-3 — QA: Test Planı, Senaryolar ve Playtest Protokolü · `QA/`
- `QA/TestPlan.md`: kapsam, ortamlar (Windows istemci, Windows Dedicated Server, macOS geliştirici), giriş/çıkış kriterleri, risk listesi.
- `QA/Senaryolar/`: **300+ manuel test senaryosu**, modül modül. Kaynak olarak `Docs/CONTRACTS.md` ve `Docs/MODUL_SPESIFIKASYONLARI.md` kullan:
  - Hareket, silahlar, envanter, iyileşme, zırh
  - İntikal, komuta zinciri, tim emirleri, topçu
  - Bölge (zone), yapay zekâ
  - HUD, menüler, ayarlar, kariyer
  - Atış Poligonu, performans
  - Online (eşleştirme, bağlantı kopması), backend uçları
  - Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.
  - CSV ve Markdown olarak; Excel'e aktarılabilir.
- `QA/Smoke.md`: her build sonrası 15 dakikalık duman testi listesi.
- `QA/Playtest/`: playtest protokolü (oturum akışı, gözlem formu), oyuncu anketi (HTML form + JSON çıktı), metrik listesi.
- `QA/Hata_Sablonu.md`, önem ve öncelik matrisi, GitHub issue şablon önerileri (yalnızca öneri; `.github/` klasörüne yazma).

**UZATMA:**
- Denge test senaryoları (`BalanceCalc` hedefleriyle).
- Yük ve ağ dayanıklılık test senaryoları (gecikme, paket kaybı).
- Erişilebilirlik kontrol listesi.
- Windows'a özgü test matrisi: GPU ve sürücü, DPI ölçekleme, çoklu monitör.

## GÖREV C2-4 — MSSQL Analitik ve Raporlama · `Tools/SqlReports/`
- Backend'in MSSQL şemasını oku (`Backend/Harekat.Infrastructure` EF modelleri ve migration'ları).
- **Salt okunur T-SQL rapor betikleri:**
  - Günlük, haftalık ve aylık aktif oyuncu.
  - Elde tutma oranları (D1 / D7 / D30).
  - Maç süresi dağılımı.
  - Silah kullanım ve öldürme oranları.
  - Tim yerleşimi ve galibiyet oranı.
  - Rütbe dağılımı.
  - Eşleştirme bekleme süreleri.
  - Sunucu doluluğu.
  - Hile şüphelisi trendi.
- Öneri betikleri ayrı klasörde: `views/`, `procedures/`, `indexes/`. Uygulamasını Cursor yapar; sen yalnızca öneri yazarsın.
- **Node raporlama aracı:** `report.mjs`. Bağlantı dizesi ortam değişkeniyle; `mssql` npm paketi kullanılabilir.
  - Betikleri çalıştırıp **tek dosyalık HTML KPI raporu** üretir. Grafikleri inline SVG ile kendin çiz.
  - Bağlantı yoksa örnek veriyle çalışır.
- Windows Görev Zamanlayıcısı ile haftalık otomatik rapor için PowerShell betiği ve README.

**UZATMA:**
- Silah denge telemetrisini `BalanceCalc` tahminleriyle karşılaştıran rapor.
- Kohort analizi.
- Anomali tespiti (basit z-skoru).

## GÖREV C2-5 — Yerelleştirme Hattı v2 · `Localization/` ve `Tools/LocTool/`
- **`Tools/LocTool/`** (Node):
  - `extract`: Assets C# dosyalarındaki Türkçe metinleri salt okunur tarar.
  - `diff`: yeni ve silinen metinler.
  - `validate`: yer tutucular `{0}`, uzunluk riski, eksik çeviri.
  - `export`: Unity için `Localization/unity/strings_<lang>.json` (anahtar → metin).
- `Localization/`:
  - Anahtar adlandırma standardı: `hud.ammo.reload` gibi.
  - TR, EN, DE, AZ, AR tabloları.
  - Askeri terim sözlüğü v2: rütbe kısaltmaları, emirler, silah sınıfları.
  - Unity'ye geçiş planı: hangi C# dosyasında hangi string'in anahtara dönüşeceği listesi. Cursor ya da Claude uygulayacak.
- Testler ve README.

**UZATMA:**
- Sağdan sola (RTL, Arapça) arayüz riskleri raporu.
- Font kapsamı kontrolü: hangi karakterler LegacyRuntime fontunda yok?

## GÖREV C2-6 — Yeni Haritalar: Tasarım Verisi · `Design/Maps/v2/`
- 2 yeni kurgusal Türk coğrafyası haritası:
  - **"Ayaz Geçidi":** karlı, yüksek dağ, geçit, kayak evi, radar üssü.
  - **"Mavi Liman":** kıyı kasabası, liman, deniz feneri, zeytinlik, sahil karakolu.
- Her harita için:
  - `layout.json`: `WorldTypes.cs` / `MapLayout` alanlarıyla birebir uyumlu (Locations, Roads, Lakes, Rivers, HalfSize, MaxHeight, WaterLevel).
  - Ölçekli SVG pafta: 100 m grid, A–J / 1–10 koordinatları.
  - Lokasyon başına taktik not.
  - İntikal sektörleri.
  - Yağma kademeleri dağılımı.
- `Design/Maps/v2/README.md`: Kuzgun Vadisi ile karşılaştırma, oynanış hedefleri, performans riskleri (su, kar yüzeyleri).
- Kuzgun Vadisi için **gece harekâtı varyantı** konsepti: aydınlatma, gece görüş, ses.

**UZATMA:**
- `layout.json`'u doğrulayan Node betiği: lokasyon çakışmaları, yol eğimi tahmini, harita sınırları.
- Her harita için ses ortamı listesi.

## GÖREV C2-7 — Arayüz Mockup'ları ve Stil Rehberi · `Design/UI/`
- Unity arayüzünün hedef görünümünü **HTML/CSS mockup'ları** olarak üret. Claude'un HUD ve menü ajanlarına görsel referans olacak.
- **Ekranlar:**
  - Ana menü
  - Harekât kurulumu (tim sayısı, zorluk, intikal)
  - Kariyer ve rütbe
  - Ayarlar
  - Oyun içi HUD (can, cephane, pusula, tim paneli, öldürme akışı, mini harita)
  - Tam harita
  - Envanter
  - Dürbün görünümü
  - Ölüm ve zafer ekranları
  - Yükleme ekranı
- 1920×1080 ve 2560×1440 ölçekleri; her ekranın ekran görüntüsü için `Design/UI/screens.html` galerisi.
- `Design/UI/StyleGuide.md`:
  - Renk tokenları: koyu zeytin, Türk kırmızısı #E30A17, kehribar, dost mavisi, düşman kırmızısı.
  - Tipografi ölçeği, boşluk ölçeği, ikon dili, animasyon süreleri, erişilebilirlik kontrastı.
- İkon seti (SVG):
  - Silah sınıfları, eşya kategorileri
  - Tim emirleri: takip, mevzi, taarruz, toplan
  - Topçu, intikal (helikopter / Kirpi)
  - Rütbe nişanları: stilize, resmi armaları kopyalamadan

**UZATMA:**
- Animasyonlu prototipler: CSS animasyonuyla HUD olayları (isabet işareti, hasar yönü, komuta devri bildirimi).
- Renk körlüğü simülasyonu raporu.

## GÖREV C2-8 — Topluluk, E-spor ve Canlı Operasyon Planı · `Marketing/LiveOps/`
- **Turnuva kural kitabı:** 10'luk tim formatı, puanlama (yerleşim + öldürme), hile ve itiraz süreçleri.
- **Lig yapısı:** sezonlar, küme düşme.
- **Canlı operasyon takvimi:** 12 haftalık etkinlik planı. Örnek: "Gece Harekâtı haftası", "Keskin Nişancı haftası".
- **Sezon ödülleri:** yalnızca kozmetik. Kazanmak için ödeme (pay-to-win) yok.
- **Topluluk:** Discord sunucu yapısı, moderasyon politikası, içerik üretici programı.
- **Oyuncu destek SSS'si:** bağlantı, Windows kurulum, sistem gereksinimleri, performans ipuçları.
- **Sistem gereksinimleri tablosu (Windows):** minimum ve önerilen.

**UZATMA:**
- Yayıncı arayüzü için tasarım: izleyici modu fikirleri (yalnızca doküman).
- Sezon 1 yol haritası metni.
