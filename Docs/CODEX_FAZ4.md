# Codex — FAZ 4 Görev Listesi (Cursor'un devri)

> **2026-10-06 01:50 GÜNCELLEME:** Codex de devreden çıktı. Bu listedeki X-1…X-9 görevlerini artık **Claude** yürütüyor (Unity batchmode build/test, CC0 varlık indirme, otomatik ekran görüntüsü ile görsel kontrol). Dosya referans olarak duruyor.

**Tarih:** 2026-10-06 · **Hazırlayan:** Claude
**Durum:** Cursor devreden çıktı. Unity içindeki doğrulama, shader, varlık, build ve sunucu işlerini **Codex** devralıyor. Claude kod yazmaya devam ediyor; ikiniz paralel çalışacaksınız.

Başlatma cümlesi: *"Docs/CODEX_FAZ4.md içindeki GÖREV X-N'i baştan sona uygula."*
Görevlerin çoğu paralel yürüyebilir. **X1 her şeyden önce bir kez koşmalı.** X2 ve X3 en yüksek önceliktir, çünkü kullanıcı gerçekçi görüntüyü bekliyor.

---

## 0. Proje özeti (kısa)
- **Oyun:** HAREKÂT. Türk askerî temalı, birinci şahıs, 10 kişilik timlerle battle royale.
- **Motor:** Unity **6000.6.4f1**, **URP 17.6**, Input System, C# 9. Proje kökü: `/Users/f2gomac/Desktop/FPS-GAME`.
  - Unity yolu: `/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity`.
  - `6000.6.0f1` kurulumu bozuk, **kullanma**.
- **Altyapı:** Windows Server + MSSQL + düz HTML/CSS/JS web. **Linux yok.**
- **Motor kararı:** Kullanıcı kararı kesin, **URP'de kalınıyor**. HDRP veya Unreal'e geçiş yok. Gerekçe: `Docs/GERCEKCILIK_PLANI.md` §1.2.
- **Görsel hedef:** Tarkov silah hissi + Sons of the Forest çevre kalitesi + bizim askerî atmosfer.
- **Kural:** Önce küçük **AAA Benchmark sahnesi** "gerçekten çok iyi" seviyesine gelecek. Kullanıcı onay vermeden büyük haritalara yayılmayacak.
- **Mimari (asmdef):** `Project.Core` → `Project.Application` → `Project.Infrastructure` → `Project.Presentation`. Ayrıca `Project.Editor`, `Project.Online(.Netcode)`, `Project.Platform`, `Project.Tests.EditMode/PlayMode`. Ayrıntı: `ARCHITECTURE.md`, `Docs/CONTRACTS.md`.
- **Altın kural:** Hazır varlık (override) yoksa prosedürel yedek her zaman çalışır. Hiçbir işin bu yedeği kırmasına izin verme.

## 1. KURALLAR

### Yazma izni (Cursor'dan devralınan alanlar)
- **Unity editör ve test kodu:**
  - `Assets/_Project/Scripts/Editor/*`
  - `Assets/_Project/Tests/PlayMode/*`
- **Shader'lar:** `Assets/_Project/Shaders/**`. Burada derleme hatası düzeltmek serbest. Shader **davranışını** değiştireceksen DURUM.md'ye not düş.
- **Hazır varlıklar:**
  - `Assets/ThirdParty/**`
  - `Assets/_Project/Resources/ContentOverrides.asset`
  - `Assets/_Project/Resources/Audio/Weapons/**`
  - `Assets/_Project/Resources/Audio/Ambience/**`
- **Unity proje ayarları:** `Packages/manifest.json`, `ProjectSettings/*`
- **Sunucu, dağıtım ve araçlar:**
  - `Backend/*`, `Deploy/*`, `.github/*`
  - `Tools/LoadTest`, `Tools/Installer`, `Tools/Launcher`, `Tools/DiscordBot`
  - `Tools/UnityVerify/run_playmode.sh`, `run_soak.sh` (yeni `*.ps1` de olur)
- **Önceki Codex alanları (aynen geçerli):** `Design/*`, `Web/*`, `Wiki/*`, `QA/*`, `Marketing/*`, `Localization/*`, `Tools/BalanceCalc|LocTool|SqlReports/*`
- **Rapor dosyaları:**
  - `Docs/DURUM.md` → "Günlük" bölümüne tarihli satır.
  - `Docs/FAZ2_DURUM.md` → "FAZ 6 — Codex" başlığı altına tur özeti.
  - `Docs/OYUN_TESTI.md`
  - `Logs/**`

### Oyun kodu (`Assets/_Project/Scripts/{Core,Application,Infrastructure,Presentation}`)
Bu kod **Claude'un** alanı.
- **Yapman gereken:** Derleme hatası ya da çalışma zamanı hatası görürsen düzeltme. Hatayı `Docs/DURUM.md`'ye şu biçimde yaz: dosya:satır, hata metni, Console log'u. Claude hızlıca düzeltir.
- **Tek istisna:** Unity'yi tamamen bloke eden **1–3 satırlık apaçık bir derleme hatası**. Bunu düzeltebilirsin ama DURUM.md'ye "Codex düzeltti: …" diye yaz.
- **Bu dosyalara kesinlikle dokunma, şu an Claude'un açık işleri:**
  - `Infrastructure/Audio/**`
  - `Presentation/Player/**`
  - `Infrastructure/AI/BotController.Combat.cs`
  - `Infrastructure/Combat/BallisticsSystem.cs`, `ExplosionSystem.cs`
  - `Infrastructure/Characters/**`
  - `Infrastructure/Weapons/**`
  - `Infrastructure/Rendering/CameraRig.cs`, `CombatScreenFx*`
  - `Presentation/UI/SettingsPanel.cs`
  - `Presentation/Bootstrap/HostageBootstrap.cs`

### Git ve dosya güvenliği
- **Asla:** `git checkout/reset/clean/stash/pull/rebase`, dosya silme, `.meta` silme. Claude ajanları aynı çalışma ağacında çalışıyor.
- **Commit:** Yalnızca **kendi yazdığın yollar**, açıklayıcı mesajla. `git add -A` **yapma**, yolları tek tek ekle.
- **Büyük ikili dosyalar:** ses, doku, model ve HDRI **Git LFS** ile. `.gitattributes` hazır; eksik uzantı varsa ekle.
- **Ses klasörleri:**
  - `Assets/_Project/Resources/Audio/Voice/v2/**` (~200 MB, TTS yer tutucu) da LFS'e girmeli.
  - Yayından önce bu sesler gerçek seslendirmeyle değişecek: `Docs/SESLENDIRME_SENARYOSU.md`.

### Unity'yi batch modda çalıştırma
```zsh
U=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
P=/Users/f2gomac/Desktop/FPS-GAME
# Kurulum (sahneler, URP asset'leri, materyaller, Always Included shader'lar, AAA benchmark sahnesi)
"$U" -batchmode -quit -projectPath $P -executeMethod Project.EditorTools.BatchEntry.SetupAll -logFile $P/Logs/setup.log
# EditMode testleri
"$U" -batchmode -projectPath $P -runTests -testPlatform EditMode -testResults $P/Logs/editmode.xml -logFile $P/Logs/editmode.log
# PlayMode duman testleri (grafik açık)
zsh Tools/UnityVerify/run_playmode.sh
# Soak
zsh Tools/UnityVerify/run_soak.sh
# Build
"$U" -batchmode -quit -projectPath $P -executeMethod Project.EditorTools.BatchEntry.BuildMac -logFile $P/Logs/build_mac.log
```
- **Hata arama:** `grep -n "error CS\|Shader error\|Exception" Logs/*.log`
- **Hızlı derleme kontrolü:** `zsh Tools/UnityVerify/verify.sh /tmp/v --player --tests`. Unity açmaz, csc ile derler ve EditMode testlerini koşar. Şu anki referans: **0 hata, 918/918**.

---

## GÖREV X-1 — Devralma Doğrulaması · EN ÖNCE
1. **`SetupAll`'u koş.** Yeni adımlar şunlar:
   - "Gölgelendiriciler (Always Included)" (`Editor/AlwaysIncludedShaders.cs`)
   - "AAA Benchmark sahnesi" (`AaaBenchmarkSceneBuilder.EnsureScene`)
   - SceneBuilder sonrası yansıma sondası ve HLOD kancaları (`RunPostWorldHooks`)
2. **Log'u temizle.** `Logs/setup.log` içinde **0 `error CS`** olmalı.
   - Shader hatalarını X-2'de çözeceksin, burada yalnızca listele.
   - `[HAREKÂT]` ile başlayan uyarıları da listele. Bunlar reflection ile URP alanı bulunamadığında çıkar ve çoğu gerçek URP 6000.6'daki alan adı farkıdır.
3. **Sahneler oluşmalı:**
   - MainMenu
   - KuzgunVadisi
   - AyazGecidi
   - MaviLiman
   - KartalYaylasi
   - TrainingRange
   - **AAA_Benchmark**
   - Hepsi Build Settings'te olmalı.
4. **Testleri koş:**
   - EditMode: Unity'deki sayı, verify.sh'taki 918'den **fazla** olmalı. `#if UNITY_EDITOR` içindeki testler yalnızca Unity'de koşar.
   - PlayMode: 6/6.
5. **URP alan adları:** Aşağıdaki reflection uyarılarının her biri için gerçek alan adını bul ve DURUM.md'ye tablo olarak yaz. Claude düzeltecek.
   - `PipelineTiers` / `UrpSetup`: `m_GPUResidentDrawerMode`, `m_LightProbeSystem`, `m_UpscalingFilter`, `m_RenderingMode`, `m_PrefilterWriteSmoothness`
   - `SsaoTuner`
   - `HarekatRendererFeatures`
6. **Frame Debugger doğrulaması:** Kuzgun Vadisi'nde Forward+, GPU Resident Drawer, STP ve APV gerçekten açık mı? Her kalite kademesi (Düşük, Orta, Yüksek, Ultra) için birer ekran görüntüsü al → `Logs/x1/`.
7. Özeti `Docs/FAZ2_DURUM.md` → "FAZ 6 — Codex / X-1" başlığına yaz.

## GÖREV X-2 — Shader Derleme ve Düzeltme · `Assets/_Project/Shaders/**` · EN YÜKSEK GÖRSEL ÖNCELİK
Claude bu shader'ları **derleyemeden** yazdı, çünkü Unity onun ortamında açılmıyor. Kaynak olarak URP 17.6 ShaderLibrary ve RenderGraph API'si kullanıldı. Senin işin bunları Unity'de derleyip çalışır hale getirmek.

| Shader / dosya | C# tarafı | Not |
|---|---|---|
| `ScreenSpace/ContactShadows.shader`, `SSRLite.shader`, `HarekatSsCommon.hlsl` | `Rendering/Features/HarekatContactShadowsFeature.cs`, `HarekatSsrLiteFeature.cs`, `HarekatRendererFeatures.cs` | **Y yönü / UV hizasını** kontrol et (`HkProject` / `HkWorldFromDepth`). Kontak gölge ana ışık gölge anahtarlarına bağlı |
| `Volumetric/HarekatVolumetricFog.shader` | `Rendering/Volumetric/*` | 3 RenderGraph geçişi (raymarch, temporal, composite) |
| `Terrain/HarekatTerrainLit.shader` | `World/TerrainShaderBinder.cs` | Height-blend, triplanar, makro varyasyon. Terrain `materialTemplate` uyumu |
| `Surface/HarekatParallaxLit.shader` + ıslak çamur | `MaterialLibrary.TryWetVariant` | POM. `_HarekatWetness` global |
| `Vegetation/HarekatVegetationWind.shader` + çim shader'ı | `World/Grass/*`, `WindSystem` | `_InstColor` buffer düzeni `GrassInstance` struct'ı ile aynı olmalı (Matrix4x4, Vector4). `UNITY_TEXTURE_STREAMING_DEBUG_VARS`, `_CLUSTER_LIGHT_LOOP` ve ProbeVolume pragma'larını kontrol et |
| Su / planar yansıma | `Rendering/PlanarReflection*.cs`, `WaterSurface*.cs` | `_PlanarReflectionTex` global |

**Her shader için:**
1. Console'da **0 hata, 0 pembe materyal** olmalı.
2. Frame Debugger'da geçişin göründüğünü ekran görüntüsüyle göster.
3. Shader açık ve kapalı haliyle önce/sonra ekran görüntüsü al → `Logs/x2/<shader>_on.png`, `_off.png`.
4. Orta kademede ms maliyetini ölç (Profiler GPU) ve tabloya yaz.
5. Düzeltmeyi yalnızca shader dosyasında yap. C# tarafında değişiklik gerekiyorsa DURUM.md'ye yaz.

**Teslim:** `Docs/FAZ2_DURUM.md` "X-2" tablosu: shader | derleme | görünüm OK? | ms (Orta) | not.

## GÖREV X-3 — AAA Benchmark Sahnesi · Kullanıcının göreceği ilk "vay" sahnesi
**Kaynaklar:** `Docs/CURSOR_AAA_BENCHMARK.md` (kontrol listesi), `Docs/AAA_URP_20.md` (20 madde ve her özelliğin Unity'de nasıl doğrulanacağı).
1. `AAA_Benchmark` sahnesini aç.
   - **İçerik:** ~150×150 m alan; çamur, kaya ve çim bölgeleri; küçük orman; içine girilebilen bir köy evi; Kirpi; nöbetçi asker; MPT-76 ile oyuncu; çelik hedefler; gündüz ve hafif sis.
2. **"Oyna" modu:**
   - Ateş et, reload yap, decal'leri ve VFX'i kontrol et.
   - Çime bas: çimin ezildiğini gör.
   - Evin içinde yansıma sondası ve kontak gölgeleri kontrol et.
3. **"Vitrin" modu:**
   - 6 çekimlik sinematik kamera yolunu oynat.
   - **F12** ile her çekimin ekran görüntüsünü `Logs/benchmark/` altına al.
4. **Ölçüm:**
   - **F3** performans HUD'u.
   - `-benchmark` argümanıyla CSV al (ortalama, %1 low, en kötü kare).
   - Hedef: Orta kademe 1080p'de 60 FPS, %1 low ≥ 45 (Mac'te ölçtüğünü not et; referans Windows PC'dir).
5. **20 madde:** Her biri için "çalışıyor / sorunlu / yok" tablosu çıkar ve ekran görüntüsü ekle.
6. **Kullanıcıya göster:** En iyi 6 ekran görüntüsünü `Logs/benchmark/SECKI/` klasörüne koy. DURUM.md'ye "Kullanıcıya gösterilecek" diye yaz.
7. Görsel olarak kötü görünen her şeyi madde madde DURUM.md'ye yaz: "çim çok parlak", "sis çok yoğun", "silah karanlık" gibi. Claude kodu düzeltecek; parametre ince ayarı senden gelecek.

## GÖREV X-4 — Ücretsiz Varlıkları İndir ve Bağla (Faz A, 0 USD) · `Assets/ThirdParty/**`
Kaynak listesi ve lisans kuralları: `Docs/GERCEKCILIK_PLANI.md` §3.2 ve §3.3. Varlık CSV'leri `Design/Assets/*.csv`'de.
- **Öncelik sırası (benchmark sahnesi için):**
  1. **Dokular (CC0):** ambientCG + Poly Haven API ile **2K** indir.
     - Çamur (ıslak ve kuru), orman zemini, çim/toprak karışımı, kaya (granit, kireçtaşı), çakıl yol, sıva duvar, taş duvar, kiremit, paslı metal, beton.
     - Biçim: ORM paketli; albedo BC7, normal BC5.
  2. **HDRI (CC0):** Poly Haven, gündüz/bulutlu/şafak/akşam, 4 adet, 4K. `SkyOverride` slotuna bağlanacak.
  3. **Ağaç ve kaya:** Megascans ve Megaplants. Fab üzerinden ücretsizse indir, **lisansı Unity kullanımına uygunsa**. Uygun değilse alternatif listesi çıkar. Her ağaçta LOD0-3 + billboard; ağaç başına LOD0 ≤ 15k üçgen.
  4. **Silah sesleri:** **Sonniss GDC** arşivleri (royalty-free). Paketin tamamını indirme.
     - Yalnızca 7.62 tüfek, 5.56, 9 mm, MG, bolt-action ve patlama; ayrıca uzak/yakın, iç/dış mekân varyantları.
     - Adlandırma kuralı: `Docs/SES_GERCEKCILIK.md` (A2 bölümü). `Resources/Audio/Weapons/<weaponId>/<katman>_<n>.wav`, sınıf yedeği `Resources/Audio/Weapons/_class/<kalibre>/...`.
     - Seviye kalibrasyonu (LUFS hedefi) yap.
  5. **Ortam sesleri:** rüzgâr, çam ormanı, gece, köy, yağmur → `Resources/Audio/Ambience/<biyom>/`.
  6. **Mixamo:** kullanıcının Adobe hesabı gerekir. Oturum açamıyorsan indirilecek animasyonların tam listesini yaz. Liste: tüfekle yürü, koş, çömel, yüzüstü sürün, ölüm ×3, reload, isabet tepkisi. İndirme adımlarını `Docs/MIXAMO_ADIMLAR.md`'ye yaz; kullanıcı yapar.
- **Bağlama:**
  - Menü: **HAREKÂT/İçerik/Varlık Eşleyici** → `ContentOverrides.asset`. Yeni slotlar: TerrainLayer, Vegetation, Rock, Prop, ViewmodelArms, WeaponAnimation, Sky, DecalSet.
  - Ardından **doğrulayıcıyı** çalıştır (ContentOverrides doğrulama menüsü): her slot için OK / UYARI / HATA. **HATA kalmasın.**
- **Lisans:** Her varlık için `Assets/ThirdParty/README.md` tablosuna satır ekle: ad | kaynak URL | lisans | yazar | tarih. CC-BY olanları `Resources/Credits.json`'a da ekle.
  - **Yasak:** CC-BY-NC, "Editorial Only", logolu model.
- **Kontrol:** Benchmark sahnesinde önce/sonra ekran görüntüsü → `Logs/x4/`.
- **Rapor:** Toplam boyutu yaz ve hepsinin LFS'te olduğunu doğrula.

## GÖREV X-5 — Ses İçe Aktarma Ayarları ve Ses Testi · `Editor/*`
1. **İçe aktarma ayarları:** Bir `AssetPostprocessor` yaz (`Editor/AudioImportRules.cs`).
   - `Resources/Audio/Voice/**`: Vorbis q≈0.5, Compressed In Memory, mono, Force To Mono.
   - `Resources/Audio/Weapons/**`: tetiklenen kısa sesler için Decompress On Load, ADPCM.
   - `Ambience/**`: Streaming.
2. **Ses testi:** Benchmark sahnesinde aşağıdakileri **kulakla** dinle. Her biri için "iyi / sorunlu" ve kısa not yaz.
   - **Atış sesleri:** yakın / 50 m / 200 m / 600 m.
   - **Mermi sesleri:** süpersonik çatlama ve vızıltı.
   - **Yankı:** vadide.
   - **Reload adımları:** şarjör çıkar, tak, mekanizma.
   - **Patlama:** kulak çınlaması.
   - **Telsiz ve diyalog v2:** stres tonları, yön bildirimi ("Saat üç yönü, yüz metre…").
   - **Ortam sesleri.**
3. Rapor `Docs/OYUN_TESTI.md` → "Ses" bölümüne. Teknik hataları DURUM.md'ye yaz.

## GÖREV X-6 — Soak ve Performans · `Tests/PlayMode/*`, `Tools/UnityVerify/run_soak.sh`
- **Soak:** F5-1'i yeniden koş. Önceki engel olan SoldierModel düzeldi. Rapor: `Logs/soak/RAPOR.md`.
  - Senaryo: 60 bot, 20 dk.
  - Ölç: bellek artışı, GC spike'ları, NavMesh hataları, Exception sayısı.
- **Performans:** Kuzgun Vadisi'nde 4 kademe için `-benchmark` CSV al.
  - Darboğaz analizi yap: CPU mu GPU mu, draw call, batch, SetPass.
  - GRD açık/kapalı batch sayısını karşılaştır.
- Çıktı: `Docs/PERFORMANS.md` → "Ölçüm (Codex)" bölümü.

## GÖREV X-7 — Windows: Build, Netcode, CI, Uçtan Uca (eski F4-2, F4-4, F4-6, F5-7)
**Ön koşul:** Kullanıcı Unity Hub'dan **Windows Build Support (Mono)** ve **Windows Dedicated Server** modüllerini kurmalı. Kurulu değilse kullanıcıya hatırlat ve diğer görevlere geç.
1. **Build:**
   - `BatchEntry.BuildWindowsClient`
   - `BatchEntry.BuildWindowsServer`
   - `BatchEntry.BuildMac`
2. **Netcode (F4-2, ayrıntı `Docs/CURSOR_FAZ4.md` ve `Docs/FAZ3_KANCALAR.md`):**
   - `manifest.json`'a `com.unity.netcode.gameobjects` (2.13.2) ve `com.unity.transport` ekle.
   - NetworkManager prefab'ı ve sahne kurulumunu SetupAll'a ekle.
   - Oyuncu prefab'ına NetworkObject.
   - ConnectionApproval, en fazla 60 oyuncu.
   - **Doğrulama:** Host + 1 Client ile intikal, ateş, hasar, maç sonu. Yapay 150 ms gecikmeyle lag compensation testi.
3. **CI (F4-4):**
   - `.github/workflows/unity.yml`: self-hosted Windows runner üzerinde EditMode testleri + Windows build'leri.
   - `verify.ps1`.
4. **Uçtan uca online (F5-7):** Windows Server üzerinde zincirin tamamı çalışmalı:
   - ServerManager → `HAREKAT_Server.exe` → maç → sonucun backend'e gitmesi → MSSQL
   - Rehber: `Deploy/windows/STAGING_PROVA.md`.
5. **Kapalı test paketi (F4-6):** Inno Setup kurulumu + Launcher.

## GÖREV X-8 — Yarım Kalan Cursor Fazları
- **F5-3 yerelleştirme:** Kalan UI metinlerini anahtara çevir. Yalnızca `Presentation/UI/*` içindeki **metinlere** dokun, ve sadece yukarıdaki yasak listede olmayan dosyalarda.
  - Not: `SettingsPanel.cs` şu an Claude'da.
  - Yeni isim anahtarları (`name.<id>`, `name.k.<id>`) tr/en'de var. **de/az/ar** dillerine de ekle.
- **F5-4 backend:** Kalan canlı oyun özellikleri (`Docs/CURSOR_FAZ5.md` F5-4).
- **F5-5 web v3:** Kalan sayfalar.
  - Yeni içerik: Kartal Yaylası haritası, Konvoy/Rehine modları, sezon kartı, tekrar sistemi.

## GÖREV X-9 — VFX Graph (isteğe bağlı, X-2 ve X-3'ten sonra)
- Spesifikasyon: `Docs/VFX_GRAPH_SPEC.md`.
- `com.unity.visualeffectgraph` paketini ekle. `HAREKAT_VFXGRAPH` versionDefine'ı `Project.Infrastructure.VfxGraph` asmdef'inde hazır.
- `.vfx` grafikleri editörde oluşturulur ve `Resources/VFX/*.vfx` altına kaydedilir. Liste:
  - namlu alevi ve dumanı (silah sınıfı başına)
  - yüzey başına isabet efektleri
  - patlama
  - rotor tozu
  - yağmur/kar
  - sis bombası
- Grafik arayüzü olmadan yapılamıyorsa **atla** ve rapor et. CPU parçacık yedeği zaten çalışıyor.

---

## Raporlama
- **Her görev sonunda:**
  - `Docs/DURUM.md` "Günlük" bölümüne tek satır: `- 2026-10-06 HH:MM — **Codex X-N:** ...`
  - `Docs/FAZ2_DURUM.md` → "FAZ 6 — Codex" altına özet.
- **Claude'a iş:** DURUM.md'de "**Codex → Claude:**" diye başlat. Dosya:satır, hata ve log alıntısı ver.
- **Kullanıcıya gösterilecek görseller:** `Logs/benchmark/SECKI/`.
