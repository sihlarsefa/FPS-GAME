# HAREKÂT — Test Planı (C2-3)

**Ürün:** HAREKÂT — FPP Tim Battle Royale (10 kişilik timler, Mavi/Kırmızı kuvvetler)  
**Sürüm hedefi:** Faz 2 aday build  
**Sahip:** QA (`QA/`)  
**Kaynaklar:** `Docs/CONTRACTS.md`, `Docs/MODUL_SPESIFIKASYONLARI.md`, `QA/cases.json`  
**Senaryo seti:** **368** manuel senaryo (`QA/Senaryolar/`)

---

## 1. Amaç

Bu plan; Windows istemci, Windows Dedicated Server ve macOS geliştirici ortamlarında oynanış, arayüz, performans, online/backend ve erişilebilirlik kabulünü tanımlar. Amaç: her aday build’de regresyonu erken yakalamak, playtest’e güvenli giriş kriteri sağlamak ve hata raporlarını tek şablona bağlamak.

## 2. Kapsam

### 2.1 Dahil

| Alan | Modül kodu | Senaryo | Not |
|------|------------|---------|-----|
| Hareket / kamera | MOV | 25 | Motor, duruş, düşme hasarı |
| Silahlar | WPN | 100 | Katalog silahları × kabul |
| Envanter / yağma | INV | 14 | Yuva, ağırlık, ölüm düşüşü |
| İyileşme / boost | HEAL | 22 | Meds, iptal, ölü kullanım |
| Zırh | ARM | 18 | Yelek/kask Sv.1–3 |
| İntikal | TRN | 13 | T-70 / Kirpi |
| Komuta + topçu | CMD | 15 | F1–F4, V çağrısı |
| Bölge / maç sonu | ZON | 12 | Zone DPS, galibiyet |
| Yapay zekâ | AI | 12 | Görüş, emir, NavMesh |
| HUD / harita | HUD | 20 | Can, pusula, işaretler |
| Menü / ayar / kariyer | MENU | 15 | FOV, XP |
| Atış Poligonu | RNG | 9 | TrainingRange |
| Performans | PERF | 12 | Frametime, bellek |
| Online dayanıklılık | NET | 16 | Kuyruk, RTT, kayıp |
| Backend uçları | API | 37 | Auth, squad, match, mod |
| Erişilebilirlik | ACC | 8 | Klavye, renk, ankette |
| Windows matrisi | WIN | 10 | DPI, GPU, uyku |
| Denge (BalanceCalc) | BAL | 10 | TTK gövde 10 m |

### 2.2 Hariç (bu planda doğrulanmaz)

- Üretim oyuncu verisi / gerçek para işlemleri
- Resmi örgüt adı veya gerçek dünya istihbaratı içeren içerik denetimi (kurgu dışı)
- Linux sunucu (platform kararı: **Windows Server only**)
- Üçüncü parti hile yazılımı forensics (yalnız telemetri şüphe sinyali)

## 3. Ortamlar

| Ortam | OS | Rol | Build |
|-------|-----|-----|-------|
| **Windows istemci** | Windows 10/11 64-bit | Ana oynanış QA | Player build (Release) |
| **Windows Dedicated Server** | Windows Server 2019/2022 | Maç otoritesi, heartbeat | Dedicated Server build |
| **macOS geliştirici** | macOS (Apple Silicon / Intel) | Editör smoke, içerik doğrulama | Unity Editor + lokal Play Mode |
| **Backend lab** | IIS + MSSQL (Windows) | API / eşleştirme | `Harekat.Api` izole |

### 3.1 Ortam kayıt alanları (her koşum)

- Build kimliği / commit SHA / Unity sürümü  
- GPU modeli + sürücü sürümü  
- Çözünürlük, DPI ölçeği, monitör sayısı  
- Güç profili (Yüksek performans)  
- Ağ laboratuvarı şekillendirici ayarı (RTT / kayıp / jitter) — varsa  
- Dil: TR (birincil), EN (ikinci)

## 4. Giriş kriterleri (build adayına)

1. Derleme başarılı; pembe materyal / missing script yok (smoke öncesi).
2. `QA/cases.json` ile `QA/Senaryolar/` senkron (aynı ID sayısı).
3. Dedicated Server ve istemci aynı sözleşme sürümünü taşır (`Docs/CONTRACTS.md` snapshot hash’i `QA/source-snapshot.json` ile uyumlu veya sapma not edilmiş).
4. Test hesapları / seed / fixture listesi hazır; **üretim verisi yok**.
5. P0 senaryolarının ortamı (API lab, Atış Poligonu) erişilebilir veya Engelli olarak işaretlenebilir.

## 5. Çıkış kriterleri

| Seviye | Koşul |
|--------|-------|
| **Smoke geçti** | `QA/Smoke.md` maddelerinin tamamı Geçti veya Engelli (Engelli gerekçeli) |
| **Regression adayı** | P0 açık hata = 0; P1 kritik oynanışta açık engelleyici = 0 |
| **Playtest’e uygun** | Smoke + MOV/WPN/INV/HUD çekirdek P1’ler yeşil veya bilinen kabul edilmiş sapma |
| **Sürüm adayı** | Açık P0 yok; P1 için sahipli mitigation; performans PERF-001/002 kaydı mevcut; API P0 (sonuç/mod) yeşil |

Engelli senaryo “geçti” sayılmaz; çıkışta Engelli oranı ve gerekçesi raporda listelenir.

## 6. Risk listesi

| ID | Risk | Etki | Olasılık | Azaltma |
|----|------|------|----------|---------|
| R1 | Online/reconnect politikası henüz eksik | NET senaryoları Engelli → sahte yeşil | Yüksek | Engelli zorunlu; politika belgelenene kadar çıkışta ayrı bayrak |
| R2 | 60 savaşan frametime hedefi cihaz bağımlı | Yanlış performans hükmü | Orta | Cihaz kaydı + P50/P95/P99; tek FPS sayısıyla karar yok |
| R3 | Katalog / BalanceCalc drift | WPN/BAL yanlış beklenen | Orta | `seed_cases.py` + `source-snapshot.json` hash |
| R4 | macOS Editör ≠ Windows Player | Editörde geçen bug Windows’ta kırılır | Yüksek | Kritik smoke Windows istemcide zorunlu |
| R5 | Zone / maç sonu çift olay | Skor/XP bozulması | Orta | ZON + MENU XP senaryoları her adayda |
| R6 | Topçu / VFX spike | GPU spike, çökme | Orta | PERF topçu + CMD zamanlama |
| R7 | DPI / çoklu monitör | UI tıklanamaz | Orta | WIN matrisi release adayında |
| R8 | API AllowAnonymous sunucu kaydı | Lab güvenliği | Orta | API-020 notu; üretimde ayrı güvenlik kapısı |
| R9 | Zaman ölçeği menüye sızıntı | Donmuş maç | Düşük | MENU offline pause senaryosu |
| R10 | Bot NavMesh kilit | Tim AI kullanılamaz | Orta | AI dar geçit + uzun oturum |

## 7. Test türleri ve sıra

1. **Smoke** (≤15 dk) — her build sonrası (`QA/Smoke.md`)
2. **Modül regresyon** — değişen alanlara göre `QA/Senaryolar/`
3. **Performans** — PERF + WIN GPU kayıtları
4. **Online / API** — lab ortamı (NET + API)
5. **Playtest** — `QA/Playtest/` (haftalık veya milestone)
6. **Denge / yük / erişilebilirlik** — UZATMA seti (BAL, NET kayıp, ACC, WIN)

## 8. Öncelik tanımları

| Kod | Anlam | Örnek |
|-----|-------|-------|
| **P0** | Engelleyici; sürüm durur | Yetkisiz maç sonucu, yasaksız ban denemesi |
| **P1** | Çekirdek oynanış / sözleşme | Hareket hızı, şarjör, zone hasarı |
| **P2** | Önemli UX / kenar | Metin kırpılması, ikincil ses |
| **P3** | Kozmetik / düşük | Nadir animasyon titremesi |

Mevcut set: çoğunluk P1; API güvenlik uçları P0.

## 9. Sorumluluklar

| Rol | Görev |
|-----|-------|
| QA yürütücü | Smoke, senaryo koşumu, Hata_Sablonu doldurma |
| Geliştirici (Claude/Cursor) | Düzeltme; Engelli gerekçesi netleştirme |
| Playtest gözlemcisi | Protokol + gözlem formu |
| Sürüm sahibi | Giriş/çıkış kararı |

## 10. İlgili dosyalar

- `QA/Senaryolar/README.md` — 368 senaryo indeksi  
- `QA/Senaryolar/senaryolar.csv` — Excel aktarımı  
- `QA/Smoke.md` — duman listesi  
- `QA/Playtest/` — protokol, anket, metrikler  
- `QA/Hata_Sablonu.md` — önem × öncelik + issue önerisi  
- `QA/scripts/seed_cases.py` — cases üretimi (oyun testi değil)
