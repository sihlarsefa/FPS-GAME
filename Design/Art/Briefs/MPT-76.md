# Brief — MPT-76 (ar_mpt76)

**Kimlik:** `ar_mpt76` · Milli piyade tüfeği · 7.62  
**Kategori:** Assault Rifle · Rol: komutan / piyade birincil  
**Art Bible:** [`../ArtBible.md`](../ArtBible.md)

---

## 1. Kullanım bağlamı
| Bağlam | Gereksinim |
|--------|------------|
| FPP viewmodel | Yüksek detay; şarjör ve sürgü animasyonu |
| Yerde eşya (loot) | Sade LOD; siluet tanınır |
| 3. şahıs bot | Orta detay; el kemiklerine `Grip_R` / `Grip_L` |

---

## 2. Poligon ve LOD
| LOD | Tris (hedef) | Mesafe (yaklaşık) |
|-----|--------------|-------------------|
| LOD0 FPP | 22–28k | elde / ADS |
| LOD0 3P | 12–16k | < 15 m |
| LOD1 | 8–10k | 15–40 m |
| LOD2 | 4–5k | 40–80 m |
| LOD3 | 2–3k | > 80 m / minimap proxy |

World pickup: LOD1 gövdesi + basit collider kapsül.

---

## 3. Doku
- Atlas FPP: **4096** BaseColor + Mask (M/R/AO) + Normal.
- 3P / world: **2048** (veya FPP atlas downscale).
- Malzemeler: polimer kundak (mat), namlu metal, şarjör polimer, küçük vida metal.
- UV: tek 0–1; texel ~22 px/cm FPP. Overlap yalnızca simetrik vida başlarında.

---

## 4. Pivot, ölçek, eksen
- Gerçekçi uzunluk ~90–100 cm; Unity **metre**.
- Pivot: tutamak (pistol grip) merkezine yakın; dipçik −Z, namlu +Z.
- FBX: Forward +Z, Up +Y.

---

## 5. Transform isimleri (zorunlu)
| Transform | Konum |
|-----------|--------|
| `Muzzle` | Namlu ağzı merkezi, ileri +Z |
| `Grip_R` | Pistol grip sağ el avuç |
| `Grip_L` | Ön kabza / handguard sol el |
| `Magazine` | Şarjör grubu kökü (üst dudak pivot) |
| `Bolt` | Taşıyıcı / sürgü (geri çekilebilir) |
| `Sight` | Demir nişangâh veya optik ray göz hizası |

---

## 6. Animasyon gereksinimleri
- Şarjör: `Magazine` aşağı + hafif eğim çıkış; takma tersi (~2.5 s oyun süresi).
- Sürgü: kısa geri çekme (reload charge / dry).
- Ayrı mesh parçası şarjör; gövdeye skinned değil (rigid child).

---

## 7. Teslim
FBX + 4K/2K PNG + `README` (transform screenshot). Kaynak .blend/.ma opsiyonel.

---

## 8. Kabul kriterleri
- [ ] Transform adları birebir
- [ ] LOD siluet MPT ailesi (kısa namlu değil; 7.62 gövde kalınlığı)
- [ ] PBR metal/rough doğru; “oyuncak plastik” yok
- [ ] Tris bütçesi LOD başına ±15%
- [ ] Logo / üretici yazısı yok (stilize “MPT-76” küçük kazıma serbest)

## 9. Referans (metin)
Modern Türk milli piyade tüfeği silueti: polimer dipçik, uzun namlu, 20’lik şarjör, rail üst kapak. HK416 / AR oranına kaçmadan daha “kalın 7.62” gövde. Telifli fotoğraf kopyalanmaz.

## 10. Süre / fiyat
| Kalem | Süre | Fiyat (USD, tahmini) |
|-------|------|----------------------|
| High-poly + retopo + FPP | 10–14 gün | 800–1400 |
| LOD + world + 3P | +3–5 gün | +250–450 |
| Doku seti | 5–7 gün | 400–700 |

---

## UZATMA — İş ilanı

### TR
**Başlık:** 3D Silah Artist — MPT-76 (FPP / Unity)  
HAREKÂT için oyun içi MPT-76 modeli. PBR, LOD0–3, zorunlu empty’ler: Muzzle, Grip_R, Grip_L, Magazine, Bolt, Sight. Teslim FBX. Portfolio’da askeri silah veya hard-surface örnekleri zorunlu.

### EN
**Title:** 3D Weapon Artist — MPT-76 (FPP / Unity)  
Game-ready MPT-76 for HAREKÂT. PBR, LOD0–3, required empties: Muzzle, Grip_R, Grip_L, Magazine, Bolt, Sight. FBX delivery. Military / hard-surface portfolio required.

### Değerlendirme puanı
| Kriter | Ağırlık |
|--------|---------|
| Oran / siluet | 25 |
| Transform / pivot | 20 |
| PBR / UV | 20 |
| LOD | 15 |
| Anim hazırlığı | 10 |
| Performans | 10 |
**Eşik:** ≥ 4.0 / 5
