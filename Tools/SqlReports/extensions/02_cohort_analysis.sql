-- UZATMA: Haftalık kayıt kohort analizi (CreatedAt haftası)
SET NOCOUNT ON;

;WITH base AS (
    SELECT
        Id,
        DATEADD(week, DATEDIFF(week, 0, CreatedAt), 0) AS CohortWeek,
        CreatedAt,
        LastSeenAt,
        Stats_Matches AS Matches,
        Stats_Wins AS Wins
    FROM dbo.Players
    WHERE CreatedAt >= DATEADD(week, -12, SYSUTCDATETIME())
      AND IsBanned = 0
)
SELECT
    CohortWeek,
    COUNT(*) AS CohortSize,
    AVG(CAST(Matches AS float)) AS AvgMatches,
    AVG(CAST(Wins AS float)) AS AvgWins,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 1, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD1,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 7, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD7,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 28, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD28,
    CAST(100.0 * SUM(CASE WHEN Matches >= 5 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctActivated5Matches
FROM base
GROUP BY CohortWeek
ORDER BY CohortWeek;
