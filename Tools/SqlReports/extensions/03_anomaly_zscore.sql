-- UZATMA: Basit z-skoru anomali tespiti
-- 1) Günlük DAU sapması
-- 2) Oyuncu öldürme oranı (Stats) sapması
SET NOCOUNT ON;

;WITH daily AS (
    SELECT
        CAST(LastSeenAt AS date) AS D,
        COUNT(DISTINCT Id) AS Dau
    FROM dbo.Players
    WHERE LastSeenAt >= DATEADD(day, -45, SYSUTCDATETIME())
      AND IsBanned = 0
    GROUP BY CAST(LastSeenAt AS date)
),
stats AS (
    SELECT
        AVG(CAST(Dau AS float)) AS Mu,
        STDEV(CAST(Dau AS float)) AS Sigma
    FROM daily
)
SELECT
    d.D AS ActivityDate,
    d.Dau,
    s.Mu AS MeanDau,
    s.Sigma AS StdDau,
    CASE WHEN s.Sigma IS NULL OR s.Sigma = 0 THEN NULL
         ELSE CAST((d.Dau - s.Mu) / s.Sigma AS decimal(8, 3))
    END AS ZScore,
    CASE
        WHEN s.Sigma IS NULL OR s.Sigma = 0 THEN N'yetersiz'
        WHEN ABS((d.Dau - s.Mu) / s.Sigma) >= 2.5 THEN N'anomali'
        WHEN ABS((d.Dau - s.Mu) / s.Sigma) >= 1.8 THEN N'izle'
        ELSE N'normal'
    END AS Flag
FROM daily d
CROSS JOIN stats s
ORDER BY D;

;WITH player_kd AS (
    SELECT
        Id,
        Username,
        Stats_Kills AS Kills,
        Stats_Matches AS Matches,
        CASE WHEN Stats_Matches = 0 THEN 0.0
             ELSE CAST(Stats_Kills AS float) / Stats_Matches
        END AS KillsPerMatch
    FROM dbo.Players
    WHERE IsBanned = 0 AND Stats_Matches >= 10
),
kd_stats AS (
    SELECT AVG(KillsPerMatch) AS Mu, STDEV(KillsPerMatch) AS Sigma FROM player_kd
)
SELECT TOP (25)
    p.Id,
    p.Username,
    p.Kills,
    p.Matches,
    CAST(p.KillsPerMatch AS decimal(8, 3)) AS KillsPerMatch,
    CAST((p.KillsPerMatch - s.Mu) / NULLIF(s.Sigma, 0) AS decimal(8, 3)) AS ZScore
FROM player_kd p
CROSS JOIN kd_stats s
WHERE s.Sigma > 0
  AND (p.KillsPerMatch - s.Mu) / s.Sigma >= 2.5
ORDER BY ZScore DESC;
