# HAREKÂT — Görsel Yönetim Kılavuzu (Grafik Yönetmeni)

Hedef: Anadolu dağ coğrafyasında Tarkov / Sons of the Forest / CoD MW2019 seviyesi foto-gerçekçilik. Bu belge **tek doğruluk kaynağı**dır; mevcut değer–hedef farkları `Docs/GORSEL_DENETIM.md` içinde dosya:satır olarak listelidir.
Referans kaynakları: gün ışığı aydınlatma düzeyleri (Wikipedia "Lux", "Daylight": doğrudan güneş 32–100 klx, açık gökte gölge ~20 klx, bulutlu öğle 1–2 klx*, gün doğumu/batımı ~400 lx, dolunay ~0,25 lx; *tam kapalı bulut genelde 10–30 klx'e kadar çıkar, kaynaklar bandı geniş verir), Unity Material Validator (non-metal albedo 50–243 sRGB, metal 186–255), PBR albedo kılavuzları (non-metal 60–240 sRGB; boyalı yüzey <0,2 çok koyu, >0,8 çok parlak), Kasten-Young hava kütlesi (güneş yüksekliği → sönüm).

> Ölçek sözleşmesi: **Unity yönlü ışık 1,0 ≈ 60 klx** (öğle güneşi 1,7 ≈ 100 klx; gökyüzü ortamı ~0,28 ≈ 17 klx). Otomatik pozlama bu ölçeğe göre kalibre edilir; ölçek değişirse ColorAdjustments.postExposure ile telafi edilir, oranlar (güneş:gökyüzü) değişmez.

---
## 1. Temel ilkeler (her harita, her saat)
1. **Işık oranı gerçekliğin kalbidir**: yatay yüzeyde güneş:gökyüzü öğlen ≈ 5:1, öğleden sonra ≈ 3:1, altın saat ≈ 1,6:1, bulutlu 0:1 (yalnız gökyüzü). Şu an ortam ışığı güneşe yakın → düz, "plastik" görüntü. (Denetim #1)
2. **Gölge = mavi gökyüzü rengi, asla saf siyah.** Gölge parlaklığı güneşli yüzeyin %18–25'i (öğle), %45–60'ı (altın saat). En koyu piksel (gölge içi, AO'lu) sRGB 12–22 (linear 0,004–0,008), saf 0 yok.
3. **Doygunluk ayarı albedoda, derecelendirmede değil.** Yeşiller zeytin/sarıya kayar (Anadolu karaçamı/kızılçam/meşe), neon yeşil yok. Derecelendirme doygunluğu -8…+4 aralığında kalır.
4. **Mesafe = kontrast kaybı + mavi/sıcak kayma.** Uzak sırt sis rengine karışır, siluet yumuşar; "düz gri sis" yerine ufuk gökyüzü renginin türevi kullanılır.
5. **Parlak yüzeyler yalnız ıslakken/metalken parlar.** Kuru toprak, kaya, sıva, kar tozu mat (pürüzlülük ≥ 0,8).
6. **Tekrarı kır** (bkz. §6) — oyuncu 10 m içinde aynı deseni iki kez görmemeli.

## 2. Harita × saat × hava hedefleri

Kısaltmalar: K = Kelvin (güneş), T/Tint/Sat/Con = URP WhiteBalance temperature/tint, ColorAdjustments saturation/contrast. Güneş yoğunluğu = nihai `Light.intensity` (physical-sun uygulandıktan sonra). "d50" = yerleşik ExpSq sisin %50 sis verdiği mesafe (d50 = 0,83/k).

### 2.1 Kuzgun Vadisi (çam ormanı, kayalık yamaç, taş/sıva köy, toprak yol)
| Durum | Güneş K / yük. | Gökyüzü ortam (lum) | Sis rengi / d50 | Gölge rengi | Derecelendirme (T / Tint / Sat / Con) | Not |
|---|---|---|---|---|---|---|
| Gündüz açık | 5600 K / 1,6–1,7 (el. 48–55°) | 0,26 (mavi-nötr 0,78,0,88,1,0 çarpan) | (0,60,0,70,0,84) / 1100 m | gökyüzü mavisi, %20 | +5 / 0 / -6 / 12 | Güneş rengi (1,0,0,97,0,90) → filtre çift sarı yok |
| Şafak | 3000–3800 K / 0,55–0,8 | 0,10, mor-mavi (0,45,0,52,0,72) | (0,75,0,62,0,58) / 500 m (vadi sisi yoğun) | mor-mavi %45 | +4 / +3 / -8 / 6 | Vadi tabanında hacimsel sis, tepeler üstte açık |
| Akşam | 2600–3200 K / 0,8–0,95 | 0,09 sıcak-lila | (0,78,0,52,0,38) / 700 m | koyu mor-mavi %50 | +10 / +2 / 0 / 10 | Güneş diskine karşı Mie hâlesi, uzun gölge |
| Gece | ay 4100 K (mavi-beyaz) / 0,06 | 0,025 (0,05,0,07,0,13) | (0,03,0,04,0,08) / 220 m | indigo | -12 / -2 / -14 / 6 | Pozlama +1,5…+2 EV otomatik |
| Yağmur | 0,15–0,2 (yalnız sızıntı) | 0,48 (yeşilimsi-mavi gri 0,62,0,70,0,76) | (0,46,0,52,0,56) / 450 m | gölge yok denecek kadar (güç 0,25) | 0 / -2 / -8 / 8 | Islak yüzeyler: albedo ×0,75, pürüzsüzlük +0,35 |

### 2.2 Ayaz Geçidi (kar, soğuk sırtlar)
| Durum | Güneş K / yük. | Ortam | Sis d50 / renk | Derecelendirme |
|---|---|---|---|---|
| Gündüz (kar yağışı varsayılan) | 0,25 (kapalı) / 6200 K | 0,50 (nötr-mavi) | 450 m, (0,70,0,74,0,80) hafif mavi-beyaz | -10 / -2 / -10 / 12 |
| Gündüz açık | 1,5 / 5800 K, güneş tonu (0,96,0,98,1,04) | 0,38 (kar yansıması: ground ortam 0,28) | 1000 m | -8 / -2 / -8 / 12 |
| Şafak / akşam | pembe-turuncu kar parlaması, 3000 K | 0,14 | 600 m | -4 / 0 / -6 / 8 |
Kar albedo sRGB 225–238 (255'e yaklaşmaz); gölgede kar mavi (R<B, fark %8–12). Çıplak kaya ve ağaç gövdeleri koyu ve ıslak: kontrast kar:kaya ≥ 4:1.

### 2.3 Mavi Liman (liman, tuz-nem pus)
Gündüz 5700 K / 1,6; sis d50 700 m, rengi (0,62,0,72,0,80) hafif turkuaz (zaten deniz sisi bloğu var); nem pusu için gökyüzü ortamı 0,30; T +3 / Tint +3 / Sat 0 / Con 8. Şafak deniz sisi ×1,9 korunur ama renk **pembe-gri** (0,72,0,66,0,66), turkuaz değil. Metal/rıhtım yüzeyleri: tuzlu paslı, pürüzsüzlük 0,25–0,5, ıslak beton koyulaşır (×0,75).

### 2.4 Kartal Yaylası (açık plato, kuru çayır)
Gündüz 5700 K / 1,7, görüş en uzun: sis d50 1400 m, ufukta ince hacimsel sis (yükseklik düşüşü 0,012). Çayır sarı-yeşil (kuru çayır ağırlıkta), doygunluk **+0…+2** (şu an +8: neon riski). T +6 / Tint +2 / Sat +2 / Con 9. Rüzgâr: çimen hareketi ve bulut gölgeleri kimliği verir.

## 3. Sayısal hedef tabloları

### 3.1 Güneş: yükseklik → Kelvin → yoğunluk (açık hava, Anadolu dağ havası)
Doğrudan normal ışınım = 1361 W/m² × 0,7^(AM^0,678). Birim: 1,0 = 60 klx.
| Yükseklik | Kelvin | lux (≈ doğrudan normal) | Unity yoğunluk | Yatay güneş (×sin el) |
|---|---|---|---|---|
| 0° (ufuk) | 2000–2300 | 3–5 klx | 0,08 | ~0 |
| 5° | 2800–3200 | 28 klx | 0,45 | 0,04 |
| 10° | 3600–3900 | 50 klx | 0,78 | 0,14 |
| 20° | 4500–4800 | 70 klx | 1,19 | 0,41 |
| 30° | 5000–5200 | 82 klx | 1,37 | 0,69 |
| 45° | 5500–5600 | 93 klx | 1,55 | 1,10 |
| 60°+ | 5700–5800 | 100 klx | 1,65–1,7 | 1,45 |
Kelvin→renk: `LightingMath.KelvinTint` ile; ön ayar rengiyle karışım ağırlığı Gündüz 0,6 / Şafak–Akşam 0,35 → Şafak–Akşam 0,6'ya çıkarılmalı (altın saat fizikle uyumlu olsun).
Ay: 4100 K, yoğunluk 0,06 (gerçek 0,25 lx; oyun için okunabilirlik taviz: ~%1 güneş). Kapalı hava: güneş 0,15–0,2 (6500 K), gölge gücü 0,25.

### 3.2 Gökyüzü / ortam
- Ortam oranı (yatay güneş : ambient sky): öğle 5:1 · 30° 3:1 · 10° 1,6:1 · kapalı ∞.
- Trilight: sky : equator : ground = 1 : 0,70 : 0,32. Ground rengi arazi albedosundan türer (0,45,0,38,0,28 normalize), gökyüzü mavi, ekvator nötr-sıcak.
- Ortam kaynağı: bugün Trilight gradyan. Hedef: skybox/HDRI SH + Adaptive Probe Volumes (orman gölgesi, iç mekân). Reflection intensity 1,0 (şu an 0,65).
- Gökyüzü pozlama (_Exposure) öğle 1,0–1,1, gökyüzü ufuk ışıması güneş diskine yakın (altın saat) 1,2.
- Skybox atmosfer kalınlığı: öğle 1,0; şafak 1,3; akşam 1,5; gece 0,5 (mevcut doğru).

### 3.3 Pozlama (EV)
| Durum | Gerçek EV100 | Oyun otomatik pozlama referans lum | Strength | Min/Max EV |
|---|---|---|---|---|
| Dış gündüz açık | 14–15 | 0,24 | 0,65 | -1,5 / +1,5 |
| Dış kapalı/yağmur | 12–13 | 0,24 | 0,65 | -1,2 / +1,8 |
| İç mekân (pencere ışığı) | 8–9 | 0,24 (strength 0,65: dışarıya göre ≈ 3,5 EV karanlık kalır) | 0,65 | -1,5 / +2,0 |
| Şafak/Akşam | 9–11 | 0,17 | 0,6 | -1,2 / +1,8 |
| Gece (ay) | -2…+1 | 0,05 | 0,45 | -0,6 / +2,2 → +2,6 (ay 0,06'ya inince) |
Adaptasyon: parlağa **3,5 /sn** (τ≈0,3 s), karanlığa **0,6 /sn** (τ≈1,7 s) — şu an 4,5 / 1,2: karanlığa dönüş fazla hızlı, "göz alışması" hissi yok. Eve girişte 2–3 sn karanlık kalma, çıkışta 0,3 sn patlama hedeflenir.

### 3.4 Sis
- Yerleşik ExpSq (hacimsel aktifken `BuiltInFogScale` 0,35 → ham = etkin / 0,35):
  | Harita (gündüz açık) | Etkin k | d50 | Ham k (hacimsel açık) |
  |---|---|---|---|
  | Kuzgun | 0,00075 | 1100 m | 0,0021 |
  | Ayaz (açık) | 0,0008 | 1000 m | 0,0023 |
  | Mavi Liman | 0,0012 (+×1,35 harita çarpanı) | 700 m | 0,0034 |
  | Kartal | 0,0006 | 1400 m | 0,0017 |
  Çarpanlar (mevcut, doğru): şafak ×1,5, akşam ×1,15, gece ×2,6, yağmur ×1,35, kar ×1,25.
- Yükseklik düşüşü (CPU `HeightFogFactor`): 0,012 → **0,008** (ölçek ~125 m; vadi–yamaç farkı), taban 0,25 → 0,35 (tepede tamamen temiz olmasın).
- Hacimsel sis (Açık/gündüz): yoğunluk 0,0035 ✓; **HeightFalloff 0,06 → 0,025** (gündüz), şafak 0,045 (ince vadi sisi katmanı); anizotropi 0,72 → **0,65** gündüz, 0,55 sisli/yağmur ✓; MaxDistance 260 ✓ (kademe: 120/220/350 ✓).
- Aerial perspective (`_HK_AerialDensity`): başlangıç ≈ 80 m (yakın PBR kontrastı korunur), 400 m'de %30–40, 1 km'de %70, 2 km'de %90 ⇒ yoğunluk **0,0012** (gündüz açık; şu an 0,0024 = 400 m'de %62, kendi yorumunun üst sınırı 0,6'yı aşıyor). Şafak 0,0018, akşam 0,0016, gece 0,0030, hava ×1,4 ✓.

### 3.5 Post-process
- **Tonemapping: ACES kalsın.** Neden: film omuzu (güneş vurmuş kaya, namlu alevi, gökyüzü patlaması) doğal sıkışır, gölgelerde kontrast verir (Tarkov/CoD benzeri). ACES yeşili sarıya, maviyi mora kaydırır ve doygunluk yer ⇒ derecelendirmede ek desatürasyon **yapma** (Kuzgun -14 → -6). Neutral yalnız menü/UI sahnesi ve kar haritasında beyaz kaymasını görürsek denenir. Sabit EV kaydırması yerine otomatik pozlama.
- Bloom: eşik **1,2** (gökyüzü/güneş yüzeyleri bloom'u sürekli tetiklemesin), yoğunluk gündüz **0,30** / gece **0,5** / Ultra +0,05, scatter 0,65, tint beyaz (gece hafif soğuk).
- Vinyet 0,18–0,25 (taban; hasarda artar), smoothness 0,4 ✓. Kromatik sapma 0,12 → **0,05** (kenarda yumuşak, merkez temiz). Film greni 0,18 → **0,10** (yalnız tip Thin1, response 0,8 ✓).
- SSAO (URP, DepthNormals, **After Opaque kapalı** = yalnız ortam ışığına): Orta r 0,35 i 0,7; Yüksek r 0,5 i 1,0; Ultra r 0,6 i 1,2 falloff 100; DownSample Ultra'da kapalı ✓. (Şu an 0,22/0,3/0,38 m: ağaç dibi ve kaya çatlağı için fazla küçük.)
- Gölge: uzaklık Düşük 50 / Orta 100 / Yüksek 180 / Ultra 300 m (1 km haritada orta-uzak ağaç gölgeleri); 4 kademe bölünmesi (kesir): Yüksek (0,05, 0,15, 0,40) ⇒ 9/27/72/180 m; Ultra (0,03, 0,10, 0,30) ⇒ 9/30/90/300 m; çözünürlük Yüksek 2048 (≥) Ultra 4096; yumuşak gölge kalite 3. Gölge gücü: açık 1,0 (güneş zaten tek gölge) – ConfigureSun'daki 0,88 sabiti ezilir, tutarsız: tek kaynak `LightingMath.ShadowStrength`.
- Temas gölgesi (HDRP "contact shadow" eşdeğeri; URP'de yoktur): uzunluk 0,5 m (karakter/prop), 1,0 m üst sınır, 25 m'de solar; yerine SSAO yarıçapı + kontak AO decal/blob kullan, ya da özel RendererFeature.
- Hacimsel sis anizotropisi: 0,65–0,72 (Mie), sis damlacığı g≈0,5.
- DoF (ADS): gaussian ✓; Ultra'da Bokeh önerilmez (maliyet).
- Motion blur: kamera yalnız, 0,2–0,3 (şu an 0,35; varsayılan kapalı ✓).

## 4. Malzeme kuralları (PBR)

### 4.1 Albedo parlaklığı (sRGB 0–255, Rec.709 ağırlıklı) ve pürüzlülük
| Malzeme | Albedo lum. | Pürüzlülük (smoothness=1−r) | Metalik | Not |
|---|---|---|---|---|
| Toprak (kuru) | 70–110 | 0,85–0,95 (s 0,05–0,15) | 0 | Islak: lum ×0,65, s 0,4–0,6 |
| Çamur | 40–60 | 0,5–0,7 (s 0,3–0,5) | 0 | Yalnız ıslak |
| Kaya (kireçtaşı/granit) | 70–140 | 0,75–0,9 | 0 | Yosun/liken sarı-yeşil leke, lum 60–90 |
| Çakıl | 90–130 | 0,8–0,9 | 0 | |
| Çimen (yeşil) | 55–95, G/R ≤ 1,35 | 0,85–0,95 | 0 | Neon yok; kuru çimen 120–160 |
| Çam iğnesi | 35–60 | 0,8–0,9 | 0 | Çok koyu; arkadan ışık (translucency) ile canlanır |
| Meşe yaprağı | 55–90 | 0,75–0,85 | 0 | |
| Ağaç kabuğu | 40–70 | 0,85–0,95 | 0 | |
| Sıva (kireç badana, eskimiş) | 170–205 | 0,8–0,9 | 0 | 215'i geçmez; leke/çatlak/su izi |
| Taş duvar | 90–150 | 0,8–0,9 | 0 | Derz koyu 60–80 |
| Beton | 110–165 | 0,75–0,9 | 0 | |
| Asfalt (kuru) | 45–65 | 0,8–0,88 | 0 | Islak s 0,6–0,8 |
| Ahşap (yaşlı) | 70–120 | 0,75–0,9 | 0 | Cilalı sürülmemiş |
| Kiremit | 80–110 | 0,7–0,85 | 0 | |
| Metal çıplak çelik | 186–220 | 0,3–0,6 | 1 | Metal=1 olduğunda albedo = F0; ikili (0/1) maske |
| Pas | 70–100 (dielektrik) | 0,7–0,9 | 0 | Metalik 0 + metal çıplak lekeler |
| Silah çeliği (parkerize/oksit) | 140–170 (metal=1) | 0,45–0,65 | 1 | "Siyah" değil koyu gri-yansıtıcı; AO + aşınma ile derinlik |
| Polimer (silah) | 25–50 | 0,4–0,6 | 0 | |
| Araç boyası (CARC mat zeytin) | 55–85 | 0,65–0,8 | 0 | |
| Kumaş (üniforma/kamuflaj) | 50–95 | 0,85–0,95 | 0 | Kamuflajda en koyu leke ≥ 25 |
| Deri (bot) | 30–55 | 0,5–0,7 | 0 | |
| Cilt (açık–orta) | 130–185 (orta ton 110–150) | 0,45–0,6 | 0 | SSS/ten tonu: kırmızı kanal kenarda |
| Kar | 225–238 | 0,7–0,85 (taze toz) / 0,5 (kabuk) | 0 | |
| Kum | 150–190 | 0,85–0,95 | 0 | |
Kural: non-metal albedo 50–243; saf siyah (<25) ve saf beyaz (>243) yok (kar hariç üst sınır 238).

### 4.2 Texel yoğunluğu (px/m)
| Yüzey | Hedef px/m | Tipik çözüm |
|---|---|---|
| Arazi zemini (yakın) | 256–512 | Katman karosu **2–4 m**, 1k–2k doku; + 0,5 m detay normal; + 40–60 m makro |
| Bina duvarı | 256–512 | 2 m karo 1k, + detay |
| Prop (varil, sandık) | 512 | 1k / 2 m |
| Birinci şahıs silah | 2048–4096 | 4k tüm silah (~1 m) |
| Birinci şahıs kol/eldiven | 1024–2048 | |
| Karakter (3. şahıs) | 512–1024 | 2k/1,8 m |
| Uzak LOD / ağaç | ≥ 32 | |
Şu an: arazi 8–15 m karo × 512–1024 px = **34–128 px/m**: bulanık ve tekrar belli (Denetim #3).

## 5. Bitki örtüsü ve arazi kompozisyonu
- Orman: kapalı çatı hissi için çam yoğunluğu dik yamaçta yüksek, tabanda seyrek; açıklıklar (clearing) 20–40 m; her 25 m²'de ≥ 1 ölü dal/kütük/kaya ("ağaç dizisi" izlenimi yok). Ağaç renk varyasyonu (HSV) ±%8 değer, ±%5 ton; gövde dibi karanlık (AO), yosun gölge yüzüyle.
- Alt kat: eğreltiotu/çalı/ölü yaprak, 3 yükseklik katmanı (çimen 0,2 m, çalı 0,6–1 m, genç ağaç 2–3 m).
- Arazi: eğime göre kaya (>35° kaya, 20–35° çakıl/toprak, <20° çimen/toprak), yükseklik+eğim blend; yollar toprak, iki tekerlek izi (daha koyu/nemli, orta çimenli), kenarlarda çakıl.
- Çimen: dönüştürülmüş renk toprak altı ile eşleşir (alt kısım toprağa karışır, uç %15 daha açık); rüzgâr genliği hava ile; mesafede çimen yoğunluğu fade, toprak rengi çimen rengine yaklaşık.
- Renk: çam yeşili mavi-yeşil (lum 40–60); meşe/çalı sarı-yeşil; kuru çimen saman rengi. Doygunluk üst sınırı HSV S ≤ 0,45.

## 6. Tekrar kırma
1. Zemin: ≥ 3 katman, yükseklik-bazlı blend; 2 farklı frekansta makro (ör. 41 m ve 7,3 m; oran irrasyonel).
2. Detay UV'si 17–23° döndürülmüş + stochastic/hex tiling (shader) ya da dünya-pozisyon gürültüsü ile ±%10 değer / ±%4 ton kayması.
3. Kaya yüzeyi: triplanar (kayalıklarda UV gerilmesi yok); duvar: parça başına rastgele UV ofseti + vertex renk.
4. Decal: leke, çatlak, yosun, tekerlek izi (DecalScatter), mesafe fade.
5. Prop: her nesneye ±%6 değer, ±%3 ton rastgelelik (MaterialPropertyBlock).

## 7. Bina eskitme
- Sıva dökülmesi: alt 0,5 m rutubet (lum ×0,8, soğuk ton), köşelerde döküntü ile alttaki taş/tuğla görünür; üst saçak altı kirlenme; pencere altı su akıntısı.
- Çatı: kiremitte yosun/solma (±%15), kırık parçalar; sac çatıda pas damarı.
- Ahşap: gri-gümüş solma (ışık alan yüz), kapı altı çürük; boya soyulması (varsa lum 120–180, altı 70–100).
- Duvar tabanı: toprak sıçraması decalı; zemine temas AO blob.
- Hasar: kurşun izi/çatlak decalları; pencere camı kirli (yansıma s 0,85, albedo kir).
- Her binada ≥ 3 eskime katmanı (kir, nem, aşınma); "temiz" yüzey yok.

## 8. Viewmodel / silah sunumu
- FOV: dünya kamerası dikey 80° = ~107° yatay (çok geniş, kenar bozulma). Varsayılan **dikey 62–66°** (≈ 100° yatay), oyuncu ayarı 55–75. Viewmodel kamerası ayrı **54°** (şu an 60°), near 0,01, silah dünyadan ayrı kamerada ama **dünya ışığı/prob ile** aydınlanır.
- Silah asla düz siyah olmamalı: metal=1 albedo 140–170 (parkerize), s 0,45–0,65, AO + kenar aşınması, yansıma probu/skybox yansıması (reflection intensity ≥ 1,0); en koyu parça lum ≥ 25 (polimer). GunLit shader (GX4) bu aralıkla beslenmeli.
- Viewmodel prob ışığı: dünya ortamının %100'ü + zayıf ten-dolgu (silah gölgede kalsa bile ortam 0,15 altına düşmez); güneşe dönükken yüzeyde güneş parlaması okunur.
- Namlu alevi HDR bloom'u kullanır; gölge/ışık eşlik eder (kısa nokta ışığı 2–3 kare). Kaplama ışıltısı optik camında mavi-mor yansıma.
- ADS: DoF yalnız arka plan hafif; silah netliği korunur; kol/eldiven albedo üniforma ile aynı eskime düzeyinde.

## 9. YAPMA listesi (kırmızı çizgiler)
- Aşırı doygun yeşil (HSV S > 0,45 yapraklarda, çimen G/R > 1,4).
- Saf siyah gölge / kırpılmış siyah (albedo < 25, ortam < 0,02 gündüz).
- Parlak/cilalı toprak, kaya, sıva (smoothness > 0,2 kuru yüzeyde).
- Düz gri sis (sis rengi ufuk gökyüzünden türemeli; yağmurda koyu mavi-yeşil gri, açık günden %25 daha koyu).
- Güneş + filtre + beyaz dengesi üçünün aynı yönde sarı eklenmesi (sepya).
- Saf beyaz/parlak sıva (>215) ve kırpılan kar (>240).
- Bloom ile "pus" görüntüsü (eşik < 1).
- Ortam ışığı ≥ güneş yatay katkısı (düz görüntü).
- Karo deseni 10 m içinde görünür tekrar.

## 10. Sıradaki en büyük gerçekçilik kazanımları (öncelik sırası)
1. **Işık oranı düzeltmesi**: ambient'i düşür, güneşi 1,7'ye çıkar, gölge mavi/dolgu; gün batımı ambient<güneş (Denetim #1–#3).
2. **Arazi doku yoğunluğu**: karo 2–4 m, 2k ambientCG dokuları, makro varyasyon, eğim/yükseklik blend (Denetim #4).
3. **Bitki dibi AO + temas gölgesi**: SSAO yarıçap 0,5–0,6 m, ağaç/kaya dibi karartma.
4. **Skybox/HDRI + APV ortam**: Trilight yerine HDRI SH ve orman gölgesi için probe volume; yansıma yoğunluğu 1,0.
5. **Gölge kademeleri**: 180/300 m, 4 kademe oranları, tek kaynak (PostProcessing ve PipelineTiers çakışıyor).
6. **Aerial perspective + yükseklik sisi** yeniden ayarı (d50 haritaya göre, aerial 0,0012, düşüş 0,025).
7. **Silah malzemesi**: GunMetal metalik=1 + yansıma + aşınma; viewmodel FOV 54°, dünya FOV 62–66°.
8. **Haritaya özel derecelendirme sadeleştirme**: sepya/mavi cast'i azalt (Kuzgun T+14→+5, Ayaz T-22→-10, Kartal Sat +8→+2).
9. **Pozlama**: karanlığa 0,6/sn adaptasyon, iç mekân karanlığı, gece +2,6 EV üst sınır.
10. **Bina eskitme + decal serpme**: nem/leke/su izi katmanları, ağaç varyasyonu (renk ±%8), kamuflaj/üniforma albedo düzeni.
