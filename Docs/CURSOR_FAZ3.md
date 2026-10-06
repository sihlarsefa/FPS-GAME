# Cursor (Opus) — FAZ 3 Görev Listesi

**Proje:** HAREKÂT. Türk askeri FPP Tim Battle Royale.
- Unity 6000.6, URP.
- Sunucular **Windows Server**, veritabanı **MSSQL**, web **düz HTML/CSS/JS**.
- **Linux yok.**

**Durum (2026-10-05 20:30):**
- Unity projesi tüm assembly'lerde **0 hata**, 399/399 test geçiyor.
- Claude ajanları son modülleri (menüler, arazi, binalar) bitiriyor. Ardından **entegrasyon ve 6 alanlı inceleme** çalışacak ve Assets'teki çoğu dosyaya dokunacak.
- Cursor F2-1/2/3'ü ve Codex'in C2-1…8 görevlerini bitirdi. Teşekkürler.

**Faz 2'den kalanlar bu listeye taşındı.**
- **F2-4 Gerçek Unity doğrulaması** ve **F2-5 İnceleme**, Claude `Docs/DURUM.md`'ye "ENTEGRASYON/İNCELEME TAMAMLANDI" yazınca başlar. Talimatlar `CURSOR_FAZ2.md`'de.

Başlatma cümlesi: *"Docs/CURSOR_FAZ3.md içindeki GÖREV F3-X'i baştan sona uygula."*
F3-1…F3-9 birbirinden bağımsızdır; ayrı ajanlarda **aynı anda** çalıştırılabilir.

## 0. KURALLAR (çok önemli: Claude ile çakışmamak için)
**Cursor'a ait Unity yerleri (yalnızca bunlara yaz):**
- `Assets/_Project/Scripts/Editor/*`
- `Assets/_Project/Scripts/Infrastructure/Vehicles/*`
- `Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs`, `LocationBuilder.cs`, `TrainingRangeBuilder.cs`
- **Yeni (F3):**
  - `Assets/_Project/Scripts/Online/*` (yeni asmdef `Project.Online`)
  - `Assets/_Project/Scripts/Platform/*` (yeni asmdef `Project.Platform`)
  - `Assets/_Project/Scripts/Infrastructure/Content/*`
  - `Assets/_Project/Scripts/Infrastructure/Diagnostics/*`
  - `Assets/_Project/Scripts/Presentation/DevTools/*`
  - `Assets/_Project/Tests/PlayMode/*`
- **Bunların dışındaki Unity dosyalarına dokunma.** Claude'un mevcut bir dosyasına bir "kanca" (hook) satırı eklemek gerekirse, değişikliği `Docs/FAZ3_KANCALAR.md` dosyasına "dosya – ne eklenecek – neden" şeklinde yaz. Claude ya da sen F2-5 penceresinde uygularsınız.

**Unity dışı:** `Backend/`, `Deploy/`, `Tools/*` (UnityVerify ve AgentWorkflows hariç), `.github/`, `Web/`, `Wiki/` serbest.

**Git:**
- Yalnızca kendi dosyalarını `git add` ile ekle.
- Dosya değiştiren git komutları (checkout, reset, clean, stash, pull --rebase) **yasak**.

**Paket ekleme:**
- `Packages/manifest.json`'a yeni paket (Netcode, Transport) eklemek tüm projeyi etkiler; bunu **F2-4 penceresinde** yap.
- O zamana kadar paket API'lerini kullanan kod **ayrı asmdef'te** dursun ve `defineConstraints` ile korunsun. Örnek: `Project.Online` asmdef'inde `"defineConstraints": ["HAREKAT_NETCODE"]` ya da `versionDefines` ile `com.unity.netcode.gameobjects` paketi varsa tanımlanan bir sembol. Böylece paket yokken proje derlenmeye devam eder.

**C# tuzağı:** `Project.*` ad alanlarında Unity'nin `Application` sınıfını her zaman **`UnityEngine.Application`** diye tam adıyla yaz; kısa `Application.` yazarsan `Project.Application` ad alanına çözümlenir ve derleme kırılır.

**Doğrulama:** `zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_cursor --player --tests` her zaman 0 hata kalmalı.
- Not: verify.sh yalnızca Core, Application, Infrastructure, Presentation, Editor ve EditMode testlerini derler. `Scripts/Online` ve `Scripts/Platform` gerçek Unity'de (F2-4) doğrulanır. Gerekirse bu asmdef'ler için `Tools/UnityVerify` yanına ayrı bir betik yaz.

**İlerleme:** Her görev sonunda `Docs/FAZ2_DURUM.md` dosyasına bir "FAZ 3" bölümü aç ve tarih ile saatle yaz.

---

## GÖREV F3-1 — Online İstemci Katmanı (backend bağlantısı) · `Scripts/Online/*`
Mimari:
- Oyun kuralları Application katmanında.
- `INetworkSession` soyutlaması var; şu an `OfflineNetworkSession` kullanılıyor.
- Ana oyun Online assembly'sini **bilmez** (bağımlılığın tersine çevrilmesi).
- Online modül kendini `[RuntimeInitializeOnLoadMethod]` ile kaydeder.
- Kayıt noktası olarak `GameSession`'a eklenecek `static Func<INetworkSession> NetworkSessionFactory` kancasını `FAZ3_KANCALAR.md`'ye yaz.

1. `Project.Online.asmdef`:
   - Referanslar: Core, Application, Infrastructure, Presentation, UnityEngine.UI, Unity.InputSystem.
   - Netcode kısmı ayrı alt klasörde ve ayrı asmdef'te: `Project.Online.Netcode` (define-constraint'li).
2. `BackendClient` (UnityWebRequest, async/await ya da coroutine):
   - Kayıt/giriş.
   - JWT ve refresh token. PlayerPrefs'te saklanacaksa makine anahtarıyla AES şifreli tut.
   - `/players/me`, `/squads/*`, `/matchmaking/queue*`, `/leaderboards`, `/achievements`.
   - Zaman aşımı, tekrar deneme, hata modelinin Türkçe mesaja eşlenmesi.
   - DTO'lar `Backend/ClientSdk` ile **birebir aynı** olmalı; mümkünse kaynak paylaşımı yap.
3. `OnlineProfileService`: backend profili ile yerel `CareerStatsService` arasında birleştirme. Online modda tek doğru kaynak backend'dir; çevrimdışı oynanan maçlar kuyruklanır ve sonra gönderilir.
4. **Menü panelleri** (yeni dosyalar, `Scripts/Online/UI/*`; `UiFactory` ve `UiTheme` kullanılır):
   - `OnlineLoginPanel`
   - `SquadPanel`: 10 kişi, davet kodu, hazır durumu
   - `MatchmakingPanel`: bekleme süresi, iptal
   - `LeaderboardPanel`
   - Ana menüye bir "ONLİNE" butonu eklemek için kancayı `FAZ3_KANCALAR.md`'ye yaz.
5. Testler (EditMode, `Tests/EditMode/OnlineTests.cs` değil, kendi `Tests/PlayMode` ya da `Scripts/Online/Tests` asmdef'inde): DTO serileştirme, token yenileme mantığı, kuyruk durum makinesi.

## GÖREV F3-2 — Netcode: Dedicated Server ve İstemci Oturumu · `Scripts/Online/Netcode/*`
`CURSOR_FAZ2.md` F2-6 madde 3–6'nın tamamı burada. Kısaca:
- `NetcodeNetworkSession : INetworkSession` (Host / Client / DedicatedServer).
- `PlayerCommand`'in tick'li `ServerRpc` ile gönderilmesi.
- Sunucu simülasyonu; istemci tarafında tahmin ve uzlaştırma.
- `ShotRequest` doğrulaması; son 1 saniyelik hitbox geçmişiyle lag compensation.
- `NetworkVariable` ile can, zırh, mermi özeti; interpolasyonlu transform.
- İlgi alanı yönetimi (interest management): 60 oyuncu.
- Botlar yalnızca sunucuda çalışır.
- **`ServerBootstrap`:**
  - Komut satırı: `-server -port -region -backend -serverKey -maxPlayers`.
  - Backend'e kayıt ve heartbeat; maç bitince sonuç gönderme.
  - Grafik ve ses kapalı; 30 Hz.
- Paketler manifest'e **F2-4 penceresinde** eklenir. Kod şimdi yazılır.
- Gerçek doğrulama: Unity'de **Host + 1 Client** iki kopyayla (ParrelSync benzeri ya da build + editör).

## GÖREV F3-3 — Windows Server Altyapısı (F2-7'nin tamamı) · `Backend/`, `Deploy/windows/*`
1. **MSSQL varsayılan provider.** SQL Server için migration'lar, indeksler, yedekleme job'ları. Npgsql'i kaldır ya da isteğe bağlı yap.
2. **IIS:**
   - Hosting Bundle.
   - Site ve app pool PowerShell betiği.
   - HTTPS (win-acme), WebSocket.
   - Statik `Web/dist` ve `Wiki/dist`; `/api` için reverse proxy.
3. **`Backend/Harekat.ServerManager`** (.NET Worker Service, **Windows servisi**):
   - UDP 7777–7900 port havuzu.
   - Backend'den maç atama ve süreç başlatma (`HAREKAT_Server.exe -batchmode -nographics -port ...`).
   - Çöken süreci yeniden başlatma, log rotation, CPU/RAM izleme, heartbeat.
4. Güvenlik duvarı betiği. Kapasite tablosu: 1.000 / 5.000 / 10.000 eşzamanlı oyuncu için kaç Windows Server gerekir.
5. İzleme: windows_exporter + Grafana panosu ya da backend `/metrics`; Event Log entegrasyonu; uyarı kuralları.
6. `Deploy/windows/README.md`: sıfır Windows Server'dan canlı sisteme adım adım (Türkçe).

## GÖREV F3-4 — Hazır Varlık (Asset) Entegrasyon Altyapısı · `Infrastructure/Content/*`, `Editor/*`
Strateji: önce **hazır paketler** (Asset Store, Mixamo, Sonniss, Poly Haven), sonra gerçekçi özel modeller. Kodla üretilen görünüm yedek olarak kalır.
**Render:** yakın vadede **URP** sabit kalır. HDRP yalnızca kalite tavanı yetmezse ileride değerlendirilir (zorunlu değil).
**Arazi:** mevcut Terrain splat (Grass/DryGrass/Dirt/Rock…) korunur. Diz boyu gerçekçi bitki örtüsü için **kalite aşamasında** mesh foliage katmanı eklenir — ayrıntı `Design/Art/ENVIRONMENT.md` (GPU instancing, LOD, culling, wind, random scale). Prosedürel yedek → `ContentOverrides` yolu silah/asker/binada olduğu gibi çevre prop’larına da uygulanır.
1. **`ContentOverrides` ScriptableObject** (`Resources/ContentOverrides.asset`):
   - `WeaponId` → model prefab'ı (namlu ve el tutma noktası Transform adlarıyla).
   - `SoundId` → AudioClip listesi (rastgele seçim, ses ve pitch aralığı).
   - `MaterialId` → Material.
   - Asker → humanoid model + Animator Controller.
   - Araç (T-70, Kirpi) → prefab.
   - Bina stili → prefab listesi (isteğe bağlı).
   - Statik erişim: `ContentOverrides.TryGetWeapon(id, out GameObject)` vb. Yoksa false döner; çağıran prosedürel yedeğe düşer.
2. **Kancalar** (`FAZ3_KANCALAR.md`'ye yazılacak, Claude uygular): `WeaponModelFactory.Build`, `SoldierModel.Build`, `GameAudio.GetClip/Play`, `MaterialLibrary.Get`, `Helicopter/ArmoredCarrier` builder'ları önce override'a baksın.
3. **Editör aracı** "HAREKÂT/İçerik/Varlık Eşleyici" penceresi:
   - Projedeki prefab, clip ve material'leri listeler; sürükle-bırak ile kimliklere eşler.
   - Eşlemede doğrulama yapar: muzzle Transform var mı, ölçek, humanoid rig.
4. **Mixamo içe aktarma yardımcısı:** FBX'leri Humanoid olarak ayarlar; lokomosyon blend tree'si içeren bir **Animator Controller'ı otomatik üretir**: idle, walk, run, crouch, prone, aim offset, reload, death.
5. `Assets/ThirdParty/README.md`: paketlerin nereye koyulacağı (`Environment/` dahil), lisans kayıt tablosu.
6. Bu görevi Codex'in C3-1 "Hazır Varlık Listesi" çıktısıyla (`Design/Assets/*.csv`) uyumlu yap. CSV'den eşleme ön doldurması yapılabilsin.

## GÖREV F3-5 — Geliştirici Konsolu ve Hile Komutları (test için) · `Presentation/DevTools/*`
- **` (backquote)** tuşuyla açılan konsol. UGUI, `UiFactory` kullanılır.
- Komut geçmişi, otomatik tamamlama, yardım.
- **Komutlar:**
  - `give <silahId>`, `ammo`, `heal`, `god`, `noclip`
  - `spawn_bots <n> [tim]`, `kill_team <tim>`, `ai off|on`
  - `zone next|pause`, `timescale <x>`
  - `tp <lokasyon adı>` (WorldMetadata.Locations), `artillery here`
  - `rank <rütbe>`, `xp <miktar>`
  - `fps`, `netstats`, `screenshot`, `loadscene <ad>`
- Release build'de kapalı olsun; `-dev` komut satırı argümanıyla ya da Development Build'de açılsın.
- Konsolu açacak kanca, örneğin MatchBootstrap'te bir satır → `FAZ3_KANCALAR.md`.

## GÖREV F3-6 — Teşhis: Hata ve Çökme Raporu, Performans Göstergesi · `Infrastructure/Diagnostics/*`, `Presentation/DevTools/*`, `Backend/`
- `ClientLogCollector`: `Application.logMessageReceivedThreaded` ile son 500 satırı halka tamponda tutar. Exception'da ve çıkışta rapor oluşturur.
- **Backend ucu** `/telemetry/client-errors`:
  - Sürüm, sahne, sistem bilgisi (`SystemInfo`), stack trace.
  - Hız sınırı; MSSQL'de saklama.
  - Yönetici panelinde listeleme: Web'e uç ekle.
- **Performans göstergesi:**
  - Kare süresi grafiği ve %1 low.
  - GC tahsis sayacı (`Profiler` recorder'ları), draw call (`Unity.Profiling.ProfilerRecorder`).
  - Aktif bot ve mermi sayısı.
  - F3 ile aç/kapa.
- **Otomatik performans koşusu** `-perfrun`:
  - KuzgunVadisi'nde 60 botla 3 dakikalık sabit kamera turu.
  - Sonuçları `Logs/perf_*.json` olarak yazar.
  - Backend'e isteğe bağlı gönderir.

## GÖREV F3-7 — Steam Hazırlığı · `Scripts/Platform/*` (define-constraint: `STEAMWORKS_NET`)
- Steamworks.NET kurulduğunda derlenen `SteamPlatformService`:
  - Başlatma, kullanıcı adı ve SteamID.
  - Başarımlar: backend başarım id'leriyle eşleme.
  - Rich presence: "Kuzgun Vadisi — 23 asker kaldı".
  - Auth session ticket → backend doğrulama ucu: `/auth/steam`, Backend'e ekle.
- `Docs/STEAM_REHBERI.md` yerine `Scripts/Platform/README.md`: AppID alma, Windows depot'ları, build yükleme (SteamPipe), mağaza onay süreci.

## GÖREV F3-8 — Windows Kurulum Paketi ve Güncelleyici (Launcher) · `Tools/Installer/*`, `Tools/Launcher/*`, `Backend/`
- **Inno Setup betiği:**
  - `Builds/Windows`'tan kurulum dosyası üretir.
  - Masaüstü kısayolu, kaldırıcı, VC++ çalışma zamanı kontrolü, Türkçe ve İngilizce kurulum dili.
  - Kod imzalama notları.
- **Launcher** (.NET 10, WPF ya da WinForms; Windows):
  - Haber akışı (backend `/news`).
  - Sürüm kontrolü (`/client/version`), yama zip'ini indirme, SHA-256 doğrulama, kurulum.
  - "OYNA" butonu (oyunu argümanlarla başlatır); giriş bilgisini oyuna token ile aktarır.
- **Backend uçları:** `/client/version`, `/news` (MSSQL'de saklama, yönetici panelinden düzenleme).

## GÖREV F3-9 — PlayMode Test Altyapısı · `Assets/_Project/Tests/PlayMode/*`
`CURSOR_FAZ2.md` F2-4 madde 4'teki testleri **şimdi yaz**; F2-4 penceresinde çalıştırılacak.
- Asmdef: `Project.Tests.PlayMode`, `UNITY_INCLUDE_TESTS`; referanslar Core, Application, Infrastructure, Presentation, UnityEngine.TestRunner, nunit.
- **Testler:**
  - MainMenu yükleme (hata logu yok).
  - KuzgunVadisi duman testi: oyuncu ve en az 30 bot; intikal başlıyor; exception yok; hızlandırılmış zamanla InMatch'e geçiş.
  - Ateş etme ve isabet (mermi azalır, manken hasar alır).
  - Tim emri: F1 takip → botlar komutana yaklaşır.
  - Komuta devri: komutan öldürülür → `CommandTransferredEvent`.
  - TrainingRange yükleme.
  - Her testte `ScreenCapture` ile `Logs/screens/` görüntüleri.
- **Batch çalıştırma betiği:** `Tools/UnityVerify/run_playmode.sh`. Bu dosya Cursor'a ait; UnityVerify klasöründe yalnızca bu yeni dosya.

---

## Sıra önerisi
- **Şimdi (paralel):** F3-1, F3-3, F3-4, F3-5, F3-6, F3-8, F3-9. Ayrıca F3-2 ve F3-7 kod olarak yazılabilir.
- **Claude "TAMAMLANDI" yazınca:**
  1. F2-4 gerçek Unity doğrulaması.
  2. `FAZ3_KANCALAR.md`'deki kancaları uygula.
  3. Netcode paketlerini ekle.
  4. PlayMode testlerini çalıştır.
  5. F2-5 inceleme.
