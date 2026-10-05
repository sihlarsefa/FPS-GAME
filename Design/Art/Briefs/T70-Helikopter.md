# Brief — T-70 Helikopter (genel maksat)

**Kimlik:** `vehicle_t70` · Havadan intikal  
**Art Bible:** [`../ArtBible.md`](../ArtBible.md) · GDD: İntikal / T-70

---

## 1. Kullanım bağlamı
İntikal uçuşu (dış kamera), iniş tozu VFX, uzaktan siluet. Kabin içi düşük öncelik (oyuncu skydiving metaforu; tam FPP kokpit şart değil — basit koltuk satırı yeterli).

Empty’ler: `Rotor_Main`, `Rotor_Tail`, `Door_L`, `Door_R`, `Exit_Point`, `Camera_Ext`, `Dust_Origin`.

Silah transform seti **yok**.

---

## 2. Poligon ve LOD
| LOD | Tris |
|-----|------|
| LOD0 | 60–90k |
| LOD1 | 30–45k |
| LOD2 | 15–22k |
| LOD3 | 6–12k |

Rotor disk LOD2+’da şeffaf kart / blur texture olabilir.

## 3. Doku
2K–4K gövde; cam ayrı. Mat askeri gri-zeytin. Ulusal bayrak / resmi uçak arması **yok**. İnce tatbikat şeridi (mavı/kırmızı) opsiyonel.

## 4. Pivot / ölçek
Genel maksat orta helikopter: gövde uzunluk ~15–18 m sınıfı (stilize).  
Pivot: yaklaşık ağırlık merkezi veya iniş takımı zemin; README’de netleştir. +Z ileri.

## 5. Animasyon
Ana/kuyruk rotor spin (bone veya UV rotate). Kapı kayar/açılır. İnme animasyonu kodda; model pose “kapılar açık / kapalı” iki morph veya ayrı mesh.

## 6. Teslim / kabul
FBX + PBR. Rotor empty doğru eksende. Tris. Logo yok. Black Hawk birebir kopya değil.

## 7. Referans (metin)
Genel maksat çift motor hissi, yan kapılar, yüksek iniş takımı. T-70 oyun adı; gerçek platform klonu değil — özgün siluet.

## 8. Süre / fiyat
20–30 gün $2500–4500 · doku $900–1600

---

## UZATMA — İş ilanı

### TR
**3D Helikopter Artist — T-70 (oyun).** LOD, rotor empty, kapılar. Askeri havacılık hard-surface. Marka/arma yok.

### EN
**3D Helicopter Artist — T-70 (game).** LODs, rotor empties, doors. Military hard-surface. No trademarks/insignia.

### Puan
Siluet 25 · Empty/pivot 20 · PBR 20 · LOD/rotor 20 · Perf 15 · ≥ 4.0
