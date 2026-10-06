# HAREKÂT: Gerçekçilik Planı (PUBG / CoD seviyesi)

> Sahibi: Teknik Sanat (lead technical artist) · Tarih: 6 Ekim 2026 · Durum: **Kesin plan v1**
> Girdi: dört araştırma raporu (teknikler, pipeline, varlıklar, Türk askeri varlıkları) + kod tabanı taraması.
> Bu belge hukuki tavsiye değildir. Fiyatlar araştırma anındaki listelemelerdir, satın almadan önce ürün sayfasında doğrulanmalıdır.

---

## 1. Yönetici özeti ve pipeline kararı

### 1.1 Tek cümlelik özet

PUBG/CoD görüntüsünün yaklaşık %80'i **içerikten** (taranmış PBR dokular, gerçek 3B modeller, mocap animasyon, kayıtlı ses), kalan %20'si **ışık + post-process + his** işçiliğinden gelir. Kodla üretilen low-poly görünüm bu seviyeye tek başına çıkamaz. Plan bu yüzden iki hatlı:

1. **Kod hattı (Claude ajanları, hemen):** URP 17.6 ayarları (Forward+, GPU Resident Drawer, STP, APV), renk/atmosfer, viewmodel hissi, katmanlı silah sesi, ContentOverrides'ın genişletilmesi ve doğrulama araçları.
2. **İçerik hattı (Cursor, Unity içinde):** önce ücretsiz CC0/Fab içerik (arazi dokusu, kaya, bitki, Mixamo animasyon, Sonniss ses), sonra düşük bütçeli paketler (silah, asker), en son TSK'ya özgü özel modeller (MPT-76, Kirpi, T-70).

Kazanılacak ilk somut hedef: **Kuzgun Köyü dikey dilimi** (bölüm 5). Bu dilim 60 FPS'te PUBG/CoD ekran görüntüleriyle yan yana konduğunda "aynı lig" görünmeden diğer haritalara yayılmayacağız.

### 1.2 Karar: **URP'de kalıyoruz. HDRP'ye geçmiyoruz.**

| Gerekçe | Açıklama |
|---|---|
| Unity'nin yönü | 2026 render pipeline stratejisinde HDRP **bakım moduna** alındı: yeni özellik yok. Tüm yeni yatırım (fiziksel ışık birimleri, fiziksel gökyüzü, gerçek zamanlı GI/SCGI, SSR) URP'ye gidiyor. HDRP'ye geçmek donmuş bir dala geçmek demek. ([Unity stratejisi](https://unity.com/topics/render-pipelines-strategy-for-2026), [80.lv](https://80.lv/articles/unity-unveils-2026-render-pipelines-strategy/), [forum](https://discussions.unity.com/t/render-pipelines-strategy-for-2026/1710004/9)) |
| Performans hedefi | 40-60 oyuncu, 1 km harita, orta PC'de 60 FPS. HDRP'nin CPU/GPU taban maliyeti bu hedefle çelişiyor. HDRP'nin asıl üstünlükleri (SSGI, ray tracing) hedef donanımda zaten kapalı kalırdı. ([karşılaştırma](https://discussions.unity.com/t/fps-comparison-for-built-in-urp-hdrp/944587), [takas](https://discussions.unity.com/t/hdrp-vs-urp-what-is-the-tradeoff/874512)) |
| Viewmodel | URP'de camera stacking hazır ve `CameraRig` zaten Overlay silah kamerası kullanıyor. HDRP'de stacking yok, Custom Pass ile AO/TAA/motion vector sorunları çıkıyor. ([URP stacking](https://docs.unity3d.com/Manual/urp/cameras/camera-stacking-concepts.html), [HDRP FOV sorunu](https://discussions.unity.com/t/override-camera-fov-for-arms-gun-in-first-person-mode-hdrp/1595075)) |
| Göç maliyeti | ~130k satır prosedürel kod: `MaterialLibrary`, `ProceduralPbr`, `AssetGeneration`, `SceneBuilder`, su/gökyüzü/bitki shader'ları, tüm ışık değerleri (lux/EV kalibrasyonu). Haftalarca iş, kazanç belirsiz. |
| Asıl darboğaz | İçerik. HDRP modellerin, dokuların, animasyonun yerini tutmaz. |

**HDRP'yi yeniden açma koşulu:** Hedef kitle RTX sınıfı yüksek donanıma taşınırsa ve "sinematik ürün" kararı alınırsa. O durumda pipeline raporundaki 14 maddelik kontrol listesi ayrı dalda uygulanır, 2. haftada orta PC'de 60 FPS yoksa göç durdurulur.

**Unity sürümü:** Proje 6000.6.4f1 (LTS değil). Geliştirmeye 6.6'da devam ediyoruz (URP SSR preview'unu deneyebilmek için). Yayın öncesi bir sonraki LTS'e (6.7 LTS bekleniyor) geçiş planlanır. SSR ve SCGI preview/duyuru aşamasında, **üretim planı bunlara bağlanmaz**; geldiklerinde "Ultra" kalite katmanı olarak eklenir. ([SSR preview](https://discussions.unity.com/t/preview-of-screen-space-reflections-for-urp/1721494), [yol haritası](https://www.strayspark.studio/blog/unity-6-4-to-6-8-roadmap-indie-developers-2026))

### 1.3 Performans hedefleri (bağlayıcı kabul ölçütleri)

Referans orta PC: **GTX 1660 Super / RTX 3050 / RX 6600**, Ryzen 5 3600 / i5-10400, 16 GB RAM, 1080p.

| Ölçüt | Hedef (Orta ön ayar) | Not |
|---|---|---|
| Kare süresi | **≤ 16,6 ms (60 FPS), %1 low ≥ 45 FPS** | Kuzgun Köyü içinde, 40 bot aktif, T-70 iniş anı dahil |
| CPU ana iş parçacığı | ≤ 10 ms (render ≤ 4 ms) | Ağ, AI, fizik payı korunmalı |
| GPU | ≤ 14 ms | STP ile iç çözünürlük %77-85 |
| SetPass / draw call | SetPass ≤ 400, batch ≤ 2.500 | GPU Resident Drawer + SRP Batcher sonrası |
| VRAM | ≤ 4,5 GB (Orta), ≤ 7 GB (Ultra) | Mipmap streaming açık |
| Sahne üçgeni | ≤ 4-6 M üçgen/kare (Orta) | LOD disiplini ile |
| Yükleme | Harita yükleme ≤ 25 sn (SSD) | |

Kalite ön ayarları (Düşük/Orta/Yüksek/Ultra) mevcut 4 kademeli `AssetGeneration` + `PerformanceProfile` yapısı üzerinde ayrı ayrı ölçeklenir: render scale, gölge mesafesi/cascade, SSAO, bitki yoğunluğu/mesafesi, doku kalitesi, LOD bias, volumetrik sis.

> Unity bu ortamda çalıştırılamıyor (yalnız csc ile derleme kontrolü). Bütün sayısal hedefler Windows referans PC'de Cursor tarafından Profiler + Frame Debugger ile doğrulanmalıdır.

---

## 2. Gerçekçilik sütunları (etki / efor sıralı)

Mevcut durum (kod taramasından): `PostProcessing.cs` zaten ACES, Bloom, renk ayarı, Vignette, Film Grain, Gaussian DoF (ADS) ve CameraOnly Motion Blur kuruyor. `AssetGeneration.cs` 4 kalite kademesinde SSAO ve Decal renderer feature'larını Orta+ için açıyor ve MSAA kullanıyor. `CameraRig.cs` Overlay silah kamerası (60° sabit FOV). **Eksik olanlar:** Forward+, GPU Resident Drawer, GPU occlusion culling, STP, APV, reflection probe stratejisi, gerçek doku/model/ses içeriği. Plan buna göre "sıfırdan" değil, **delta** üzerine kuruludur.

| Sıra | Sütun | Etki | Efor | Kim | Neden bu sırada |
|---|---|---|---|---|---|
| 1 | **Işık & post-process 2.0** | Çok yüksek | Düşük-orta | Claude + Cursor (bake) | Her pikseli etkiler. Kod çoğunlukla hazır, ince ayar ve eksik özellikler kaldı |
| 2 | **Arazi & bitki örtüsü** | Çok yüksek | Orta | Cursor (içerik) + Claude (LOD/override) | BR'da oyuncunun %70 gördüğü şey 100-300 m arazi, ağaç, kaya |
| 3 | **Ses** | Yüksek (algı) | Düşük-orta | Claude (katmanlama) + Cursor (Sonniss klipleri) | CoD'un "gerçek" hissinin yarısı ses. Ucuz ve hızlı ([Activision ses](https://blog.activision.com/call-of-duty/2019-07/Modern-Warfare-Initial-Intel-Creating-an-Orchestra-of-Incredible-Audio-Effects-Weapon-Sounds-in-Call-of-Duty-Modern-Warfare)) |
| 4 | **Silah / viewmodel** | Yüksek | Orta | Claude (his) + Cursor (model/kol) | Ekranda sürekli. Sway/recoil kodda ucuz, model paketle gelir |
| 5 | **Materyaller / texel density** | Çok yüksek | Orta-yüksek | Claude (MaterialSpec) + Cursor (doku) | Düz renk yerine taranmış PBR, detay normal, makro varyasyon |
| 6 | **Karakter & animasyon** | En yüksek (yakında) | Yüksek | Cursor (rig/anim) + Claude (LOD/culling) | 40-60 oyuncuda LOD ve animasyon bütçesi kritik |
| 7 | **VFX & decal** | Orta-yüksek | Düşük-orta | Claude | `GameVfx`, `DecalPool` mevcut, kalite ve varyasyon artırılacak |
| 8 | **Araçlar** | Orta | Orta (satın alma) | Cursor + Claude (soket/rotor) | Kısa ekranda kalır ama "TSK kimliği" için önemli |

### 2.1 Işık & post-process (sütun 1)

- **Rendering path:** Forward+ (GPU Resident Drawer yalnızca Forward+ ile çalışır). Namlu flaşı, fener, patlama, araç farı için ışık sınırı kalkar. ([GRD dokümanı](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html), [path karşılaştırması](https://docs.unity3d.com/Manual/urp/rendering-paths-comparison.html))
- **GI:** Adaptive Probe Volumes (APV) + güneş için Mixed/Baked. Dinamik nesneler (asker, araç) probe'lardan tutarlı ışık alır, lightmap UV işinden kurtuluruz. Gün döngüsü varsa APV Scenario Blending. ([Unity 6 özellikleri](https://unity.com/blog/engine-platform/unity-6-preview-release), [80.lv](https://80.lv/articles/unity-6-a-deep-dive-into-the-update-s-new-features-enhancements/))
- **Gölge:** 4 cascade, Orta 120 m / Yüksek 180 m / Ultra 250 m, soft shadow (Orta: Medium). Split oranı yakına ağırlıklı (≈ %6 / %18 / %42). Viewmodel için gölge alma açık, gölge atma kapalı.
- **Yansıma:** Köy başına 2-4 baked Reflection Probe (box projection iç mekânda), harita geneli bir sky probe. SSR, 6.7'de "Ultra" katmanı.
- **Post (hedef görünüm "çöl-step askerî gerçekçilik"):** ACES (veya Neutral + LUT), hafif kontrast +8-12, doygunluk -10, gölgeye hafif soğuk/ışığa sıcak (Split Toning / Shadows-Midtones-Highlights), Bloom eşik ≥ 1,1, Vignette ≤ 0,25, Film Grain çok hafif, Motion Blur **varsayılan kapalı**, DoF yalnızca ADS/dürbün/ölüm kamerası. ([URP post özet](https://uhiyama-lab.com/en/notes/unity/unity-post-processing-guide/))
- **Atmosfer:** Yükseklik sisi + aerial perspective (uzak dağların maviye/griye kayması), güneş yönünde Mie hâlesi. Hacimsel ışık için açık kaynak [Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) değerlendirilir (yalnız Yüksek/Ultra, yarım çözünürlük).
- **Upscaler:** STP (Orta %77, Yüksek %87, Ultra %100). STP açıkken MSAA kapalı. FSR/DLSS sonraki adım.

### 2.2 Arazi & bitki örtüsü (sütun 2)

- **Arazi:** Unity Terrain (zaten `TerrainGenerator` TerrainData üretiyor). 6-8 Terrain Layer (kuru çimen, yeşil çimen, toprak, çamur yol, çakıl, kaya, kar), her biri albedo + normal + mask (AO/height/smoothness) 2K. Height-based blend, düşük frekanslı makro renk varyasyonu (tiling kırma), yamaçta kaya katmanı (eğime göre `TerrainPainter`). Basemap distance Orta'da 120 m.
- **Ağaç:** LOD0 tam mesh (≤ 8-15k üçgen), LOD1 yarı, LOD2 kart kümesi, LOD3 billboard/impostor. Alpha-to-coverage yaprak kenarı, rüzgâr vertex animasyonu. Anadolu türleri: kızılçam, karaçam, meşe, ardıç, kavak (dere kenarı).
- **Çimen/çiçek:** Terrain detail (GPU instanced), Orta'da 60 m'ye kadar, yoğunluk kademeli. Megaplants çimende değil ağaç/çalıda.
- **Kaya:** Taranmış kaya, LOD0-LOD3 + basit collider. Toprağa gömülme için arazi rengine karışan alt bant (decal veya vertex color).
- **Decal:** yol kenarı çamur, duvar dibi kir, yosun. Terrain'de değil, prop/bina üzerinde.

### 2.3 Ses (sütun 3)

CoD MW: silah başına ~90 mikrofon, atış gerçek zamanlı katmanlanıyor (bang, mekanik, thump, çevresel tail, kovan) + reverb/slap-delay + yansıma sistemi ([Activision](https://blog.activision.com/call-of-duty/2019-07/Modern-Warfare-Initial-Intel-Creating-an-Orchestra-of-Incredible-Audio-Effects-Weapon-Sounds-in-Call-of-Duty-Modern-Warfare), [A Sound Effect](https://www.asoundeffect.com/?p=388201), [MW2 reverb](https://www.sportskeeda.com/esports/3-audio-improvements-look-modern-warfare-2-new-reverb-engine-improved-3d-directionality)). Bizim karşılığımız:

- Atış = **5 katman**: yakın crack/bang, mekanik (bolt/foley), düşük frekans thump, tail (dış: açık alan / iç: oda / vadi: uzun yankı), uzak sürüm (`ShotDistantMid/Far` zaten var). Mesafeye göre çapraz geçiş.
- **Süpersonik crack + whiz** (mermi yakından geçince) ayrı katman. `BulletWhiz` var, `BulletCrack` eklenecek.
- **Occlusion:** dinleyici-kaynak raycast, engelde low-pass + kısma. İç/dış tespiti (tavan raycast) ile tail seçimi ve reverb filtresi.
- Yüzeye göre ayak, isabet ve **kovan düşüş** sesi. Pitch/volume rastgeleleştirme `SoundOverrideEntry`'de zaten var.
- Prosedürel sentez yedek olarak kalır; Sonniss kayıtları override ile önceliklidir.

### 2.4 Silah / viewmodel (sütun 4)

- Spring-damper sway (bakış gecikmesi), bob (yürüme/sprint), ADS geçiş eğrisi, nefes (dürbünde), duvara yaklaşınca silahı indirme.
- Recoil: kamera tepmesi (oyuncunun gördüğü sapma, `RecoilPattern`) ile silah modelinin görsel tepmesi (kick, roll, geri itme) **ayrı yaylar**; toparlanma ayrı. ADS/hipfire ölçeklemesi.
- Viewmodel FOV ayarı (55-70), ana FOV'dan bağımsız (Overlay kamera zaten bunu sağlıyor).
- Sol el IK: override prefab'ının `Grip_L` soketi. Animation Rigging Two Bone IK.
- Animasyon seti (silah paketinden veya mocap): draw, holster, reload (taktik/boş), inspect, sprint, şarjör kontrolü.
- VFX: namlu flaşı (atış başına rastgele 3-4 varyant, 1-2 kare), kısa ömürlü nokta ışık (`FlashLightPool`), namlu dumanı, kovan atma (`ShellCasingPool`), ısınma pusu (Ultra).

### 2.5 Materyaller / texel density (sütun 5)

Standart (CoD MW ≈ 64 px/inç ≈ 25 px/cm referansıyla, bizim bütçeye uyarlanmış) ([Activision fotogrametri](https://blog.activision.com/call-of-duty/2019-06/Initial-Intel-How-Photogrammetry-is-Helping-to-Shape-Call-of-Duty-Modern-Warfare-into-a-new-high-watermark-for-graphics-in-gaming), [GamesBeat](https://gamesbeat.com/call-of-duty-modern-warfares-photogrammetry-captures-gritty-realism-like-never-before/)):

| Sınıf | Texel density | Doku boyutu | Ek |
|---|---|---|---|
| Viewmodel silah + kollar | 2048 px/m (≈ 20 px/cm) | 2K (Ultra 4K) | Detay normal (mikro çizik), clear-coat yok |
| Asker (3. kişi) | 1024 px/m | 2K gövde, 1K teçhizat | Kamuflaj deseni ayrı tile'lı detay |
| Araç | 512-1024 px/m | 2K, trim sheet | Kir/çamur maskesi |
| Bina / prop | 512 px/m | 1-2K tiling + trim sheet | Kir decal, AO |
| Arazi | 256-512 px/m + detay | 2K layer | Makro varyasyon 1 adet 2K |

Kural: tüm materyaller URP/Lit (SRP Batcher uyumlu), ORM paketleme tek tipte, mipmap streaming açık, normal haritalar BC5, albedo BC7 (PC).

### 2.6 Karakter & animasyon (sütun 6)

- Üçgen bütçesi: LOD0 15-25k (yalnız ≤ 15 m), LOD1 ~8k (≤ 40 m), LOD2 ~3k (≤ 120 m), LOD3 ~800 / impostor. Eski CoD4 verisi LOD0 ~4,5k idi, bugün katı sınır LOD ile çözülüyor ([Polycount](https://polycount.com/discussion/comment/847286)).
- Animator: `cullingMode = CullUpdateTransforms`, 60 m ötesi güncelleme frekansı 1/2, 150 m ötesi 1/4. GPU skinning açık.
- Teçhizat katmanı (yelek, kask, çanta, NVG) ayrı mesh + rütbe/rol aksesuarları (telsizci anteni, komutan beresi). TSK dijital kamuflaj dokusunu **kendimiz** üreteceğiz (hazır paket yok).
- Mixamo animasyonları (yürüme, koşma, çömelme, yüzüstü, ölüm, paraşüt) Humanoid rig ile `SoldierOverrideEntry.animatorController`'a. ([Mixamo lisansı](https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400))

### 2.7 VFX & decal (sütun 7)

- `DecalPool` FIFO sınırı: Orta 64, Yüksek 128, Ultra 256 decal. Yüzey türüne (`SurfaceKind`) göre mermi izi atlası, kan, yanık, çamur.
- İsabet efekti: toz (toprak), kıvılcım (metal), kıymık (ahşap), su sıçraması. Flipbook dokular CC0'dan veya kodla üretilmiş.
- Patlama: şok halkası + toz + ateş topu + kalıcı yanık decal + kısa ışık.
- Helikopter iniş tozu (rotor downwash) Kuzgun Köyü dilimi için şart.

### 2.8 Araçlar (sütun 8)

LOD0 40-60k üçgen, tekerlek/jant ayrı mesh, rotor ayrı pivot, iç mekân yalnız kokpit görünürse. Hasar durumu (temiz / kirli / yanmış) materyal swap ile.

---

## 3. Varlık listesi

### 3.1 ContentOverrides slotları (mevcut + eklenecek)

Mevcut (`Infrastructure/Content/ContentOverrideEntries.cs`): `WeaponOverrideEntry(weaponId, prefab, Muzzle/Grip_R/Grip_L)`, `SoundOverrideEntry(SoundId, clips[], volume/pitch aralığı)`, `MaterialOverrideEntry(MaterialId, material)`, `SoldierOverrideEntry(humanoidPrefab, animatorController)`, `VehicleOverrideEntry(vehicleId: kirpi/cobra/t70, prefab)`, `BuildingOverrideEntry(BuildingStyle, prefabs[])`.

**Eklenecek (görev C7):** `TerrainLayerOverrideEntry(MaterialId/katman → TerrainLayer)`, `VegetationOverrideEntry(tür → prefab[] + LODGroup)`, `RockOverrideEntry(boyut sınıfı → prefab[])`, `PropOverrideEntry(prop id → prefab)`, `ViewmodelArmsOverrideEntry(kollar prefab + eldiven materyali)`, `WeaponAnimationOverrideEntry(weaponId → AnimatorOverrideController)`, `SkyOverrideEntry(HDRI cubemap + exposure)`, `DecalSetOverrideEntry(SurfaceKind → decal materyali)`.

### 3.2 Lisans kuralları (özet)

| Kaynak | Lisans | Ticari/Steam | Atıf | Kural |
|---|---|---|---|---|
| [Poly Haven](https://polyhaven.com/faq) | CC0 | Evet | Hayır | Serbest |
| [ambientCG](https://licenseorg.com/guide/3d-assets/ambientcg) | CC0 | Evet | Hayır | Serbest |
| Fab / Megascans | Fab Standard | Evet, tüm motorlar | Hayır | **Yalnız Fab Standard altında edinilenler.** Eski "Unreal'e özel" indirmeler Unity'de kullanılmaz ([geçiş rehberi](https://www.strayspark.studio/blog/quixel-to-fab-migration-indie-developer-survival-guide-2026), [Fab SSS](https://support.fab.com/s/article/Fab-Transition-FAQs?language=en_US)) |
| [Unity Asset Store](https://unity.com/legal/as-terms) | Asset Store EULA | Evet (gömülü) | Genelde hayır | Ham dosya dağıtılmaz, AI eğitimi yasak, paket içi ThirdPartyNotices okunur |
| Mixamo | Adobe ücretsiz | Evet | Hayır | Ham dosya tek başına dağıtılmaz |
| [Sonniss GDC](https://licenseorg.com/guide/music-audio/sonniss) | Royalty-free | Evet | Hayır | AI eğitimi yasak, ham dağıtım yasak |
| Sketchfab CC-BY | CC BY 4.0 | Evet | **Zorunlu** | Credits ekranı + lisans linki ([Sketchfab](https://help.sketchfab.com/en/articles/16152215-crediting-users-for-3d-model-downloads)) |
| Sketchfab CC-BY-NC | | **Hayır** | | Kullanılmaz |
| CGTrader / TurboSquid | Royalty-free | Evet | Hayır | **"Editorial Only" etiketli model kullanılmaz** (gerçek askerî modellerde sık) ([karşılaştırma](https://licenseorg.com/compare/cgtrader-vs-sketchfab)) |

Her varlık `Assets/ThirdParty/README.md` kaydına girer: ad, URL, lisans, tarih, atıf gereği, hangi slot. Mixamo/Sonniss/Asset Store ham dosyaları herkese açık repoya konmaz (Git LFS + özel repo veya `.gitignore` + paylaşılan sürücü).

### 3.3 Faz A: ücretsiz (0 USD, hemen)

| # | Varlık | Kaynak | Lisans | Slot |
|---|---|---|---|---|
| A1 | Arazi dokuları: kuru çimen, yeşil çimen, toprak, çamurlu yol, çakıl, kaya yüzeyi, kar (2K PBR) | [ambientCG](https://ambientcg.com), [Poly Haven](https://polyhaven.com/textures) | CC0 | `TerrainLayerOverride` (yeni) + `MaterialOverride` Grass/DryGrass/Dirt/Mud/Gravel/Rock/Snow |
| A2 | Yapı dokuları: sıva, taş duvar, kiremit, ahşap, paslı metal, beton | ambientCG, Poly Haven | CC0 | `MaterialOverride` Plaster/PlasterWarm/Stone/RoofTile/Wood/WoodDark/Rust/Concrete/MetalPanel |
| A3 | HDRI gökyüzü: açık gün, parçalı bulut, gün batımı (step/dağ) | [Poly Haven HDRI](https://polyhaven.com/hdris) | CC0 | `SkyOverride` (yeni) + reflection probe kaynağı |
| A4 | Kayalar, kütükler, taş duvar parçaları | Megascans ücretsiz başlangıç koleksiyonu (~1.500 varlık), Poly Haven modelleri | Fab Standard / CC0 | `RockOverride` (yeni), `PropOverride` (yeni) |
| A5 | Ağaç/çalı (çam, meşe, çalı) | **Megaplants** (Fab, ücretsiz) ([Quixel](https://quixel.com/news/quixel-on-fab-new-megascans-and-megaplants)) | Fab Standard | `VegetationOverride` (yeni). **LOD + impostor zorunlu** |
| A6 | Ağaç/çimen/kaya yedek seti | Book of the Dead environment ([80.lv](https://80.lv/articles/book-of-the-dead-is-available-online/)) | Asset Store EULA + ThirdPartyNotices | Aynı slotlar. HDRP kökenli shader'lar URP'ye dönüştürülür |
| A7 | İnsan animasyonları (yürü/koş/çömel/yüzüstü/ölüm/paraşüt/el bombası) | Mixamo | Adobe | `SoldierOverride.animatorController` |
| A8 | Silah, patlama, adım, ortam, araç, helikopter sesleri | [Sonniss GDC 2015-2026](https://rekkerd.org/sonniss-releases-gdc-2026-game-audio-bundle/) | Royalty-free | `SoundOverride` (ShotRifle762, ShotDistantFar, HelicopterRotor, FootstepGrass, ...) |
| A9 | Ek ses (kuş, rüzgâr, köy ortamı) | Freesound (CC0 filtreli) | CC0 | `SoundOverride` Ambience/Wind |
| A10 | Decal: kir, yosun, çatlak, mermi izi | Megascans decal (Fab), ambientCG | Fab Std / CC0 | `DecalSetOverride` (yeni) |

### 3.4 Faz B: düşük bütçe (~150-450 USD)

| # | Varlık | Kaynak | Fiyat (yaklaşık) | Slot | Not |
|---|---|---|---|---|---|
| B1 | **HQ Realistic Weapons Pack #1** | [Asset Store](https://assetstore.unity.com/packages/3d/props/weapons/hq-realistic-weapons-pack-1-357518) | 15 USD | `WeaponOverride` (yerdeki/botların silahları, yakın tipler) | URP + Unity 6000 destekli. İlk test paketi |
| B2 | **Realistic Modular Assault Rifles Pack** | [Unity Marketplace](https://marketplace.unity.com/packages/3d/props/guns/realistic-modular-assault-rifles-pack-266600) | sayfadan doğrula | `WeaponOverride` + aksesuar sistemi | Modüler şarjör/dipçik/nişangâh, ek parça sistemimize en uygun |
| B3 | PBR FPS Weapons Pack 1 Complete Bundle (alternatif) | [Asset Store](https://assetstore.unity.com/packages/3d/props/guns/pbr-fps-weapons-pack-1-complete-bundle-246697) | 105 USD | `WeaponOverride` + `WeaponAnimationOverride` | 18 model, animasyonlu. B2 yetmezse |
| B4 | FPS kolları (eldivenli, animasyonlu) | Silah paketinin kolları veya ayrı "FPS arms" paketi | 0-40 USD | `ViewmodelArmsOverride` (yeni) | Eldiven/kol kumaşına TSK kamuflajı |
| B5 | **Modular Soldier** (Starlight Arts) | [Unity Marketplace](https://marketplace.unity.com/packages/3d/characters/modular-soldier-157932) | sayfadan doğrula (~30-60 USD) | `SoldierOverride.humanoidPrefab` | ~15k poligon, 18 parça. Kamuflaj dokusu TSK desenine çevrilir |
| B6 | Canik TP9 | [CGTrader](https://www.cgtrader.com/3d-models/military/gun/canik-tp9) | sayfadan doğrula | `WeaponOverride` pistol_tp9 | Animasyonlu, düşük poligon |
| B7 | BMC Kirpi oyun modeli | [CGTrader](https://www.cgtrader.com/3d-models/military/military-vehicle/bmc-kirpi-mrap-e99d7767-e820-4613-a147-dd590f7e1194), [RenderHub](https://www.renderhub.com/3d-models/vehicles/military-vehicles) | ~79-150 USD | `VehicleOverride` kirpi | Editorial olmadığını doğrula |
| B8 | **UH-60 (T-70 tabanı)** rigged, LOD0-2 | [CGTrader rigged](https://www.cgtrader.com/3d-models/aircraft/helicopter/uh-60-blackhawk-fully-rigged-animated-game-ready-2-skins) (~59 USD, 16.9k üçgen), [iç mekânlı](https://www.cgtrader.com/3d-models/aircraft/helicopter/uh60-v-black-hawk-full-interior) (~90 USD) | 59-90 USD | `VehicleOverride` t70 | Türk boyası, T-70 burun/anten farkları, işaretler bizden |
| B9 | Otokar Cobra (Cobra II low-poly) | [CGTrader](https://www.cgtrader.com/3d-models/vehicle/military-vehicle/otokar-cobra-ii-armored-vehicle-low-poly-tactical-model) | sayfadan doğrula | `VehicleOverride` cobra | TurboSquid sürümü (179 USD) eski, kalite kontrolü |
| B10 | Cami / taş yapı tabanı | [Middle East Desert City](https://assetstore.unity.com/packages/3d/environments/urban/middle-east-desert-city-12003) | sayfadan doğrula | `BuildingOverride` Mosque | Tek minare + kurşun kubbe ile Türk üslubuna uyarlanır |

Faz B toplam beklenti: **150-450 USD**.

### 3.5 Faz C: özel model / yüksek bütçe

| # | Varlık | Yol | Tahmini maliyet | Slot |
|---|---|---|---|---|
| C1 | **MPT-76 / MPT-76K** (dikey dilim silahı) | Önce: B2/B3'ten AR-10/HK417 tipi gövdeyi MPT-76 siluetine yaklaştırma (geçici). Sonra: referans fotoğraflı özel sipariş | Geçici 0 USD (iç iş). Özel 1.500-3.000 USD ([RocketBrush](https://rocketbrush.com/blog/3d-weapon-vehicle-art-price-what-studios-should-expect)) | `WeaponOverride` ar_mpt76 |
| C2 | MPT-55, KNT-76, JNG-90, PMT-76, MG3, SAR 9, G3A7 | Özel sipariş (hazır AAA model yok; itch.io'daki [MPT-76s](https://kamelionn.itch.io/mpt-78s) 747 üçgen, yetersiz) | 1.500-3.000 USD/silah. TR serbest sanatçılarla belirgin daha düşük ([Upwork TR](https://www.upwork.com/hire/mesh-freelancers/tr/)) | `WeaponOverride` |
| C3 | T-70 doğru model, T-129 ATAK | T-129: [Superhive](https://superhivemarket.com/products/atak-t129-helicopter-ultra-high-textures-changeable-decal-text) 79 USD, [RenderHub/TurboSquid](https://www.turbosquid.com/de/3d-models/3d-t129-atak-green-helicopter-rigged-2041001) 99-199 USD. T-70 özel: 4.000-8.000 USD | 79 USD - 8.000 USD | `VehicleOverride` |
| C4 | TSK asker seti (2-3 üniforma, teçhizat varyasyonu) | Özel karakter siparişi veya B5 + iç doku işi | Fiyat verisi yok, tahmin 3.000-8.000 USD | `SoldierOverride` |
| C5 | Anadolu köyü modüler seti (taş-ahşap ev, kırma çatı, kiremit, ahır, ağıl) | Blender'da iç iş (A2 dokuları + trim sheet) veya sipariş | 0 / 2.000-5.000 USD | `BuildingOverride` VillageHouse, TwoStoryHouse, Barn, Shed, ShepherdHut |

Faz C toplam (tam set): **~15.000-40.000 USD**. TR serbest sanatçılarla ve toplu siparişle aşağı çekilebilir. Kalite ve portföy kontrolü şart. Dikey dilim için yalnızca C1 (MPT-76) ve isteğe bağlı C5'in Kuzgun Köyü parçası gerekir.

### 3.6 Gerçek Türk silah/araç adları: hukuki notlar

Bu bir hukuki görüş değildir. Yayın öncesi bir Türk fikri mülkiyet avukatından yazılı görüş alınmalıdır.

- **Emsal:** AM General v. Activision (Humvee) davasında ABD mahkemesi gerçekçi tasviri ifade özgürlüğü kapsamında saydı ([Steptoe](https://www.steptoe.com/en/news-publications/sdny-blog/complaint-call-of-duty-video-game-infringes-trademarks-by-pervasively-featuring-humvees.html), [Georgetown](https://freespeechproject.georgetown.edu/?p=8329)). EA 2013'te silah üreticisi lisanslarını bıraktı ama silahları tuttu ([Engadget](https://www.engadget.com/2013-05-08-ea-kills-licensing-deals-with-gun-makers-keeps-those-guns-in-ga.html)). Colt CZ'nin bir silah görünümünü AB markası yapma girişimini EUIPO reddetti ([Lewis Silkin](https://www.lewissilkin.com/insights/2023/11/16/it-was-all-gun-and-gamesuntil-the-euipo-rejected-an-application-to-register-the-102isrc)). **ABD emsali Türkiye/AB'de bağlayıcı değil.**
- Kod tabanı bugün gerçek adları kullanıyor (`WeaponCatalog`/`ItemCatalog`: "MPT-76", "SAR 9", "JNG-90", "Canik TP9", "Escort"; araçlar Kirpi/Cobra/T-70). Plan:
  1. **İsim profili katmanı** (görev C15): `DisplayName` doğrudan dize yerine yerelleştirme anahtarına bağlanır. İki profil: `Gercek` (geliştirme/iç test) ve `Kurgusal` (örnek: MPT-76 → "MT-76 Piyade Tüfeği", Kirpi → "Kirpi-tipi MKKA" gibi bir kademe uzak adlar). Avukat görüşüne göre yayında biri seçilir, kod değişmez.
  2. Modellerde ve dokularda **gerçek logo, marka yazısı, üretici damgası yok** (MKE, Sarsılmaz, Canik, BMC, Otokar, TUSAŞ). Seri no/damga alanları jenerik.
  3. Hiçbir pazarlama metninde "resmî", "TSK onaylı", "MKE işbirliği" izlenimi yok.
  4. TSK/MSB amblemleri, rütbe işaretleri: ayrı mevzuat kontrolü. Türk Bayrağı (`MaterialId.TurkishFlag`) 2893 sayılı Türk Bayrağı Kanunu ve yönetmeliğine uygun kullanılmalı (yere düşme, yırtılma, hasar efekti, üzerine decal gibi durumlar engellenir).
  5. Resmî lisans opsiyonu: MKE/BMC/Otokar/TUSAŞ ile tanıtım iş birliği görüşmesi (fırsat, zorunluluk değil).
  6. Satın alınan model lisansı yalnızca 3B dosyanın telifini çözer, ürünün marka/tasarım hakkını çözmez. "Editorial Only" modeller ticari oyunda kullanılmaz.

---

## 4. Mühendislik iş listesi

### 4.1 Çalışma kuralları

- **Claude ajanları:** yalnız kod. Her görev dosya-ayrık (aynı dosyaya iki ajan dokunmaz). Doğrulama: `Tools/UnityVerify` csc + Unity DLL derlemesi ve mevcut EditMode test projeleri. Unity açmadan ölçülemeyen her şey "Cursor doğrulaması" olarak işaretlenir.
- **Cursor (Unity eli):** içe aktarma, prefab/LOD kurulumu, bake, profil, ekran görüntüsü. Her teslimde Frame Debugger + Profiler ekran görüntüsü ve `Docs/OYUN_TESTI.md`'ye satır.
- **Altın kural:** Override yoksa prosedürel yedek her zaman çalışır (mevcut ContentOverrides davranışı). Hiçbir görev bu yedeği kırmaz.

### 4.2 Claude paralel görevleri (dalga G3)

| ID | Modül / dosyalar | İş | Kabul ölçütleri |
|---|---|---|---|
| **C1 Pipeline** | `Editor/AssetGeneration.cs`, `Infrastructure/Rendering/PerformanceProfile.cs`, `RenderPipelineInfo.cs` | Renderer'ı **Forward+**'a al; **GPU Resident Drawer (Instanced Drawing)** + GPU Occlusion Culling (Orta+); **STP** upscaling ve render scale tablosu (D %67, O %77, Y %87, U %100); STP açıkken MSAA kapalı; Light Probe System = **APV**; soft shadow kalitesi; 4 cascade ve split oranları; SRP Batcher zorunlu; mipmap streaming bütçesi | Derleme temiz. 4 kademenin tablosu kodda tek yerde. Reflection ile ayar yoksa uyarı logu, çökme yok. `RenderPipelineInfo` mevcut değerleri raporluyor |
| **C2 Post 2.0** | `Rendering/PostProcessing.cs`, `RuntimeGlobalVolume.cs`, `ScreenEffectsMath.cs` | Shadows-Midtones-Highlights + Split Toning + White Balance ile "Anadolu askerî" grade; harita/saat başına preset (`AtmospherePresets` ile); Motion Blur varsayılan kapalı; SSAO parametreleri kademe başına (radius, falloff, downsample); ADS ve ölüm kamerası DoF eğrileri | Ayarlar menüsünden motion blur/DoF/grain kapatılabilir. Preset değişimi kare içinde ani sıçrama yapmadan 0,5 sn blend. EditMode testleri `ScreenEffectsMath` için |
| **C3 Atmosfer** | `Rendering/Atmosphere.cs`, `RenderSettingsUtil.cs`, su-gökyüzü shader'ları | Yükseklik sisi + aerial perspective; güneş Mie hâlesi; HDRI `SkyOverride` varsa skybox + ortam ışığını ondan al, yoksa prosedürel; volumetrik ışık entegrasyon noktası (Yüksek/Ultra, kapatılabilir) | 400 m+ dağların renk sönümü görünür (Cursor ekran görüntüsü). Override yokken bugünkü görünüm bozulmuyor |
| **C4 Materyal sistemi** | `Rendering/MaterialLibrary.cs`, `MaterialSpec.cs`, `ProceduralPbr.cs` | `MaterialSpec`'e detay normal/albedo, triplanar bayrağı, makro varyasyon, texel density alanı; `MaterialOverride` önceliği ve SRP Batcher uyumu; tüm prosedürel dokulara ORM paketleme tutarlılığı | Override verilen her `MaterialId` sahnede override materyaliyle çiziliyor. Yeni alanlar override yoksa varsayılan. Shader property adları URP/Lit ile birebir |
| **C5 Arazi** | `World/TerrainGenerator.cs`, `TerrainPainter.cs`, `TerrainTextureFactory.cs` | `TerrainLayerOverride` desteği (6-8 katman), eğim/yükseklik/yol maskesine göre boyama, makro varyasyon katmanı, basemap mesafesi kademe başına, çimen/çiçek detail yoğunluğu ve mesafesi kademe başına | Override katmanları gelince otomatik kullanılıyor. 1 km harita üretimi süresi bugünkünden ≤ %20 uzun. Detail mesafe/yoğunluk ayarlardan değişiyor |
| **C6 Bitki & kaya** | `World/TreeFactory.cs`, `TreeScatter.cs`, `RockFactory.cs`, `RockScatter.cs`, `VegetationMaterials.cs` | `VegetationOverride` ve `RockOverride`'dan tür bazlı prefab seçimi; LODGroup yoksa otomatik uyarı + prosedürel LOD; impostor/billboard mesafesi kademe başına; rüzgâr parametreleri; GPU Resident Drawer uyumlu (statik, aynı materyal) yerleşim | Override'lı ağaç sahnede LOD geçişleriyle görünüyor. Kaya ve ağaç collider'ları bot NavMesh'i bozmuyor (`NavMeshBaker` testi) |
| **C7 ContentOverrides v2** | `Content/ContentOverrideEntries.cs`, `ContentOverrides.cs`, `ContentIds.cs`, `Editor/ContentOverridesSetup.cs` | 3.1'deki yeni slotlar; isim kuralıyla otomatik doldurma (`HK_W_ar_mpt76`, `HK_V_t70`...); **doğrulayıcı:** soketler (Muzzle, Grip_R, Grip_L, Magazine, Bolt, Sight), ölçek (1 birim = 1 m), pivot, LODGroup, üçgen bütçesi, materyal shader'ı URP/Lit mi, lisans kaydı var mı (`Assets/ThirdParty/README.md`) | Editor menüsü tek tıkla rapor üretiyor: her slot için OK/UYARI/HATA. Eski asset dosyası bozulmadan yükleniyor (serileştirme geriye uyumlu) |
| **C8 Viewmodel hissi** | `Weapons/WeaponViewModel.cs`, `WeaponViewModel.Actions.cs`, `Rendering/CameraRig.cs`, `Application/Services/RecoilPattern.cs` (yalnız okuma) | Spring-damper sway/bob/ADS; kamera tepmesi ile model tepmesi ayrı yaylar; viewmodel FOV ayarı; duvar yakınında silah indirme; override prefab için `Grip_L` IK hedefi (Animation Rigging varsa Two Bone IK, yoksa Animator IK); `WeaponAnimationOverride` ile Animator sürme | 60/144/240 FPS'te aynı his (kare bağımsız). ADS süresi silah verisinden. Override yokken prosedürel viewmodel aynen çalışıyor |
| **C9 Asker** | `Characters/SoldierModel.cs`, `SoldierLook.cs`, `SoldierDetailRules.cs` | Humanoid override yolu: LOD kuralları, Animator culling + mesafeye göre güncelleme frekansı, kamuflaj `MaterialId.Camo*` swap, rol aksesuarı soketleri (kask, yelek, anten, bere), ragdoll uyumu | 60 asker sahnesinde animasyon CPU süresi ≤ 2 ms (Cursor ölçümü). Rol aksesuarları override modelde doğru kemiğe bağlanıyor |
| **C10 VFX** | `Vfx/VfxEffectLibrary*.cs`, `GameVfx*.cs`, `DecalPool.cs`, `VfxDecalVariants.cs`, `VfxQuality.cs` | Namlu flaşı varyantları, namlu dumanı, `SurfaceKind` başına isabet seti, decal FIFO limitleri kademe başına, `DecalSetOverride` desteği, helikopter rotor tozu efekti | Kademe başına aktif parçacık/decal sayısı sınırı test ediliyor. Rotor tozu yere 15 m'den yakınken çıkıyor |
| **C11 Ses katmanları** | `Audio/GameAudio.cs`, `SoundId.cs`, `AudioPool.cs`, `AudioQuality.cs`, `FootstepEmitter.cs` | `SoundId` sonuna ekleme (sıra korunur): `ShotMech*`, `ShotThump`, `ShotTailOutdoor/Indoor/Valley`, `BulletCrack`, `ShellDropGrass/Concrete/Metal`; mesafeye göre katman çapraz geçişi; raycast occlusion (low-pass + kısma, kare başına sınırlı ray); iç/dış tespiti ile tail ve `AudioReverbFilter` seçimi | Tek atışta ≤ 5 ses kaynağı. 60 oyunculu çatışmada eşzamanlı ses ≤ 48 (öncelik sistemi). Override yokken prosedürel sentez yedeği çalışıyor |
| **C12 Araç soketleri** | `Transport/HelicopterModel.cs`, `KirpiModel.cs`, `Vehicles/*ModelBuilder.cs`, `TransportFactory.cs` | Override prefab sözleşmesi: `Rotor_Main`, `Rotor_Tail`, `Wheel_FL/FR/RL/RR`, `Door_*`, `Seat_*`, `Light_*`; rotor bulanık disk geçişi; temiz/kirli/yanmış materyal durumu | Override T-70'te rotor dönüyor, oturma noktaları doğru, iniş/kalkış animasyonu bozulmuyor |
| **C13 Ölçüm** | `Presentation/*` (yeni HUD), `Editor/BuildTool.cs`, `Tools/UnityVerify` | Kare süresi/CPU/GPU/draw call yerleşik HUD (F3), Kuzgun Köyü sabit kamera güzergâhı ile **benchmark modu** (`-benchmark` argümanı, CSV çıktı) | Cursor tek komutla CSV alıyor. CSV'de ortalama, %1 low, en kötü kare |
| **C14 Credits** | `Assets/ThirdParty/README.md` biçimi, yeni `Resources/Credits.json`, credits ekranı | Lisans kaydı şeması, CC-BY otomatik credits satırı, ship öncesi lisans denetim raporu (CC-BY-NC / Editorial yakalama) | Kayıtsız varlık C7 doğrulayıcısında HATA. Credits ekranı JSON'dan dolu |
| **C15 İsim profili** | `Application/Catalogs/WeaponCatalog.cs`, `ItemCatalog.cs`, yerelleştirme tabloları | `DisplayName` → yerelleştirme anahtarı; `Gercek`/`Kurgusal` isim profilleri; araçlar için de aynı | Profil değişimi tek ayar. Tüm UI, killfeed, envanter, web portalı aynı kaynağı kullanıyor |

Paralellik: C1-C15 dosya-ayrık. Yalnız C7 diğerlerinin kullandığı slot tiplerini tanımlar, bu yüzden **C7 ilk saatte arayüzü (sınıf iskeletleri) commit eder**, diğerleri ona göre ilerler.

### 4.3 Cursor (Unity içinde) görevleri

| ID | İş | Kabul ölçütleri |
|---|---|---|
| **U1 Pipeline doğrulama** | C1 sonrası URP asset'lerini yeniden üret; Forward+/GRD/STP/APV'nin gerçekten açık olduğunu Frame Debugger'da doğrula; GRD'nin Terrain tree/detail ile etkileşimini ölç | 4 kademe için Frame Debugger ekran görüntüsü. GRD ile batch sayısı önce/sonra |
| **U2 Arazi dokuları** | A1 dokularını Terrain Layer olarak içe aktar (2K, BC7/BC5), A2'yi URP/Lit materyal yap, `ContentOverrides.asset`'e bağla | Kuzgun Vadisi'nde tiling tekrarı 50 m'den fark edilmiyor. Ekran görüntüsü önce/sonra |
| **U3 Kaya & bitki** | A4/A5/A6 içe aktar; her ağaç için LOD0-3 + billboard; kaya LOD + basit collider; prefab isim kuralı | Ağaç başına LOD0 ≤ 15k üçgen. Override doğrulayıcı (C7) temiz |
| **U4 HDRI & ışık bake** | A3 HDRI'ları skybox/ortam olarak bağla; Kuzgun Köyü için APV volume + 2-4 reflection probe; gündüz senaryosu bake | Bake ≤ 30 dk. Köy içi gölge altı karakterler "parlamıyor" (probe ışığı doğru) |
| **U5 Silahlar** | B1/B2 içe aktar; soketleri (Muzzle, Grip_R/L, Magazine, Bolt, Sight) yerleştir; MPT-76 geçici modeli (B2 gövdesinden siluet uyarlama); kollar (B4) | MPT-76 override ile elde, ADS'de nişangâh merkezli, reload animasyonu oynuyor |
| **U6 Asker** | B5 içe aktar, Humanoid avatar; TSK dijital kamuflaj dokusunu üret (2K tile, 3 renk ailesi); Mixamo animasyonları (A7) ile Animator Controller; LOD0-3 | Botlar override askeriyle koşuyor, çömeliyor, ölüyor. LOD geçişleri 15/40/120 m |
| **U7 T-70** | B8 içe aktar; Türk boyası + jenerik işaretler (logo yok); rotor pivotları ve C12 soketleri; LOD0-2 | Dikey dilim iniş sahnesi override helikopterle oynuyor |
| **U8 Sesler** | A8'den kalibre eşleşmesiyle klip seçimi (7.62 tüfek, MG, 9 mm, uzak/yakın), C11 SoundId'lerine bağla; seviye kalibrasyonu (LUFS hedefi) | Atış 50 m / 200 m / 600 m'de farklı duyuluyor. İç mekân tail değişiyor |
| **U9 Kuzgun Köyü binaları** | VillageHouse, TwoStoryHouse, Mosque, Barn, Shed için modüler set (A2 dokularıyla Blender iş veya B10 uyarlaması) | `BuildingOverride` ile köyde ≥ 5 farklı bina tipi, iç mekân girilebilir, NavMesh temiz |
| **U10 Profil kapısı** | Referans PC'de C13 benchmark; 4 kademe CSV; darboğaz raporu | Bölüm 1.3 hedefleri Orta kademede tutuyor. Tutmuyorsa darboğaz listesi ve önerilen düşürme |
| **U11 Görsel karşılaştırma** | PUBG/CoD referans karelerinin (aynı saat, benzer arazi) yanına dilim ekran görüntüleri | Sahip onayı ("aynı lig") |

---

## 5. Dikey dilim ve kilometre taşları

### 5.1 Dikey dilim tanımı

**Kapsam:** Kuzgun Vadisi haritasında **Kuzgun Köyü** (merkez x=-118, z=22, yarıçap 85 m) ve çevresindeki ~300 × 300 m alan (köy yolu, dere kenarı, yamaç ormanı).

| Öğe | Dilimde olması gereken |
|---|---|
| Ortam | CC0/Megascans arazi katmanları, taranmış kayalar, LOD'lu çam/meşe/çalı, çimen ve çiçek örtüsü, ≥ 5 bina tipi (cami dahil), taş duvarlar, kir/yosun decal'ları, HDRI gökyüzü, APV + reflection probe, yükseklik sisi |
| Silah | **MPT-76**: override model, kollar + eldiven, sway/bob/recoil 2.0, reload/draw/inspect animasyonları, 5 katmanlı atış sesi, namlu flaşı/duman/kovan, yüzey başına isabet + decal |
| Asker | **Bir piyade modeli**: TSK dijital kamuflaj, teçhizat (yelek, kask), Mixamo hareket seti, LOD0-3. Botlar aynı modeli kullanır |
| Araç | **T-70**: override model, Türk boyası, rotor dönüşü ve bulanık disk, rotor tozu, iç mekân ses filtresi, iniş + ip/kapıdan çıkış sekansı |
| Ses | Köy ortamı (rüzgâr, kuş, uzak köpek), occlusion, iç/dış tail, bullet crack/whiz, yüzeye göre ayak sesi |
| Oynanış | T-70 ile köy kenarına iniş, 10 kişilik tim + 30 bot (toplam 40), 5 dakikalık çatışma |

**Dilim kabul ölçütleri:**
1. Referans orta PC'de Orta kademe, 1080p: ortalama ≥ 60 FPS, %1 low ≥ 45 FPS (C13 CSV ile).
2. Yüksek kademe RTX 3060 sınıfında ≥ 60 FPS.
3. Prosedürel yedek görünüm hâlâ çalışıyor (override dosyası silinince oyun açılıyor).
4. C7 doğrulayıcı ve C14 lisans denetimi sıfır HATA.
5. Sahibin yan yana karşılaştırmada (U11) onayı.

### 5.2 Kilometre taşları

| Taş | Süre | İçerik | Çıkış kapısı |
|---|---|---|---|
| **M0: Kod temeli** | Hafta 1 | C1, C2, C3, C7 (iskelet + doğrulayıcı), C11, C13. Cursor: U1 | Forward+/GRD/STP/APV aktif, benchmark CSV alınabiliyor, katmanlı ses yolu çalışıyor (prosedürel kliplerle) |
| **M1: Ücretsiz içerik** | Hafta 1-3 | Faz A tamamı. C4, C5, C6, C10. Cursor: U2, U3, U4, U8 | Kuzgun Köyü çevresi taranmış doku/kaya/bitkiyle. Önce/sonra ekran görüntüsü. Orta kademe ≥ 60 FPS |
| **M2: Silah ve asker** | Hafta 2-5 | Faz B (B1, B2/B3, B4, B5, B8). C8, C9, C12, C14, C15. Cursor: U5, U6, U7 | MPT-76 geçici modeli elde, override asker botlarda, T-70 iniş sahnesi |
| **M3: Dikey dilim** | Hafta 5-8 | U9 (köy binaları), U10, U11, cila turu (renk grade, ses miksajı, LOD mesafeleri) | **5.1 kabul ölçütlerinin tamamı.** Kapı geçmezse yayılma yok, darboğaz turu |
| **M4: Özel modeller** | M2'den itibaren paralel, 2-4 ay | Faz C siparişleri (MPT-76 kalıcı, diğer TSK silahları, T-70 doğru model, TSK asker seti, Anadolu köyü seti). Avukat görüşü → C15 isim profili kararı | Her teslim C7 doğrulayıcıdan geçiyor ve dilimde değiştirilebiliyor |
| **M5: Yayılma** | M3 sonrası, 4-6 hafta | Kartal Yaylası, Ayaz Geçidi, Mavi Liman haritalarına aynı içerik + harita özel dokular/HDRI | Her harita için benchmark CSV ve 60 FPS kapısı |
| **M6: Gelecek katmanı** | 6.7 LTS sonrası | URP SSR, SCGI denemesi (ayrı dal), Ultra kademe, DLSS/FSR | Yalnız Ultra'da, Orta bütçeye dokunmadan |

### 5.3 Bütçe özeti

| Faz | Maliyet | Ne kazandırır |
|---|---|---|
| A (ücretsiz) | 0 USD | Arazi, kaya, bitki, gökyüzü, animasyon, ses: görsel sıçramanın büyük kısmı |
| B (paketler) | 150-450 USD | Gerçekçi silah/asker/araç tabanı, dikey dilimin tamamı |
| C (özel) | 15.000-40.000 USD (TR serbest sanatçıyla daha düşük) | TSK'ya özgü doğru silah/araç/üniforma, Anadolu köyü kimliği |

### 5.4 Riskler

| Risk | Etki | Önlem |
|---|---|---|
| Megascans/Megaplants poligonu yüksek | FPS düşer | U3'te LOD0 ≤ 15k sınırı, impostor zorunlu, C7 bütçe uyarısı |
| GRD ile Terrain tree/detail etkileşimi belirsiz | Beklenen CPU kazancı gelmez | U1'de ölçüm. Gerekirse ağaçları Terrain tree yerine GameObject + GRD ile yerleştir (C6 iki yolu da destekler) |
| TSK silahlarının hazır AAA modeli yok | Dilim gecikir | MPT-76 için geçici siluet uyarlaması (U5), kalıcı model M4'te |
| İsim/marka itirazı | Yayın riski | C15 isim profili + logo yok + avukat görüşü |
| Unity 6.6 LTS değil | Kararlılık | Yayın öncesi 6.7 LTS geçişi, SSR/SCGI'ye bağımlılık yok |
| Bu ortamda Unity çalışmıyor | Kod doğrulaması eksik kalır | Her Claude görevi csc derleme + EditMode testi, her görsel iddia Cursor ekran görüntüsü ile kapanır |

---

## 6. Kaynaklar

**Teknik / pipeline:** [Unity 2026 render pipeline stratejisi](https://unity.com/topics/render-pipelines-strategy-for-2026) · [80.lv strateji](https://80.lv/articles/unity-unveils-2026-render-pipelines-strategy/) · [Forum: strateji](https://discussions.unity.com/t/render-pipelines-strategy-for-2026/1710004/9) · [GameFromScratch: HDRP](https://gamefromscratch.com/the-unity-hdrp-is-dead/) · [StraySpark 6.4-6.8 yol haritası](https://www.strayspark.studio/blog/unity-6-4-to-6-8-roadmap-indie-developers-2026) · [URP SSR preview](https://discussions.unity.com/t/preview-of-screen-space-reflections-for-urp/1721494) · [GPU Resident Drawer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html) · [Rendering path karşılaştırması](https://docs.unity3d.com/Manual/urp/rendering-paths-comparison.html) · [Forward/Forward+](https://docs.unity3d.com/Manual/urp/rendering/forward-rendering-paths.html) · [Camera stacking](https://docs.unity3d.com/Manual/urp/cameras/camera-stacking-concepts.html) · [Unity 6 duyurusu](https://unity.com/blog/engine-platform/unity-6-preview-release) · [80.lv Unity 6](https://80.lv/articles/unity-6-a-deep-dive-into-the-update-s-new-features-enhancements/) · [Unity 6 performans](https://discussions.unity.com/t/boost-rendering-performance-and-achieve-more-engaging-visuals/1529800) · [FPS karşılaştırması](https://discussions.unity.com/t/fps-comparison-for-built-in-urp-hdrp/944587) · [HDRP vs URP](https://discussions.unity.com/t/hdrp-vs-urp-what-is-the-tradeoff/874512) · [HDRP viewmodel FOV](https://discussions.unity.com/t/override-camera-fov-for-arms-gun-in-first-person-mode-hdrp/1595075) · [URP Volumetric Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) · [Post-process rehberi](https://uhiyama-lab.com/en/notes/unity/unity-post-processing-guide/)

**CoD / PUBG:** [CoD fotogrametri](https://blog.activision.com/call-of-duty/2019-06/Initial-Intel-How-Photogrammetry-is-Helping-to-Shape-Call-of-Duty-Modern-Warfare-into-a-new-high-watermark-for-graphics-in-gaming) · [GamesBeat](https://gamesbeat.com/call-of-duty-modern-warfares-photogrammetry-captures-gritty-realism-like-never-before/) · [24 M üçgen](https://www.dsogaming.com/news/call-of-duty-modern-warfares-new-engine-can-push-up-to-24-million-triangles-per-frame/) · [CoD silah sesi](https://blog.activision.com/call-of-duty/2019-07/Modern-Warfare-Initial-Intel-Creating-an-Orchestra-of-Incredible-Audio-Effects-Weapon-Sounds-in-Call-of-Duty-Modern-Warfare) · [MW2 reverb](https://www.sportskeeda.com/esports/3-audio-improvements-look-modern-warfare-2-new-reverb-engine-improved-3d-directionality) · [A Sound Effect](https://www.asoundeffect.com/?p=388201) · [PUBG UE5](https://www.purexbox.com/news/2025/03/pubg-is-planning-a-fresh-start-on-xbox-series-xs-with-impressive-unreal-engine-5-visuals) · [PUBG yıkım](https://www.dsogaming.com/news/pubg-battlegrounds-unreal-engine-5-destructible-environments/) · [Polycount CoD4 bütçe](https://polycount.com/discussion/comment/847286) · [Prosedürel recoil örneği](https://www.fab.com/listings/4508565a-93fb-4084-95c8-b6ff0910ad81)

**Varlık / lisans:** [Poly Haven SSS](https://polyhaven.com/faq) · [ambientCG lisans](https://licenseorg.com/guide/3d-assets/ambientcg) · [Quixel→Fab rehberi](https://www.strayspark.studio/blog/quixel-to-fab-migration-indie-developer-survival-guide-2026) · [Megascans/Megaplants](https://quixel.com/news/quixel-on-fab-new-megascans-and-megaplants) · [Fab geçiş SSS](https://support.fab.com/s/article/Fab-Transition-FAQs?language=en_US) · [Asset Store şartları](https://unity.com/legal/as-terms) · [Mixamo SSS](https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400) · [Sonniss GDC 2026](https://rekkerd.org/sonniss-releases-gdc-2026-game-audio-bundle/) · [Sonniss lisans](https://licenseorg.com/guide/music-audio/sonniss) · [Sketchfab atıf](https://help.sketchfab.com/en/articles/16152215-crediting-users-for-3d-model-downloads) · [Book of the Dead](https://80.lv/articles/book-of-the-dead-is-available-online/) · [HQ Realistic Weapons #1](https://assetstore.unity.com/packages/3d/props/weapons/hq-realistic-weapons-pack-1-357518) · [PBR FPS Bundle](https://assetstore.unity.com/packages/3d/props/guns/pbr-fps-weapons-pack-1-complete-bundle-246697) · [Modular Assault Rifles](https://marketplace.unity.com/packages/3d/props/guns/realistic-modular-assault-rifles-pack-266600) · [FPS Weapons Set](https://marketplace.unity.com/packages/3d/props/weapons/fps-weapons-set-urp-hdrp-187205) · [Modular Soldier](https://marketplace.unity.com/packages/3d/characters/modular-soldier-157932) · [Middle East Desert City](https://assetstore.unity.com/packages/3d/environments/urban/middle-east-desert-city-12003)

**Türk askerî varlık / hukuk:** [MPT-76s itch.io](https://kamelionn.itch.io/mpt-78s) · [Kirpi CGTrader](https://www.cgtrader.com/3d-models/military/military-vehicle/bmc-kirpi-mrap-e99d7767-e820-4613-a147-dd590f7e1194) · [Cobra II](https://www.cgtrader.com/3d-models/vehicle/military-vehicle/otokar-cobra-ii-armored-vehicle-low-poly-tactical-model) · [Canik TP9](https://www.cgtrader.com/3d-models/military/gun/canik-tp9) · [UH-60 rigged](https://www.cgtrader.com/3d-models/aircraft/helicopter/uh-60-blackhawk-fully-rigged-animated-game-ready-2-skins) · [UH-60 iç mekân](https://www.cgtrader.com/3d-models/aircraft/helicopter/uh60-v-black-hawk-full-interior) · [T-129 Superhive](https://superhivemarket.com/products/atak-t129-helicopter-ultra-high-textures-changeable-decal-text) · [T-129 TurboSquid](https://www.turbosquid.com/de/3d-models/3d-t129-atak-green-helicopter-rigged-2041001) · [RocketBrush fiyatları](https://rocketbrush.com/blog/3d-weapon-vehicle-art-price-what-studios-should-expect) · [AM General v. Activision](https://www.steptoe.com/en/news-publications/sdny-blog/complaint-call-of-duty-video-game-infringes-trademarks-by-pervasively-featuring-humvees.html) · [Georgetown](https://freespeechproject.georgetown.edu/?p=8329) · [EA silah lisansları](https://www.engadget.com/2013-05-08-ea-kills-licensing-deals-with-gun-makers-keeps-those-guns-in-ga.html) · [EUIPO / Colt CZ](https://www.lewissilkin.com/insights/2023/11/16/it-was-all-gun-and-gamesuntil-the-euipo-rejected-an-application-to-register-the-102isrc)
