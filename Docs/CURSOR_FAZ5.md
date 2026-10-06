# Cursor (Opus) — FAZ 5 Görev Listesi

**Durum (2026-10-06 00:50):**
- Cursor: F4-1 ✅ (Windows hariç), F5-2/8 ✅, F5-3/4/5 🔄→kısmi ✅, F5-6 hazırlık (ContentOverrides + LFS).
- Gerçekçilik: `Docs/CURSOR_GERCEKCILIK.md` — araştırma bitince onay beklemeden uygula.
- Soak: Claude `SoldierModel` kırığı (DURUM); KartalYaylasiProps Cursor tamamladı.
- **Altyapı:** Windows Server, MSSQL, düz HTML/CSS/JS. Linux yok.

**Önce F4 bitsin:** `Docs/CURSOR_FAZ4.md`. F4-1 doğrulamasını sürdür; Windows modülleri kurulunca F4-2, F4-4, F4-5 ve F4-6'ya geç. Faz 5 görevleri F4 ile **paralel** yürüyebilir; her birini ayrı ajanda başlat.
Başlatma cümlesi: *"Docs/CURSOR_FAZ5.md içindeki GÖREV F5-X'i baştan sona uygula."*

## 0. ÇAKIŞMA KURALLARI (Claude dalga 6 çalışıyor)
Bu dosyalara **dokunma**; hata görürsen `Docs/DURUM.md`'ye not düş:
- `Infrastructure/AI/*`
- `Application/Catalogs/WeaponCatalog.cs`, `Application/Services/WeaponRuntimeService.cs`, `RecoilPattern.cs`, `PenetrationRules.cs`
- `Presentation/Player/PlayerWeaponHandler.cs`
- `Infrastructure/Combat/BallisticsSystem.cs`, `Penetration.cs`
- `Presentation/UI/Suppression*`, `HitFeedback*`
- `Infrastructure/Rendering/ScreenEffects*`
- `Infrastructure/Player/FirstPersonCameraController.cs`
- `Infrastructure/Audio/*`
- `Infrastructure/World/BuildingGenerator.cs`
- `Infrastructure/Weapons/*`
- `Infrastructure/Characters/*`
- `Infrastructure/Input/InputBindings.cs`

**Bu fazda Cursor'a özel alanlar:**
- `Presentation/UI/*` içindeki **yerelleştirme dönüşümü** (F5-3)
- `Tests/PlayMode/*`
- `Editor/*`
- `Backend/`, `Deploy/`, `Web/`, `Wiki/`, `Tools/*` (UnityVerify ve AgentWorkflows hariç)

**Genel kurallar:**
- Git: yalnızca kendi dosyalarını ekle; dosya değiştiren git komutları (checkout, reset, clean, stash) yok.
- C# tuzağı: `Project.*` ad alanı içinde **`UnityEngine.Application`** tam adını kullan.
- Doğrulama: `zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_cursor --player --tests` (0 hata) + gerçek Unity.
- İlerleme: `Docs/FAZ2_DURUM.md` dosyasına "FAZ 5" başlığıyla, tarih ve saatle yaz.

---

## GÖREV F5-1 — Otomatik Oyun Testi ve Dayanıklılık (Soak) Koşusu · `Tests/PlayMode/*`, `Editor/*`, `Tools/UnityVerify/run_soak.sh`
Oyunun **kendi kendine oynandığı** uzun testler. Claude Unity'yi açamadığı için bu, projenin en değerli doğrulamasıdır.
1. **"Bot maçı" PlayMode testi:**
   - KuzgunVadisi'nde oyuncu izleyici modunda, 4 tim × 10 bot; hızlandırılmış zaman ×2.
   - Maç **bitene kadar** koşsun (en fazla 20 dakika gerçek zaman).
   - Toplanacaklar:
     - Tüm `LogType.Exception` ve `Error` kayıtları.
     - Kare süresi istatistikleri (PerfSampler).
     - Bot sayısı zaman serisi ve maç sonucu (kazanan tim).
     - Her 30 sn'de bir ekran görüntüsü → `Logs/soak/<tarih>/`.
2. **Ayaz Geçidi, Mavi Liman, Çatışma modu ve Atış Poligonu (eğitimli)** için de 5'er dakikalık soak testleri.
3. **Rapor:** `Logs/soak/RAPOR.md`. Hata başına stack trace, kaç kez oluştuğu, hangi dosyada olduğu ve öneri.
4. **Düzeltme:** Bulunan hataları **Claude alanı dışındaysa düzelt**. Claude alanındaysa `DURUM.md`'ye "dosya:satır — hata — öneri" biçiminde yaz.
5. **Tekrar koşum:** Döngüyü sıfır exception olana kadar tekrarla.
6. **Batch betiği:** `Tools/UnityVerify/run_soak.sh`; tek komutla tüm soak testlerini koşturur.

## GÖREV F5-2 — Gerçek Oynanış Kontrol Listesi ve Ekran Görüntüleri · `Docs/OYUN_TESTI.md`
Kullanıcının oyunu ilk kez oynayacağı an için hazırlık:
1. MainMenu'den başlayarak **her özelliği tek tek dene** (editörde Play):
   - Harekât kurulumu (3 harita, gün saati, hava, intikal)
   - T-70 / Kirpi intikali ve iniş
   - Silahlar (10 Türk silahı, eklentiler), ateş, şarjör, dürbün
   - Bombalar, sis, topçu (V), İHA (U)
   - Tim emirleri (F1–F4, komut çarkı Y), ping (orta tık)
   - Yaralı düşme ve canlandırma, Kirpi sürüşü, taret ve tim taşıma
   - Envanter, harita, skor tablosu, telsiz altyazıları, başarım bildirimleri
   - Killcam ve izleyici, maç sonu, kariyer ve rütbe
   - Ayarlar (tuş atama, gamepad, dil, grafik), kozmetikler
   - Online paneller (backend çalışırken)
2. Her madde için ekran görüntüsü ve "çalışıyor / hatalı / eksik" durumu. Hataları F5-1'deki gibi düzelt ya da raporla.
3. Kullanıcı için **5 dakikalık "ilk oyun" rehberi:** nasıl açılır, nereye basılır, neye dikkat edilir.

## GÖREV F5-3 — Tam Yerelleştirme Dönüşümü · `Presentation/UI/*` (yalnızca metin), `Assets/_Project/Resources/Localization/*`
Şu an yalnızca ana menü, duraklatma, maç sonu ve HUD başlıkları `Loc.Get` kullanıyor.
1. `Presentation/UI/*` ve `Presentation/Bootstrap/*` içindeki **tüm sabit Türkçe metinleri** `Loc.Get("anahtar", "Türkçe yedek")` biçimine çevir. İstisnalar:
   - Claude'un dalga 6 dosyaları (`Suppression*`, `HitFeedback*`)
   - `Scripts/Online/UI/*` (o klasör de sende; çevirebilirsin)
2. Anahtar adlandırma: `Localization/KEY_NAMING.md`.
3. Yeni anahtarları **TR, EN, DE, AZ, AR** JSON dosyalarına ekle. Çeviri kalitesi: askeri terim sözlüğü (`Localization/ASKERI_TERIMLER_SOZLUGU.md`).
4. `Tools/LocTool` ile doğrula: eksik anahtar, yer tutucu, uzunluk riski. `Localization/strings.csv`'yi güncelle.
5. Dil değişince tüm açık ekranlar yenilensin: `Loc.LanguageChanged` aboneliklerini tamamla.
6. **Arapça (RTL):** en azından metin hizası ve kısa sağdan sola düzeni; tam şekillendirme gerekiyorsa öneriyi raporla.

## GÖREV F5-4 — Backend: Canlı Oyun Özellikleri · `Backend/*` (MSSQL)
Oyunda yapılan özelliklerin backend karşılıkları:
1. **Başarımlar:** `POST /achievements/sync` (oyuncu → açılan id'ler + ilerleme); sunucu tarafında doğrulama (maç sonuçlarından türetilebilenler sunucuda hesaplanır).
2. **Kozmetikler:** sahiplik ve kuşanılmış kozmetik uçları; rütbe ve başarım kilit kontrolü; `Design/Progression/cosmetics.json` tohumu.
3. **Sezonlar:** `season1.json` tohumu; sezon puanı; sezon sonu ödülleri.
4. **Sıralamalar:** harita ve moda göre filtre (Kuzgun / Ayaz / Mavi Liman, BR / Çatışma).
5. **Arkadaşlar ve parti:** istek, kabul, çevrimiçi durumu (SignalR); 10 kişilik parti hazır kontrolü.
6. **Hile telemetrisi:** HAREKÂT'a özgü kurallar.
   - JNG-90 ile 600 m üstü sürekli kafa vuruşu
   - Duvar arkasından isabet oranı (delme özelliği **gerçek**: 7.62 tahtayı deler; kural bunu dikkate alsın)
   - Kirpi hız sınırı aşımı
7. **Testler:** her uç için xUnit; MSSQL migration'ları; `dotnet build` ve `dotnet test` yeşil.
8. **Dokümantasyon:** OpenAPI ve `Backend/README.md` güncel.

## GÖREV F5-5 — Web Portalı v3 · `Web/*` (düz HTML/CSS/JS)
1. **Oyuncu profili:** rütbe nişanı, kariyer, **başarımlar** (77'lik katalog, açılanlar), **kozmetikler**, son maçlar, en iyi silah.
2. **Harita sayfaları:** Kuzgun Vadisi, Ayaz Geçidi, Mavi Liman. Lokasyonlar, taktik notları, ısı haritası (telemetri ucu varsa).
3. **Maç detayı:** tim sıralaması, öldürme akışı, harita üzerinde iniş ve ölüm noktaları.
4. **Haber yönetimi:** yönetici panelinden haber ve yama notu yazma (backend `/news`).
5. **Sistem gereksinimleri ve indirme:** launcher ve kurulum dosyasıyla bağlantılı.
6. **Kalite:** lint, test, build yeşil; IIS `web.config` korunsun.

## GÖREV F5-6 — Hazır Varlıkların Bağlanması (F4-5'in devamı) · `Assets/ThirdParty/*`, `Resources/ContentOverrides.asset`, `Editor/*`
1. F4-5'te indirilen dokuları, sesleri ve modelleri **Varlık Eşleyici** ile `ContentOverrides`'a bağla.
2. Kod değiştirme. Kancalar hazır: `WeaponModelFactory`, `GameAudio`, `MaterialLibrary`.
3. Eklenen her varlık grubu için **önce/sonra ekran görüntüsü**.
4. **Mixamo:**
   - Kullanıcının hesabıyla animasyonları indir.
   - Humanoid içe aktarma ve Animator Controller üretimi: Editor aracı hazır.
   - Asker modeli override'ı için `SoldierModel` kancası henüz yok. Gerekliliği ve tasarımı `FAZ3_KANCALAR.md`'ye yaz; Claude uygulayacak.
5. Lisans tablosu, Git LFS ve `.gitattributes`.

## GÖREV F5-7 — Windows Server'da Uçtan Uca Online Maç (F4-2 + F4-3 sonrası)
1. Staging Windows Server'da Backend (IIS) + MSSQL + ServerManager + **Windows Dedicated Server** build'i.
2. İki istemciyle (Windows build + editör) tam akış:
   - Giriş, tim kurma, eşleştirme, sunucu atama
   - Maç: intikal, çatışma, bitiş
   - Sonuçların backend'e gitmesi, başarım ve XP senkronu
3. **Gecikme simülasyonu** (Clumsy veya Unity Transport simülatörü): 50 / 150 / 250 ms; isabet doğrulama ve lag compensation gözlemleri.
4. **Rapor:** `Deploy/windows/E2E_RAPOR.md`. Hatalar online koddaysa (`Scripts/Online/**`) düzelt; Claude alanındaysa not düş.

## GÖREV F5-8 — Geliştirici Dokümantasyonu · `Docs/*` (yeni dosyalar)
1. **`Docs/GELISTIRICI_REHBERI.md`:**
   - Projeyi sıfırdan açma, katmanlar, yeni silah, harita, mod ve başarım ekleme adımları.
   - Doğrulama araçları (verify.sh, PlayMode, soak).
   - Ajan çalışma düzeni: DURUM.md ve sahiplik kuralları.
2. **`Docs/ADR/`:** mimari karar kayıtları (ADR):
   - Sunucu otoritesi
   - Prosedürel içerik → hazır varlık → gerçekçi grafik stratejisi
   - URP seçimi
   - Windows Server + MSSQL
   - Netcode for GameObjects
3. **`CONTRIBUTING.md`:** kod stili, commit mesajı formatı, PR kontrol listesi.
