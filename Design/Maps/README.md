# HAREKÂT — Harita Tasarım Paftaları

Bu klasör yalnızca tasarım çıktısıdır (`Design/Maps/`). Oyun koduna yazılmaz.

Kaynak: `WorldTypes.cs`, `MapLayoutKuzgunVadisi.cs`, `MapMath.cs` (A–J / 1–10, hücre 100 m, satır 1 = kuzey).

## İçindekiler

| Yol | Açıklama |
|-----|----------|
| [KuzgunVadisi/kuzgun-vadisi-pafta.svg](KuzgunVadisi/kuzgun-vadisi-pafta.svg) | Ana askeri pafta (grid, lokasyon, yol, dere, eğri, intikal sektörleri) |
| [KuzgunVadisi/Lokasyonlar/](KuzgunVadisi/Lokasyonlar/) | Her lokasyon için taktik not (MD) |
| [KuzgunVadisi/YakinPlanlar/](KuzgunVadisi/YakinPlanlar/) | Yakın plan SVG paftaları |
| [KuzgunVadisi/Veri/kuzgun-vadisi-yerlesim.json](KuzgunVadisi/Veri/kuzgun-vadisi-yerlesim.json) | `MapLayout` uyumlu yerleşim verisi |
| [KuzgunVadisi/taktik-rehber.md](KuzgunVadisi/taktik-rehber.md) | Saldırı / savunma / intikal rotaları |
| [GelecekHaritalar/KarliDag/](GelecekHaritalar/KarliDag/) | Bozkurt Sırtları konsept + SVG + JSON |
| [GelecekHaritalar/KiyiKasabasi/](GelecekHaritalar/KiyiKasabasi/) | Liman Koyu konsept + SVG + JSON |
| [Rehber/pafta-okuma-rehberi.md](Rehber/pafta-okuma-rehberi.md) | Pafta işaretleri ve grid kullanımı |

## Koordinat özeti

- Harita: 1024 × 1024 m (`HalfSize = 512`)
- Eksen: X doğu, Z kuzey, Y yukarı
- Grid: sütun A–J (batı→doğu), satır 1–10 (kuzey→güney)
- Yerleşim tohumdan bağımsız; `Seed` yalnızca arazi ayrıntısı

## İntikal sektörleri (tasarım)

| Kod | Ad | Yöntem |
|-----|-----|--------|
| S1 | Kuzey Boğazı | Kirpi |
| S2 | Kuzey-Doğu Sırt | T-70 |
| S3 | Doğu Sırtı | T-70 |
| S4 | Güney Boğazı | Kirpi |
| S5 | Batı Dağ Yolu | T-70 |
| S6 | FOB Helipad | T-70 |
