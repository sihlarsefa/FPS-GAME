# Cursor (Opus) — FAZ 2 Görev Listesi

**Proje:** HAREKÂT. Türk askeri temalı, 10 kişilik timlerle oynanan FPP Tim Battle Royale.
- Oyun motoru: Unity 6000.6.0f1, URP, Input System.
- Sunucu altyapısı: **Windows Server**.
- Veritabanı: **MSSQL (SQL Server)**.
- Web: **düz HTML/CSS/JS** (framework yok).
- Linux yok; Linux'a özgü çözüm önerme. Kubernetes/Agones yalnızca "ileride" notu olarak kalabilir.

**Neden bu dosya:** Claude ajanlarının kullanım limiti zaman zaman doluyor. Bu dosya Cursor'un:
1. Kendisine ayrılan Unity modüllerini **şimdi** yazmasını,
2. Claude durduğunda ya da işini bitirdiğinde Unity entegrasyonunu ve **gerçek Unity doğrulamasını** devralmasını,
3. Windows Server + MSSQL altyapısını tamamlamasını

sağlar.

Başlatma cümlesi: *"Docs/CURSOR_FAZ2.md içindeki GÖREV F2-X'i baştan sona uygula."*
Görevler numara sırasıyla önceliklidir. "ŞİMDİ" etiketliler hemen paralel başlatılabilir.

---

## 0. ÖNCE OKU — Kurallar ve koordinasyon

**Okunacak dosyalar:**
- [Docs/CONTRACTS.md](CONTRACTS.md): mimari, katmanlar ve tüm modül API'leri.
- [Docs/MODUL_SPESIFIKASYONLARI.md](MODUL_SPESIFIKASYONLARI.md): 25 modülün ayrıntılı tanımı.
- [Docs/DURUM.md](DURUM.md): **kim hangi dosyanın sahibi, şu an kim çalışıyor**. Bu dosyayı her görevin başında ve sonunda güncelle.

**Katmanlar:**
- Core ve Application saf C#; UnityEngine kullanmaz.
- Infrastructure Unity sistemlerini içerir; Presentation arayüz ve akışı; Editor yalnızca editörde çalışan kodu.
- Yukarı yönde referans verme.

**Sahiplik:**
- Cursor'a ŞİMDİ ait Unity dosyaları:
  - `Assets/_Project/Scripts/Editor/*`
  - `Assets/_Project/Scripts/Infrastructure/Vehicles/*`
  - `Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs`, `LocationBuilder.cs`, `TrainingRangeBuilder.cs` (ve yeni `LocationLayouts*.cs`)
  - Yeni klasör `Assets/_Project/Scripts/Infrastructure/Online/*` (GÖREV F2-6)
- Diğer Unity dosyalarına, **DURUM.md'de "Claude: TAMAMLANDI/DURDU" yazana kadar dokunma**. Claude ajanları o dosyalarda çalışıyor.

**Git:**
- Claude çalışırken `git checkout`, `git reset`, `git clean`, `git stash`, `git pull --rebase` gibi dosya değiştiren komutları **kesinlikle çalıştırma**.
- Commit atarken yalnızca kendi dosyalarını `git add` ile ekle (`git add -A` kullanma).
- Push serbest.

**Kod kuralları:**
- Public imzaları (CONTRACTS.md) değiştirme; ekleme serbest.
- Dosya silme. Eskimiş bir sınıf varsa `[Obsolete]` ile derlenebilir bırak.
- `.meta` dosyalarına dokunma. Unity'nin ürettikleri hariç.
- Arayüz metinleri Türkçe.
- Hiç harici asset yok: her şey prosedürel (primitive'ler, üretilen mesh'ler, `Texture2D`, `AudioClip.Create`).
- Unity 6.6:
  - `Rigidbody.linearVelocity` kullan.
  - `UnityEngine.Input` kullanma; Input System var.
  - `FindObjectsOfType` kullanma.
  - Yalnızca editörde çalışan kodu `#if UNITY_EDITOR` içine al.

**Hızlı doğrulama (saniyeler sürer, Unity açmadan):**
```bash
cd ~/Desktop/FPS-GAME
zsh Tools/UnityVerify/setup_deps.sh                                   # bir kez (UGUI, InputSystem, URP stub'ları)
zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_cursor --player --tests
```
- Her assembly için "N errors" ve hataları yazdırır; EditMode testlerini çalıştırır.
- Şu an Core ve Application 0 hata, **399/399 test geçiyor**. Bunu bozma.

**Durum günlüğü:** Her görevin sonunda `Docs/FAZ2_DURUM.md` dosyasına tarih ve saatle ne yaptığını, kalan sorunları ve bir sonraki adımı yaz. Claude limit sonrası buradan devam eder.

---

## GÖREV F2-1 — Unity Editör Kurulumu ve Build Araçları (ŞİMDİ) · `Assets/_Project/Scripts/Editor/*`
Tam tanım: MODUL_SPESIFIKASYONLARI.md → `editor-setup`. Aşağıdaki değişikliklerle uygula:

1. **Menü:** "HAREKÂT/Kurulum/1) Her Şeyi Kur". Ayrıca her adım tek tek çalıştırılabilsin.
   Batch giriş noktası: `Project.EditorTools.BatchEntry.SetupAll`.
2. **TagManager:** `GameLayers.CustomLayerNames` katmanları (8 Viewmodel, 9 Player, 10 Bot, 11 Hitbox, 12 Loot, 13 Projectile, 14 Vehicle) ve "Head" tag'i. `SerializedObject` ile yazılsın.
3. **PlayerSettings:**
   - Şirket "FPSGameStudio", ürün adı "HAREKÂT".
   - Active Input Handling = Input System Package.
   - Linear renk uzayı, varsayılan tam ekran, arka planda çalışma.
   - .NET Standard 2.1.
   - Windows için uygulama ikonu prosedürel PNG'den üretilsin.
4. **URP:** `Assets/_Project/Settings/Rendering/URP_{Low,Medium,High,Ultra}.asset` ve renderer data.
   - Renderer data için `UniversalRendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset")`.
   - Asset'i `UniversalRenderPipelineAsset.Create(rendererData)` ile oluştur.
   - `GraphicsSettings.defaultRenderPipeline` ata; QualitySettings seviye adlarını ve her seviyenin pipeline'ını ayarla.
5. **Sanat kütüphanesi:**
   - `MaterialLibrary.CreateFromSpec` ile her `MaterialId` için `.mat` dosyası → `Assets/_Project/Art/Materials`.
   - Prosedürel dokular PNG olarak kaydedilsin.
   - `Assets/_Project/Resources/GameArtLibrary.asset` oluşturulsun.
   - Varsayılan `PlayerMovementConfig` Resources altına konsun.
6. **Sahneler:** `SceneNames.PathOf` kullan.
   - `MainMenu.unity`: `MainMenuBootstrap`.
   - `KuzgunVadisi.unity`:
     - `WorldGenerator.Generate`
     - TerrainData, TerrainLayer'lar, ağaç prefab'ları, prosedürel mesh/doku/malzeme **asset olarak kaydedilsin**. Kaydedilmezse sahne kaydedilince kaybolurlar.
     - NavMeshData asset olarak kaydedilip WorldMetadata'ya atansın.
     - Mini harita PNG'si kaydedilsin.
     - Dünya geometrisi static işaretlensin.
     - `MatchBootstrap` objesi, directional light ve skybox eklensin.
   - `TrainingRange.unity`: `TrainingRangeBuilder` + `TrainingBootstrap`.
7. **Build Settings sahne sırası:** MainMenu, KuzgunVadisi, TrainingRange.
8. **BuildTool (Windows odaklı):**
   - "HAREKÂT/Build/Windows İstemci (x64)" → `Builds/Windows/HAREKAT.exe` (`BuildTarget.StandaloneWindows64`).
   - "HAREKÂT/Build/Windows Dedicated Server" → `Builds/WindowsServer/HAREKAT_Server.exe` (`StandaloneWindows64` + `StandaloneBuildSubtarget.Server`, headless).
   - "HAREKÂT/Build/macOS" → `Builds/macOS/HAREKAT.app` (geliştirici testi için).
   - Batch metotları: `BatchEntry.BuildWindowsClient`, `BuildWindowsServer`, `BuildMac`.
   - Gerekli Unity Hub modülü kurulu değilse (Windows Build Support (Mono) / Windows Dedicated Server Build Support) anlaşılır bir Türkçe hata ver ve kurulumu README'de anlat.
     Şu an bu Mac'te yalnızca **Mac Build Support** kurulu.
9. **Genel:**
   - Her adım idempotent olsun; tekrar çalıştırmak güvenli olmalı.
   - `AssetDatabase.StartAssetEditing/StopAssetEditing` ile try/finally kullan.
   - Adım bazlı Türkçe log yaz; hata bir adımı durdursun ama diğerlerini çalıştırmaya devam etsin.
10. **Sunucu modu:**
    - Dedicated server, komut satırı argümanlarıyla başlasın: `-server -port 7777 -region istanbul -backend https://... -serverKey ...`.
    - Bunun için Editor'de değil, **Infrastructure/Online** altında `ServerBootstrap` yaz (GÖREV F2-6).
    - Server build'de grafik ve ses kapalı olsun (`Application.isBatchMode`).

**Bitiş kriteri:** `verify.sh` Editor assembly'sinde 0 hata, ve Unity'de (GÖREV F2-4) `SetupAll` hatasız çalışıyor.

## GÖREV F2-2 — Sürülebilir Kirpi (ŞİMDİ) · `Infrastructure/Vehicles/*`
Tam tanım: MODUL_SPESIFIKASYONLARI.md → `infra-vehicle-drive`. Kısaca:
- `DrivableVehicle.Spawn(Vector3 position, float yaw)`:
  - Kirpi MRAP modeli kendi builder'ında olsun; Transport modülüne bağımlı olma.
  - Rigidbody: 14 ton, alçak ağırlık merkezi. Gövde collider'ı `GameLayers.Vehicle` katmanında.
  - 4 raycast süspansiyonlu teker (yay/sönüm), yanal tutunma, çekiş, fren, en fazla ~85 km/s.
  - Eğimde dengeli, devrilme önleyici. Görsel tekerler dönsün ve direksiyon kırsın.
  - Motor sesi hıza göre pitch değiştirsin; toz efekti olsun.
- **Binme/inme:**
  - `TryEnter(Combatant)`: sürücü koltuğu.
  - `Exit()`: aracın yanına, yere indirir.
  - `SetInput(throttle, steer, brake)`; `DriverViewPoint`, `SpeedKmh`, `Health`.
- **Hasar:**
  - Mermiler gövdede durur.
  - Can 0 olunca `ExplosionSystem.Explode` ile patlar.
  - 25 km/s üstünde çarptığı askerlere `CombatService.ApplyEnvironmentalDamage(..., DamageSourceIds.Vehicle)`.
- `VehicleRegistry` statik liste ve `FindNearest`.
- **Ek iş:** 9 kişilik yolcu koltuğu (tim taşıma), yolcu kamera noktaları, yolcuların araçtan ateş edememesi (ileride açılabilir).

## GÖREV F2-3 — Lokasyonlar ve Dekor (ŞİMDİ) · `Infrastructure/World/PropFactory.cs`, `LocationBuilder.cs`, `TrainingRangeBuilder.cs`
Tam tanım: MODUL_SPESIFIKASYONLARI.md → `infra-world-locations`.
- **Bağımlılıklar:**
  - Binaları `BuildingGenerator.Build(BuildingSpec)` ile üret. Bu dosya Claude'un `infra-world-buildings` ajanına ait; düzenleme.
  - Harita verisi `WorldTypes.cs` / `MapLayout.CreateKuzgunVadisi`. Claude'un `infra-world-terrain` ajanına ait; okuma serbest.
- **PropFactory:** 20+ obje: kum torbası, Hesco, konteyner, Mühimmat Sandığı, varil, yanmış araç, askeri çadır, kamuflaj ağı, çit, saman balyası, kaya, tanksavar engeli, **Türk bayraklı direk** (`ProceduralTextures.TurkishFlag`, hafif dalgalanma), helipad, anten, sokak lambası, çeşme, kütük, odun yığını, beton bariyer, nöbetçi kulübesi.
- **LocationBuilder.BuildAll:**
  - Kuzgun Köyü ve Yamaç Köyü: 10–16 ev, cami, sokaklar, çeşme.
  - Sınır Karakolu: duvar, kapı, kuleler, bayrak.
  - İleri Üs: Hesco, çadırlar, helipad, araç noktaları.
  - Taş Ocağı.
  - Kuzgun Barajı: dere üzerinde beton duvar.
  - Röle Tepesi.
  - Ağıl.
  - Yıkık Köy.
  - Gözetleme noktaları.
  - Yol kenarı dekoru.
  - Çıktılar: yağma noktaları (İleri Üs/Karakol = Military), yapı sınırları, araç noktaları.
- **TrainingRangeBuilder:** 300×300 m "Atış Poligonu": atış şeritleri, 25/50/100/200/300 m levhaları, silah rafları, 2 binalı kill house, engeller, bayraklar.

## GÖREV F2-4 — Gerçek Unity Doğrulaması ve Entegrasyon (Claude bitince / durunca)
Claude bu ortamda Unity'yi açamıyor; sen açabilirsin. Bu görevin en değerli kısmı bu.
**Başlamadan önce:**
- DURUM.md'yi kontrol et.
- Ya Claude "TAMAMLANDI/DURDU" yazmış olmalı, ya da `find Assets -name '*.cs' -mmin -20` boş dönmeli (20 dakikadır değişiklik yok).
- Unity Editor açıksa kapat; batch mode aynı projeyi aynı anda açamaz.

```bash
U="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
P=~/Desktop/FPS-GAME; mkdir -p $P/Logs
# 1) İçe aktarma + derleme
"$U" -batchmode -nographics -quit -projectPath $P -logFile $P/Logs/import.log; grep -n "error CS" $P/Logs/import.log | head -50
# 2) Kurulum (sahneler, URP, asset'ler)
"$U" -batchmode -quit -projectPath $P -executeMethod Project.EditorTools.BatchEntry.SetupAll -logFile $P/Logs/setup.log
# 3) EditMode testleri (-quit kullanma)
"$U" -batchmode -nographics -projectPath $P -runTests -testPlatform EditMode -testResults $P/Logs/editmode.xml -logFile $P/Logs/tests.log
# 4) PlayMode duman testleri (grafik açık)
"$U" -batchmode -projectPath $P -runTests -testPlatform PlayMode -testResults $P/Logs/playmode.xml -logFile $P/Logs/playmode.log
# 5) macOS build + kısa çalıştırma
"$U" -batchmode -quit -projectPath $P -executeMethod Project.EditorTools.BatchEntry.BuildMac -logFile $P/Logs/build.log
```

**Yapılacaklar:**
1. Önce hızlı `verify.sh` ile **tüm assembly'lerde 0 hata** yap. Artık dosya sahipliği yok; modüller arası uyumsuzlukları çağıran tarafı sözleşmeye uyarlayarak düzelt.
2. Ardından **Unity batch derlemesinde 0 hata** yap. Stub ile Unity arasındaki farklar (özellikle URP API) burada çıkar.
3. `SetupAll` → sahneler oluşmalı.
   - Sahneyi kaydedip yeniden açınca dünya, terrain, ağaçlar, NavMesh ve mini haritanın durduğunu doğrula.
   - Duruyorsa prosedürel objeler asset olarak kaydedilmiş demektir.
4. `Assets/_Project/Tests/PlayMode/` yaz (yeni asmdef, `UNITY_INCLUDE_TESTS`):
   - **MainMenu yükleme testi:** hata logu yok, menü Canvas'ı var.
   - **KuzgunVadisi duman testi:** 20 sn içinde oyuncu ve en az 30 bot oluşmalı; intikal başlamalı; hiç `LogType.Exception` olmamalı; `MatchService` Insertion'dan InMatch'e geçmeli (ya da hızlandırılmış zamanla).
   - **Atış testi:** oyuncunun silahı ateşlenince mermi sayısı azalmalı; ateş edilen manken hasar almalı.
   - **TrainingRange yükleme testi.**
   - **Ekran görüntüsü:** `ScreenCapture` ile `Logs/screens/*.png`. Kullanıcıya göstermek için.
5. Hataları düzelterek döngüyü tekrarla: derleme → kurulum → test → build.
6. Her turun özetini FAZ2_DURUM.md'ye yaz: kaç hata, hangi dosyalar.

## GÖREV F2-5 — Kapsamlı Kod İncelemesi ve Düzeltme (F2-4'ten sonra)
Her alan için kontrol listesi. Bulduğun gerçek hataları düzelt; şüpheli olanları FAZ2_DURUM.md'ye yaz.
1. **Oyun akışı:**
   - MainMenu → KuzgunVadisi yükleme
   - `MatchBootstrap` Awake/Start sırası
   - Servis kaydı: CONTRACTS §2'deki **tüm** servisler
   - Araçlar → oyuncu ve botlar koltuklarda → intikal → iniş → InMatch
   - Daralan alan → ölümler → komuta devri → maç sonu ekranı → tekrar oyna ya da menüye dönüş
   - Atış Poligonu akışı
   - `Time.timeScale`, imleç kilidi, sahne değişiminde statik kayıtların temizlenmesi: `CombatantRegistry`, `LootRegistry`, `SmokeVolume`, `GameContext.Clear`
2. **Çatışma:**
   - Ateş zinciri: oyuncu → `WeaponRuntimeService.TryTrigger` → `BallisticsSystem.FireWeapon` → `Hitbox` → `CombatService` → `HealthService` → event'ler → HUD isabet işareti, öldürme akışı, istatistik
   - Dost ateşi kapalı
   - Zırh aşınması
   - El bombası, sis, topçu
   - Bölge hasarı
   - Ölünce eşya düşürme
3. **Yapay zekâ:**
   - Koltuktan inme → `NavMesh.SamplePosition` + Warp
   - Algı: asla dosta ateş etmemeli; sis görüşü kesmeli
   - Kama düzeni, emirler (F1–F4)
   - Komutan ölünce yeni komutanı takip
   - 60 bot için performans: GC tahsisi yok, raycast bütçesi, yol isteklerinin sınırlanması
4. **Dünya ve editör:**
   - Binalarda kapı ve rampa geçilebilirliği; NavMesh kapsaması (köy evleri, cami, karakol)
   - WorldMetadata serileştirmesi
   - Sahne kaydında kaybolan obje olmamalı
5. **Arayüz:**
   - EventSystem tek olmalı
   - Event aboneliklerinin karşılığında abonelikten çıkılmalı
   - Her karede string üretimi olmamalı
   - 16:9 ve 16:10 yerleşimi
   - Türkçe karakterler: LegacyRuntime fontu Ş, Ğ, İ harflerini gösteriyor mu?
6. **Performans:**
   - Hedef: Windows'ta orta seviye GPU ile 60 FPS.
   - Draw call sayısı (static batching, SRP Batcher), ışık sayısı, gölge mesafesi.
   - Ağaç sayısı ve terrain ayarları.
   - Profiler ile en pahalı 10 metot.

## GÖREV F2-6 — Online: Unity ↔ Backend ↔ Dedicated Server · `Assets/_Project/Scripts/Infrastructure/Online/*` (+ `Backend/` ClientSdk)
Mimari zaten sunucu-otoriteli tasarlandı:
- Oyun kuralları Application katmanında saf C#.
- Girdi `PlayerCommand` olarak gidiyor.
- Simülasyon sabit tick'li (`SimulationClock`, 30 Hz).
- Ağ soyutlaması `INetworkSession`; şu an `OfflineNetworkSession` var.

1. **BackendClient** (`Infrastructure/Online/BackendClient.cs`, UnityWebRequest, async):
   - Login ve register.
   - JWT saklama: PlayerPrefs'te şifreli, makineye bağlı anahtarla.
   - `/players/me` (rütbe, XP, kariyer).
   - `/squads`.
   - `/matchmaking/queue`: gir, çık, durum; sonuçta sunucu ip:port ve join ticket döner.
   - `/leaderboards`.
   - Maç sonucunu yalnızca sunucu gönderir.
   - Backend'deki ClientSdk ile aynı DTO'ları kullan.
2. **Kariyer senkronu:** `CareerStatsService` yerel kayıtları backend profiliyle birleştirsin. Online modda **tek doğru kaynak backend** olsun.
3. **Netcode:**
   - Paketler `com.unity.netcode.gameobjects` (2.13.2) ve `com.unity.transport`; editör sürümüyle gelen sürümler.
   - `NetcodeNetworkSession : INetworkSession` (Host / Client / DedicatedServer).
   - Oyuncu prefab'ında `NetworkObject` olsun. İstemci `PlayerCommand` gönderir (`ServerRpc`, tick'li); sunucu simüle eder.
   - Can, zırh, mermi ve envanter özeti `NetworkVariable` ile; konum ve rotasyon için interpolasyonlu ağ transform'u.
   - İstemci tarafında hareket tahmini (prediction) ve uzlaştırma (reconciliation).
   - Atış: istemci `ShotRequest` gönderir; sunucu ateş hızını ve mermiyi doğrular, balistiği kendisi simüle eder; **lag compensation** için son 1 saniyelik hitbox geçmişi tutulur.
   - Botlar yalnızca sunucuda çalışır.
   - Yağma, kapı, araç gibi etkileşimler sunucuda doğrulanır.
   - İlgi alanı yönetimi (interest management): uzaktaki oyuncuların güncelleme sıklığı düşürülür (60 oyunculuk maç).
4. **ServerBootstrap** (dedicated server girişi):
   - Komut satırını okur: port, region, backendUrl, serverKey, maxPlayers.
   - Backend'e `/servers/register` ve heartbeat gönderir.
   - Eşleştirmeden gelen maçı başlatır; maç sonunda `/matches/{id}/result` gönderir; sonra yeniden "boşta" durumuna döner.
   - Grafik ve ses devre dışı; hedef 30 Hz tick.
5. **Ana menüye "ONLİNE" bölümü:** giriş paneli, tim kur/davet, sıraya gir, eşleşme bekleme ekranı.
   - Presentation/UI dosyaları Claude'a ait. Bu kısmı **DURUM.md'de Claude TAMAMLANDI** yazdıktan sonra yap, ya da yeni dosyalar olarak ekle: `OnlineMenuPanel.cs`.
6. **Hile önleme (temel):**
   - Sunucu-otoriteli hasar ve hareket.
   - Hız ve ışınlanma kontrolü.
   - Ateş hızı doğrulaması.
   - Şüpheli olayları Telemetri servisine gönder (Backend/Harekat.Telemetry).

## GÖREV F2-7 — Windows Server Altyapısı (MSSQL + IIS + oyun sunucuları) · `Deploy/windows/*`, `Backend/*`
1. **MSSQL'i varsayılan yap:**
   - Backend'in varsayılan provider'ı **SQL Server** olsun.
   - Production connection string örneği: SQL Server 2022 Express ya da Standard.
   - EF Core migration'ları SQL Server için yeniden üret.
   - SQLite yalnızca birim testlerde ve hızlı yerel geliştirmede kalsın.
   - PostgreSQL/Npgsql bağımlılığını kaldır ya da tamamen isteğe bağlı yap. **Linux yok.**
2. **Veritabanı tasarımı:**
   - İndeksler: oyuncu adı, XP sıralaması, sezon.
   - Sıralamalar için indexed view ya da önbellek tablosu.
   - Maç sonuçları için partition stratejisi notu.
   - Yedekleme: SQL Agent tam, fark ve log yedekleri; `Deploy/windows/sql/backup-jobs.sql`.
   - Bakım: index rebuild ve istatistik güncelleme.
3. **IIS'te backend:**
   - ASP.NET Core Hosting Bundle kurulumu.
   - Site ve app pool betiği (PowerShell).
   - HTTPS sertifikası (win-acme / Let's Encrypt).
   - WebSocket'i aç (SignalR için).
   - `Deploy/windows/deploy.ps1`'i genişlet: build → publish → app pool'u durdur → kopyala → başlat → sağlık kontrolü.
4. **Web portalı:**
   - Düz HTML/CSS/JS `Web/dist`, IIS'te statik site olarak.
   - `/api` reverse proxy (URL Rewrite + ARR) ya da CORS ayarı.
5. **Oyun sunucuları (Windows Dedicated Server build'i):**
   - Her maç bir süreç: `HAREKAT_Server.exe -batchmode -nographics -port 77xx`.
   - **Windows servisi** olarak çalışan bir "Sunucu Yöneticisi" (.NET Worker Service, `Backend/Harekat.ServerManager` yeni proje):
     - Port havuzunu yönetir (UDP 7777–7900).
     - Backend'den maç ataması alıp süreç başlatır; çöken süreci yeniden başlatır.
     - Logları döndürür (rotation).
     - CPU ve RAM sınırını izler.
   - Güvenlik duvarı kuralları için PowerShell betiği (UDP port aralığı, yalnızca gerekli TCP portları).
   - Kapasite hesabı tablosu: maç başına CPU çekirdeği ve RAM, sunucu başına eşzamanlı maç, 1.000 / 5.000 / 10.000 eşzamanlı oyuncu için kaç Windows Server gerekir.
6. **İzleme:**
   - windows_exporter + Prometheus + Grafana (Windows'ta), ya da daha basit: Backend'de `/metrics` + bir Grafana panosu.
   - Event Log entegrasyonu.
   - Uyarılar: sunucu çöktü, veritabanı yavaş, kuyruk birikti.
7. **Dokümantasyon:** `Deploy/windows/README.md` adım adım kurulum (Türkçe): sıfır Windows Server → çalışan oyun altyapısı.
   - Mevcut Linux/k8s dokümanlarının başına "isteğe bağlı / ileride" notu ekle.

## GÖREV F2-8 — Web Portalı ile Entegrasyon (Codex'in Web işini bozmadan)
- Codex `Web/` klasörünü düz HTML/CSS/JS ile yazdı. **Web/ klasörüne yazma**; Codex'e ait.
- Backend tarafında web'in ihtiyaç duyduğu endpoint'lerin eksiksiz olduğunu doğrula: profil, maç geçmişi, sezon, başarımlar, yönetici uçları.
- Eksikleri ekle; OpenAPI dokümanını güncelle.
- `Web/` içindeki API çağrılarıyla backend arasında sözleşme testi yaz: `Backend/Harekat.Tests` altına; Web'deki endpoint listesini okuyan basit bir test.

---

## Bitiş kriterleri (Faz 2'nin tamamı)
- `verify.sh`: tüm assembly'lerde 0 hata, oyuncu (player) yapılandırmasında 0 hata, testlerin hepsi geçiyor.
- Unity batch: derleme 0 hata, `SetupAll` başarılı, EditMode ve PlayMode testleri geçiyor.
- macOS ve Windows istemci build'leri ile Windows Dedicated Server build'i alınıyor.
- Backend MSSQL ile çalışıyor: Windows Server kurulum betikleri ve dokümanı hazır.
- `Docs/FAZ2_DURUM.md` güncel.
