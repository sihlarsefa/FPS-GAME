# HAREKÂT — Art Bible

> Görsel kimlik, kalite hedefi ve performans bütçeleri.  
> Freelancer şartnameleri: [`Briefs/`](Briefs/). Kamuflaj: [`TSK_Kamuflaj_Rehberi.md`](TSK_Kamuflaj_Rehberi.md).

**Proje:** HAREKÂT — Türk askeri FPP Tim Battle Royale  
**Kurgu:** Harekât tatbikatı; Mavi / Kırmızı kuvvetler. Gerçek örgüt adı yok.  
**Resmi armalar / logolar kopyalanmaz** — özgün, stilize tasarım.

---

## 1. Görsel kimlik

### 1.1 Ton
Askeri gerçekçilik + okunabilir siluet. PUBG / modern CoD seviyesinde malzeme ve oran; Hollywood abartısı yok. Anadolu dağ, vadi, köy ve liman coğrafyasına uyumlu toprak-zeytin paleti.

### 1.2 Coğrafya ve atmosfer
| Bölge tipi | Işık | Atmosfer |
|------------|------|----------|
| Dağ / geçit (Ayaz) | Soğuk gün ışığı, uzun gölge | İnce sis, rüzgâr tozu |
| Vadi / köy (Kuzgun) | Öğlen sıcak, kontrastlı gölge | Toz, kuru ot |
| Liman (Mavi Liman) | Nemli gökyüzü, yansımalı zemin | Tuz, metal oksit |

Gölgelendirme: yumuşak yönlü güneş + hafif gökyüzü fill. Aşırı bloom / neon yok. Zone VFX mavi duvar + beyaz gelecek çember UI ile uyumlu kalır; dünya asset’leri zone rengini taşımaz.

### 1.3 Renk paleti (dünya)
| Rol | Hex (yaklaşık) | Kullanım |
|-----|----------------|----------|
| Zeytin mat | `#4A5A3C` | Kamuflaj taban, bitki |
| Toprak | `#6B5344` | Yol, duvar sıva |
| Beton | `#8A8A82` | Karakol, köprü |
| Metal oksit | `#5C4A3A` | Araç gövde, varil |
| Kış gri | `#9AA3A8` | Ayaz kaya / kar gölgesi |
| UI vurgu (yalnız HUD) | `#E30A17` | Asset yüzeyinde kullanılmaz |

Kamuflaj renkleri için ayrıntı: `TSK_Kamuflaj_Rehberi.md`.

### 1.4 Siluet kuralları
- Dost / düşman: kol bandı rengi (mavi / kırmızı) + siluet okunurluğu; kamuflaj tek başına kimlik taşımaz.
- Silahlar: FPP’de namlu / şarjör / tutamak ayrımı net; LOD3’te bile kategori silueti korunur.
- Araçlar: Kirpi kutu silueti, T-70 genel maksat helikopter silueti — marka logosu yok.

---

## 2. Kalite hedefi

| Aşama | Hedef |
|-------|--------|
| Şimdi (kod) | Low-poly prosedürel placeholder |
| Ara | Hazır paket (Asset Store / Mixamo) + bu brieflere göre yeniden ölçekleme |
| Son | Özel modeller: PUBG / CoD oran ve PBR kalitesi |

**Hedef yüzey kalitesi (son aşama):**
- Silah FPP: 4K ana atlas, net vida / aşınma, doğru namlu uzunluğu.
- Karakter: dijital kamuflaj okunur; yüzde fotoreal abartı yok (askeri teçhizat odaklı).
- Yapı: Anadolu köy ölçeği; “jenerik Orta Doğu” klişesi yok.

---

## 3. Teknik ortak kurallar

### 3.1 Birimler ve eksenler (Unity)
- **1 birim = 1 metre.**
- Model ileri yönü **+Z**, yukarı **+Y**, sağ **+X** (Unity FBX: Apply Transform / Bake Axis Conversion).
- Pivot: silahlarda tutamak merkezine yakın (elde hizalama); araçlarda zemin temas düzlemi; yapılarda zemin köşe / merkez (brief’te belirtilir).

### 3.2 Transform isimleri (zorunlu — F3-4 eşleyici)
Silah ve silahlı viewmodel kökünde boş Transform’lar:

| Ad | Amaç |
|----|------|
| `Muzzle` | Namlu ağzı; flaş / iz mermisi |
| `Grip_R` | Sağ el tutuş |
| `Grip_L` | Sol el tutuş (ön kabza / el kundağı) |
| `Magazine` | Şarjör / fişek kutusu grubu kökü |
| `Bolt` | Sürgü / mekanizma (bolt-action ve ilgili) |
| `Sight` | Nişangâh / dürbün göz hizası |

İsimler **birebir** (büyük/küçük harf duyarlı). Ek yardımcı: `Slide`, `Pump`, `Cover` — brief’te belirtilirse.

### 3.3 PBR doku seti
Standart haritalar (Unity URP / HDRP uyumlu isimlendirme):
- `*_BaseColor` (sRGB)
- `*_Mask` veya ayrı: Metallic, Occlusion, Roughness (veya Smoothness — brief’te not)
- `*_Normal` (OpenGL / Unity standart; DirectX ise brief’te belirt)
- Opsiyon: Height, Emission (namlu ısısı yok — sabit emission kullanılmaz)

Çözünürlük: FPP silah 4096; dünya / 3P / yapı 2048 (LOD’a göre düşer).

### 3.4 UV
- Tek UV0 unwrap; UV1 lightmap (yapı / araç).
- Silah: 0–1 kare, overlap yok (simetri hariç açıkça izin verilen yerler).
- Texel yoğunluğu hedef: silah ~20–25 px/cm FPP; yapı ~5–8 px/cm.

---

## 4. Performans bütçeleri (özet)

| Varlık sınıfı | LOD0 tris (hedef) | LOD3 tris | Ekranda eşzamanlı üst sınır (tasarım) |
|---------------|-------------------|-----------|----------------------------------------|
| Tabanca FPP | 8–12k | 1–2k | 1 viewmodel |
| Tüfek / DMR / SR FPP | 15–28k | 2–4k | 1 viewmodel |
| LMG FPP | 22–35k | 3–5k | 1 |
| Silah world pickup | 4–8k | 0.5–1k | onlarca |
| Asker 3P | 25–40k | 3–6k | ~100 bot |
| Kirpi | 40–70k | 5–10k | az sayıda |
| T-70 | 50–90k | 6–12k | 1–2 |
| Köy evi | 8–20k | 1–3k | onlarca |
| Cami | 25–60k | 3–8k | birkaç |
| Karakol | 30–80k | 4–10k | birkaç |

Draw call: malzeme sayısı silah başına ≤ 3 (gövde / cam / emisyon yok); karakter ≤ 4.

---

## 5. Teslim ve lisans
- Format: FBX (binary) + PNG/TGA dokular + kısa README (ölçek, pivot, transform listesi).
- Kaynak: tercihen Blender / Maya / 3ds Max — kaynak dosya istenirse brief’te yazılır.
- Ticari / Steam dağıtımına uygun; üçüncü parti varlık gömülmez.
- Resmi marka logosu, seri numarası plakası veya gerçek birim arması yok.

---

## 6. Değerlendirme (ortak puan tablosu)

| Kriter | Ağırlık | 0–5 |
|--------|---------|-----|
| Oran / siluet doğruluğu | 25% | |
| Transform / pivot / ölçek | 20% | |
| PBR / UV kalitesi | 20% | |
| LOD tutarlılığı | 15% | |
| Animasyon hazırlığı (şarjör/bolt) | 10% | |
| Performans bütçesi | 10% | |

**Kabul eşiği:** ağırlıklı ortalama ≥ 4.0 ve Transform isimleri %100 doğru.
