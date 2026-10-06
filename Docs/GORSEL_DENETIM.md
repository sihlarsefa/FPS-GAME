# HAREKÂT — Görsel Denetim (mevcut değer → hedef)

Yol ön eki: `Assets/_Project/Scripts/`. Hedef değerlerin gerekçesi/ölçeği: `Docs/GORSEL_YONETIM.md` (Unity ışık 1,0 ≈ 60 klx). Etki sırasına göre; her satır mekanik uygulanabilir. Hiçbir kod dosyası bu denetimde değiştirilmedi.

## A. Işık oranı / atmosfer (en büyük etki)
| # | dosya:satır | mevcut | hedef | neden | ✓ |
|---|---|---|---|---|---|
| 1 | Infrastructure/Rendering/RenderSettingsUtil.cs:67 | ambientSky (0,56,0,64,0,76) lum≈0,63 | (0,28,0,34,0,44) lum≈0,26 | Güneş yatay 1,3·sin48°=0,97 vs gökyüzü 0,63 → oran 1,5:1; hedef 5:1. Düz/plastik görünümün ana nedeni | ✓ |
| 2 | …/RenderSettingsUtil.cs:68 | ambientEquator (0,47,0,48,0,45) | (0,20,0,22,0,22) (=sky×0,7, nötr-sıcak) | Trilight 1:0,7:0,32 oranı | ✓ |
| 3 | …/RenderSettingsUtil.cs:69 | ambientGround (0,24,0,22,0,18) | (0,09,0,08,0,06) | Zemin yansıması arazi albedosu × ~0,3 | ✓ |
| 4 | …/RenderSettingsUtil.cs:71 | sunIntensity 1,3 | 1,7 (öğle el. 55°) | 100 klx ≈ 1,7; ambient inince pozlama (ref lum 0,24) telafi eder; ColorAdjustments exposure 0,12 → -0,1 | ✓ (poz. -0,05: #37 ile tutarlı) |
| 5 | …/RenderSettingsUtil.cs:70 | sunColor (1,0,955,0,87) | (1,0,975,0,92) | Kelvin bleed (+0,6 ağırlık) + MapGrade SunTint + filtre üçlü sarı → sepya; ham rengi nötrleştir | ✓ |
| 6 | …/RenderSettingsUtil.cs:124 | reflectionIntensity 0,65 | 1,0 | Metal/silah/ıslak yüzey/kar yansıması; 0,65 gunları "siyah" bırakır | ✓ |
| 7 | …/RenderSettingsUtil.cs:65 | fogColor (0,64,0,7,0,76) lum 0,69 | (0,60,0,70,0,84) | Ufuk gökyüzü rengine yakın, hafif mavi; düz-gri sis riski azalır | ✓ |
| 8 | …/RenderSettingsUtil.cs:66 | fogDensity 0,0016 (ham) | 0,0021 (hacimsel açık, ×0,35 → etkin 0,00075) | Etkin değer şu an 0,00056 (d50 1480 m): uzakta derinlik yok; hedef d50 1100 m | ✓ |
| 9 | …/RenderSettingsUtil.cs:171-172 | shadowBias 0,05 / normalBias 0,4; L170 shadowStrength 0,88 | strength → LightingMath.ShadowStrength tek kaynak; normalBias 0,4 → 0,3 (cascade1 9 m'de) | İki yerde gölge gücü; sıkı cascade'de 0,4 "uçan nesne" (peter-panning) | ✓ |
| 10 | Infrastructure/Rendering/AtmospherePresets.cs:40 (Akşam) | güneş 1,0 (el.12°), ambient sky (0,5,0,42,0,5) lum 0,45, ground (0,22,0,16,0,13) | güneş 1,25, sky (0,22,0,17,0,24) lum 0,18, eq (0,17,0,12,0,12), ground (0,08,0,06,0,05) | Altın saatte ambient (0,4) güneşin yatay katkısının (≈0,2) iki katı: gölge ışıktan parlak = düz. Hedef oran 1,6:1 | ✓ |
| 11 | …/AtmospherePresets.cs:34-36 (Şafak) | güneş 0,85 el.8°, sky (0,42,0,46,0,62) lum 0,48 | güneş 0,95, sky (0,20,0,22,0,34), eq (0,17,0,14,0,15), gr (0,07,0,06,0,06) | Aynı sorun; şafak soğuk-mor gölge kalmalı | ✓ |
| 12 | …/AtmospherePresets.cs:42-44 (Gece) | güneş(ay) 0,12; sky lum 0,07; skyExposure 0,12; postExposure 0,35 | ay 0,06; sky (0,02,0,03,0,06); eq (0,02,0,025,0,045); fog (0,03,0,04,0,08) kalsın | Ay/ambient oranı 1:2; fazla parlak gece Tarkov-karanlığı vermez; pozlama otomatik +2,6 EV üst sınır | ✓ |
| 13 | …/AtmospherePresets.cs:47 (Gündüz) | postExposure 0,12 | -0,05 | Güneş 1,7'ye çıkınca | ✓ |
| 14 | Core/Domain/Models/LightingMath.cs:49 | SunFullElevationDeg 60 | 45 | 20° → 4500 K, 30° → 5100 K (gerçek 4500–4800 / 5000–5200); şu an 4190/4690 | ✓ |
| 15 | …/LightingMath.cs:137-138 | Yağmur ambient ×1,08, Kar ×1,14 | ×1,7 / ×1,9 | Kapalı havada gökyüzü ışığı 20–30 klx (açık gökyüzünün ~1,5–2 katı); güneş yerine ambient hâkim olmalı | ✓ |
| 16 | …/LightingMath.cs:149-150 | Gölge gücü Yağmur 0,55 / Kar 0,62 | 0,25 / 0,30 | Tam kapalı havada belirgin güneş gölgesi yok | ✓ |
| 17 | Infrastructure/Rendering/Atmosphere.cs:159-160,192 | dim 0,78 (yağmur güneşi) | 0,15 | Yağmurda güneş 1,3·0,78≈1,0 → "güneşli yağmur"; hedef 0,15–0,2 mutlak | ✓ |
| 18 | …/Atmosphere.cs:166 | yağmur sis rengi grey*1,0 (0,55,0,58,0,62) gündüz | (0,46,0,52,0,56) (renk ×0,75 koyu, hafif yeşil-mavi) | Yağmurda gri fog açık günden parlak/düz — "flat gray" | ✓ |
| 19 | …/Atmosphere.cs:81 | HeightFogFactor falloff 0,012 | 0,008 (taban 0,25→0,35 AtmosphereMath.cs:13) | Yükseklikle sis aşırı hızlı kayboluyor | ✓ |
| 20 | Core/Domain/Models/AtmosphereMath.cs:27 | AerialDensity gündüz 0,0024 (400 m'de %62; kendi yorumu 0,25–0,6) | 0,0012; Şafak 0,0018; Akşam 0,0016; Gece 0,0030 | 400 m'de %38, 1 km'de %70 hedef; şu an 290 m'de %50 | ✓ |
| 21 | Core/Domain/Models/VolumetricFogMath.cs:292 (Açık) | falloff 0,06; g 0,72 | falloff 0,025; g 0,65 | 16 m ölçek: yalnız zemin sisi, gün ışığı huzmesi yok | ✓ |
| 22 | …/VolumetricFogMath.cs:300 (Şafak) | density ×1,7 | ×1,7 ama falloff 0,045 override | Vadi sisi katmanı | ✓ |
| 23 | …/VolumetricFogMath.cs:306 (Gündüz tint) | (0,78,0,84,0,92) | (0,80,0,86,0,95) | Gökyüzü ufuk tonu | ✓ |
| 24 | Infrastructure/Rendering/Volumetric/VolumetricFogSettings.cs:16,18 | HeightFalloff 0,06, Anisotropy 0,72 varsayılan | 0,025 / 0,65 | Presetle uyum | ✓ |

## B. Pozlama / post
| # | dosya:satır | mevcut | hedef | neden | ✓ |
|---|---|---|---|---|---|
| 25 | LightingMath.cs:194 | adaptasyon parlağa 4,5 / karanlığa 1,2 | 3,5 / 0,6 | Göz uyumu: karanlığa τ≈1,7 s | ✓ |
| 26 | LightingMath.cs:182 | Gece maxEv 2,2 | 2,6 (ay 0,06'ya inince) | Okunabilirlik | ✓ |
| 27 | Infrastructure/Rendering/PostProcessing.cs:287 | Bloom threshold 1,05 | 1,2 | Gökyüzü/güneş yüzeyleri sürekli bloom → pus | ✓ |
| 28 | …/PostProcessing.cs:288 | intensity 0,5 / Ultra 0,6 | 0,30 / Ultra 0,35 (gece 0,5) | Gerçekçi, sinematik pus yok | ✓ |
| 29 | …/PostProcessing.cs:308 | ChromaticAberration 0,12 | 0,05 | Merkezde bulanıklık hissi | ✓ |
| 30 | …/PostProcessing.cs:314 | FilmGrain 0,18 | 0,10 | Doku detayını yemesin | ✓ |
| 31 | …/PostProcessing.cs:329 | MotionBlur 0,35 | 0,25 | | ✓ |
| 32 | Core/Domain/Models/MapGrade.cs:49 (Kuzgun) | T+14, Tint+4, Sat -14, Con 12, filtre (1,1,0,9) | T+5, Tint+1, Sat -6, Con 12, filtre (1,1,0,96) | ACES zaten desatürasyon yapar; sepya/aşırı sarı. Shadows (0,98,0,96,0,88) → (0,97,0,97,1,0) (gölge mavi) ; Highlights (1,06,1,02,0,9) → (1,03,1,01,0,96) | ✓ |
| 33 | …/MapGrade.cs:34 (Ayaz) | T-22, Sat -22, filtre (0,92,0,97,1,04) | T-10, Sat -12, filtre (0,96,0,98,1,02) | Karda cilt/silah mavi-gri; kar cyan | ✓ |
| 34 | …/MapGrade.cs:39 (Liman) | Tint +8 | +3 | Macenta ten | ✓ |
| 35 | …/MapGrade.cs:44 (Kartal) | T+10, Sat +8, Highlights (1,08,1,05,0,9) | T+6, Sat +2, (1,05,1,03,0,95) | Neon çimen riski | ✓ |
| 36 | Infrastructure/Rendering/ScreenEffectsMath.cs:105-106 | Akşam T+18, Sat+4; Gece Sat -16 | Akşam T+10, Sat 0; Gece Sat -10 | MapGrade + saat toplamı aşırı | ✓ |
| 37 | …/ScreenEffectsMath.cs:90-93 | TimeExposure 0,05/0,1/0,35/0,12 | 0,0/0,05/0,20/-0,05 | Işık ölçeği değişince; gece otomatik pozlama zaten yükseltir | ✓ |
| 38 | …/ScreenEffectsMath.cs:199-202 | SSAO r 0,22/0,3/0,38; i 0,55/0,8/1,0 | r 0,35/0,5/0,6; i 0,7/1,0/1,2 (falloff 100) | Ağaç dibi / kaya çatlağı ölçeği 0,3–0,6 m; ambient-only (After Opaque kapalı) olduğundan SsaoTuner'a `AfterOpaque=false` | ✓ |
| 39 | Infrastructure/Rendering/PostProcessing.cs:24-27 | ShadowDistances 60/110/170/260; cascades 1/2/3/4; res 1024/2048/2048/4096 | 50/100/180/300; cascades 1/2/4/4; res 1024/2048/2048/4096 | 1 km harita | ✓ |
| 40 | Infrastructure/Rendering/PipelineTiers.cs:57-60 | shadowDist 40/60/90/120; splits Y (0,07,0,20,0,45) U (0,06,0,18,0,42) | 50/100/180/300; Y (0,05,0,15,0,40) U (0,03,0,10,0,30) | **Çakışma**: ApplyQuality (PostProcessing 170 m) ve ApplyRuntime (PipelineTiers 90 m) aynı URP varlığına farklı değer yazıyor; son çalışan kazanır. Tek kaynak PipelineTiers olmalı | ✓ |
| 41 | …/PipelineTiers.cs:59 | High yok SoftShadowQuality 2 | 3 (High/Ultra) | Gölge kenarı | ✓ |

## C. Gökyüzü / bitki
| # | dosya:satır | mevcut | hedef | neden | ✓ |
|---|---|---|---|---|---|
| 42 | Infrastructure/Rendering/SkyEnvironment.cs:230-232 | Gündüz bulut tint (1,1,1,0,95) saf beyaz; yağmur ×0,5 | Gündüz (0,97,0,98,1,0, 0,9); yağmur (0,45,0,48,0,52) | Saf beyaz bulut ACES'te kırpılır; yağmur bulutu koyu mavi-gri | ✓ |
| 43 | …/SkyEnvironment.cs:202-203 | farRidge tint×0,75→fog, nearRidge tint×0,45 | far: fog'a %55 karış; near: tint×0,55 | Aerial perspective ile tutarlı siluet | ✓ |
| 44 | Infrastructure/World/VegetationMaterials.cs:58 | Çam iğnesi tint (0,95,1,0,95) | (0,78,0,86,0,8) | İğne dokusu zaten yeşil; tint ile lum 35–60 hedefi | ✓ |
| 45 | …/VegetationMaterials.cs:62 | Meşe (1,1,0,92); çalı (0,78,0,9,0,7) | (0,9,0,92,0,78); (0,66,0,76,0,58) | Sarı-yeşil, S ≤ 0,45 | ✓ |
| 46 | …/VegetationMaterials.cs:67 | Foliage (0,26,0,34,0,17) | (0,22,0,28,0,14) | Lum 86 → ~70 | ✓ |

## D. Malzeme / doku
| # | dosya:satır | mevcut | hedef | neden | ✓ |
|---|---|---|---|---|---|
| 47 | Infrastructure/World/TerrainTextureFactory.cs:68-80 | TileSize Grass 9, DryGrass 10, Dirt 8, Rock 13, Gravel 5, Mud 7, Snow 15 | 3, 3,5, 3, 4, 2, 3, 4 (m) | 34–128 px/m → 256–512 px/m; bulanık + tekrar. (+ macro 40–60 m ayrı katman) | ✓ |
| 48 | …/TerrainTextureFactory.cs:26,29 | DefaultTextureSize 512 | 1024 (Ultra 2048) | Texel yoğunluğu | ✓ |
| 49 | …/TerrainTextureFactory.cs:84-95 | Smoothness Snow 0,35, Rock 0,16, Mud 0,42 | Snow 0,25, Rock 0,12 | Kuru yüzey mat | ✓ |
| 50 | …/TerrainTextureFactory.cs:107 + 340 | Snow ort. (0,9,0,92,0,96), prosedürel (0,95,0,96,0,98) | (0,86,0,89,0,94) / (0,89,0,91,0,95) | lum 239–250 → 225–238, ACES'te kırpılmasın | ✓ |
| 51 | …/TerrainTextureFactory.cs:247-248 | Çimen (0,27,0,36,0,15)↔(0,38,0,46,0,2) | (0,24,0,31,0,13)↔(0,33,0,40,0,17) | Lum ≈ 95–110 → 70–95; G/R ≤ 1,35 | ✓ |
| 52 | Infrastructure/Rendering/MaterialLibrary.cs:850 (Grass) | (0,34,0,42,0,2) lum 99 | (0,29,0,36,0,17) | Neon yeşil engeli | ✓ |
| 53 | …/MaterialLibrary.cs:904 (GunMetal) | (0,13,0,13,0,14), s 0,45, metal 0,7 | (0,55,0,55,0,57), s 0,45, metal 1,0 | F0 ≈ 0,05: ışığı yansıtmaz = düz siyah silah. Parkerize çelik F0 0,25–0,35 | ✓ |
| 54 | …/MaterialLibrary.cs:881 (MetalDark) | (0,22,0,23,0,24), metal 0,6 | (0,5,0,5,0,52), metal 1,0, s 0,35 | Metal albedosu 186+/0,5+ | ✓ |
| 55 | …/MaterialLibrary.cs:880,876 (MetalPanel/RoofMetal) | metal 0,5 / 0,55 | 1,0 (kirli kısım doku maskesinde 0) | İkili metal kuralı | ✓ |
| 56 | …/MaterialLibrary.cs:870 (Plaster) | (0,85,0,82,0,75) lum 209 | (0,74,0,71,0,64) lum ≈ 185 | Eskimiş badana ≤ 205; güneşte kırpılma | ✓ |
| 57 | …/MaterialLibrary.cs:871 (PlasterWarm) | (0,86,0,74,0,58) | (0,76,0,64,0,5) | Aynı | ✓ |
| 58 | …/MaterialLibrary.cs:857 (Snow) | (0,92,0,94,0,97), s 0,5 | (0,87,0,9,0,94), s 0,28 | Kar lum 225–238; taze toz mat | ✓ |
| 59 | …/MaterialLibrary.cs:868 (Concrete) | (0,62,0,61,0,58) lum 158 | (0,55,0,54,0,51) | 110–165 orta | ✓ |
| 60 | …/MaterialLibrary.cs:918 (Skin) | (0,85,0,66,0,52) lum 176 | (0,74,0,56,0,44) | Anadolu orta ton; parlak ten ACES'te yanar. SkinDark :919 OK | ✓ |
| 61 | …/MaterialLibrary.cs:918-919 (Skin smoothness) | 0,3 | 0,4 | Ten özgül parlaklığı | ✓ |
| 62 | …/MaterialLibrary.cs:853-855 (Mud/Rock) | Mud s 0,45 kuru dünyada | Mud kuru 0,25 / ıslak 0,5 (ıslaklık Wetness ile) | "Glossy dirt" riski | ✓ |
| 63 | …/MaterialLibrary.cs:859 (Asphalt) | s 0,22 | 0,14 (kuru) | Mat kuru asfalt | ✓ |
| 64 | Infrastructure/Rendering/ProceduralPbr.cs:328,345 | Stone albedo çarpanı 0,74+; Plaster 0,88+ | Stone 0,66+; Plaster 0,8+ | Malzeme rengiyle çarpılıyor; toplam lum hedefi aşılıyor | ✓ |

## E. Kamera / viewmodel / el feneri
| # | dosya:satır | mevcut | hedef | neden | ✓ |
|---|---|---|---|---|---|
| 65 | Infrastructure/Config/PlayerMovementConfig.cs:126 | fieldOfView 80 (dikey ≈ 107° yatay) | 64 (ayar 55–75) | Kenar bozulma, uzak hedef küçük | ✓ |
| 66 | Infrastructure/Rendering/CameraRig.cs:22 | ViewmodelFieldOfView 60 | 54 | Silah bozulması az | ✓ |
| 67 | Infrastructure/Rendering/Atmosphere.cs:331-333 | Fener 4,5, range 45, 55° | kalsın; gece ay 0,06'ya inince 6,0 | Kontrast | ✓ |
| 68 | Atmosphere.cs:36-38 (nominal) | ShadeFill/AmbientScale gece 0,02 | kalsın | Gece iç mekân karanlık ✓ | ✓ (değişiklik yok) |

## Çözüm sırası önerisi
1) #1–#4, #10–#11, #15–#18 (ışık oranı), 2) #47–#48, #52 (arazi), 3) #53–#55 (silah), 4) #38–#41 (AO/gölge), 5) #32–#36 (derecelendirme), 6) kalan.
Doğrulama: AAA Benchmark sahnesi (F9/F12 vitrin) öncesi/sonrası; luma histogramı: gölge içi piksel ≥ sRGB 12, güneşli kaya orta ton ≈ 130–170, kar tepe ≤ 238, çim ortalama 70–95.


Uygulama notu (2026-10-06): 68 satırın tümü kodda uygulandı. #4 ColorAdjustments pozlaması -0,1 yerine #37 ile çelişmemesi için -0,05 (gündüz). #39/#40: gölge mesafesi tek kaynak PipelineTiers (PostProcessing yazmıyor). #65: ayar aralığı Min 55 (Max 110 korundu, kayıtlı ayarlar bozulmaz).
