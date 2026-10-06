# ChatGPT/Codex — FAZ 3 Görev Listesi
> Codex limitteyse bu görevleri Cursor'un paralel ajanları da yapabilir. Klasör kuralları aynı kalır.

**Proje:** HAREKÂT. Türk askeri FPP Tim Battle Royale.
- Unity oyunu Claude'da; backend, altyapı ve Unity entegrasyonu Cursor'da.
- **Altyapı:** Windows Server, MSSQL, düz HTML/CSS/JS web. Linux yok.
- **Görsel strateji:**
  - Şimdi: kodla üretilen low-poly görünüm + Unity Terrain splat (Grass/DryGrass/Dirt/Rock…).
  - **Sonra:** hazır varlık paketleri (Asset Store, Mixamo, Sonniss, Poly Haven) → `ContentOverrides`.
  - **Kalite aşaması:** mesh foliage (çim/çalı), LOD/instancing; ayrıntı `Design/Art/ENVIRONMENT.md`.
  - **En son:** gerçekçi özel modeller. **URP** yakın vadede sabit; HDRP yalnızca gerekirse değerlendirilir.

Başlatma cümlesi: *"Docs/CODEX_FAZ3.md içindeki GÖREV C3-X'i baştan sona uygula."* 8 görev de bağımsızdır, paralel çalışabilir.

## KURALLAR
- **Yazma izni:** `Design/*`, `Web/*`, `Wiki/*`, `QA/*`, `Marketing/*`, `Localization/*`, `Tools/BalanceCalc/*`, `Tools/LocTool/*`, `Tools/SqlReports/*`.
- **Asla yazma:** `Assets/`, `Packages/`, `ProjectSettings/`, `Backend/`, `Deploy/`, `.github/`, `Tools/UnityVerify/`, `Tools/AgentWorkflows/`, `Tools/Installer/`, `Tools/Launcher/`, `Docs/`. Okumak serbest.
- **Kaynak veriler (salt okunur):**
  - `Assets/_Project/Scripts/Infrastructure/Rendering/MaterialId.cs`
  - `Assets/_Project/Scripts/Infrastructure/Audio/SoundId.cs`
  - `Assets/_Project/Scripts/Application/Catalogs/*.cs` (WeaponIds, ItemIds, RankCatalog, LoadoutCatalog)
  - `Assets/_Project/Scripts/Core/Domain/Enums/*` (MilitaryRank, TeamRole, SquadOrder...)
  - `Assets/_Project/Scripts/Infrastructure/World/WorldTypes.cs`
  - `Docs/CONTRACTS.md`, `Design/GDD/*`
- İlerleme kaydı: `Design/CODEX_DURUM.md` dosyasına bir "FAZ 3" bölümü ekle.
- Git: yalnızca kendi klasörlerini `git add` ile ekle; dosya değiştiren git komutları yasak.
- Kurgu: harekât tatbikatı, Mavi/Kırmızı kuvvetler; gerçek örgüt ya da grup adı yok.
- Resmi TSK armaları ya da logoları **kopyalanmaz**; özgün, stilize tasarım yapılır.
- **DURMA KURALI:** Ana iş bitince UZATMA maddelerine geç; o da bitince kalite, doğruluk ve dokümantasyonu derinleştir.

---

## GÖREV C3-1 — Hazır Varlık Listesi ve Eşleme Tablosu · `Design/Assets/`
Cursor'un "Varlık Eşleyici" aracı (F3-4) bu CSV'leri okuyacak; **kolon adları sabit** olmalı.
- **`materials.csv`:** `MaterialId,kaynak_site,varlik_adi,url,lisans,ucretsiz_mi,notlar`
  - Her `MaterialId` için Poly Haven / ambientCG önerisi: zemin, kaya, beton, taş duvar, kiremit, metal, kamuflaj dokusu fikri.
- **`sounds.csv`:** `SoundId,kaynak_paket,dosya_onerisi,katman(yakın/orta/uzak),lisans,notlar`
  - Her `SoundId` için Sonniss GDC paketlerinden öneri.
  - Silahlar için yakın, orta ve uzak katman; yankı notu.
- **`animations.csv`:** `durum,mixamo_animasyon_adi,in_place,notlar`
  - Asker: idle, walk, run, sprint, crouch walk, prone crawl, rifle aim, reload, grenade throw, death (ön/arka), sitting (araç koltuğu), hit react.
  - FPS el animasyonları için ayrıca Asset Store önerileri.
- **`models.csv`:** `kimlik(WeaponId/arac/asker/bina),gecici_hazir_model_onerisi,kaynak,fiyat_araligi,benzerlik_notu,lisans`
  - MPT-76'ya benzeyen genel tüfek modelleri gibi geçici seçenekler.
  - Kirpi'ye benzer MRAP, T-70'e benzer genel maksat helikopteri, asker, köy evi, cami, karakol, Hesco, kum torbası.
- **`README.md`:**
  - Öncelik sırası: önce ücretsiz paketlerle "her şey yerinde" sürümü, sonra seçici ücretli paketler.
  - Toplam bütçe senaryoları: 0 $ / 500 $ / 2.000 $.
  - Lisans kontrol listesi: ticari kullanım, Steam dağıtımı.

**UZATMA:** Her eşleme için Unity içe aktarma ayarı notları: ölçek, Humanoid rig, doku sıkıştırma, mesh read/write.

## GÖREV C3-2 — Freelancer Teknik Şartnameleri · `Design/Art/Briefs/`
Her öğe için ayrı Markdown şartname. **Türk silahları:** MPT-76, MPT-55, G3A7, KNT-76, JNG-90 Bora-12, PMT-76, SAR 9, Canik TP9, SAR 109T, Escort. **Araçlar:** Kirpi MRAP, T-70 helikopter. **Karakter:** TSK dijital kamuflajlı asker; komutan berelisi varyantı. **Yapılar:** Anadolu köy evi, cami, karakol.

Her şartnamede:
- Kullanım bağlamı (FPP elde, yerde eşya, 3. şahıs bot).
- Poligon bütçesi ve LOD0–LOD3.
- Doku setleri (PBR metal/roughness, 4K/2K), UV kuralları.
- Pivot ve ölçek (metre), Unity'ye uygun eksenler.
- **Transform isimlendirmesi:** `Muzzle`, `Grip_R`, `Grip_L`, `Magazine`, `Bolt`, `Sight`. Bunlar F3-4 eşleyicisiyle uyumlu olmalı.
- Animasyon gereksinimleri (şarjör, mekanizma).
- Teslim formatı (FBX + dokular), kabul kriterleri, kontrol listesi.
- Referans açıklaması (metinle; telifli görsel kopyalama yok).
- Tahmini süre ve fiyat aralığı.

Genel dokümanlar:
- `Design/Art/ArtBible.md`: görsel kimlik (askeri gerçekçilik, Anadolu dağ coğrafyası, renk paleti, ışık ve atmosfer), kalite hedefi (PUBG / CoD referans seviyesi), performans bütçeleri.
- `Design/Art/TSK_Kamuflaj_Rehberi.md`: dijital kamuflajın renk ve desen tarifi (stilize, özgün).

**UZATMA:** Her şartname için iş ilanı metni (TR/EN) ve değerlendirme puan tablosu.

## GÖREV C3-3 — Ses Tasarımı ve Telsiz Konuşmaları · `Design/Audio/`
- **`ses_haritasi.md`:** her `SoundId` için tanım, katmanlar, menzil, yankı (iç/dış mekân), karışım önceliği, kaç varyasyon gerektiği.
- **`telsiz_replikleri.csv`** (`anahtar,durum,konusan_rol,metin_tr,metin_en,ton,sure_sn`), **200+ replik**:
  - Temas bildirimi: "Temas! Saat iki yönü, iki yüz metre!"
  - Şarjör değişimi: "Şarjör!"
  - Yaralanma: "Vuruldum!"
  - Sıhhiye çağrısı.
  - Tim emirlerine onay (F1–F4): "Anlaşıldı komutanım, takipteyiz!"
  - Komuta devri: "Komuta bende! Tim, beni takip et!"
  - Topçu istek ve onayı: "Atış için hazır, hedef koordinatı alındı."
  - Harekât alanı daralıyor, intikal (helikopter ve Kirpi), düşman elendi, tim elendi, zafer.
  - Rütbeye uygun hitaplar: "komutanım", "çavuşum".
- **`ortam_sesleri.md`:** Kuzgun Vadisi bölgelerine göre ortam: köy (ezan sesi **yok**, köpek, rüzgâr), orman, baraj (su), taş ocağı. Ayaz Geçidi ve Mavi Liman için de.
- **`muzik_brifi.md`:** ana menü, maç başı ve zafer müzikleri; besteci için brif (askeri, Anadolu enstrümanları esintili).

**UZATMA:** Replikler için metinden sese (TTS) üretim planı ve seslendirme sanatçısı kayıt yönergesi.

## GÖREV C3-4 — Tim Kimlikleri, Amblemler ve Rütbe Nişanları · `Design/Teams/`
- 8 tim (Kartal, Bozkurt, Şimşek, Yıldırım, Kılıç, Kaplan, Pars, Atmaca Timi), her biri için:
  - Özgün **SVG amblem**: hilal-yıldız esinli ama resmi arma değil.
  - Renk, kol bandı rengi, slogan.
  - Kısa tim hikâyesi (kurgu).
- Rütbe nişanları SVG setinin tamamı (`MilitaryRank` 19 rütbe). `Design/UI` altında varsa tutarlılığı denetle ve eksikleri tamamla.
- `Design/Teams/README.md`: oyunda nerede kullanılacağı (kol bandı dokusu, HUD tim paneli, skor ekranı, web profil).

**UZATMA:** Her amblemin 64/128/256/512 px PNG dışa aktarımı için betik (`Design/Teams/export.mjs`, bağımlılıksız; SVG'den PNG mümkün değilse talimat).

## GÖREV C3-5 — Eğitim (Tutorial) ve İlk Deneyim Tasarımı · `Design/Tutorial/`
- **Atış Poligonu görev zinciri:** 15+ adım.
  - Hareket, eğilme ve yüzüstü, yana eğilme.
  - Ateş, nişan, dürbün, şarjör.
  - Ateş modu, el bombası, sis.
  - İyileşme, envanter, harita.
  - Tim emirleri (F1–F4), topçu (V).
  - Araç (Kirpi) kullanımı.
  - Her adım için: hedef, ekran metni (TR/EN), başarı koşulu (oyun içi olaya göre), ipucu, süre.
- **İlk maç rehberliği:** ilk 3 maçta gösterilecek bağlamsal ipuçları ve tetikleyiciler.
- **`tutorial_steps.json`:** Unity'de okunabilecek veri formatı (alanlar ve olay adları `Core/Events` ile uyumlu).

**UZATMA:** Zorluk ayarlı "Er → Uzman → Komando" eğitim parkuru tasarımı.

## GÖREV C3-6 — İlerleme: Başarımlar, Sezon, Kozmetik Katalog · `Design/Progression/`
- **50+ başarım:** id, ad (TR/EN), koşul (oyun olaylarına göre), XP ödülü, ikon fikri.
  - Örnek: "İlk Zafer", "Komuta Devri — komutan şehit düştükten sonra timi zafere taşı", "Keskin Nişancı — JNG-90 ile 300 m üstü kafa vuruşu", "Topçu Ustası".
- **Sezon yapısı:** 10 haftalık sezon, rütbe ve sezon puanı ayrımı, ödül merdiveni (**yalnızca kozmetik**).
- **Kozmetik katalog:**
  - Kamuflaj varyantları (orman, dağ, çöl, kış, şehir).
  - Bere renkleri, kol bandı desenleri.
  - Silah kaplamaları (desen tarifleri).
  - Zafer pozları ve amblem çerçeveleri.
- **Backend için tohum verisi (JSON):** `achievements.json`, `cosmetics.json`, `season1.json`. Cursor backend'e aktaracak; şemayı `Backend/Harekat.Domain` modelleriyle uyumlu tut (salt okunur incele).

**UZATMA:** Ekonomi dengesi tablosu: sezon başına beklenen kazanım, oynama süresi analizi.

## GÖREV C3-7 — Web: İndirme Sayfası, Sistem Gereksinimleri, Yama Notları · `Web/`
- **"İndir" sayfası:**
  - Windows kurulum dosyası bağlantısı (Cursor F3-8 üretiyor; şimdilik yer tutucu).
  - Kurulum adımları.
  - SHA-256 gösterimi.
  - Steam bağlantısı (yakında).
- **Sistem gereksinimleri (Windows, minimum ve önerilen):** `Marketing/LiveOps` ile tutarlı.
- **Yama notları sistemi:** `Web/content/patchnotes/*.md` → build sırasında HTML'e dönüşür; RSS beslemesi.
- **"Tim Kimlikleri" sayfası:** C3-4 amblemleri.
- **"Başarımlar" sayfası:** C3-6 listesi.
- Mevcut lint/test/build betikleri geçmeli; IIS `web.config` korunmalı.

**UZATMA:** Basın kiti sayfası (Marketing'deki içerikten), çerez/KVKK ve gizlilik metni taslağı.

## GÖREV C3-8 — QA ve Wiki Güncellemesi (Unity entegrasyonu sonrası) · `QA/`, `Wiki/`
**Başlama koşulu:** Claude `Docs/DURUM.md`'ye "ENTEGRASYON/İNCELEME TAMAMLANDI" yazdıktan sonra.
- Unity kodundaki son değişikliklere göre QA senaryolarını güncelle: yeni özellikler (sürülebilir Kirpi, dev konsolu, online paneller) için senaryo ekle.
- Wiki'yi kataloglardan yeniden üret; silah değerleri değiştiyse güncelle.
- `QA/RegresyonListesi.md`: her sürümde koşulacak 60 kritik senaryo.

**UZATMA:** QA sonuç takip panosu (`QA/dashboard.html`, düz HTML/JS; CSV'den okur).
