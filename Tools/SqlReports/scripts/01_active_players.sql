-- Günlük / haftalık / aylık aktif oyuncu (DAU / WAU / MAU)
-- Kaynak: Players.LastSeenAt (UTC)
SET NOCOUNT ON;

DECLARE @Now datetimeoffset = SYSUTCDATETIME();

;WITH bounds AS (
    SELECT
        CAST(@Now AS date) AS TodayUtc,
        DATEADD(day, -6, CAST(@Now AS date)) AS WeekStart,
        DATEADD(day, -29, CAST(@Now AS date)) AS MonthStart
)
SELECT
    'dau' AS Metric,
    COUNT(DISTINCT p.Id) AS Value
FROM dbo.Players p
CROSS JOIN bounds b
WHERE p.LastSeenAt >= CAST(b.TodayUtc AS datetimeoffset)
  AND p.IsBanned = 0

UNION ALL

SELECT
    'wau',
    COUNT(DISTINCT p.Id)
FROM dbo.Players p
CROSS JOIN bounds b
WHERE p.LastSeenAt >= CAST(b.WeekStart AS datetimeoffset)
  AND p.IsBanned = 0

UNION ALL

SELECT
    'mau',
    COUNT(DISTINCT p.Id)
FROM dbo.Players p
CROSS JOIN bounds b
WHERE p.LastSeenAt >= CAST(b.MonthStart AS datetimeoffset)
  AND p.IsBanned = 0

UNION ALL

SELECT
    'online_now',
    COUNT(*)
FROM dbo.Players
WHERE IsOnline = 1
  AND IsBanned = 0;

-- Son 30 gün günlük trend
SELECT
    CAST(p.LastSeenAt AS date) AS ActivityDate,
    COUNT(DISTINCT p.Id) AS ActivePlayers
FROM dbo.Players p
WHERE p.LastSeenAt >= DATEADD(day, -30, SYSUTCDATETIME())
  AND p.IsBanned = 0
GROUP BY CAST(p.LastSeenAt AS date)
ORDER BY ActivityDate;
