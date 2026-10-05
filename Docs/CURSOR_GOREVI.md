# Cursor (Opus) Görevleri — HAREKÂT Online Altyapı

HAREKÂT, Türk askeri temalı, 10 kişilik timlerle oynanan bir FPP tim battle royale. Hedef, binlerce oyuncunun online oynaması.
- Unity oyununu aynı anda 16 Claude ajanı yazıyor.
- Web portalını ve tasarım işlerini ChatGPT/Codex yapıyor.

Aşağıdaki **6 görev birbirinden bağımsız**. Her birini ayrı bir Cursor ajanında (ayrı sekme veya background agent) aynı anda başlatabilirsin.
Başlatma cümlesi: *"Docs/CURSOR_GOREVI.md içindeki GÖREV X'i baştan sona uygula."*

## ORTAK KURALLAR (çakışma olmaması için)
- Her görev **yalnızca kendi klasörüne** yazar (aşağıda belirtilmiş).
- Şu klasörlere **asla yazma**: `Assets/`, `Packages/`, `ProjectSettings/`, `Web/`, `Design/`, `Marketing/`, `Localization/`, `Tools/BalanceCalc/`, `Docs/`. Okumak serbest.
- Rütbeler `Assets/_Project/Scripts/Core/Domain/Enums/MilitaryRank.cs` ile birebir aynı olmalı.
- Kariyer alanları `CareerStats.cs`, silah kimlikleri `Assets/_Project/Scripts/Application/Catalogs/WeaponIds.cs` ile aynı olmalı.
- Kod kalitesi: katmanlı mimari, SOLID, testler. Bittiğinde build ve test hatasız geçmeli; yaptıklarını özetle.
- **DURMA KURALI:** Görevin ana kısmı bitince durma. Aynı görevin aşağıdaki "UZATMA HEDEFLERİ" listesine sırayla geç.
  Hepsi bitince şu sırayla devam et:
  1. Test kapsamını %80'in üzerine çıkar.
  2. Uç durumları ve hata yönetimini sertleştir.
  3. Performans ölçümü ekle.
  4. README'yi diyagramlarla genişlet.
  Her adımın sonunda build ve testleri tekrar çalıştır.

---

## GÖREV 1 — Backend API (`Backend/`)
`Backend/Harekat.Backend.sln` — .NET 10, katmanlı:
- `Harekat.Domain`: Player, Squad (en fazla 10 kişi), MatchTicket, Match, GameServer, MilitaryRank, CareerStats.
- `Harekat.Application`:
  - Kayıt/giriş, profil
  - Tim kurma ve davet
  - Eşleştirme: N tim × 10 kişi; eksik koltuklar bot; bölge/ping tercihi
  - Sunucu tahsisi
  - Maç sonucu işleme: XP = `kills*100 + headshots*25 + (teamCount - teamPlacement)*150 + galibiyet 1000`, ardından rütbe güncellemesi
  - Sıralama tabloları
- `Harekat.Infrastructure`: EF Core (geliştirmede SQLite, üretimde PostgreSQL; NuGet yoksa JSON repository), PBKDF2 şifre hash'i, JWT.
- `Harekat.Api` (minimal API):
  - `/auth/register`, `/auth/login`, `/players/me`
  - `/squads`
  - `/matchmaking/queue`
  - `/servers/register`, `/servers/heartbeat`
  - `/matches/{id}/result` (yalnızca server key ile)
  - `/leaderboards`, `/health`
  - OpenAPI/Swagger
- `Harekat.Tests` (xUnit), `Dockerfile`, `docker-compose.yml` (api + postgres + redis).
- `Backend/README.md` (Türkçe): ölçekleme (durumsuz API, Redis kuyruğu) ve Unity entegrasyonu.

## GÖREV 2 — Dedicated Server Filosu ve Dağıtım (`Deploy/`)
- `Deploy/server/Dockerfile`: Unity Linux dedicated server build'ini çalıştıran imaj. Build yolu parametre olarak alınsın; port, region ve server key ortam değişkeniyle verilsin.
- `Deploy/k8s/`: Kubernetes manifest'leri.
  - Backend için Deployment, Service, HPA
  - Postgres StatefulSet, Redis
  - Ingress
- `Deploy/agones/`: Agones Fleet, FleetAutoscaler ve GameServer şablonları (maç başına bir sunucu, 40–60 oyuncu).
- `Deploy/terraform/`: bulut iskeleti (değişkenlerle; sağlayıcı seçimi README'de). Örnek modül: Kubernetes cluster ve node pool'lar.
- `Deploy/README.md` (Türkçe): binlerce eşzamanlı oyuncu için kapasite hesabı (maç başına oyuncu, sunucu başına CPU/RAM, bölgeler), maliyet tahmini tablosu, adım adım kurulum.

## GÖREV 3 — Telemetri ve Hile Tespiti Servisi (`Backend/Harekat.Telemetry/`)
Ayrı bir .NET 10 projesi; kendi `.sln` dosyası olsun ya da GÖREV 1'in çözümüne eklenebilir, ama yalnızca bu klasörde dosya oluştur.
- Maç olaylarını toplama (öldürme, isabet, ölüm konumu, atış sayısı) için toplu (batch) endpoint.
- Şüpheli istatistik kuralları:
  - İmkânsız isabet oranı
  - Çok yüksek kafa vuruşu oranı
  - Fiziksel olarak imkânsız hız
  - Duvar arkasından tutarlı isabet
- Şüpheliler için skor ve rapor listesi.
- Harita için ısı haritası verisi (ölüm ve iniş noktaları, 10 m grid).
- xUnit testleri ve Türkçe README.

## GÖREV 4 — Yük Testi Aracı (`Tools/LoadTest/`)
- .NET 10 konsol uygulaması: binlerce sanal oyuncu oluşturur.
  - Kayıt, giriş ve 10'luk tim kurma
  - Eşleştirme kuyruğu
  - Sahte maç sonucu gönderme
- Gecikme yüzdelikleri (p50, p95, p99), hata oranı ve saniyedeki istek sayısı raporu (konsol + CSV).
- Ayrıca `Tools/LoadTest/k6/` altında aynı senaryonun k6 betikleri.
- Türkçe README.

## GÖREV 5 — CI/CD (`.github/workflows/`)
- `backend.yml`: build, test, Docker imajı.
- `web.yml`: `Web/` için `npm ci`, lint ve build.
- `unity.yml`: game-ci (unity-builder) ile macOS ve Linux dedicated server build'leri. Unity lisansı secret'larla verilsin; `BatchEntry.BuildMac` metoduna referans.
- `release.yml`: tag ile sürüm, build artefaktlarını yükleme.
- `.github/README_CI.md` (Türkçe): hangi secret'ların gerektiği.

## GÖREV 6 — Discord Botu (`Tools/DiscordBot/`)
- .NET 10 ya da Node.js.
- Komutlar:
  - `/siralama` (sıralama tablosu)
  - `/profil <isim>` (rütbe ve istatistik)
  - `/tim` (tim arama ilanı)
  - `/sunucu` (sunucu durumu)
- Maç bitince kazanan timi kanala duyurur (backend'den webhook).
- Bot token'ı ortam değişkeniyle verilsin. Türkçe README.

---

# UZATMA HEDEFLERİ (ana görev bitince sırayla)

### GÖREV 1 — Backend API
1. **Parti/lobi sistemi:** tim komutanı hazır onayı, rütbe kontrolü, davet kodu, tim içi sohbet (SignalR hub).
2. **Arkadaşlık sistemi:** istek gönderme/kabul, çevrimiçi durumu (presence).
3. **Eşleştirme kalitesi:** rütbe/XP tabanlı beceri derecesi (Elo/TrueSkill benzeri), bölge bazlı kuyruklar, bekleme süresine göre esneyen kurallar, solo oyuncuları otomatik tim yapma.
4. **Sezon sistemi:** sezonluk sıralama, sezon sonu ödül rozetleri, sezon arşivi.
5. **Başarımlar (achievements):** "İlk Zafer", "Keskin Nişancı 100 kafa vuruşu", "Komuta Devri" gibi 30+ başarım, ilerleme takibi.
6. **Kozmetik envanter:** kamuflaj ve bere rengi açılımları (yalnızca kozmetik), sahiplik API'si.
7. **Moderasyon:** yasaklama/sessize alma, rapor sistemi, yönetici rolleri ve yetkilendirme (policy tabanlı).
8. **Güvenlik:** rate limiting, refresh token, e-posta doğrulama akışı (sahte e-posta servisiyle), denetim günlüğü (audit log).
9. **Gözlemlenebilirlik:** OpenTelemetry iz ve metrikleri, yapılandırılmış loglama (Serilog), `/metrics` için Prometheus.
10. **Unity istemci SDK'sı:** yalnızca `Backend/ClientSdk/` altında, Unity'ye kopyalanmaya hazır, bağımlılıksız bir C# istemcisi (UnityWebRequest kullanmayan, saf HttpClient; Unity'ye uyarlama notlarıyla).

### GÖREV 2 — Dağıtım
1. Çok bölgeli kurulum: İstanbul/Frankfurt/Amsterdam; ping tabanlı yönlendirme.
2. Mavi-yeşil (blue-green) dağıtım ve geri alma (rollback) betikleri.
3. Gizli bilgi yönetimi: sealed-secrets ya da external-secrets.
4. Yedekleme: Postgres için PITR, Redis kalıcılığı, felaket kurtarma runbook'u.
5. Grafana panoları (JSON): aktif oyuncu, eşleşme süresi, sunucu doluluğu, hata oranı. Prometheus alarm kuralları.
6. Yerel geliştirme için kind/minikube ile tek komutta kurulum (`make dev-up`).
7. Maliyet optimizasyonu: spot node'lar, gece saatleri ölçek kuralları, otomatik ölçekleme simülasyon tablosu.

### GÖREV 3 — Telemetri ve Hile Tespiti
1. Gerçek zamanlı akış: Redis Streams ya da Kafka uyumlu soyutlama.
2. Hile tespiti için kural motoru: kurallar JSON ile tanımlanabilsin, hot reload.
3. Oyuncu bazlı risk skoru zaman serisi ve otomatik inceleme kuyruğu.
4. Silah denge telemetrisi: silah başına öldürme, ortalama mesafe, TTK dağılımı raporu.
5. Isı haritası PNG üretimi (Kuzgun Vadisi koordinatları -512..512) ve sezonluk karşılaştırma.
6. Maç tekrarı (replay) veri formatı tasarımı ve sıkıştırılmış saklama.

### GÖREV 4 — Yük Testi
1. 10.000 eşzamanlı sanal oyuncu senaryosu: kademeli artış, soak testi (2 saat), ani yük (spike).
2. Dağıtık yük üretimi: birden çok makineden koordineli çalıştırma.
3. Sonuçları HTML raporla karşılaştırma (önceki koşu ile fark).
4. Darboğaz analiz rehberi ve otomatik "geçti/kaldı" eşikleri (SLO).

### GÖREV 5 — CI/CD
1. PR kontrolleri: format, analyzer'lar, güvenlik taraması (CodeQL, dependabot yapılandırması).
2. Unity EditMode testlerini CI'da koşturma (game-ci test runner).
3. Sürüm notu üretimi (conventional commits), otomatik changelog.
4. Staging ve production ortamları, onaylı dağıtım akışı.
5. Build önbelleği (Library cache) ile Unity build sürelerini kısaltma.

### GÖREV 6 — Discord Botu
1. Sezon sıralaması ve haftalık "En Çok Öldüren Tim" duyurusu.
2. Rütbe terfisi bildirimi; Discord rol senkronu (Discord'daki rolleri oyundaki rütbeye göre ayarlama).
3. Tim kurma eşleştirmesi: `/tim-ara` ile Discord üzerinden 10 kişilik tim toplama.
4. Moderasyon komutları ve log kanalı.
5. Turnuva modu: braket oluşturma, maç sonucu girişi, otomatik ilerleme.
