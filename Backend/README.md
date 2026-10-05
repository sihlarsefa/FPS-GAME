# HAREKÂT Backend API

Türk askeri temalı FPP tim battle royale için .NET 10 katmanlı backend.

## Mimari

```
┌────────────┐     JWT      ┌──────────────┐     EF/Memory    ┌────────────┐
│ Unity /    │─────────────▶│  Harekat.Api │────────────────▶│ SQLite /   │
│ ClientSdk  │◀─────────────│  Minimal API │                  │ PostgreSQL │
└────────────┘   SignalR    │  + LobbyHub  │                  └────────────┘
                            └──────┬───────┘
                                   │
                     ┌─────────────┼─────────────┐
                     ▼             ▼             ▼
              Application    Infrastructure   Domain
              (iş kuralları) (JWT, PBKDF2,    (Player, Squad,
                             EF/JSON)         Match, Rank…)
```

### Ölçekleme

- **Durumsuz API:** Oturum JWT ile taşınır; birden fazla API pod'u yatay ölçeklenebilir (HPA).
- **Redis:** `docker-compose` içinde Redis vardır; üretimde eşleştirme kuyruğu ve presence için kullanılabilir (geliştirmede bellek içi kuyruk).
- **Dedicated sunucular:** `/servers/register` + heartbeat; maç sonucu yalnızca `X-Server-Id` + `X-Server-Key` ile.
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
| `Harekat.Infrastructure` | EF Core (SQLite/Postgres) veya JSON/Memory, PBKDF2, JWT |
| `Harekat.Api` | Minimal API + Swagger + SignalR + Serilog + OpenTelemetry/Prometheus |
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

API: `http://localhost:8080` · Postgres: `5432` · Redis: `6379`

Ortam: `Storage__Provider=Postgres` (veya `Sqlite` / `Memory`).

## Ana uç noktalar

| Method | Path | Not |
|--------|------|-----|
| POST | `/auth/register`, `/auth/login`, `/auth/refresh` | JWT + refresh |
| GET | `/players/me` | Bearer |
| POST | `/squads`, `/squads/join`, `/squads/ready` | Tim max 10 |
| POST | `/matchmaking/queue` | N×10, eksik = bot |
| POST | `/servers/register`, `/servers/heartbeat` | Dedicated |
| POST | `/matches/{id}/result` | Server key header |
| GET | `/leaderboards` | experience/kills/wins/elo |
| GET | `/health` | |

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
