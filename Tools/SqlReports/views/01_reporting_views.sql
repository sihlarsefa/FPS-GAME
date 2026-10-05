-- ÖNERİ: Aktif oyuncu günlük özeti (salt okunur rapor view)
-- Uygulama: Cursor / DBA. Bu dosya yalnızca öneridir.
CREATE OR ALTER VIEW dbo.vw_DailyActivePlayers
AS
SELECT
    CAST(LastSeenAt AS date) AS ActivityDate,
    Region,
    COUNT(DISTINCT Id) AS ActivePlayers,
    SUM(CASE WHEN IsOnline = 1 THEN 1 ELSE 0 END) AS OnlineSnapshot
FROM dbo.Players
WHERE IsBanned = 0
GROUP BY CAST(LastSeenAt AS date), Region;
GO

CREATE OR ALTER VIEW dbo.vw_MatchDurationCompleted
AS
SELECT
    Id,
    Region,
    SeasonNumber,
    StartedAt,
    CompletedAt,
    DATEDIFF(second, StartedAt, CompletedAt) AS DurationSec
FROM dbo.Matches
WHERE Status = 3
  AND StartedAt IS NOT NULL
  AND CompletedAt IS NOT NULL
  AND CompletedAt > StartedAt;
GO

CREATE OR ALTER VIEW dbo.vw_SquadMatchResults
AS
SELECT
    m.Id AS MatchId,
    m.SeasonNumber,
    m.Region,
    m.CompletedAt,
    CAST(j.SquadId AS uniqueidentifier) AS SquadId,
    j.SquadName,
    CAST(j.Placement AS int) AS Placement,
    CAST(j.BotCount AS int) AS BotCount,
    CASE WHEN CAST(j.Placement AS int) = 1 THEN 1 ELSE 0 END AS IsWin
FROM dbo.Matches m
CROSS APPLY OPENJSON(m.Teams)
WITH (
    SquadId uniqueidentifier '$.SquadId',
    SquadName nvarchar(128) '$.SquadName',
    Placement int '$.Placement',
    BotCount int '$.BotCount'
) j
WHERE m.Status = 3;
GO
