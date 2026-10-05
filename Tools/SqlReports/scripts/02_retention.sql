-- Elde tutma: D1 / D7 / D30
-- Yaklaşım: kohort = CreatedAt günü; tutma = LastSeenAt >= CreatedAt + N gün
-- Not: Oturum geçmişi yoksa bu üst sınırdır; PlayerSessions önerisi için views/ bakın.
SET NOCOUNT ON;

DECLARE @LookbackDays int = 60;

;WITH cohorts AS (
    SELECT
        Id,
        CAST(CreatedAt AS date) AS CohortDate,
        CreatedAt,
        LastSeenAt
    FROM dbo.Players
    WHERE CreatedAt >= DATEADD(day, -@LookbackDays, SYSUTCDATETIME())
      AND IsBanned = 0
),
agg AS (
    SELECT
        CohortDate,
        COUNT(*) AS CohortSize,
        SUM(CASE WHEN LastSeenAt >= DATEADD(day, 1, CreatedAt) THEN 1 ELSE 0 END) AS RetainedD1,
        SUM(CASE WHEN LastSeenAt >= DATEADD(day, 7, CreatedAt) THEN 1 ELSE 0 END) AS RetainedD7,
        SUM(CASE WHEN LastSeenAt >= DATEADD(day, 30, CreatedAt) THEN 1 ELSE 0 END) AS RetainedD30
    FROM cohorts
    GROUP BY CohortDate
)
SELECT
    CohortDate,
    CohortSize,
    RetainedD1,
    RetainedD7,
    RetainedD30,
    CAST(100.0 * RetainedD1 / NULLIF(CohortSize, 0) AS decimal(5, 2)) AS PctD1,
    CAST(100.0 * RetainedD7 / NULLIF(CohortSize, 0) AS decimal(5, 2)) AS PctD7,
    CAST(100.0 * RetainedD30 / NULLIF(CohortSize, 0) AS decimal(5, 2)) AS PctD30
FROM agg
ORDER BY CohortDate;

-- Özet (tüm lookback)
SELECT
    COUNT(*) AS PlayersInWindow,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 1, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS OverallPctD1,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 7, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS OverallPctD7,
    CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 30, CreatedAt) THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS OverallPctD30
FROM dbo.Players
WHERE CreatedAt >= DATEADD(day, -@LookbackDays, SYSUTCDATETIME())
  AND IsBanned = 0;
