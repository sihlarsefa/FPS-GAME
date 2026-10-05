# TSK Dijital Kamuflaj Rehberi (Özgün / Stilize)

> HAREKÂT için **özgün** dijital kamuflaj tarifi.  
> Resmi TSK kamuflaj deseni, arma veya birim işareti **kopyalanmaz**. Bu belge oyun içi “Anadolu Dağ Dijital” (ADD) desenini tanımlar.

İlgili: [`ArtBible.md`](ArtBible.md) · karakter brief: [`Briefs/Asker_Dijital.md`](Briefs/Asker_Dijital.md)

---

## 1. Amaç
- Uzaktan dağ / vadi / kuru ot zemininde erime.
- Yakından okunabilir “dijital blok” silueti (oyuncu tanıma).
- Mavi / kırmızı kuvvet ayrımını **kol bandı** ile bırakmak; kamuflaj tarafsız.

---

## 2. Renk paleti — Anadolu Dağ Dijital (ADD)

| Kanal | Ad | Hex | RGB | Kullanım oranı (yaklaşık) |
|-------|-----|-----|-----|---------------------------|
| C0 | Kaya gölgesi | `#2F352C` | 47, 53, 44 | %12 |
| C1 | Zeytin koyu | `#3E4A34` | 62, 74, 52 | %28 |
| C2 | Yaprak orta | `#5A6B45` | 90, 107, 69 | %30 |
| C3 | Toprak açık | `#7A6A4E` | 122, 106, 78 | %18 |
| C4 | Toz bej | `#A09070` | 160, 144, 112 | %12 |

**Yasak:** saf siyah (`#000`), neon yeşil, çöl turuncusu tek başına, resmi arma renkleri olarak kırmızı-beyaz hilal-yıldız kombinasyonu kumaş baskısında.

### 2.1 Ortam varyantları (kozmetik / sezon)
Aynı piksel topolojisi; sadece palet kaydırılır:

| Varyant | Not |
|---------|-----|
| ADD-Orman | C2–C3 doygunluğu +10% |
| ADD-Çöl | C3–C4 baskın; C1 soluk |
| ADD-Kış | C0–C1 gri-maviye kayar (`#4A5250` … `#C8D0D4`) |
| ADD-Şehir | C0 beton gri, C4 açık sıva; yeşil oranı düşer |

---

## 3. Desen topolojisi

### 3.1 Izgara
- Temel hücre: **8×8 px** (4K kumaş atlasında 1 hücre ≈ 0.5–0.8 cm yüzey).
- Makro grup: 4–7 hücrelik “adalar”; dikey/yatay kırık kenarlar (organik kamuflaj değil, dijital).
- Rastgele tohum: `HAREKAT_ADD_v1` — üretimde sabit seed; her parçaya farklı offset (tekrar bandı görünmesin).

### 3.2 Kurallar
1. Tek renk adası en fazla 12 hücre çapında.
2. C0 yalnızca gölge kırığı / dikiş kenarı vurgusu.
3. Omuz / sırt / bacak panellerinde desen **sürekli**; UV seam’de renk sıçraması ≤ 1 hücre.
4. Kask kılıfı aynı palet; mesh üzerinde %15 daha koyu AO.

### 3.3 Yasak motifler
- Okunabilir harf, rakam, birim kodu.
- Hilal-yıldız, ay-yıldız silueti kumaşta.
- Gerçek üretici etiketleri (velcro üstünde sahte etiket: “H-ADD / TATBIKAT”).

---

## 4. Malzeme (PBR)
| Harita | Not |
|--------|-----|
| BaseColor | ADD paleti; aşınma: dirsek/diz C4 + kir overlay |
| Roughness | Kumaş 0.72–0.88; ıslak varyant −0.15 |
| Metallic | 0 (tekstil); toka / fermuar ayrı metal mesh |
| Normal | İnce twill + dikiş; güçlü “plastik digicam” bump yok |
| AO | Panel kıvrımları; kamuflajın üstüne baskı gibi değil |

Tekstil shader: subtace scattering yok veya çok düşük; askeri kumaş mat.

---

## 5. Komutan bere varyantı
- Bere rengi: **matar bordo** `#4A1C28` (stilize; resmi bere tonu kopyası değil).
- Desen yok; düz kumaş + ince AO.
- Rozet yeri: boş daire pad (oyuncu amblemi runtime); resmi nişan yok.
- Ayrıntı: [`Briefs/Asker_Komutan_Bere.md`](Briefs/Asker_Komutan_Bere.md)

---

## 6. Kol bandı (kuvvet kimliği)
Kamuflajın üstünde sol üst kol:
| Kuvvet | Band | Hex |
|--------|------|-----|
| Mavi | 6 cm geniş | `#2B5A9E` |
| Kırmızı | 6 cm geniş | `#9E2B2B` |

Band üzerinde ince koyu dikiş; logo yok. Tim amblemi ayrı (C3-4); bu rehberde yok.

---

## 7. Kontrol listesi (dokuma / texture artist)
- [ ] Yalnızca ADD paleti (veya onaylı varyant)
- [ ] 8×8 dijital hücre; makro ada kuralı
- [ ] Resmi arma / hilal-yıldız / birim yazısı yok
- [ ] Seam’de renk sıçraması kontrolü
- [ ] Kol bandı ayrı albedo veya mask kanalı
- [ ] Steam / ticari kullanım; üçüncü parti digicam fotoğrafı izinsiz basılmamış
