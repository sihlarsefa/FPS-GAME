# HAREKÂT — Discord Yapısı ve Moderasyon (LiveOps)

Faz 1 taslağı (`Marketing/community/discord_yapisi.md`) üzerine **lig / turnuva / destek** kanalları eklenir.  
**Vurgu:** `#E30A17` · Ton: disiplinli tatbikat, toksik yok.

---

## 1. Kategori ve kanallar (LiveOps genişletmesi)

### 📌 BİLGİ
| Kanal | Amaç |
|-------|------|
| `#duyurular` | Resmi duyuru (yalnızca Komutanlık) |
| `#yama-notlari` | Patch |
| `#kurallar` | Sunucu + kurgu hatırlatması |
| `#sss` | Destek SSS linkleri |
| `#yol-haritasi` | Sezon 1 özeti |
| `#lig-tablo` | Sezon sıralaması / sonuçlar |
| `#canli-ops` | Haftalık tema duyurusu |

### 🏆 REKABET
| Kanal | Amaç |
|-------|------|
| `#turnuva-duyuru` | Lobiler, saatler, preset |
| `#turnuva-sonuc` | Resmi sonuç + cezalar |
| `#turnuva-rapor` | Hile / ihlal raporu |
| `#turnuva-itiraz` | 60 dk itiraz penceresi |
| `#tim-kayit` | Lig kayıt ve roster |

### 🛰️ GENEL
`#sohbet`, `#tanisma`, `#ekran-alintilari`, `#fan-icerik` — mevcut yapı.

### 🪖 TATBİKAT
`#tim-kur`, `#taktik`, `#silahlar`, `#kuzgun-vadisi`, `#bug-rapor` — mevcut yapı.

### 🆘 DESTEK
| Kanal | Amaç |
|-------|------|
| `#destek` | Ticket bot giriş |
| `#kurulum-windows` | Yükleme / launcher ipuçları |
| `#baglanti` | Nat, DNS, paket kaybı tartışması (destek özeti) |

### 🎙️ SES
Lobide Bekleme · Tim 1–5 (limit 10) · Komuta Brifing · **Cast Odası** (yayın ekibi).

### 🛠️ YÖNETİM (gizli)
`#mod-log`, `#raporlar`, `#partner`, `#liveops-ops` (haftalık runbook), `#cheat-review`.

---

## 2. Roller

| Rol | Renk | Yetki |
|-----|------|-------|
| `@Komutanlık` | `#E30A17` | Admin / duyuru |
| `@Subay` | `#c45c5c` | Moderasyon |
| `@LiveOps` | `#b33b3b` | Takvim, lobby, sonuç işleme |
| `@Hakim` | `#6b4c9a` | İtiraz paneli (2 kişi kuralı) |
| `@Telsizci` | `#4a90a4` | Duyuru ping (opt-in) |
| `@Creator` | `#d4a017` | Doğrulanmış üretici |
| `@LigTim` | `#3d8b5f` | Kayıtlı roster |
| `@Playtester` | `#3d8b5f` | Kapalı test |
| `@Mavi Kuvvet` / `@Kırmızı Kuvvet` | mavi/kırmızı | Self-role |

Self-roles: `#roller` reaksiyon menüsü.

---

## 3. Moderasyon politikası

### 3.1 Seviyeler

| Seviye | Örnek | Aksiyon |
|-------:|-------|---------|
| 1 | Flood, off-topic, spoiler etiketsiz küçük spoiler | Uyarı + sil |
| 2 | Hakaret, toxic, tekrar flood | Timeout 1–24s |
| 3 | Nefret, tehdit, doxxing | Ban + log |
| 4 | Hile satışı, hesap ticareti, exploit yayma | Ban + turnuva DQ + (gerekirse) platform bildirimi |

### 3.2 LiveOps özel

- Turnuva kanalında sonuç tartışması serbest; **hakaret / tehdit** seviye 2+.  
- İtiraz kanalında yalnızca formatlı başvuru; flood → sil + yönlendir.  
- Maç sırasında rakip pozisyon spoileri (resmi final) → uyarı / VOD kısıtı.

### 3.3 Kanıt ve gizlilik

- Raporlarda kişisel veri (telefon, adres, gerçek ad) silinir.  
- Cheat inceleme kanıtı yalnızca `#cheat-review` içinde.  
- Kamuya “şu hileci” teşhiri yok; resmi sonuç satırı yeter.

### 3.4 Kriz iletişimi

1. Doğrula (`#liveops-ops`).  
2. `#duyurular` kısa status.  
3. 24s içinde güncelleme veya ETA.  
4. Post-mortem (PII yok).

### 3.5 Moderatör işe alım

- 2 hafta deneme, kural testi, gölge vardiya.  
- Aylık “en iyi taktik post” → kozmetik rol (güç yok).

---

## 4. Botlar (öneri)

- Ticket (`#destek`)  
- Rol menüsü  
- Lig tablo webhook (Web API’den)  
- Turnuva reminder  
- Seviye rolleri (kozmetik; oyun avantajı yok)

## 5. Sunucu kuralları (özet — `#kurallar`)

1. Saygı; nefret / taciz yok.  
2. Gerçek örgüt adı yok; Mavi–Kırmızı.  
3. P2W vaadi / hesap satışı yok.  
4. Resmi arma / bayrak kopyası yok.  
5. Hile ve exploit paylaşımı yasak.  
6. Bug raporunda PII yok.
