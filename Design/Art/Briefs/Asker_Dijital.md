# Brief — Asker (TSK dijital kamuflaj) — 3P / FPS eller

**Kimlik:** `char_soldier_add` · Standart piyade  
**Kamuflaj:** [`../TSK_Kamuflaj_Rehberi.md`](../TSK_Kamuflaj_Rehberi.md) · **Art Bible:** [`../ArtBible.md`](../ArtBible.md)

---

## 1. Kullanım bağlamı
| Bağlam | Gereksinim |
|--------|------------|
| 3P bot / diğer oyuncular | Humanoid rig, Mixamo uyumlu |
| FPP | Ayrı **el + önkol** seti (gövde yok); silah `Grip_R`/`Grip_L` ile hizalı |
| Ragdoll / ölüm | LOD1 yeterli |

Kuvvet: sol kol bandı Mavi/Kırmızı (rehber). Tim amblemi runtime.

---

## 2. Poligon ve LOD
| LOD | Tris (vücut + teçhizat) |
|-----|-------------------------|
| LOD0 | 28–40k |
| LOD1 | 14–20k |
| LOD2 | 7–10k |
| LOD3 | 3–6k |

FPP eller: 8–14k (iki el + kollar).

## 3. Doku
4K vücut atlas (ADD kamuflaj) + 2K ekipman (yelek, kask, bot).  
PBR tekstil kuralları kamuflaj rehberinde. Yüz: stilize gerçekçilik; fotogrametri ünlü yüz yok.

## 4. Pivot / rig
Humanoid: ayaklar Y=0. Mixamo skeleton map.  
Silah tutuş: el socket’leri `Hand_R` / `Hand_L` → silah `Grip_*` ile eşlenir.

## 5. Transform / socket
Karakter: `WeaponSocket_R`, `Holster`, `Helmet`, `Armband_L`.  
FPP el setinde silah child empty’leri silah brief’indeki gibi kalır.

## 6. Animasyon gereksinimleri (model tarafı)
Skin ağırlıkları: omuz, dirsek, kalça temiz.  
Oturarak araç (Kirpi/T-70) — bacak bükülmesi bozulmasın.  
Hit / death morph yok; animasyon Mixamo’dan.

## 7. Teslim
FBX Humanoid + 4K/2K + ayrı FPP arms FBX. Kaynak tercih Blender.

## 8. Kabul
- [ ] ADD paleti doğru; resmi arma yok
- [ ] Kol bandı mesh veya mask
- [ ] Mixamo retarget sorunsuz
- [ ] Tris bütçesi
- [ ] Bere varyantı ayrı brief ile uyumlu (aynı body)

## 9. Referans (metin)
Modern Türk piyade teçhizatı hissi: kask kılıfı, plate carrier, dizlik. Hollywood “tacticool” abartısı yok. Anadolu Dağ Dijital kumaş.

## 10. Süre / fiyat
Vücut 15–22 gün $1800–3000 · FPP eller 5–8 gün $500–900 · doku $700–1200

---

## UZATMA — İş ilanı

### TR
**Karakter Artist — Tatbikat askeri (dijital kamuflaj).** Humanoid Unity, LOD, FPP eller. Özgün kamuflaj rehberine uyum. Resmi TSK arması yasak.

### EN
**Character Artist — Exercise soldier (digital camo).** Unity Humanoid, LODs, FPP arms. Follow proprietary camo guide. No official insignia.

### Puan
Siluet/teçhizat 25 · Rig/skin 20 · Kamuflaj/PBR 25 · LOD 15 · FPP eller 15 · ≥ 4.0
