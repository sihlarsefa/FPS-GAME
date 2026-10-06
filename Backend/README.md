# HAREKÂT Backend API

Türk askeri temalı FPP tim battle royale için .NET 10 katmanlı backend.

## Mimari

```
┌────────────┐     JWT      ┌──────────────┐     EF/Memory    ┌────────────┐
│ Unity /    │─────────────▶│  Harekat.Api │────────────────▶│ SQL Server │
│ ClientSdk  │◀─────────────│  Minimal API │                  │ (üretim) / │
└────────────┘   SignalR    │  + LobbyHub  │                  │ Sqlite/Mem │
                            └──────┬───────┘                  └────────────┘
                                   │
                     ┌─────────────┼─────────────┐
                     ▼             ▼             ▼
              Application    Infrastructure   Domain
              (iş kuralları) (JWT, PBKDF2,    (Player, Squad,
                             EF/JSON)         Match, Rank…)
```

> **Üretim:** `Storage:Provider=SqlServer` (varsayılan). PostgreSQL/Npgsql kaldırıldı. Sqlite/Memory yalnızca yerel test.

### Ölçekleme

- **Durumsuz API:** Oturum JWT ile taşınır; birden fazla API pod'u yatay ölçeklenebilir (HPA).
- **Redis:** Windows host’ta `HarekatRedis` veya docker-compose; üretimde eşleştirme kuyruğu / presence.
- **Dedicated sunucular:** `Harekat.ServerManager` + `/servers/*` + `/matches/pending-allocation` + `/matches/{id}/claim`.
- **Bölge:** Eşleştirme `region` + ping tercihi ile ayrılır (`tr`, `eu`, …).

```
Oyuncu → API (N replica) → Matchmaking (bölge kuyruğu)
                              ↓
                         GameServer filosu
                              ↓
                         /matches/{id}/result → XP + rütbe + Elo
```

## Projeler

| Proje | Görev |
|-------|--------|
| `Harekat.Domain` | Player, Squad (max 10), MatchTicket, Match, GameServer, MilitaryRank, CareerStats |
| `Harekat.Application` | Auth, tim, eşleştirme, maç sonucu, sıralama, arkadaş, sezon, başarım, kozmetik, moderasyon |
| `Harekat.Infrastructure` | EF Core (**SQL Server** / Sqlite) veya Memory, PBKDF2, JWT |
| `Harekat.Api` | Minimal API + Swagger + SignalR + Serilog + OpenTelemetry/Prometheus |
| `Harekat.ServerManager` | Windows Service — port havuzu, spawn, heartbeat, metrics |
| `Harekat.Tests` | xUnit |
| `ClientSdk/` | Saf HttpClient Unity SDK (bağımlılıksız) |

## Hızlı başlangıç

```bash
cd Backend
dotnet restore
dotnet build
dotnet test
dotnet run --project Harekat.Api
```

Swagger: `http://localhost:5xxx/swagger`  
Health: `/health` · Metrikler: `/metrics`

### Docker

```bash
docker compose up --build
```

API: `http://localhost:8080` · Redis: `6379`

Ortam: `Storage__Provider=SqlServer` (veya `Sqlite` / `Memory`).

## Ana uç noktalar

| Method | Path | Not |
|--------|------|-----|
| POST | `/auth/register`, `/auth/login`, `/auth/refresh` | JWT + refresh |
| GET | `/players/me` | Bearer |
| POST | `/squads`, `/squads/join`, `/squads/ready` | Tim max 10 |
| POST | `/matchmaking/queue` | N×10, eksik = bot |
| POST | `/servers/register`, `/servers/heartbeat`, `/servers/release` | Dedicated / ServerManager |
| GET | `/servers`, `/servers/by-host/{host}` | Liste |
| GET | `/matches/pending-allocation?region=` | ServerManager poll |
| POST | `/matches/{id}/claim` | → `ClaimMatchResponse` (`match`, `serverId`, `endpoint`) |
| POST | `/matches/{id}/result` | Server key header |
| GET | `/matchmaking/queue-depth` | İzleme |
| GET | `/leaderboards?metric=&take=&map=&mode=` | experience/kills/wins/elo + harita/mod süzgeci |
| GET | `/achievements/me` | Bearer — katalog + ilerleme |
| POST | `/achievements/sync` | Bearer — istemci ilerleme birleştir + kilidi aç |
| GET | `/cosmetics/me` | Bearer |
| POST | `/cosmetics/equip` | Bearer |
| GET | `/seasons/active`, `/seasons/{n}/archive` | Sezon |
| GET | `/friends`, POST `/friends/request`, POST `/friends/{id}/accept` | Arkadaşlık |
| GET | `/anticheat/rules` | HAREKÂT hile eşikleri (JNG-90 / duvar / Kirpi) |
| GET | `/health` | |
| GET | `/metrics` | Prometheus |
| GET | `/news?lang=tr` | Launcher haber akışı |
| GET | `/client/version?channel=stable` | Yama zip URL + SHA-256 |
| PUT | `/admin/client/version` | Admin — sürüm yayımla |
| POST/PUT/DELETE | `/admin/news` | Admin — haber CRUD |

Hub: `/hubs/lobby?access_token=…&squadId=…`

## XP formülü

`kills*100 + headshots*25 + (teamCount - teamPlacement)*150 + (galibiyet ? 1000 : 0)`

Rütbeler Unity `MilitaryRank` ile birebir (Er=0 … Albay=18).

## Unity entegrasyonu

1. `Backend/ClientSdk/HarekatClient.cs` dosyasını Unity projene kopyala.
2. `new HarekatClient("https://api.harekat.example")` ile bağlan.
3. `RegisterAsync` / `LoginAsync` → token otomatik set edilir.
4. Tim kur → `CreateSquadAsync` → `EnqueueAsync`.
5. Lobi sohbeti için SignalR client (ayrı paket) veya REST ready endpoint.

```csharp
using var api = new Harekat.ClientSdk.HarekatClient("http://localhost:8080");
await api.RegisterAsync("Asker42", "a@mail.com", "password123");
var me = await api.GetMeAsync();
var squad = await api.CreateSquadAsync("Kurt Tim");
await api.EnqueueAsync("tr", 100);
```

## Uzatmalar (dahil)

1. Parti/lobi SignalR (`LobbyHub`)
2. Arkadaşlık + presence
3. Elo tabanlı eşleştirme + bekleme esnemesi
4. Sezon + arşiv ödül rozetleri
5. 30+ başarım
6. Kozmetik (kamuflaj/bere)
7. Moderasyon (rapor/ban/mute)
8. Rate limit, refresh token, e-posta doğrulama (fake), audit log
9. OpenTelemetry + Serilog + Prometheus `/metrics`
10. `ClientSdk/` saf HttpClient

## Test

```bash
dotnet test --collect:"XPlat Code Coverage"
```
