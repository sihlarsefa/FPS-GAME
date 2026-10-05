# Brief — Kirpi MRAP (zırhlı personel taşıyıcı)

**Kimlik:** `vehicle_kirpi` · Karadan intikal + sürülebilir ZPT  
**Art Bible:** [`../ArtBible.md`](../ArtBible.md) · GDD: İntikal / Kirpi

---

## 1. Kullanım bağlamı
| Bağlam | Gereksinim |
|--------|------------|
| Dış görünüm (3P / intikal) | Tanınır kutu MRAP silueti, 4–6 teker |
| İç kabin (FPP sürülebilir) | Koltuk, direksiyon, kapı; LOD orta |
| Harita / minimap | LOD3 proxy |

Silah empty’leri yok; araç kökünde yardımcı:
`Seat_Driver`, `Seat_Pass_01..N`, `Door_L`, `Door_R`, `Wheel_FL/FR/RL/RR`, `Exhaust`, `Camera_Ext`.

> F3-4 silah transform seti bu varlıkta **uygulanmaz**. İsim standardı araç brief’ine özeldir.

---

## 2. Poligon ve LOD
| LOD | Tris |
|-----|------|
| LOD0 | 50–70k (dış + görünür iç) |
| LOD1 | 25–35k |
| LOD2 | 12–18k |
| LOD3 | 5–10k |

Tekerlekler ayrı; süspansiyon basitleştirilmiş.

## 3. Doku
2K–4K gövde atlas; iç 2K. PBR: mat askeri boya (zeytin/kara), lastik, cam (ayrı mat).  
Kamuflaj opsiyon: ADD (`TSK_Kamuflaj_Rehberi.md`) veya düz RAL-benzeri zeytin — **resmi plaka / arma yok**.

## 4. Pivot / ölçek
Gerçekçi uzunluk ~6–7 m, yükseklik ~2.5–2.8 m.  
Pivot: zemin kontak düzlemi merkez (X/Z), Y=0 teker altı. +Z ileri.

## 5. Animasyon / rig
Kapı açılır (hinge). Teker spin/steer kemikleri veya ayrı mesh. İç koltuk oturma pozları Mixamo “sitting” ile uyumlu.

## 6. Teslim
FBX + dokular + collider önerisi (box + wheel capsules) README.

## 7. Kabul
- [ ] MRAP V-gövde / yüksek siluet okunur
- [ ] Ölçek metre; pivot zemin
- [ ] Logo / gerçek plaka yok
- [ ] Tris bütçesi
- [ ] En az 4 koltuk empty

## 8. Referans (metin)
Türk Kirpi sınıfı MRAP: yüksek gövde, dikiz zırh camları, arka ramp/kapı. Fotogrametri kopyası değil; stilize hard-surface.

## 9. Süre / fiyat
18–25 gün $2000–3500 · doku $800–1400

---

## UZATMA — İş ilanı

### TR
**3D Araç Artist — Kirpi MRAP.** Unity game-ready, LOD0–3, sürülebilir iç kabin, kapı/teker. Askeri araç portfolio. Resmi arma yok.

### EN
**3D Vehicle Artist — Kirpi-class MRAP.** Unity game-ready, LOD0–3, drivable interior, doors/wheels. Military vehicle portfolio. No official insignia.

### Puan tablosu
| Kriter | Ağırlık |
|--------|---------|
| Siluet / oran | 25 |
| Pivot / ölçek / empty’ler | 20 |
| PBR / UV | 20 |
| LOD / perf | 20 |
| İç okunurluk | 15 |
**Eşik ≥ 4.0**
