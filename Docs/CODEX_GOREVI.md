# ChatGPT/Codex Görevleri — HAREKÂT Web, Tasarım ve Denge

HAREKÂT, Türk askeri temalı, 10 kişilik timlerle oynanan bir FPP tim battle royale.
- Unity oyununu aynı anda 16 Claude ajanı yazıyor.
- Backend ve online altyapıyı Cursor yazıyor (bkz. `Docs/CURSOR_GOREVI.md`).

Aşağıdaki **6 görev birbirinden bağımsız**. Her birini ayrı bir Codex görevinde aynı anda başlatabilirsin.
Başlatma cümlesi: *"Docs/CODEX_GOREVI.md içindeki GÖREV X'i baştan sona uygula."*

## ORTAK KURALLAR (çakışma olmaması için)
- Her görev **yalnızca kendi klasörüne** yazar (aşağıda belirtilmiş).
- Şu klasörlere **asla yazma**: `Assets/`, `Packages/`, `ProjectSettings/`, `Backend/`, `Deploy/`, `Tools/LoadTest/`, `Tools/DiscordBot/`, `.github/`, `Docs/`. Okumak serbest.
- Arayüz dili Türkçe. Gerçek dünyadan bir örgüt ya da grup adı kullanma; kurgu "harekât tatbikatı", Mavi/Kırmızı kuvvetler.
- Oyunla ilgili kaynak veriler (salt okunur):
  - `Assets/_Project/Scripts/Application/Catalogs/*` (silahlar, eşyalar, rütbeler, teçhizat)
  - `Assets/_Project/Scripts/Core/Domain/Enums/MilitaryRank.cs`
  - `Assets/_Project/Scripts/Infrastructure/World/WorldTypes.cs` (harita yerleşimi)
  - `Docs/CONTRACTS.md`

---

## GÖREV 1 — Web Portalı (`Web/`)
React + Vite + TypeScript + Tailwind; askeri ve koyu tema, vurgu rengi #E30A17.

Sayfalar:
- **Ana sayfa:** 10 kişilik timler, TSK rütbeleri ve komuta zinciri, T-70 / Kirpi ile intikal, Türk silahları, "Kuzgun Vadisi", topçu desteği.
- **Sıralamalar** (`/leaderboards`)
- **Profil:** giriş/kayıt, rütbe nişanı, XP çubuğu, kariyer istatistikleri.
- **Tim:** kurma, davet etme, katılma.
- **Silahlar ve harita**
- **Yama notları**
- **Yönetici paneli:** sunucu filosu ve aktif maçlar.

Teknik:
- Tipli API istemcisi; endpoint'ler `Docs/CURSOR_GOREVI.md` GÖREV 1 ile aynı olmalı.
- Backend çalışmıyorsa sahte veriyle çalışan mock modu (`VITE_USE_MOCK=true`).
- `npm run build` ve `npm run lint` hatasız geçmeli. Türkçe README.

## GÖREV 2 — Oyun Tasarım Dokümanı / GDD (`Design/GDD/`)
Türkçe ve kapsamlı bir GDD; Markdown, bölüm bölüm ayrı dosyalar ve bir içindekiler sayfası.
- **Vizyon ve oyun döngüsü**
- **Tim yapısı:** görevler (Tim Komutanı, Keskin Nişancı, Makineli Tüfekçi, Sıhhiyeci, Telsizci, Bombacı, Piyade).
- **TSK rütbeleri ve komuta zinciri:** devir kuralları, rütbe atlama XP eşikleri.
- **İntikal sistemi:** T-70 ve Kirpi.
- **Silahlar:** her Türk silahının rolü, artı ve eksileri. Değerler kataloglardan okunsun.
- **Teçhizat ve envanter**
- **Harekât alanı (zone) fazları**
- **Topçu desteği**
- **Kuzgun Vadisi bölgeleri:** her lokasyonun taktik önemi.
- **Botlar ve yapay zekâ davranışı**
- **Ses ve görsel yönelim**
- **İlerleme sistemi**
- **Gelecek modlar:** gece harekâtı, rehine kurtarma, konvoy koruma (fikir düzeyinde).
- **Online mimari özeti**
- **Para kazanma:** yalnızca kozmetik; kazanmak için ödeme (pay-to-win) yok.

## GÖREV 3 — Denge Hesaplayıcısı (`Tools/BalanceCalc/`)
- TypeScript (Node) ya da Python aracı. `WeaponCatalog.cs` ve `ItemCatalog.cs` dosyalarını ayrıştırarak silah değerlerini okusun.
- Her silah için öldürme süresi (TTK) ve gereken vuruş sayısı:
  - Zırh seviyesi 0–3 ve kask seviyesi 0–3
  - Mesafe 10, 50, 100, 200, 300 m (düşüş dahil)
  - Kafa ve gövde vuruşu
- Çıktılar:
  - CSV
  - Markdown rapor
  - Basit grafikli HTML rapor
- Denge önerileri: aşırı güçlü veya zayıf silahlar. Türkçe README.

## GÖREV 4 — Harita Tasarım Paftası (`Design/Maps/`)
- `WorldTypes.cs` ve harita yerleşim kodunu okuyarak "Kuzgun Vadisi" için askeri pafta tarzında bir SVG harita üret:
  - 100 m'lik grid ve koordinatlar (A–J / 1–10)
  - Lokasyonlar, yollar, dere, yükseklik eğrileri (yaklaşık)
  - İntikal sektörleri
- Her lokasyon için taktik not sayfası (Markdown).
- Gelecek iki harita için konsept dokümanı ve SVG taslak (kurgusal Türk coğrafyası):
  - Karlı dağlık bir harita
  - Kıyı kasabası haritası

## GÖREV 5 — Yerelleştirme Hazırlığı (`Localization/`)
- `Assets/` altındaki C# kodlarında geçen tüm Türkçe arayüz metinlerini tara (salt okunur).
- Anahtar, Türkçe metin, İngilizce çeviri ve kaynak dosya sütunlarıyla `strings.csv` oluştur.
- Gelecekte Unity Localization paketine geçiş için öneri dokümanı.
- Askeri terimler sözlüğü (rütbeler, silahlar, emirler; Türkçe ve İngilizce).

## GÖREV 6 — Mağaza Sayfası ve Basın Kiti (`Marketing/`)
- Steam mağaza sayfası metinleri (Türkçe ve İngilizce): kısa ve uzun açıklama, özellik maddeleri, etiket önerileri.
- Basın bülteni (Türkçe ve İngilizce).
- Fragman senaryosu: 60–90 saniyelik çekim listesi.
- Sosyal medya duyuru serisi (10 gönderi).
- Logo ve ikon için SVG taslakları: HAREKÂT yazısı ve hilal-yıldız esinli amblem.

---

# DURMA KURALI
Görevin ana kısmı bitince **durma**. Aynı görevin aşağıdaki "UZATMA HEDEFLERİ" listesine sırayla geç.
Hepsi bitince kaliteyi derinleştir:
- Testler
- Erişilebilirlik
- Performans
- Daha fazla içerik ve görsel cila
- Dokümantasyon

Her adımın sonunda build, lint ve testleri tekrar çalıştır.

# UZATMA HEDEFLERİ

### GÖREV 1 — Web Portalı
1. **Oyuncu kartı:** paylaşılabilir profil sayfası (rütbe nişanı SVG, en iyi silah, son 20 maç grafiği).
2. **Maç geçmişi:** maç detay sayfası (tim sıralaması, öldürme akışı, harita üzerinde ölüm/iniş noktaları).
3. **Rütbe nişanları:** tüm TSK rütbeleri için özgün SVG rütbe işaretleri (stilize, resmi armalar kopyalanmadan).
4. **Sezon ve başarım sayfaları**
5. **Haberler ve topluluk:** duyurular, yazılı rehberler (silah rehberi, tim taktikleri).
6. **Yönetici paneli:** oyuncu arama, yasaklama, rapor inceleme, sunucu metrikleri grafikleri, hile şüphelileri listesi.
7. **Çok dilli yapı:** i18n (TR/EN).
8. **PWA:** çevrimdışı önbellek, bildirim altyapısı.
9. **Testler:** Vitest + Testing Library (bileşen testleri), Playwright (uçtan uca, mock modunda).
10. **Lighthouse skorları:** 90 üzeri performans, erişilebilirlik ve SEO.

### GÖREV 2 — GDD
1. Her silah için ayrıntılı denge gerekçesi ve gerçek dünyadaki karşılığına dair kısa not (genel bilgi).
2. Tim taktikleri el kitabı: kama düzeni, L şekli pusu, bina temizleme, zırhlı araçla ilerleme (oyun içi mekanik karşılıklarıyla).
3. Görev ve rol yetenekleri genişlemesi: sıhhiyecinin yaralı kaldırması, telsizcinin İHA keşfi gibi fikirler; maliyet ve fayda analiziyle.
4. Ses tasarım dokümanı: her ses olayının tarifi ve karışım öncelikleri.
5. UI/UX akış diyagramları (Mermaid): menü, maç akışı, ölüm ve izleyici modu.
6. Oyuncu tutundurma: ilk 10 dakika deneyimi, eğitim (tutorial) akışı, atış poligonu görevleri.

### GÖREV 3 — Denge Hesaplayıcısı
1. Monte Carlo simülasyonu: sekme ve sapma dahil, mesafeye göre isabet olasılığı ve beklenen TTK.
2. 1'e 1 düello simülatörü: silah A ile silah B, mesafe ve zırh matrisi, kazanma oranı ısı haritası.
3. Etkileşimli web arayüzü: tek HTML dosyası, kaydırıcılarla anlık TTK.
4. "Ne olur?" modu: değer değiştirip etkiyi görme, değişiklik önerisi çıktısı.
5. Otomatik denge raporu ve önerilerin gerekçeleri.

### GÖREV 4 — Harita Paftaları
1. Her lokasyon için ayrıntılı yakın plan SVG paftası: binalar, mevziler, giriş yolları, keskin nişancı noktaları.
2. Taktik rehber: her bölge için saldırı ve savunma planı, intikal sektörlerine göre önerilen rotalar.
3. Yeni harita konseptleri: karlı dağ ve kıyı kasabası için tam yerleşim (lokasyon listesi, yollar, ölçekli SVG) ve `WorldTypes.cs` formatına uygun örnek yerleşim verisi (JSON).
4. Harita okuma rehberi (pafta işaretleri ve grid kullanımı).

### GÖREV 5 — Yerelleştirme
1. Almanca, Azerbaycan Türkçesi ve Arapça çeviri sütunları (askeri terim sözlüğüyle tutarlı).
2. Çoğul ve cinsiyet kuralları ile yer tutucu (placeholder) doğrulama betiği.
3. Metin uzunluğu riski raporu (UI taşma ihtimali olan metinler).

### GÖREV 6 — Pazarlama
1. Steam mağaza sayfası için ekran görüntüsü planı ve açıklamaları; capsule görsel SVG taslakları (tüm boyutlar).
2. 3 aylık içerik takvimi, topluluk yönetimi rehberi, Discord sunucu yapısı.
3. İçerik üreticileri (influencer) için tanıtım kiti.
4. Erken erişim yol haritası metni, SSS (sıkça sorulan sorular).
