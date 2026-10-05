# Kuzgun Vadisi — Gece Harekâtı Varyantı

Kurgu tatbikatı: aynı yerleşim (`MapLayout.CreateKuzgunVadisi`), farklı aydınlatma / algı / ses profili. Yeni lokasyon yok; HalfSize 512, MaxHeight 160, WaterLevel 18 korunur.

## Amaç

- Gündüz açık vadi avantajını kısaltmak; KN ve gözetleme noktalarının menzilini kısıtlamak.
- Tim içi iletişim ve ses disiplinini ödüllendirmek.
- Gece görüş / termal ekipmanı yağma ve başlangıç dengesiyle sınırlamak (pay-to-win yok).

## Aydınlatma

| Kaynak | Davranış |
|--------|----------|
| Ay / gökyüzü | Düşük ambient; bulutlarla 0.2–0.5 lux bandı (tasarım hedefi) |
| Yapı içi | Köy, karakol, FOB, baraj kontrol: sıcak nokta ışık (pencere sızıntısı) |
| Yol | Ana asfaltta seyrek direk; patikalarda yok |
| FOB helipad | Güçlü ama yerel; uzaktan silüet verir, alanı boyamaz |
| Ateş / işaret | Oyuncu kaynaklı; duman + ışık dengesi (konum ifşa) |

HUD / minimap: gece modunda renk desaturasyonu; dost mavi / düşman kırmızı kontrastı korunur (stil rehberi tokenları).

## Gece görüş (NVG) ve termal

- **NVG:** yakın–orta menzil; açık ay ışığında thrash (bloom) riski.
- **Termal:** yapı içi / yeni ateş izi; cam ve ince örtü zayıf.
- Yağma: High / Military noktalarında sınırlı NVG kiti; Low lokasyonlarda yok.
- Başlangıç: tim görevinde en fazla 2 NVG (tasarım önerisi); termal yalnızca Military çekirdek veya topçu gözlemci rolü.

## Ses

| Bölge | Gece farkı |
|-------|------------|
| Dere / baraj | Su sesi gündüze göre daha baskın; ayak sesi maskelemesi artar |
| Köy sokakları | Sessiz ortam; kapı/ahşap net duyulur |
| Orman sırt | Rüzgâr yaprak; koşu ayak izi uzak taşır |
| FOB / karakol | Jeneratör uğultusu sabit maske — yaklaşınca ani kesilme uyarı |
| Açık tarla | Ayak + ekipman metal; konuşma fısıltısı şart |

Atmosfer kanalı ayrı ses ayarı; kritik olaylar (intikal, alan daralması, emir) metin/ikonla da verilir.

## Oynanış etkileri (özet)

1. S1/S4 Kirpi inişleri karanlık boğazda daha riskli → fener disiplini.
2. Röle / gözetleme: silüet yerine ses ve ışık sızıntısı ile tespit.
3. İleri Üs (H6): dış halka karanlık; iç avlu aydınlık tuzak.
4. Bölge (zone) kenarı: gece sis + karanlık birleşince yön kaybı → pusula HUD önceliği.

## Uygulama notları (Cursor / Unity)

- Yerleşim JSON’u değiştirilmez; sahne aydınlatma preset + post-process + ses bus.
- `design.json` bu klasörde yalnızca varyant metadata tutar.
- Playtest: aynı seed gündüz/gece A/B; öldürme mesafesi ve iniş hayatta kalma oranı ölçülür.

## İlgili

- Ana pafta: `Design/Maps/KuzgunVadisi/`
- Stil: `Design/UI/StyleGuide.md` (Faz 2 C2-7)
- Canlı ops: “Gece Harekâtı haftası” (`Marketing/LiveOps/`)
