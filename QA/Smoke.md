# HAREKÂT — Duman Testi (Smoke)

**Süre hedefi:** ≤ **15 dakika**  
**Ne zaman:** Her Player / Dedicated Server aday build sonrası  
**Ortam:** Windows istemci birincil; macOS Editör yalnızca “Editör smoke” satırları  
**Sonuç:** Geçti / Kaldı / Engelli (gerekçe zorunlu)

Build: _____________ · Commit: _____________ · Tarih: _____________ · Testçi: _____________

---

## A. Başlatma (≈2 dk)

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S01 | Uygulamayı soğuk başlat | Crash yok; ana menü 60 sn içinde | |
| S02 | Türkçe metinler (İ/ı/Ş/ğ) | Bozuk glif yok | |
| S03 | Ayarlar aç → FOV/ses görünür → kapat | Pencere kapanır; imleç düzeni bozulmaz | |
| S04 | FPS gösterimini aç | Sayaç görünür | |

## B. Atış Poligonu (≈4 dk)

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S05 | Ana menü → Atış Poligonu | TrainingRange yüklenir; BR maçı başlamaz | |
| S06 | Bir tüfek al → ateş et → R doldur | Mermi azalır / şarjör dolar; boş tetik hasar vermez | |
| S07 | RMB nişan → bırak | ADS/FOV döner; örtü takılı kalmaz | |
| S08 | 50 m hedefe isabet | Hasar / isabet göstergesi yerel | |
| S09 | Esc → Ana menü | Poligon mühimmatı menüye sızmaz | |

## C. Offline harekât (≈5 dk)

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S10 | 2–4 tim, Normal zorluk, Helikopter veya Kirpi seç → başlat | İntikal başlar; koltukta WASD ile yürüme yok | |
| S11 | Varış / inme (F veya otomatik) | Yerde hareket açılır; harita altına düşmez | |
| S12 | W / Shift / C / Space | Yürüme, sprint, çömelme, zıplama çalışır | |
| S13 | Tab envanter → F yağma (varsa) | Pencere açılır; imleç kilidi tutarlı | |
| S14 | F1 takip emri (komutanken) | Tim emri kabul / bildirim | |
| S15 | M harita aç/kapa | İşaret veya imleç; kapanınca bakış geri | |
| S16 | Zone dışı kısa bekle (faz başladıysa) | Uyarı / hasar tutarlı; anında ölüm yok | |
| S17 | Esc duraklat → Devam | Offline’da timeScale düzelir | |
| S18 | Ana menüye çık → yeni maç başlat | Donmuş timeScale=0 ile başlamaz | |

## D. HUD / ses (≈2 dk)

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S19 | Can / şarjör / pusula görünür | Değerler boş veya NaN değil | |
| S20 | Ateş sesi + ayak sesi | Sessizlik veya çığlık yok; ana ses 0 iken susturulabilir | |
| S21 | Dost işaretleri (tim varsa) | Rakipte dost işareti yok | |

## E. Sunucu / API (lab varsa, ≈2 dk) — yoksa Engelli

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S22 | GET `/health` | 200, status=ok | |
| S23 | Test hesabı login | Token; parola yanıtta yok | |
| S24 | Dedicated Server heartbeat (lab) | Canlılık güncellenir | |

## F. macOS Editör smoke (opsiyonel, ≤3 dk)

| # | Adım | Beklenen | Sonuç |
|---|------|----------|-------|
| S25 | Play Mode TrainingRange | Console’da NullReference yağmuru yok | |
| S26 | Stop → Play tekrar | Çift abonelik / çift HUD yok | |

---

## Karar

- [ ] **Smoke geçti** — tüm zorunlu satırlar Geçti veya gerekçeli Engelli  
- [ ] **Smoke kaldı** — herhangi bir zorunlu Kaldı → aday reddedilir; `QA/Hata_Sablonu.md` ile ticket  

**Notlar:**

_________________________________________________________________

## Hızlı eşleme (detay senaryolar)

| Smoke | İlgili senaryo ID |
|-------|-------------------|
| S05–S09 | RNG-001… |
| S10–S11 | TRN-* |
| S12 | MOV-* |
| S13 | INV-* |
| S14 | CMD-* |
| S15–S16 | HUD-*, ZON-* |
| S17–S18 | MENU-* |
| S22–S24 | API-*, NET-* |
