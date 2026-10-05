# HAREKÂT Yük Testi Aracı

Binlerce sanal oyuncu ile Backend API'yi (GÖREV 1) zorlayan .NET 10 konsol aracı + k6 betikleri.

```
                    ┌─────────────────────┐
                    │  Harekat.LoadTest   │
                    │  (konsol / worker)  │
                    └─────────┬───────────┘
                              │ HTTP
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
   /auth/register      /squads (+join)     /matchmaking/queue
   /auth/login         /players/me         /matches/{id}/result
         │                    │                    │
         └────────────────────┴────────────────────┘
                              │
                    ┌─────────▼──────────┐
                    │  Metrik + SLO +    │
                    │  CSV / JSON / HTML │
                    └────────────────────┘
```

## Gereksinimler

- .NET 10 SDK
- (İsteğe bağlı) [k6](https://k6.io/) — `k6/` betikleri için
- Çalışan Backend API (`BASE_URL`, varsayılan `http://localhost:5080`)

## Hızlı başlangıç

```bash
cd Tools/LoadTest

# Derleme + test
dotnet build Harekat.LoadTest.sln
dotnet test Harekat.LoadTest.sln

# Dry-run (API yokken metrik boru hattını doğrula)
dotnet run --project Harekat.LoadTest -- --profile default --dry-run --players 40

# Gerçek API'ye karşı
dotnet run --project Harekat.LoadTest -- \
  --base-url http://localhost:5080 \
  --players 200 \
  --concurrency 50 \
  --scenario player
```

`scripts/run-local.sh` aynı dry-run akışını sarmalar.

## Sanal oyuncu senaryosu

Her sanal oyuncu sırasıyla:

1. **Kayıt** → `POST /auth/register`
2. **Giriş** → `POST /auth/login`
3. **Profil** → `GET /players/me`
4. **Tim** — her 10 oyuncudan biri komutan (`POST /squads`); diğerleri `POST /squads/{id}/join`
5. **Matchmaking** → `POST /matchmaking/queue`
6. **Sahte maç sonucu** (komutan) → `POST /matches/{id}/result` + `X-Server-Key`

## Senaryolar

| Senaryo | Açıklama | Profil / komut |
|---------|----------|----------------|
| `player` | Sabit N sanal oyuncu | `--scenario player -n 500` |
| `ramp` | 10k'ya kademeli artış | `--profile ramp-10k` |
| `soak` | 2 saat sabit yük | `--profile soak-2h` |
| `spike` | Taban → ani zirve → toparlanma | `--profile spike` |
| `distributed-coordinator` | Çok makineli koordinasyon | `--profile distributed` |
| `distributed-worker` | Worker düğümü | `scripts/run-distributed-worker.sh` |

### 10.000 eşzamanlı (uzatma)

```bash
# .NET ramp
dotnet run --project Harekat.LoadTest -- --profile ramp-10k --base-url http://API

# .NET soak (2h) — kısa deneme: --soak-minutes 5
dotnet run --project Harekat.LoadTest -- --profile soak-2h --soak-minutes 5

# .NET spike
dotnet run --project Harekat.LoadTest -- --profile spike
```

### Dağıtık yük

```
 Makine A (koordinatör)          Makine B/C/D (worker)
 ┌──────────────────┐            ┌──────────────────┐
 │ :9090 /register  │◄───────────│ offset + count   │
 │ /results aggregate│◄──────────│ yerel senaryo    │
 └──────────────────┘            └──────────────────┘
```

```bash
# Makine A
EXPECTED_WORKERS=4 PLAYERS=10000 ./scripts/run-distributed-coordinator.sh

# Her worker makinesi
COORDINATOR_URL=http://A_IP:9090 BASE_URL=http://API ./scripts/run-distributed-worker.sh
```

## Raporlama

Her koşu `reports/` (veya `--out`) altına yazar:

| Dosya | İçerik |
|-------|--------|
| `{runId}.csv` | İşlem başına p50/p95/p99, hata oranı, RPS |
| `{runId}.json` | Makine-okunur özet (karşılaştırma kaynağı) |
| `{runId}.html` | SLO geçti/kaldı, Δ metrikler, darboğaz listesi |

Konsolda da aynı özet basılır.

### Önceki koşu ile HTML karşılaştırma

```bash
dotnet run --project Harekat.LoadTest -- --dry-run --players 40 --run-id once-a
dotnet run --project Harekat.LoadTest -- --dry-run --players 40 --run-id once-b --compare once-a
# reports/once-b.html → Δ p95 / RPS / hata oranı
```

### SLO (geçti / kaldı)

Varsayılan eşikler (`SloThresholds`):

- p50 ≤ 100 ms, p95 ≤ 250 ms, p99 ≤ 500 ms
- hata oranı ≤ %1
- RPS ≥ 50

CLI: `--slo-p95 400 --slo-error 0.02 --slo-rps 100`

Çıkış kodu: `0` geçti, `2` kaldı, `1` hata.

### Darboğaz rehberi

Rapor, yavaş/hatalı uç noktalara göre Türkçe öneriler üretir (PBKDF2, Redis kuyruk, HPA, connection pool, vb.).

## k6

```bash
cd Tools/LoadTest
k6 run k6/harekat-scenario.js -e BASE_URL=http://localhost:5080
k6 run k6/ramp-10k.js -e BASE_URL=http://localhost:5080
k6 run k6/soak-2h.js -e BASE_URL=http://localhost:5080 -e DURATION=5m -e VUS=100
k6 run k6/spike.js -e BASE_URL=http://localhost:5080
```

Ortak API sarmalayıcı: `k6/lib/api.js` (register → login → squad → queue → match result).

Threshold'lar .NET SLO ile hizalıdır (`p(95)`, `http_req_failed`).

## Klasör yapısı

```
Tools/LoadTest/
├── Harekat.LoadTest.sln
├── Harekat.LoadTest/          # konsol uygulaması
│   ├── Client/                # API istemcisi
│   ├── Scenarios/             # player, ramp, soak, spike
│   ├── Metrics/               # p50/p95/p99, RPS, hata
│   ├── Reporting/             # konsol, CSV, JSON, HTML, SLO
│   ├── Distributed/           # koordinatör + worker
│   └── Profiles/              # JSON senaryo profilleri
├── Harekat.LoadTest.Tests/
├── k6/
├── scripts/
└── reports/
```

## Notlar

- Backend henüz hazır değilse `--dry-run` ile raporlama boru hattını doğrulayın.
- Tim boyutu sabittir: **10** (HAREKÂT sözleşmesi).
- Maç sonucu yalnızca `X-Server-Key` ile gönderilir (`--server-key`).
- Yalnızca `Tools/LoadTest/` altına yazılır; diğer klasörlere dokunulmaz.
