# HAREKÂT — Lig Yapısı

**Sezon modeli:** 12 haftalık canlı operasyon döngüsü + 1 hafta final / ödül  
**Birim:** 10 kişilik tim  
**Ödül:** Kozmetik (P2W yok)

---

## 1. Sezon takvimi (yıllık çerçeve)

| Faz | Süre | Açıklama |
|-----|------|----------|
| Sezon öncesi | 1 hafta | Kayıt, seed, yedek listesi |
| Normal sezon | **12 hafta** | Haftalık lig maçları + LiveOps etkinlikleri |
| Playoff / final | 1 hafta | Üst kümeler + açık turnuva finali |
| Sezon arası | 1–2 hafta | Yama, kozmetik vitrin, anket |

Sezon adı örneği: **Sezon 1 — İlk Tatbikat** (`sezon1_yol_haritasi.md`).

## 2. Küme (division) piramidi

```
        ┌─────────────────┐
        │   Şampiyonlar   │  8 tim
        │     Kümesi      │
        └────────┬────────┘
                 │ ↑↓
        ┌────────┴────────┐
        │   Kıdemliler    │  16 tim
        │     Kümesi      │
        └────────┬────────┘
                 │ ↑↓
        ┌────────┴────────┐
        │   Acemiler      │  açık / çoklu grup
        │     Kümesi      │
        └─────────────────┘
```

| Küme | Kapasite (hedef) | Haftalık resmi maç |
|------|------------------|--------------------|
| Şampiyonlar | 8 | 2 (BO2 seri veya 2 tek maç) |
| Kıdemliler | 16 | 2 |
| Acemiler | Açık kayıt, gruplar hâlinde | 1–2 |

Sezon 1’de kapasite düşük başlar; kayıt sayısına göre ölçeklenir.

## 3. Puan ve sıralama

Lig maçları `turnuva_kural_kitabi.md` puanlamasını kullanır.

**Haftalık lig puanı** = o haftanın resmi maç puanları toplamı.  
**Sezon puanı** = 12 haftanın toplamı (en kötü 1 hafta düşürülebilir — “soft discard”, Sezon 1’de opsiyonel).

Beraberlik kırıcıları: turnuva kitabı §4.3 ile aynı.

## 4. Yükselme ve küme düşme

Her sezon sonunda (playoff sonrası):

| Hareket | Kural |
|---------|--------|
| Şampiyonlar → düşme | Son **2** tim Kıdemliler’e iner |
| Kıdemliler → yükselme | İlk **2** tim Şampiyonlar’a çıkar |
| Kıdemliler → düşme | Son **4** tim Acemiler’e iner |
| Acemiler → yükselme | Playoff kazanan **4** tim Kıdemliler’e çıkar |

**Otomatik düşme (davranış):** Ciddi hile DQ’su olan tim sezon sonu küme koruması alamaz; bir alt kümeye veya açık listeye alınır.

## 5. Playoff formatı (özet)

1. **Şampiyonlar:** 8 tim tek grup veya 1–4 / 2–3 yarı final BO3 → final BO5.  
2. **Kıdemliler yükselme:** 3–6. sıra play-in; 1–2 direkt yükselme.  
3. **Acemiler:** açık bracket; finalist 4 yükselir.

Harita ve intikal preset’leri haftalık LiveOps ile uyumlu duyurulur.

## 6. Seed ve kayıt

- Sezon öncesi kayıt formu (tim adı, 10+2 roster, kaptan Discord).  
- Seed: önceki sezon sıralaması → yoksa açık eleme sonucu → kura.  
- Roster kilidi: resmi maç gününden **24 saat** önce (acil yedek: Komutanlık).

## 7. Lig dışı etkinlikler

12 haftalık takvimdeki temalı haftalar (Gece Harekâtı, Keskin Nişancı vb.) **lig puanına etki etmeyebilir**; ayrı “etkinlik puanı” ve kozmetik drop verir. Ayrım `takvim_12_hafta.md` içinde işaretlenir.

## 8. İletişim

| Konu | Kanal |
|------|--------|
| Resmi tablo | Web sıralamalar + Discord `#lig-tablo` |
| İtiraz | Turnuva itiraz süreci |
| Duyuru | `#duyurular` |

## 9. Fair play notu

Lig, rekabeti teşvik eder; toksik “solo carry” kültürü teşvik edilmez. En değerli metrikler: tim hayatta kalma, komuta zinciri disiplini, temiz oyun.
