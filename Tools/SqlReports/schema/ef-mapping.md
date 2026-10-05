# EF → MSSQL eşlemesi (HarekatDbContext)

Kaynak: `Backend/Harekat.Infrastructure/Persistence/HarekatDbContext.cs`  
Migration yok; tablo adları EF Core varsayılanıyla DbSet özellik adlarından türetilir.

## Ana şema (`Harekat`)

| DbSet | Tablo | Not |
|-------|--------|-----|
| Players | `Players` | `CareerStats` owned → `Stats_*` sütunları |
| Squads | `Squads` | `MemberIds` / `ReadyMemberIds` JSON string |
| MatchTickets | `MatchTickets` | `EloTolerance` ignore (hesaplanan) |
| Matches | `Matches` | `Teams` JSON (`List<MatchTeamSlot>`) |
| GameServers | `GameServers` | `Endpoint` ignore |
| Friendships | `Friendships` | |
| Seasons | `Seasons` | PK = `Number` (IDENTITY değil) |
| SeasonArchives | `SeasonArchives` | |
| Reports | `Reports` | `PlayerReport` |
| AuditLogs | `AuditLogs` | |

### Players önemli sütunlar

- `Id`, `Username`, `Email`, `CreatedAt`, `LastSeenAt`, `IsOnline`, `Region`
- `Stats_Matches`, `Stats_Wins`, `Stats_Kills`, `Stats_Headshots`, `Stats_BestPlacement`
- `Stats_TotalDamage`, `Stats_LongestSurvivalSeconds`, `Stats_Experience`, `Stats_Rank` (0–18)
- `SeasonXp`, `EloRating`, `IsBanned`, `Role`

### Matches

- `Status`: 0 Allocating … 3 Completed, 4 Cancelled
- Süre: `DATEDIFF(second, StartedAt, CompletedAt)` (yalnızca Completed)
- Yerleşim: `OPENJSON(Teams)` → `Placement`, `SquadId`, `SquadName`

### MatchTickets

- Bekleme: eşleşmiş biletlerde `Matches.CreatedAt - MatchTickets.EnqueuedAt`
- `Status`: 0 Queued, 1 Matched, 2 Cancelled, 3 Expired

### GameServers

- `Status`: 0 Starting … 4 Offline
- Doluluk: `CurrentPlayers / MaxPlayers`

## Telemetri (önerilen kalıcı şema)

Telemetry şu an bellek içi (`InMemoryEventStore`). Raporlar aşağıdaki tabloları varsayar; Cursor MSSQL’e taşıyınca `views/` / `indexes/` önerileri uygulanır.

| Tablo | Kaynak entity |
|-------|----------------|
| `Telemetry.MatchEvents` | `MatchEvent` |
| `Telemetry.PlayerSuspicionReports` | `PlayerSuspicionReport` |

`MatchEventType`: Kill=0, Hit=1, Death=2, Shot=3, Landing=4, PositionSample=5  
Silah kimlikleri: `pistol_sar9`, `ar_mpt76`, … (`WeaponIds`)
