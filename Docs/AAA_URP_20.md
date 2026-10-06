
## S7-vfx-graph
**Ne yapar:** Madde 16 (+14 entegrasyon). `Infrastructure/Vfx/GpuVfx.cs`: `IGpuVfxBackend` + `GpuVfx.Backend/TryPlay/SetWeather` statik kancası + `GpuVfxBudget` (kademe bütçesi, saf). `Infrastructure/VfxGraph/` yeni asmdef (`Project.Infrastructure.VfxGraph`, `versionDefines` com.unity.visualeffectgraph -> `HAREKAT_VFXGRAPH`): define varsa `VfxGraphPlayer` (Resources/VFX/*.vfx halka havuz), yoksa no-op. Grafik tarifleri: `Docs/VFX_GRAPH_SPEC.md`.
**Kademe maliyeti:** Düşük kapalı; Orta x1 parçacık (<=15k canlı); Yüksek x2; Ultra x4. GPU VFX hedefi <=1.2 ms (Orta).
**Cursor doğrulaması:** Paket kurulumu + define; 12 .vfx dosyasını spec'teki adlarla yap; Play'de `GpuVfx.Available`; .vfx yokken tek uyarı + CPU geri dönüşü; GPU ms bütçesi; GameVfx kancaları eklenince çift efekt olmadığını kontrol et.

## S6-hlod-akis

**Ne yapar (madde 19: HLOD + occlusion + streaming)**
- `Editor/HlodBuilder.cs`: menü "HAREKÂT/Optimizasyon/HLOD Oluştur (Seçili Yerleşim)". Kökteki okunabilir MeshRenderer'ları malzeme başına tek mesh'e birleştirir; 1.5 m altı parçalar atılır, 60k üçgen bütçesi (büyükten küçüğe, `HlodMath.SelectParts`). Çıktı `HLOD_Proxy` + `HlodProxy`. Mesh'ler `Assets/_Project/Generated/Hlod/`. LODGroup altındaki nesneler (ağaç) dahil edilmez.
- `Infrastructure/World/HlodProxy.cs`: 0.3 sn'de bir mesafe bakar; `HlodMath.ShouldUseProxy` histerezisli (%10, en az 8 m). Yalnız `Renderer.enabled` değiştirir, çarpıştırıcıya dokunmaz. Proxy yoksa hiçbir şey yapmaz.
- `Editor/OcclusionBakeTool.cs`: "HAREKÂT/Optimizasyon/Occlusion Bake": smallestOccluder 5 m, smallestHole 0.25 m, backface 100 (güvenli). Büyük yapıları OccluderStatic işaretler. Ayrıca "Doku Akışını Aç" (importer streamingMipmaps).
- `Infrastructure/Rendering/TextureStreamingSetup.cs`: `QualitySettings.streamingMipmaps*` kademe başına.
- Saf mantık: `Core/Domain/Models/HlodMath.cs`, test `Tests/EditMode/HlodMathTests.cs`.

**Kademe maliyetleri**

| Kademe | HLOD takas mesafesi | Doku bütçesi (VRAM %35 tavanlı) | Maks. mip düşürme | Occlusion kamera |
|---|---|---|---|---|
| Düşük | 120 m | 384 MB | 3 | kapalı |
| Orta | 160 m | 640 MB | 2 | açık |
| Yüksek | 220 m | 1024 MB | 1 | açık |
| Ultra | 300 m | 1536 MB | 1 | açık |

HLOD tüm kademelerde açık (kazanç sağlar); Düşük'te en erken geçer.

**Cursor Unity'de doğrulamalı**
1. Mesh Read/Write kapalıysa HLOD "okunabilir yok" uyarısı verir; yerleşim mesh'lerinde Read/Write aç.
2. HLOD Oluştur sonrası uzaklaşınca (>swap+histerezis) sıçrama/ışık farkı var mı; proxy malzemesi aynı olduğundan görünüm eşleşmeli. Statik batching ile birleşmiş kaynaklarda `enabled=false` etkisini kontrol et.
3. Occlusion Bake süresi/boyutu; Frame Debugger'da draw call düşüşü. `streamingMipmaps*` özellik adları 6000.6'da derlendi.
4. Doku Akışını Aç tüm `Assets/_Project` dokularını yeniden içe aktarır (uzun sürebilir).

**ENTEGRASYON notları:** bkz. görev çıktısı (TextureStreamingSetup.Install çağrısı).

### TerrainStreaming notu (8-10 km haritalar için, ileride)
- Tek Terrain yerine 1000 m'lik karo ızgarası: 10 km = 10x10 = 100 karo (`HlodMath.TileCount`). Karo başına heightmap 513, splat 512, ayrıntı/ağaç kendi Terrain'inde; komşu karolar `SetNeighbors` ile dikişsiz.
- Yükleme yarıçapı 1500 m => en çok 25 karo canlı (`TilesInRadius`); boşaltma yarıçapı + tile/2 histerezisi (`TileShouldBeLoaded`). Karo yüklemesi additive sahne ya da Addressables; uzak karolar 64 çözünürlüklü düşük detay proxy mesh'i.
- Yerleşimler karo içinde ayrı prefab/sahne + HLOD proxy; NavMesh karo başına (NavMeshSurface) ve yalnız yüklü karolarda; occlusion verisi karo başına bake (`SmallestOccluderForMap` 8 m).
- Ağ: karo kimliği = (x,z) indeks; sunucu tüm karoları mantıksal tutar, yalnız istemci render'ı akıtılır.

## S3-yansima

**Kalem 8 (yansıma probları) + 9 (planar yansıma).** Yeni dosyalar: `Infrastructure/Rendering/PlanarReflectionMath.cs`, `PlanarReflection.cs`, `ReflectionProbePlanner.cs`, `ReflectionProbePlacer.cs`; testler `Tests/EditMode/PlanarReflectionMathTests.cs`.

**Planar yansıma:** su yüzeyinin altından bakan gizli ikinci kamera (yansıtılmış view matrisi + eğik yakın kırpma düzlemi, `GL.invertCulling` begin/endCameraRendering ile). Küçük eşya katmanları (Loot, Projectile, Viewmodel, Hitbox, Water) çıkarılmış; aynı anda EN FAZLA 1 yüzey (kameraya en yakın, frustum'da görünen). Çıktı: küresel `_PlanarReflectionTex` (ekran UV'siyle örneklenir), `_PlanarReflectionParams` (x açık/kapalı, y su Y, z güç, w bozulma), `_PlanarReflectionPlane`. Kapalıyken doku siyah, x=0 (shader probe yansımasına döner). Kamera su altındaysa çizilmez.

| Kademe | Durum | Çözünürlük | Yenileme | Uzak kırpma | Yüzey mesafesi | Gölge |
|---|---|---|---|---|---|---|
| Düşük | KAPALI | - | - | - | - | - |
| Orta | açık | 1/4 | 3 karede 1 | 250 m | 120 m | yok |
| Yüksek | açık | 1/2 | 2 karede 1 | 400 m | 220 m | yok |
| Ultra | açık | 1/2 | her kare | 600 m | 350 m | var |

**ReflectionProbePlacer:** konum başına 1 dış prob (kutu izdüşümlü, geniş) + bina başına iç prob (bina sınırı+marj, kutu izdüşümlü, importance 10, blend 1.5 m); 4 m'den küçük yapılara prob yok. Realtime + ViaScripting + zaman dilimli yüzler; aynı anda tek prob çizilir; kamera çevresinde yalnız MaxActive prob etkin.

| Kademe | Toplam | Aktif | Çözünürlük | İç/konum | Mod |
|---|---|---|---|---|---|
| Düşük | 6 | 3 | 32 | 1 | tek sefer ("pişmiş") |
| Orta | 12 | 5 | 64 | 2 | tek sefer |
| Yüksek | 24 | 8 | 128 | 3 | zaman dilimi değişince yeniden |
| Ultra | 40 | 12 | 256 | 4 | zaman dilimi değişince yeniden |

**Cursor Unity'de doğrulamalı:** (1) `Camera.Render()` + `GL.invertCulling` URP 17.6'da yansımayı ters yüz çizmiyor mu (gerekirse `UniversalRenderPipeline.SingleCameraRequest` ile `RenderPipeline.SubmitRenderRequest`'e geçilmeli); (2) eğik yakın kırpmada su kenarı sızıntısı (clipOffset 0.05); (3) su shader'ı henüz `_PlanarReflectionTex`'i örneklemiyor — örnek: `float3 refl = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_LinearClamp, screenUV + normal.xz * _PlanarReflectionParams.w).rgb * _PlanarReflectionParams.z;` `lerp(probeRefl, refl, _PlanarReflectionParams.x)`; (4) GPU maliyeti (Orta hedef <0,6 ms); (5) Forward+ prob sayısı sınırı ve iç/dış prob karışımı; (6) runtime Realtime probun ViaScripting+IndividualFaces davranışı (RenderProbe/IsFinishedRendering).

## S5-cim-ruzgar

**Kalemler 12 ve 13: GPU instancing'li mesh çim + rüzgâr shader'ı.**

**Ne yapar**
- `Infrastructure/World/Grass/GrassSystem.cs`: kamera çevresinde 16 m'lik hücreler tembel üretilir (kare başına bütçeli, en yakın önce), `Graphics.RenderMeshInstanced` ile çizilir. Örnek başına konum/dönüş/ölçek/renk (`GrassInstance` = Matrix4x4 + float4, shader'daki `_InstColor`). Frustum + mesafe elemesi, uzakta rastgele seyreltme, çizim mesafesi sonunda boy solması (`_HarekatGrassFade`). Yoğunluk arazi alphamap'inden pişirilir (`GrassDensityMap`): Çim 1.0, Kuru çim 0.75, Toprak 0.12, Çamur 0.1, Çakıl 0.03; Kaya/Kar/Asfalt 0. Kuru çim payı renge yansır. 3 demet mesh varyantı (`GrassBladeGeometry`, kısa 7 / orta 9 / uzun 5 bıçak).
- Ezilme: `GrassInteractors` (en fazla 8) kameraya en yakın N ezici `_HarekatInteractors` dizisine yazılır; shader bıçakları uzağa iter ve yatırır. Kamera kökü otomatik 0.9 m ezici olarak eklenir.
- Rüzgâr: `WindSystem.SetWeather(WeatherKind)` / `SetTier` / `SetBaseDirection`. Globaller `_HarekatWind` (xyz yön, w güç) ve `_HarekatWindParams` (gust, hız, türbülans, aktif). Geçişler yumuşak, yön ±25 derece salınır. Kendi sürücüsü vardır (GrassSystem gerekmez).
- Shader'lar (`Assets/_Project/Shaders/Vegetation/`): `HAREKAT/Grass` (cim: vertex color R eğilme ağırlığı, G faz, B AO; normal yukarı yatırma; alt-uç gradyanı; translucency), `HAREKAT/VegetationWind` (ağaç/çalı: govde sallanması h² ile, dal/yaprak titremesi R ile, alfa kesme + translucency). İkisi de URP 17 ForwardLit + ShadowCaster + DepthOnly + DepthNormals, GPU instancing ve SRP Batcher uyumlu (tüm özellikler `UnityPerMaterial`; ortak kod `HarekatFoliagePasses.hlsl`, `HarekatWindCommon.hlsl`).
- `GrassAssets.TryCreateWindFoliage(...)` rüzgârlı yaprak malzemesi üretir; shader yoksa null döner (çağıran mevcut `VegetationMaterials` yolunu kullanır).
- Altın kural: `HAREKAT/Grass` bulunamazsa bir kez uyarı, mesh çim kapanır, arazi detay çimi çalışmaya devam eder. Çim aktifken arazinin `detailObjectDensity` değeri 0'lanır (çift çim olmasın), kapanınca eski değer geri gelir.

**Kademe maliyetleri** (`GrassRules.ForTier`)

| Kademe | Mesh çim | Mesafe | Yoğunluk (demet/m²) | Hücre/kare | Ezici | Rüzgâr |
|---|---|---|---|---|---|---|
| 0 Düşük | KAPALI | 0 | 0 | 0 | 0 | KAPALI |
| 1 Orta | açık | 40 m | 3 (~768/hücre, bıçak %70) | 2 | 2 | var, titremesiz |
| 2 Yüksek | açık | 65 m | 6 (~1536/hücre) | 3 | 4 | tam |
| 3 Ultra | açık | 95 m | 10 (~2560/hücre) | 4 | 8 | tam |

Çim gölge düşürmez (maliyet). Hücre başına 3 çizim çağrısı (varyant başı); Ultra'da ~100 hücre = en kötü ~300 çağrı, seyreltme ile gerçek örnek sayısı çok daha düşük.

**Cursor'un Unity'de doğrulaması gerekenler**
1. İki .shader derleniyor mu (Konsolda hata yok, özellikle `UNITY_TEXTURE_STREAMING_DEBUG_VARS`, `_CLUSTER_LIGHT_LOOP`, `FRONT_FACE_TYPE` kullanımı). Shader'ları Project Settings → Graphics → Always Included Shaders listesine ekle (yoksa player'da `Shader.Find` boş döner).
2. Instancing: Frame Debugger'da çim çizimleri `RenderMeshInstanced` ile birleşik mi, örnek rengi (`_InstColor`) doğru geliyor mu (siyah/beyaz ise struct sırası veya `HarekatFoliage` tamponu). SRP Batcher: Grass/VegetationWind malzemeleri inspector'da "SRP Batcher compatible" göstermeli.
3. Çim yalnız çim/kuru çim katmanlarında mı; yol, kaya, kar, asfaltta yok mu; su seviyesi altında yok mu.
4. Rüzgâr: `WindSystem.SetWeather(WeatherKind.Yagmur)` çimi belirgin salladı mı; Düşük kademede donuk mu. Ezilme: oyuncu yürürken çim yatıyor mu (kamera kökü doğru Transform mu).
5. Mesafe solması (`_HarekatGrassFade`) uçta ani belirme yapıyor mu; kademe değişiminde çim/arazi detay çimi çifte görünmüyor mu.
6. Perf: GTX1660S/RTX3050, 1080p Orta'da çim açıkken 60 FPS; HUD'da `GrassSystem.Instance.LastDrawnInstances/LastDrawCalls/CellCount`.
7. Ağaç gövde sallanması için ağaç mesh'lerinin vertex color R (dal/yaprak) değerini yazması gerekir; yoksa yalnız gövde sallanması çalışır (G3 TreeMeshes'e bağlı).

**ENTEGRASYON** (eklenecek tek satırlar) `Docs/DURUM.md` ve görev notlarında listelendi: sahne kurulumunda `GrassSystem.Install(cam, terrain, tier, layout.WaterLevel)`; hava değişiminde `WindSystem.SetWeather(weather)`; kalite uygulamasında `GrassSystem.SetTier(level)` (rüzgâr kademesini de ayarlar); oyuncu/araç `GrassSystem.AddInteractor(transform, radius)`; arazi detay kademesi uygulanırken `GrassSystem.Active` ise `detailObjectDensity` 0 kalmalı.

## S8-benchmark-sahne

**Ne yapar:** `AAA_Benchmark` sahnesi — 150×150 m küçük bir parça. Çamur + kaya + çim bölgeleri, küçük orman, girilebilir köy evi (BuildingGenerator), Kirpi, nöbetçi asker (devriye), MPT-76 ile oyuncu, çelik hedefler + beton duvar (dekal/VFX), gündüz + hafif sis, gerçek zamanlı yansıma probu. İki mod: **Oyna** ve **Vitrin** (6 planlık betikli kamera: silah yakın plan + şarjör, asker, Kirpi, ev içi, orman, hedeflere ateş). F9: mod değiştir, F12: `Logs/benchmark/*.png`. Ana menüde "AAA TEST SAHNESİ" ve "AAA VİTRİN" düğmeleri (`MainMenuController.ExtraButtons`).
Dosyalar: `Presentation/Benchmark/` (AaaBenchmarkBootstrap, AaaBenchmarkTerrain, AaaBenchmarkVitrin, AaaBenchmarkPatrol, AaaBenchmarkFeatureInstaller, BenchmarkLayout, BenchmarkShotPath), `Editor/AaaBenchmarkSceneBuilder.cs`, `SceneNames.AaaBenchmark`, test: `AaaBenchmarkTests` (`#if UNITY_EDITOR`).

**Kademe maliyeti:** Sahne her zaman Ultra (3) kademede kurulur (kayıtlı ayara dokunulmaz). `AaaBenchmarkFeatureInstaller.InstallAll` G4 özelliklerini yansımayla bulur: statik sınıf adı SSAO / ContactShadow / VolumetricFog / LightShaft / SSR / ReflectionProbe / TerrainBlend / GrassInstanc / Wind / Decal / Parallax / ColorGrad / DynamicResolution / Hlod / FrameBudget... parçalarından birini içerir ve `Install`/`Enable`/`Apply` statik yöntemi (parametreler Camera, Terrain, Transform/GameObject, int=kademe, bool=true, float=1) varsa çağrılır. Bulunan/atlanan türler Console'a `[AAA Benchmark] G4 özellikleri (Ultra)` satırıyla yazılır. Hiçbiri yoksa sahne yine çalışır (mevcut URP-Lit yolu).
Maliyet: ağaç hedefi 520, çim yoğunluğu `PerformanceProfile.DetailDensityScale(3)`, ağaç mesafesi Ultra. 1080p'de bu parça tek başına Orta kademe hedefini (60 FPS) tutmalı; Ultra'da ölçüm amaçlıdır.

**Cursor Unity'de doğrulayacak:**
1. `HAREKÂT/Kurulum/AAA Benchmark Sahnesi` çalışır, `Assets/_Project/Scenes/AAA_Benchmark.unity` oluşur ve Build Settings'e girer.
2. Oyna: arazi/ağaç/kaya/çim görünür; ev girilir; Kirpi'ye binilir; asker devriye atar (NavMesh pişti mi?); MPT-76 ateş + reload; çelik hedef/duvarda dekal.
3. Vitrin: F9; 6 plan döngüsü; silah planlarında viewmodel görünür ve otomatik ateş/şarjör çalışır (`PlayerWeaponHandler.Tick` dt=0 çift tetik sorunu çıkarmıyor mu); diğer planlarda viewmodel gizli ve kamera taşınıyor (FirstPersonCameraController LateUpdate sırasından sonra çalışıyor mu).
4. Hafif sis: `Atmosphere` yükseklik sisi `fogDensity`'yi ezerse `SetupAtmosphere` içindeki 0.0075 alt sınırı Atmosphere'e taşınmalı.
5. F12 `Logs/benchmark/` altına PNG yazıyor.
6. Konsolda hangi G4 özelliklerinin yansımayla açıldığına bak; açılmayan her özellik için ilgili sınıfa uygun `Install(Camera, int tier)` kancası eklenmeli (bkz. Docs/CURSOR_AAA_BENCHMARK.md).

## S4-arazi-shader

Maddeler 10 (PBR standardı), 11 (arazi yükseklik karışımı), 15 (parallax/detay).

### Ne yapıyor
- `Shaders/Terrain/HarekatTerrainLit.shader` (`HAREKAT/Terrain/Lit`) + `HarekatTerrainLitAdd.shader` (Add Pass, katman 4-7): URP 17.6 Terrain/Lit'in kopyası; `HarekatTerrainLitPasses.hlsl` URP `TerrainLitPasses.hlsl` kopyasıdır (URP sürümü değişirse yeniden eşitle). Farklar:
  - Yükseklik karışımı her zaman açık ve 4+ katmanda da çalışır (maske haritası **B = yükseklik**; `_HeightTransition` geçiş sertliği). Add Pass kendi içinde normalleşir, alpha ağırlığı korunur (geçişler arası karışım yoktur).
  - Yamaç triplanar (UDN): normal.y eşiğinin altında Kaya katmanı dokusu 3 eksenden örneklenir (gradyanlı, dal içinde güvenli).
  - Mesafe makro varyasyonu: prosedürel düşük frekanslı renk/parlaklık lekesi (doku yok).
  - Detay normal (Toprak normali, ~2.5 m karo, yakında görünür).
  - Islaklık: `_HarekatWetness` küresel; albedo kararır, pürüzsüzlük yükselir, düz yerlerde su birikintisi (normal düzleşir), Çamur katmanında (Add Pass .g) daha ıslak/geniş.
- `Shaders/Surface/HarekatParallaxLit.shader` (`HAREKAT/Surface/ParallaxLit`): Lit uyumlu özellik adları (`_BaseMap _BaseColor _Smoothness _Metallic _BumpMap _BumpScale _OcclusionStrength _ParallaxMap _Parallax`, anahtar `_NORMALMAP _PARALLAXMAP`), POM (doğrusal arama + enterpolasyon, mesafe/kalite ile adım azalır), `_MaskMap` = ORMH, ıslak çamur + yükseklik haritasına göre su birikintisi.
- `Scripts/Infrastructure/World/TerrainShaderBinder.cs`: `Install(terrain, kademe)` malzeme şablonunu değiştirir (shader yoksa tek uyarı, URP yolu korunur), küresel parametreleri yazar; `Tick(rain01, dt)` ıslaklığı entegre eder; `CreateParallaxMaterial/CreateWetMud` Lit malzemeden POM malzemesi üretir.
- Saf matematik: `Core/Domain/Models/TerrainShadingMath.cs` (testler `TerrainShadingMathTests`).

### Kademe maliyetleri
| Kademe | Arazi shader | Yamaç | Makro | Detay normal | POM adım çarpanı | Islaklık |
|---|---|---|---|---|---|---|
| 0 Düşük | URP (değişmez) | - | - | - | 0 (kapalı) | 0 |
| 1 Orta | HAREKAT | kapalı | 0.12 | kapalı | 0.5 | var |
| 2 Yüksek | HAREKAT | 3 örnek yalnız yamaçta | 0.2 | 25 m | 1.0 | var |
| 3 Ultra | HAREKAT | aynı | 0.25 | 45 m | 1.5 | var |

Güç değeri 0 ise ilgili dal tamamen atlanır (tek tip dal, anahtar kelime/varyant yok). Islaklık yağmursuzken (`_HarekatWetness` = 0) atlanır. Terrain shader'ının ek maliyeti: hesap ALU (gürültü 2 oktav) + yalnız yamak pikselinde 6 doku örneği.

### PBR standardı (HAREKAT)
- **Albedo parlaklığı** (sRGB 0-255, Rec.709): genel sınır 30-240. Çamur 30-75, Toprak 45-115, Kaya 55-145, Çim 35-100, Asfalt 35-85, Kar 200-240, Beton 100-195, Ahşap 40-130. Saf siyah/beyaz yasak; gölge/ışık albedoya pişirilmez. Doğrulama: `TerrainShadingMath.IsAlbedoInRange`.
- **ORM(H) paketleme** (lineer, sRGB işaretsiz): dosya soneki `_ORMH`; **R = AO, G = Pürüzlülük (roughness), B = Metalik, A = Yükseklik** (0.5 = referans düzey). Normal: OpenGL (Y+), `_N` soneki. Arazi katman maskesi Unity biçiminde kalır: **R Metalik, G AO, B Yükseklik, A Pürüzsüzlük**.
- **Texel yoğunluğu: 512 px/metre.** 1 m karo = 512, 2 m = 1024, 4 m = 2048 (üst sınır 4096; daha büyük alan makro varyasyonla kırılır). Yardımcılar: `TextureSizeForTile`, `TexelDensityRatio` (hedef 0.5-2).
- Metalik yalnız ham metalde 0/1; organik yüzeylerde 0. Pürüzsüzlük aralığı: kuru toprak 0.1-0.3, ıslak 0.9.

### Cursor'un Unity'de doğrulaması
1. Shader'lar derleniyor mu (Console'da HLSL hatası yok)? Özellikle `Assets/_Project/...` göreli include yolları, `SAMPLE_TEXTURE2D_GRAD` kullanımı ve `HarekatTerrainLitPasses.hlsl` (URP kopyası) ve Add Pass bağımlılığı `Hidden/HAREKAT/Terrain/Lit (Add Pass)`.
2. Terrain'e `TerrainShaderBinder.Install(terrain, kademe)` çağrısı bağlanınca 8 katman doğru çiziliyor mu (katman 4-7 Add Pass); basemap uzak mesafede URP Base Pass ile aynı mı.
3. Yamaçta (>35 derece) triplanar uzamıyor mu; `Shader.SetGlobalFloat("_HarekatWetness", 1)` ile (veya `SetWetness(1)`) çamurda birikinti, albedo kararması ve parlaklık görülüyor mu.
4. `CreateWetMud` malzemesi: POM açıkken sınırda (silüet) kabul edilebilir mi, adım sayıları 60 FPS hedefini bozuyor mu (RTX3050 Orta).
5. DepthNormals/SSAO/DBuffer decal arazi ve POM zeminde çalışıyor mu (POM zemin DepthNormals'ta düz normal kullanır, bilinçli).
6. Frame Debugger: HAREKAT arazi SRP Batcher dışı çiziliyor (Terrain zaten instanced); POM malzemesi SRP Batcher uyumlu olmalı.

ENTEGRASYON kancaları: `TerrainGenerator`: `TerrainShaderBinder.Install(terrain, PerformanceProfile/kalite kademesi)` (materialTemplate atamasından sonra); kalite değişiminde aynı çağrı; hava/yağmur sistemi: `TerrainShaderBinder.Tick(rain01, Time.deltaTime)`; `MaterialLibrary`: çamur/zemin/kaya için `TerrainShaderBinder.CreateWetMud(litMat, ormh, height) ?? litMat`.

## S1-ekran-uzayi

**Kapsam (madde 1, 2, 4):** özel renderer-feature çerçevesi + ekran-uzayı kontakt gölgeleri + SSR-lite. Kod: `Infrastructure/Rendering/Features/` (`HarekatRendererFeatures.cs`, `HarekatContactShadowsFeature.cs`, `HarekatSsrLiteFeature.cs`), saf matematik `Core/Domain/Models/ScreenSpaceMath.cs` (+ `ScreenSpaceMathTests`, CPU referans ışın yürüyüşü dahil), gölgelendiriciler `Shaders/ScreenSpace/` (`HarekatSsCommon.hlsl`, `ContactShadows.shader`, `SSRLite.shader`), editör `ScreenSpaceShaderInclude` (menü HAREKÂT/Render).

**Çerçeve.** `HarekatRendererFeatures.Install(tier)`: aktif render hattından (`GraphicsSettings.currentRenderPipeline` -> `m_RendererDataList[m_DefaultRendererIndex]`, reflection) `UniversalRendererData.rendererFeatures` listesine kayıtlı HAREKAT özelliklerini (yoksa) ekler, `SetDirty()` ile renderer'ı yeniden yaratır ve her özelliğe `ApplyTier` uygular (`SetActive`). Idempotent; kademe değişiminde yalnız `SetActive` çağrılır. Yeni özellik: `HarekatRendererFeatures.Register("Anahtar", () => ScriptableObject.CreateInstance<X>())` (X : `HarekatFeatureBase` veya `IHarekatFeature`). `HarekatShaders.CreateMaterial` = `Shader.Find` + tek uyarı; gölgelendirici yoksa özellik kendini kapatır, mevcut yol aynen çalışır. `Application.quitting` ile eklenenler geri alınır (asset belleğinde HideAndDontSave örnek kalmaz). Yalnız ana oyun kamerası (Game, targetTexture yok, perspektif) işlenir: planar yansıma/RT kameraları atlanır.

**Kontakt gölgeleri.** Pass 0: yarı çözünürlük R8, güneş yönünde (`_MainLightPosition`) dünya uzayında kısa ışın, her adımda projeksiyon + derinlik karşılaştırması (yüzeyin arkası ve kalınlık içi = isabet), IGN jitter, mesafe sönümü. Pass 1: tam çözünürlük, derinlik duyarlı 2x2 yukarı örnekleme, `MainLightRealtimeShadow` maskesiyle çarpımsal (`Blend DstColor Zero`) uygulama: gölge haritasında zaten gölgede olan yerde etkisiz (çifte koyulaşma yok). Olay: `BeforeRenderingTransparents`. Küresel: `_HarekatContactShadowTex`.

**SSR-lite.** Pass 0: yarı çözünürlük RGBA16F; piksel başına derinlik + normal; ekran-uzayı doğrusal yürüyüş (`ScreenSpaceMath.MarchScreenRay` ile birebir: u,v ve 1/w doğrusal, adım = clamp(ceil(pikselUzunluk/stride)), kalınlık = mutlak + göreli, ikili arama inceltme, arka yüz reddi). Pürüzsüzlük = `max(normal alfası (URP 17.6 _WRITE_SMOOTHNESS), ıslaklık * yukarı-bakan)`; `smoothstep(cutoff, fadeStart)` altı yansımasız. Ağırlık = pürüzsüzlük² x Fresnel x mesafe x kenar x kameraya-dönük sönümü; isabet yoksa 0 olur: sahne rengindeki yansıma probu aynen kalır (probe'a geri düşer). Pass 1: derinlik duyarlı yukarı örnekleme, toplamsal (`Blend One One`). Çalışma zamanı knob: `ScreenSpaceSettings.Wetness` (yağmur/çamurda yükseltin), `SsrStrengthScale`, `ContactShadowStrengthScale`.

**Kademe maliyetleri (yarı çözünürlük; 1080p hedef: yaklaşık, ölçülmedi).**

| Kademe | Kontakt gölge | SSR-lite |
|---|---|---|
| 0 Düşük | KAPALI | KAPALI |
| 1 Orta | AÇIK: 8 adım, 0,45 m, 25 m, güç 0,55 | KAPALI |
| 2 Yüksek | 12 adım, 0,6 m, 40 m, güç 0,7 | 24 adım, stride 4 px, 45 m, inceltme 3 |
| 3 Ultra | 16 adım, 0,8 m, 60 m, güç 0,8 | 40 adım, stride 3 px, 80 m, inceltme 5 |

Bütçe önerisi: kontakt gölge Orta'da <= 0,3 ms, SSR Yüksek'te <= 0,8 ms (GTX1660S, 1080p, render ölçeği ile). Aşılırsa `ScreenSpaceMath` tablosundaki adım/stride düşürülür (tek yer).

**Cursor'un Unity'de doğrulaması gereken noktalar (gölgelendiriciler burada derlenemedi):**
1. `Shaders/ScreenSpace/*.shader` pembe/derleme hatası yok mu (özellikle `Blit.hlsl` yolu `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`, `ComputeWorldSpacePosition`, `UNITY_MATRIX_I_VP`). Menü `HAREKÂT/Render/Ekran-Uzayı Gölgelendiricilerini Dahil Et` bir kez çalıştırılmalı (derlenmiş oyunda `Shader.Find` için).
2. Play modunda Console'da `[HAREKAT] Gölgelendirici bulunamadı` uyarısı olmamalı; Frame Debugger'da "HAREKAT Contact Shadows March/Apply" ve "HAREKAT SSR March/Apply" geçişleri `BeforeRenderingTransparents` civarında görünmeli.
3. Y yönü/UV: yansımalar ve kontakt gölge dünyayla hizalı mı (ters Y ise `HarekatSsCommon.hlsl` HkProject/HkWorldFromDepth çiftini `GetTextureUVOrigin` ile düzeltin). Ara hedef olmayan kamerada (doğrudan backbuffer) ters çıkabilir.
4. Ana ışık gölge anahtarları bu noktada hâlâ küresel açık mı (`_MAIN_LIGHT_SHADOWS(_CASCADE)`)? Değilse kontakt gölge maskesi 1 olur (gölgedeki yerde çifte koyulaşma görürsünüz): o durumda geçiş `AfterRenderingOpaques`'a çekilip gölge maskesi `cameraData` üzerinden küresel dokudan okunmalı.
5. SSR pürüzsüzlük alfası: `writesSmoothnessToDepthNormalsAlpha` reflection ile ayarlanır (log: "pürüzsüzlük alfası istenemedi" çıkarsa sezgisele düşer). Derlenmiş oyunda URP Asset > Shader Stripping'de "Strip WriteSmoothness" (m_PrefilterWriteSmoothness) KAPALI olmalı; yoksa Lit varyantı atılır ve yalnız ıslaklık sezgiseli çalışır.
6. Su yüzeyi saydam ise derinlik/normal yazmaz: SSR suya düşmez (su için S3 planar yansıma). Cam/metal için malzemenin opak kuyrukta DepthNormals geçişi olmalı.
7. Maliyet: Frame Debugger/Profiler ile Orta/Yüksek/Ultra ms ölçümü; sonuçları bu tabloya yazın.
8. Not: URP 17.6 kendi `ScreenSpaceReflectionRendererFeature`'ını (`URP_SCREEN_SPACE_REFLECTION` tanımı arkasında, hi-Z) içerir; bizimki hafif alternatiftir, ikisini birlikte açmayın.

**ENTEGRASYON (başka ajanların dosyalarına eklenecek tek satırlar):**
- `Infrastructure/Rendering/PostProcessing.cs` (SsaoTuner.Apply(level) yanında): `Project.Infrastructure.Rendering.Features.HarekatRendererFeatures.Install(level);`
- Hava durumu/yağmur (Atmosphere): `Project.Infrastructure.Rendering.Features.ScreenSpaceSettings.Wetness = rain01;`

## S2-volumetrik

**Kapsam (madde 5, 6):** hacimsel sis + güneş ışık huzmesi (god ray), URP RenderGraph renderer özelliği olarak.

**Dosyalar**
- `Core/Domain/Models/VolumetricFogMath.cs`: saf matematik (HG faz, yükseklik yoğunluğu, analitik optik derinlik, dilim dağılımı, zamansal ağırlık, bilateral ağırlık, mavi gürültü üretici, kademe tablosu) + `VolumetricFogPresets` (Açık/Sisli/Yağmurlu x Şafak/Gündüz/Akşam/Gece). Testler: `Tests/EditMode/VolumetricFogMathTests.cs`.
- `Infrastructure/Rendering/Volumetric/`: `VolumetricFogSettings` (yoğunluk, yükseklik azalması, taban yükseklik, anizotropi g, güneş/ortam çarpanı, azami mesafe, renk), `VolumetricFog` (giriş), `VolumetricFogFeature` (ScriptableRendererFeature), `VolumetricFogPass` (3 raster geçişi).
- `Shaders/Volumetric/HarekatVolumetricFog.shader` (`HAREKAT/Volumetric/Fog`, 3 geçiş).

**Nasıl çalışır:** (1) çeyrek çözünürlükte ışın yürütme: derinlikten dünya konumu, kameradan sahne yüzeyine (gökyüzünde azami mesafeye) kareye yakın dağılımlı dilimler, her dilimde yükseklik sisi yoğunluğu + ana ışık gölge haritası örneği (URP `Shadows.hlsl`, cascade) x Henyey-Greenstein faz; mavi gürültü (64x64, C# ile üretilir) + altın oran kare kayması. (2) zamansal: önceki VP ile yeniden izdüşüm, 3x3 varyans kutusuna sıkıştırma, kamera hareketine göre geçmiş ağırlığı, kamera sıçramasında/boyut değişiminde sıfırlama. (3) birleştirme: derinlik duyarlı 4 örnekli bilateral üst örnekleme, `Blend One SrcAlpha` ile kamera rengine (inscatter + renk x geçirgenlik). Olay: `BeforeRenderingTransparents`.

**API**
- `VolumetricFog.Install(int tier, bool userEnabled = true)` (ya da `Install(Camera, tier)`): kademeyi ayarlar, özelliği etkin URP renderer'ına çalışma zamanında ekler (`HideFlags.DontSave`, çıkışta kaldırılır, yinelenmez). Shader yoksa tek uyarı, sessizce kapalı.
- `VolumetricFog.ApplyPreset(TimeOfDay, VolumetricWeather | WeatherKind, float blendSeconds = 0)`: ön ayar (yumuşak geçiş destekli). Kar = Sisli.
- `VolumetricFog.Settings` canlı değiştirilebilir; `DebugMode` (1 zamansal kapalı, 2 gölge terimi); `BuiltInFogScale` (açıkken 0.35, çifte sis önlemi).

**Kademe maliyeti** (1080p; piksel x adım, GTX1660S tahmini, ölçülmeli)

| Kademe | Durum | Çözünürlük | Adım | Azami mesafe | Tahmini GPU |
|---|---|---|---|---|---|
| 0 Düşük | KAPALI | - | - | - | 0 |
| 1 Orta | açık (en ucuz), gölge ışınları + zamansal | 1/4 (480x270) | 12 | 120 m | ~0.2-0.3 ms |
| 2 Yüksek | açık | 1/4 | 24 | 220 m | ~0.4-0.6 ms |
| 3 Ultra | açık | 1/2 (960x540) | 32 | 350 m | ~1.2-1.8 ms |

**Cursor'un Unity'de doğrulaması gereken**
1. Shader derlenir mi (`HAREKAT/Volumetric/Fog`, 3 geçiş; `Shadows.hlsl` + `_MAIN_LIGHT_SHADOWS(_CASCADE)` varyantları). Shader'ı **Project Settings > Graphics > Always Included Shaders**'a ekle (Shader.Find build'de bulunsun).
2. Zemin/gökyüzünde sis var mı; güneşe bakınca ışık huzmesi (gölgeli ağaç/bina arkasında çizgiler) görünüyor mu. `DebugMode = 2` gölge terimini gösterir (tamamen beyaz = gölge haritası bağlanmamış).
3. Zamansal yeniden izdüşüm yönü: hareket ederken hayalet/sürüklenme varsa `ComputeNormalizedDeviceCoordinatesWithZ(..., _VolPrevVP)` Y yönünü ve `GL.GetGPUProjectionMatrix(proj, true)` kullanımını kontrol et. `DebugMode = 1` ile ham sonuç karşılaştırılır.
4. Renderer listesine çalışma zamanında ekleme (`SetDirty` ile renderer yeniden oluşur) ve `Dispose` sonrası tembel yeniden kurulum; Editor'de varlığa kayıt sızmadığını doğrula. İstersen `VolumetricFogFeature`'ı renderer varlığına elle ekle.
5. Forward+ ve Render Scale / STP altında piksel hizası (düşük çözünürlük uv eşlemesi), MSAA açıkken derinlik; Sahne görünümü kamerası.
6. Ölçüm: kademe başına GPU ms (F3 HUD), hedef 1080p Orta 60 FPS bütçesi içinde.

**ENTEGRASYON (entegratör ekleyecek)**
- `PerformanceProfile`/kalite uygulayıcı: `VolumetricFog.Install(tier)` (kalite değişiminde yeniden çağır).
- `Atmosphere.cs` (gün saati/hava değişince): `VolumetricFog.ApplyPreset(timeOfDay, weatherKind, 4f)`; sis yoğunluğu yazarken `RenderSettings.fogDensity *= VolumetricFog.BuiltInFogScale`. Mevcut `AtmosphereMath.VolumetricAllowed` (Yüksek+) ile Orta'daki "açık" kararı uyumlanmalı.
- Doğrulama için `Tools/UnityVerify/stubs/UrpRenderGraphStub.cs` içine `TextureDesc`, `RenderGraph.CreateTexture/ImportTexture`, `RasterCommandBuffer.DrawProcedural`, `RenderingUtils.ReAllocateHandleIfNeeded`, `RTHandle.rt/Release`, `UniversalCameraData.renderType` eklendi (ortak dosya; üzerine yazılırsa geri ekle).

## GX6-arazi-erozyon

Gerçekçi dağ arazisi: aşındırma + akış/eğrilik haritaları + renk haritası (200 m+ tekrar kırma, AO, yol izi).

- `World/TerrainErosion.cs` (saf, Unity'siz): damla tabanlı hidrolik + talus (ısıl) aşındırma. Bütçe zamanla değil damla/adım sayısıyla sınırlı (`TerrainErosionSettings.ForTier(0..3)`; Ultra 513² ≈ 130k damla × 28 adım), `System.Random(seed)` ile belirlenimci. Hücre başına en çok `MaxDelta` (3.5 m) sapma. `BuildProtectMask`: yerleşim düzlüğü, yol (kenara 20 m), dere/göl, su hattı +5 m, harita kenarı özgün yüksekliğe geri karıştırılır → yerleşim/yol/dere yerleşimi aynı kalır. Çıktı `TerrainErosionMaps` (Flow, Curvature, Scree, Cliff); `TerrainErosionMaps.Of(model)` ile modelden okunur (iliştirilmemişse boyama eski davranışta).
- `TerrainGenerator.Create`: `Build` sonrası `ApplyErosion` (ayar: `TerrainBuildSettings.Erosion/ErosionTier/ColorMapSize`); hata olursa özgün yükseklik korunur.
- `TerrainPainter` + `TerrainPaintRules` (partial, `TerrainPaintRulesErosion.cs`): sırtta kaya, uçurum dibinde çakıl/iri taş (scree), olukta çamur (yavaş) / çakıl (dik), yol omzu gürültüyle yumuşatıldı.
- `World/TerrainColorMap.cs`: makro renk + eğrilik AO + oluk ıslaklığı + toprak yolda lastik izi koyulaşması tek RGBA32 dokuya pişirilir (rgb = tint*0.5, a = AO). Küresel: `_HarekatTerrainColorMap`, `_HarekatTerrainColorMapRect` (originX, originZ, 1/size), `_HarekatTerrainColorMapParams` (tint gücü, AO gücü, solma başı/sonu m).
- `TerrainErosion.CollectOutcropSites(model, maps, protect, seed, max)`: dik yamaçlarda kaya çıkıntısı adayları (konum/normal/boyut).
- Testler: `TerrainErosionTests` (belirlenimcilik, sınır, koruma, bütçe, talus, boyama kuralları, renk haritası aralığı).

ENTEGRASYON (kapsam dışı): (1) `HarekatTerrainLitPasses.hlsl` içinde `uv = (worldXZ - rect.xy) * rect.z` ile `_HarekatTerrainColorMap` örnekle, `albedo *= lerp(1, rgb*2, params.x * fade)`, `occlusion *= lerp(1, a, params.y)`; fade = 1 - smoothstep(params.z, params.w, camDist) ile yakında detay dokusu baskın kalır. (2) `QualityTierApplier` Terrain adımında `TerrainColorMap.SetTier(tier)`. (3) `WorldGenerator`, RockScatter sonrası `TerrainErosion.CollectOutcropSites(model, TerrainErosionMaps.Of(model), TerrainErosion.BuildProtectMask(model), seed, 120)` ile kaya serpiştirme (RockFactory varyantı, normale hizalı). (4) Editör sahnesi dokuyu kalıcı kaydetmiyor (`TerrainColorMap.Current`); çalışma zamanında üretim yapılıyorsa gerek yok.

## GX2-aa-keskinlik

**Amaç:** kademe başına kenar yumuşatma + TAA/STP sonrası keskinleştirme + doku mip kayması. Matematik tek yerde: `Core/Domain/Models/AntiAliasingMath.cs` (testli: `AntiAliasingMathTests`).

| Kademe | İstenen AA | CAS taban gücü | Not |
|---|---|---|---|
| 0 Düşük | FXAA | 0,35 | |
| 1 Orta | SMAA High | 0,40 | |
| 2 Yüksek | TAA (quality High); yükseltici STP ise STP | 0,55 | |
| 3 Ultra | TAA/STP | 0,50 | render ölçeği zaten 1,00; üstü süper-örnekleme ayrı karar |

**Dosyalar**
- `Rendering/CameraRig.cs`: `ApplyAntialiasing` artık kademeden (`PostProcessing.QualityLevel` -> `PipelineTiers.AaFor`) yöntem seçer; `SetAntialiasingLevel(0)` hâlâ ana kapatma anahtarı. TAA kalitesi High, `m_TaaSettings.quality` yansımayla yazılır (olmazsa tek uyarı, varsayılan kalite).
- `Rendering/PipelineTiers.cs`: `AaFor(level)` (AA sütunu; STP yükselticiyse zamansal mod = STP).
- `Rendering/Features/HarekatSharpenFeature.cs` + `Shaders/PostFx/CasSharpen.shader` (`HAREKAT/PostFx/CasSharpen`): AMD CAS mantığı (artı biçimli 5 örnek, kontrast uyarlamalı kazanç, `1/lerp(8,5,keskinlik)`), `AfterRenderingPostProcessing`, iki geçiş (CAS -> geçici, geçici -> renk). `HarekatRendererFeatures`'a `CasSharpen` anahtarıyla kayıtlı; yalnız post-processing'in uygulandığı (yığının son) kamerada çalışır; güç < 0,02 ise atlanır.
- `Rendering/Features/HarekatSharpenFeature.cs` içinde `SharpenSettings`: kullanıcı anahtarı **`Keskinlik`** (PlayerPrefs float 0..1, varsayılan 0,5 = tablo değeri; 0 kapalı, 1 = taban x2, üst sınır 1). Etkin güç = `EffectiveSharpness(kademeTabanı, kullanıcı)`; kaydırıcı değişince bir sonraki karede geçerli (yeniden kurulum gerekmez).
- `Rendering/Features/AaMipBias.cs`: negatif LOD bias. `MipBias(ölçek, mod)`: ölçek < 1 ise log2(ölçek) (en çok -1; 0,67 -> -0,58), zamansal modda en az -0,25, yerel çözünürlük + zamansal olmayan -> 0. Küresel `_HkMipBias` + `AaMipBias.Track(tex)` ile kayıtlı dokulara `Texture.mipMapBias`. URP kendi gölgelendiricilerinde yükseltmede `_GlobalMipBias`'ı zaten uygular.

**Silah (overlay) kamerası ve TAA kararı:** URP TAA/STP, kamera yığınındaki overlay kamerayla birlikte hayalet/sürüklenme bırakır (overlay'in hareket vektörü yok). Bu yüzden `AntiAliasingMath.Effective`: silah görünürken (yığın aktif) taban kamera **SMAA High**'a düşer, overlay tabanın AA'sını devralır ve silah keskin kalır; silah gizliyken (dürbün, araç, ölüm) `SetViewmodelVisible` AA'yı yeniden uygular ve TAA/STP açılır. Unity'de görsel doğrulama sonucu yığınla TAA temizse `AntiAliasingMath.TemporalSupportsOverlayStack = true` yapılır, başka değişiklik gerekmez.

**Unity'de doğrulanacak**
1. `HAREKAT/PostFx/CasSharpen` derlenir mi; Always Included Shaders'a eklenmeli (`Shader.Find`).
2. Düşük/Orta'da STP yükselticisi kamera AA'sını (FXAA/SMAA) geçersiz kılar mı: tablo `UseStp=true` (aşağıdaki ENTEGRASYON).
3. CAS geçişi son post sonrası ve UI öncesi mi (overlay UI'a çalışmamalı); halo/aşırı keskinlik varsa `Keskinlik` 0,5 altı.
4. `m_TaaSettings` alan adı 17.6'da doğru mu (yansıma uyarısı log'da çıkmamalı).

**ENTEGRASYON**
- `SettingsPanel`: "Keskinlik" kaydırıcısı (0..1) -> `Features.SharpenSettings.UserSharpness = v` (PlayerPrefs `Keskinlik` kaydı içeride).
- `PipelineTiers` tablosunun UseStp sütunu (AA sütunu dışı): Düşük/Orta'da FXAA/SMAA'nın görülmesi için `UseStp=false` (ölçek bilineer/FSR) önerilir; STP URP'de kamera AA'sını devralır.
- `MaterialLibrary`/`ProceduralPbr`: üretilen dokular için `AaMipBias.Track(tex)`.
- Ortak stub (`Tools/UnityVerify/stubs`) değişmedi; yeni URP API'si yalnız `renderPostProcessing`/`antialiasing` kullanır.

## GX3-karakter-materyal

Gerçekçi prosedürel asker malzemeleri (humanoid/ContentOverrides yolu dokunulmadı; yalnızca prosedürel `SoldierModel` kullanır).

**Shader'lar** (`Assets/_Project/Shaders/Character/`, ortak `HarekatCharacterPass.hlsl`, mod define'ı ile):
- `HAREKAT/Character/Fabric`: kumaş sheen/fuzz (fresnel rim, ışık+GI beslemeli), ripstop/twill detay normali (UV metre × `_DetailTiling`), makro kir/leke (`_MacroMap`), nesne-uzayı y gradyanından diz/bot çamuru + yukarı bakan toz, ıslak kumaş koyulaşması (`_HarekatWetness × _WetResponse`).
- `HAREKAT/Character/Skin`: wrap-diffuse ek terimi (alt-yüzey tonu `_SssColor`), gözenek normali, cavity ile speküler azaltma + speküler oklüzyon (Lagarde), benekli ten.
- `HAREKAT/Character/Gear`: kask boyası (mat, normal eğiminden kenar aşınması → `_WearColor`), cordura, kauçuk, deri (aynı shader, tür parametreleri).
- `HAREKAT/Character/Nvg`: koyu cam, yüksek yansıma, yeşil fresnel parıltı.
- Hepsi UniversalFragmentPBR üstüne ek terimlerle; ShadowCaster/DepthOnly/DepthNormals Lit'ten; Fallback URP/Lit.

**C#** (`Infrastructure/Characters/`): `CharacterMaterialMath` (shader formüllerinin testli aynası + tür varsayılanları), `CharacterTextureGen` (saf yükseklik haritaları + ProceduralPbr.NormalFromHeight ile önbellekli normaller, makro = ProceduralTextures.Noise), `CharacterMaterials` (Shader.Find + tek uyarı, null → çağıran MaterialLibrary.Lit'e düşer). `SoldierModel.CreateMaterials`: Camo/Cloth=Fabric, Skin=Skin, Gear/GearDark=Cordura, kask seviye 1=HelmetPaint (`Helmet`), Boots=Leather (güçlü çamur), Gloves=Rubber, NVG tüpleri=lens.

**Notlar:** Kir gradyanı nesne-uzayı y'ye bağlı (uzuv pivotları kalça/diz/ayak bileği olduğundan diz-bot-dirsek doğal kirlenir; gövdede hafif). Test: `CharacterMaterialMathTests`.

**ENTEGRASYON**
- `Tools/UnityVerify` hızlı derleme: yeni shader'lar C# içermez; Unity build logunda `HAREKAT/Character/*` shader hataları okunmalı.
- Gerekirse QualityTierApplier: düşük kademede `Fabric` sheen/`Skin` ek terimleri için `_SheenStrength`/`_SssStrength` global kısma (şimdilik yok).
- Mevcut 5 test hatası (CombatTests, CoverageCombatTests, InventoryTests.LootSpawn, ZoneTests) bu görevden değil, paralel dalgadan.

## GX1-isik-pozlama

Göz uyumu (otomatik pozlama) + fiziksel güneş/ortam. Yeni dosyalar: `Core/Domain/Models/LightingMath.cs` (saf matematik, 14 test: `LightingMathTests`), `Infrastructure/Rendering/Features/HarekatAutoExposureFeature.cs`, `Shaders/PostFx/AutoExposure.shader` ("HAREKAT/PostFx/AutoExposure"). `Atmosphere.cs`'e yalnız güneş/ortam değerleri eklendi.

- **Pozlama hattı** (`BeforeRenderingPostProcessing`, yalnız ana oyun kamerası; `HarekatRendererFeatures` anahtarı `AutoExposure`): (0) sahne rengi -> GxG log2-parlaklık ızgarası (hücre başına 4x4 örnek, parlaklık 32'ye kırpılı), (1) 8x8 merkez ağırlıklı log-ortalama, (2) 1x1 EV: `target = clamp((log2(ref) - avg) * strength, minEV, maxEV)` ve önceki kare EV'siyle üstel uyum (ping-pong RTHandle 1x1 R32F; ilk karede/yeniden oluşturmada anında yakalama), (3) çarpım geçişi `Blend DstColor Zero, Zero One` ile sahne rengi `*= 2^EV`. Ölçüm pozlama öncesinden yapılır: açık döngü, geri besleme/salınım yok. Ton eşleme + `ColorAdjustments.postExposure` (grade) bundan sonra aynen çalışır.
- **Uyum hızı**: karanlıktan aydınlığa (EV düşer) 4,5 /sn hızlı, aydınlıktan karanlığa 1,2 /sn yavaş (`ExposureParams.SpeedToBright/SpeedToDark`). dt 0,1 sn'ye kırpılı.
- **Gerçekçi iç/dış**: telafi gücü < 1 (gündüz 0,65, gece 0,45); iç mekân tam açılmaz = koyu okunur, dış mekân parlak kalır. Referans parlaklık: gündüz 0,24, şafak/akşam 0,17, gece 0,05. Harita kırpmaları `LightingMath.ExposureFor(time, mapId)` (Ayaz kar: daha geniş kısma). Gündüz EV [-1,5, +1,5], gece [-0,6, +2,2].
- **Kademe**: Düşük kapalı (mevcut pozlama aynen), Orta 32x32 ızgara, Yüksek/Ultra 64x64 (`LightingMath.ExposureForTier`). Gölgelendirici bulunamazsa tek uyarı + özellik kendini kapatır.
- **Çalışma zamanı**: `AutoExposureSettings.Amount` (0..1 etki), `EvBias` (bölgesel kayma, örn. iç mekân tetikleyici), `RequestSnap()` (ışınlanma/sahne geçişi/ölüm kamerası sonrası anında yakala).
- **Fiziksel güneş** (`Atmosphere.ApplyPhysicalSun`): renk sıcaklığı yüksekliğe göre 2000 K (ufuk) -> 5800 K (>=60°) (`SunKelvin`, sqrt eğrisi; 10°'de ~3500 K), `KelvinTint` (parlaklığı 1'e normalize) ön ayar rengiyle çarpımsal karıştırılır (gündüz 0,6, şafak/akşam 0,35, gece 0 = ay değişmez). Yoğunluk Kasten-Young hava kütlesiyle `SunIntensityFactor`; ön ayara yarı ağırlıkla uygulanır (alçak güneş biraz sönük). Gölge gücü: açık 1,0, yağmur 0,55, kar 0,62 (`ShadowStrength`).
- **Ortam** (`Atmosphere.ApplyPhysicalAmbient`): gökyüzünden ortam, yağmur/kar örtüsünde dağınık ışık oranı artar (`AmbientScale`); gölge dolgusu `ShadeFill` ekvator/zemin ortamını gökyüzüne doğru kaldırır (gölge/iç mekân probe ile uyumlu, gece ~0,02). HDRI `SkyOverride` varsa SH yoğunluğu (`RenderSettings.ambientIntensity`) ölçeklenir. Küresel `_HK_ShadeFill` (0..0,3) gölgelendiriciler için yazılır.
- **Doğrulama (Unity'de)**: Shader derlenmedi. Kontrol: (1) `HAREKAT/PostFx/AutoExposure` Always Included Shaders'ta, (2) karanlık iç mekâna girince ~1 sn'de açılma ama dışarıdan koyu, dışarı çıkınca ~0,3 sn'de kısılma, (3) Düşük kademede özellik pasif, (4) önceki parlaklık dengesi gündüz Kuzgun'da belirgin değişmemeli (gerekirse `ExposureFor` referansını ayarla; RenderGraph'ta `RasterCommandBuffer.SetGlobalTexture(int, RTHandle)` imzası derlemede doğrulanmalı).

## GX7-bulut-golge-atmosfer

Dosyalar: `Infrastructure/Rendering/CloudShadows(+Math).cs`, `AtmosphereDetail(+Math).cs`, `SkyEnvironment.cs` (tek kanca), `Tests/EditMode/CloudShadowsAtmosphereTests.cs`.

- **Bulut gölgeleri**: ana yön ışığına `Light.cookie` olarak döngüsel (TileableFbm) prosedürel doku; `UniversalAdditionalLightData.lightCookieSize` = 800 m, `lightCookieOffset` WindSystem yönünde (ışığın right/up eksenine izdüşüm, hız 3–14 m/sn) kaydırılır. URP cookie UV'si = (ışık uzayı - ofset)/boyut olduğundan ofset metre cinsindendir ve 800 m'de sarılır. Hava başına kapsama/derinlik (Açık 0.36/0.62, Yağmur 0.80/0.28, Kar 0.70/0.32). Düşük kademe ve gece kapalı. Doku 128/256/256 px (kademe 1/2/3). Tip/özellik yansımayla çözülür (verify stub'larında URP ışık verisi yok); bulunamazsa özellik uyarıyla kapanır.
- **Uzak sis halkaları**: iki dikey gradyanlı halka (r=1175/1148 m) uzak (3002) ve yakın (3003) dağ halkası arasında (sortingOrder ile); renk = FogColor + biraz güneş. Düşük 0, Orta 1, Yüksek/Ultra 2.
- **Sirüs**: r=1040 m yassı kubbe, anizotropik döngüsel gürültü (çizgili), rüzgâr yönüne döndürülür, yavaş UV kayması; yalnız açık havada. Düşük kapalı.
- **Toz zerreleri**: kamerayı izleyen 14x6x14 m kutuda dünya-uzayı ParticleSystem (Yüksek 48, Ultra 96); yalnız güneş ufuk üstünde + açık hava + gündüz/şafak/akşam + `VolumetricFog.IsActive`; güneşe bakışta ileri-saçılımla parlar, yumuşak giriş/çıkış.
- **Güneş parlaması**: büyük hale billboard + (Yüksek/Ultra) 3–4 lens hayaleti (ekran merkezine göre yansıtılmış hat); engel için kameradan güneşe tek `Physics.Raycast`, sönüm üstel. `HAREKAT_LENSFLARE_SRP` define'ı tanımlanırsa (asmdef versionDefines `com.unity.render-pipelines.core`) URP `LensFlareComponentSRP` + kod ile üretilmiş `LensFlareDataSRP` ana ışığa eklenir, sprite hayaletler kapanır (bu yol verify'da derlenmez; stub yok).
- Kademe: kendi sürücüleri `Atmosphere.QualityLevel`'ı saniyede bir yoklar; QualityTierApplier'a bağlanmak zorunda değil (istenirse `CloudShadows.Configure(...)`).

## GX4-silah-materyal

Silah modelleri Tarkov/MW tarzı fiziksel malzemeye geçti (yalnız `Infrastructure/Weapons/**` + `Shaders/Weapon/`).

- **Shader** `HAREKAT/Weapon/GunLit` (+ `HarekatGunLitPass.hlsl`): `_GunKind` 0 parkerize çelik, 1 anodize alüminyum, 2 polimer, 3 ahşap (damar+gözenek, G3), 4 boya, 5 kauçuk. Kenar aşınması: `WeaponMeshBuilder` her dörtgene UV1 (yerel u,v) + UV2 (metre boyutu) yazar, gölgelendirici kenara metre uzaklığından aşınma çıkarır (eğri yüzeylerde yalnız eksen uçları); çizik + geniş sürtünme; aşınan yer `_WearColor/_WearMetallic/_WearSmoothness` (çelik parlar, boya altından koyu metal, polimer açılır). Mikro doku türev tabanlı normal bozma (teğet gerekmez). Yağ parlaması: düşük frekanslı maske, pürüzsüzlük artar.
- **Namlu karbonu**: `_HarekatCarbon` (MPB) namlu ucuna ve kovan penceresine kurum, mat+koyu, ısıdan mavimsi temper. `WeaponCarbonState` silah başına kalıcı; `WeaponWearMath.CarbonPerShot(kategori)`, atışta `AddShot` (doygunlukta yavaşlar), zamanla `Clean` (%0,25/sn).
- **Optik cam** `HAREKAT/Weapon/OpticGlass`: kaplama tonu (mavi↔amber) fresnel ile kayar, yüksek yansıma.
- **Viewmodel ışığı**: renderer'lar artık `BlendProbes` (SH) + `BlendProbesAndSkybox` (yansıma) kullanır; gölgelendirici taban ortam ışığı 0.05 + `_HarekatGunFillBias` (sürücü SH parlaklığından 0,4 sn'de bir hesaplar; iç mekânda artar).
- **Kademe**: `WeaponMaterialDriver.SetTier(t)`: t<=0 mikro doku/çizik kapalı (`_HarekatGunLowDetail`); mesafeyle (2.5-7 m) doku sönümü.
- Hazır varlık (`ContentOverrides` GunMetal/GunPolymer/GunTan/GunWood) varsa o malzeme kullanılır. `Shader.Find` yoksa tek uyarı + `MaterialLibrary.Lit`.
- Testler: `WeaponWearMathTests`.

## GX8-decal-serpme

Dünyaya prosedürel çıkartma (decal) serpiştirme: su birikintisi, çamur, lastik izi, yaprak/iğne, yağ, duvar kiri, is, yosun, çatlak, afiş. Hepsi kodla üretilir (dosya/varlık yok).

**Dosyalar**
- `Infrastructure/Rendering/DecalLibraryMath.cs` — saf doku matematiği (`DecalKind` x11, `Sample(kind,u,v,seed)`, `BuildMaps` albedo/normal/maske; aynı tohum = aynı doku). `DecalLibrary.cs` — Texture2D + URP `Shader Graphs/Decal` malzemesi (tür x 3 varyant, tembel önbellek; gölgelendirici yoksa tek uyarı + null).
- `Infrastructure/World/DecalScatterPlanner.cs` — saf, deterministik plan (`Plan(input, tier)`), `DecalScatterTiers` bütçe tablosu. `DecalScatter.cs` — `Run(worldRoot, layout, seed, tier)`, `SetTier(tier)`, `Clear()`.
- Testler: `Tests/EditMode/DecalScatterTests.cs`.

**Kurallar:** su birikintisi = yol/köy yakını alçak (halka ortalamasından düşük) düz nokta, yağmurda 1x, açıkta 0.35x sayı ve +%50 boyut; çamur = toprak yol kenarı + birikinti çevresi; lastik izi = yol boyunca (toprak yol %75), çift şerit 1.9 m x 5-9 m; yaprak/iğne = ağaç altı (yüksek rakımda iğne); duvar kiri = bina dibi (raycast ile duvara oturur); is = baca (çatıya aşağı izdüşüm; ad "baca/chimney") + pencere altı akıntı; çatlak = bina çevresi/asfalt/karakol-FOB; yağ = üs/ocak/çiftlik/asfalt; yosun = dere kıyısı + orman; afiş = bina duvarı (grafiti/slogan yok). Su, dere, göl, köprü, bina içi hariç. RNG tür başına ayrı: bir türün bütçesi diğerini kaydırmaz.

**Kademe tablosu (Orta/Yüksek/Ultra; Düşük = 0, Decal renderer özelliği kapalı):** toplam üst sınır 320 / 700 / 1300; çizim mesafesi 45 / 70 / 110 m (büyük çıkartmada x0.8-1.6); solma 0.75 / 0.8 / 0.85; harita alanına göre x0.6-1.8. `QualityTierApplier` -> `DecalScatter.SetTier`: kademe 0 gizler, düşerken Rank01 yüksek olanları kapatır, mesafeyi günceller (kurulu kademeden yukarı çıkmak için Run yeniden).

**Kurulum:** DecalProjector yansımayla kurulur (Universal Runtime sürüm farkı güvenli); proje Decal renderer özelliğini `AssetGeneration` ile Orta+ kademede zaten ekler. DecalProjector veya `Shader Graphs/Decal` yoksa düz quad yedeği (`VfxMaterials.CreateDecal`, yüzeyin 4 cm üstü).

**Gerçek Unity'de doğrulanacak:** (1) `Shader Graphs/Decal` build'e girer mi (Always Included Shaders / Graphics ayarı); malzeme özellik adları (`_BaseColorMap`, `_NormalMap`, `_MaskMap`) shader graph sürümüyle uyuşuyor mu, değilse tek satır `DecalLibrary`. (2) Decal renderer özelliği Automatic tekniğinde Forward+ ile çalışıyor mu. (3) Normal haritası ham RGBA32 (RG yolu) doğru mu okunuyor. (4) 1000+ projector maliyeti; yüksekse tier tablosu `DecalScatterTiers.TierCap`. (5) Bina AABB'si döndürülmüş binalarda duvara ışın kaçırabilir (ıskalar atlanır, hata yok). (6) Açı solması (startAngleFade) bilerek varsayılan.

**ENTEGRASYON**
- `WorldGenerator.cs`: `Generate` içinde, `RockScatter` ve `LastStructureBounds` atamasından sonra (`meta.StructureBounds` dolduktan sonra), `if (!options.Headless) Guard("DecalScatter", () => DecalScatter.Run(root.transform, layout, seed, QualityTierApplier.LastTier >= 0 ? QualityTierApplier.LastTier : <ayarlardaki QualityLevel>));` (kademe kaynağı projedeki geçerli kalite seviyesi).
- `SceneBuilder.cs`/`ProjectSetup`: editörde önceden üretilmiş dünyada aynı çağrı gerekmez; çalışma zamanı üretiminde WorldGenerator yeter. Hava değişirse (Atmosphere.Apply) isteğe bağlı: `DecalScatter.Run(...)` yeniden (ıslaklık yeniden hesaplanır).

## GX5-bina-yipranma

Anadolu köy evi yıpranması / detayı. Kod: `World/BuildingGeneratorWeathering.cs` (BuildingGenerator artık `partial`; `Complete` sonunda `ApplyWeathering`), `World/BuildingWeathering.cs` (kademe + elektrik hattı), `World/BuildingWeatheringMath.cs` (saf matematik, `BuildingWeatheringMathTests`). Hepsi çarpıştırıcısız görsel kutu: gövde collider'ı, NavMesh, Destructible camlar değişmez. Kendi `System.Random` akışı (SeedFor) kullanır; bina yerleşimi/ganimet aynı kalır. Ruined binalarda uygulanmaz.

- **Sıva yamaları**: sıvalı duvarda dökülmüş yamalar (kırık kenarlı 3-4 parça, açık gri iç sıva halkası + altta tuğla/taş); pencere/kapı payı korunur.
- **Taban nemi**: kapı hariç 0.2-0.7 m düzensiz üst kenarlı Mud/ConcreteDark bandı.
- **Yağmur izleri**: pencere denizliğinden ve saçak/parapet altından ince koyu şeritler; düz damda metal su oluğu + iz.
- **Saçak**: Gable/Hip çatıda ahşap kiriş uçları (0.75 m aralık) + saçak tahtası. **Kiremit**: eğim boyunca 0.3 m kiremit sıraları (kırma çatıda trapez uzunlukla, %12 yenileme tuğlası) + sırt kiremitleri.
- **Pencere**: ahşap dikme/denizlik, çıkıntılı taş denizlik, gömük iç karanlık (kapalı iç panjur %18 / yarım perde %32). **Kapı**: ahşap alın, taş eşik, ikinci basamak, %35 aralık kanat.
- **Baca külahı**, çatı **TV anteni** (baca ile çakışmaz; düz damda IsFree), **çamaşır ipi** (duvardan direğe sarkık ip + renkli bezler), **iç mekân** (süpürgelik, ahşap tavan kirişleri, döşeme tahta araları), **elektrik girişi** (saçak altı konsol + yalıtkan, `ElektrikGirisi` boş nesnesi).
- **Kademe** (`BuildingWeathering.SetTier`, QualityTierApplier'da "BuildingWeathering" adımı): 0 yalnız nem/iz/saçak/baca; 1 + sıva yamaları, pencere, kapı, anten, elektrik; 2 + kiremit sıraları, çamaşır, iç mekân; 3 kablo sayısı 3 ve ince segment. Kademe, binalar üretilirken geçerlidir (yeni üretilenleri etkiler).

**Doğrulanacak (Unity)**: Mud/ConcreteDark yama ve iz renkleri gerçek ışıkta fazla koyu/açık mı; kiremit sırası kalınlığı (0.024 m) gölgede okunuyor mu; çamaşır direği komşu nesneye girmiyor mu (çarpışmasız).

**ENTEGRASYON**
- `LocationBuilder.cs`: köydeki binalar üretildikten sonra (BuildingResult listesi toplanınca) `BuildingWeathering.BuildPowerLines(results, parent, (x, z) => terrainHeight(x, z));` - direk + sarkık kablo hattı. Çağrılmazsa yalnız duvardaki konsollar görünür.
