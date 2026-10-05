# Pafta Okuma Rehberi

HAREKÂT askeri paftalarını oyun içi mini harita ve tasarım SVG’leriyle aynı dilde okumak için kısa rehber.

## 1. Grid (A–J / 1–10)

- Harita 10 × 10 hücreye bölünür; her hücre **100 m**.
- **Sütun** A batı kenarı, J doğu kenarı (`MapMath.GridColumn`).
- **Satır** 1 kuzey kenarı, 10 güney kenarı (`MapMath.GridRow`: `t = (MaxZ − z) / Size`).
- Etiket biçimi: sütun + satır → örnek `D5`, `H6`.
- Hücre merkezi yaklaşık: dünya X/Z = hücre ortası (±50 m belirsizlik tasarımda kabul edilir).

### Hızlı dönüşüm (HalfSize = 512)

| Dünya | Grid |
|-------|------|
| X = −512 … −412 | A |
| X = −112 … −12 | D |
| X = 412 … 512 | J |
| Z = 512 … 412 | 1 |
| Z = 112 … 12 | 5 |
| Z = −412 … −512 | 10 |

Örnek: Kuzgun Köyü (−118, 22) → **D5**.

## 2. Renk ve çizgi işaretleri

| İşaret | Anlam |
|--------|--------|
| Kalın siyah sürekli çizgi | Asfalt ana yol |
| Kahverengi kesikli çizgi | Toprak yol / patika |
| Mavi kalın çizgi | Dere (Kuzgun Deresi) |
| Mavi dolgu elips | Göl / baraj göleti |
| Kahverengi kesikli eğri | Yükseklik eğrisi (yaklaşık sırt) |
| Renkli halka + nokta | Lokasyon (yarıçap ≈ AO) |
| Turuncu kesikli daire | İntikal sektörü (S1–S6) |
| Kırmızı üçgen (yakın plan) | Giriş / yaklaşma ağzı |
| Kırmızı halka “KN” | Keskin nişancı / baskın noktası |

## 3. Kuzey ve ölçek

- SVG’de **üst = kuzey (+Z)**.
- Ana pafta görünümü ~1:10 000 ekran ölçeği; oyun içi mesafe metre cinsindendir.
- Köprüler paftada yol–dere kesişiminde okunur; kodda `ComputeBridges()` üretir.

## 4. Lokasyon halkası neyi gösterir?

`LocationSpec.Radius` yaklaşık etki / yapı alanı. `FlattenRadius` arazi düzleştirme; `ClearRadius` ağaç/kaya muafiyeti (≤0 ise Radius). Ganimet için `Tier`: Low / Medium / High / Military.

## 5. İntikal sektörü nasıl kullanılır?

1. Tim T-70 veya Kirpi seçer.
2. Paftada S1–S6’dan birini hedef LZ olarak işaretler.
3. Kirpi: ana yol boğazları (S1, S4) daha güvenli.
4. T-70: sırt ve FOB sektörleri (S2, S3, S5, S6) hızlı baskın.
5. İniş sonrası ilk hareket: en yakın örtüye dağıl, ana yolu dik kesme.

## 6. Yakın plan paftası

Her lokasyon SVG’sinde:

- Merkez halka = AO
- Dikdörtgenler = bina / mevzi taslağı
- Üçgenler = giriş yolları
- KN halkaları = uzun mesafe ateş hatları
- Alt notlar = mevzi önceliği

## 7. Telsiz dili (öneri)

- “D5 Kuzgun Köyü batı — iki kat çatı KN”
- “H6 FOB, S6 iniş, doğu Hesco”
- “B3 Röle, yol kıvrımı pusu”

Gerçek örgüt adı kullanma; Mavi / Kırmızı kuvvet ve tatbikat dili yeterlidir.
