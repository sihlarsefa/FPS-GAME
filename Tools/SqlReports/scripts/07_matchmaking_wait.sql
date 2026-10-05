-- Eşleştirme bekleme süreleri
-- Matched bilet: Matches.CreatedAt - MatchTickets.EnqueuedAt
SET NOCOUNT ON;

;WITH waits AS (
    SELECT
        t.Id AS TicketId,
        t.Region,
        t.AverageElo,
        t.Status,
        t.EnqueuedAt,
        m.CreatedAt AS MatchedAt,
        DATEDIFF(second, t.EnqueuedAt, m.CreatedAt) AS WaitSec
    FROM dbo.MatchTickets t
    INNER JOIN dbo.Matches m ON m.Id = t.MatchId
    WHERE t.Status = 1 -- Matched
      AND t.MatchId IS NOT NULL
      AND m.CreatedAt >= t.EnqueuedAt
      AND t.EnqueuedAt >= DATEADD(day, -30, SYSUTCDATETIME())
)
SELECT
    CASE
        WHEN WaitSec < 15 THEN '0-15sn'
        WHEN WaitSec < 45 THEN '15-45sn'
        WHEN WaitSec < 90 THEN '45-90sn'
        WHEN WaitSec < 180 THEN '90-180sn'
        ELSE '180sn+'
    END AS Bucket,
    COUNT(*) AS TicketCount,
    AVG(WaitSec * 1.0) AS AvgWaitSec,
    MIN(WaitSec) AS MinWaitSec,
    MAX(WaitSec) AS MaxWaitSec
FROM waits
GROUP BY
    CASE
        WHEN WaitSec < 15 THEN '0-15sn'
        WHEN WaitSec < 45 THEN '15-45sn'
        WHEN WaitSec < 90 THEN '45-90sn'
        WHEN WaitSec < 180 THEN '90-180sn'
        ELSE '180sn+'
    END
ORDER BY MIN(WaitSec);

SELECT
    Region,
    COUNT(*) AS MatchedTickets,
    AVG(WaitSec * 1.0) AS AvgWaitSec,
    AVG(AverageElo * 1.0) AS AvgElo
FROM waits
GROUP BY Region
ORDER BY Region;

-- Anlık kuyruk
SELECT
    Region,
    COUNT(*) AS QueuedNow,
    AVG(DATEDIFF(second, EnqueuedAt, SYSUTCDATETIME()) * 1.0) AS AvgCurrentWaitSec
FROM dbo.MatchTickets
WHERE Status = 0 -- Queued
GROUP BY Region;
