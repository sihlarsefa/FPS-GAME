-- Maç süresi dağılımı (tamamlanan maçlar)
SET NOCOUNT ON;

;WITH durations AS (
    SELECT
        m.Id,
        m.Region,
        m.SeasonNumber,
        DATEDIFF(second, m.StartedAt, m.CompletedAt) AS DurationSec
    FROM dbo.Matches m
    WHERE m.Status = 3 -- Completed
      AND m.StartedAt IS NOT NULL
      AND m.CompletedAt IS NOT NULL
      AND m.CompletedAt > m.StartedAt
)
SELECT
    CASE
        WHEN DurationSec < 300 THEN '0-5dk'
        WHEN DurationSec < 600 THEN '5-10dk'
        WHEN DurationSec < 900 THEN '10-15dk'
        WHEN DurationSec < 1200 THEN '15-20dk'
        WHEN DurationSec < 1800 THEN '20-30dk'
        ELSE '30dk+'
    END AS Bucket,
    COUNT(*) AS MatchCount,
    AVG(DurationSec) AS AvgSec,
    MIN(DurationSec) AS MinSec,
    MAX(DurationSec) AS MaxSec
FROM durations
GROUP BY
    CASE
        WHEN DurationSec < 300 THEN '0-5dk'
        WHEN DurationSec < 600 THEN '5-10dk'
        WHEN DurationSec < 900 THEN '10-15dk'
        WHEN DurationSec < 1200 THEN '15-20dk'
        WHEN DurationSec < 1800 THEN '20-30dk'
        ELSE '30dk+'
    END
ORDER BY MIN(DurationSec);

SELECT
    COUNT(*) AS CompletedMatches,
    AVG(DurationSec * 1.0) AS AvgDurationSec,
    MIN(DurationSec) AS MinSec,
    MAX(DurationSec) AS MaxSec
FROM durations;
