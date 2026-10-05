-- Silah kullanım ve öldürme oranları
-- Kaynak: Telemetry.MatchEvents (önerilen tablo; henüz EF migration yok)
-- EventType: Kill=0, Shot=3
SET NOCOUNT ON;

IF OBJECT_ID(N'Telemetry.MatchEvents', N'U') IS NULL
BEGIN
    SELECT
        CAST('schema_missing' AS nvarchar(40)) AS Status,
        CAST(N'Telemetry.MatchEvents yok — örnek veri / Cursor migration bekleniyor' AS nvarchar(200)) AS Message;
    RETURN;
END;

;WITH kills AS (
    SELECT
        WeaponId,
        COUNT(*) AS KillCount,
        SUM(CASE WHEN IsHeadshot = 1 THEN 1 ELSE 0 END) AS HeadshotKills,
        AVG(CAST(DistanceMeters AS float)) AS AvgKillDistanceM,
        AVG(CAST(TimeToKillMs AS float)) AS AvgTtkMs
    FROM Telemetry.MatchEvents
    WHERE EventType = 0 -- Kill
      AND WeaponId IS NOT NULL
      AND Timestamp >= DATEADD(day, -30, SYSUTCDATETIME())
    GROUP BY WeaponId
),
shots AS (
    SELECT
        WeaponId,
        COUNT(*) AS ShotCount
    FROM Telemetry.MatchEvents
    WHERE EventType = 3 -- Shot
      AND WeaponId IS NOT NULL
      AND Timestamp >= DATEADD(day, -30, SYSUTCDATETIME())
    GROUP BY WeaponId
)
SELECT
    COALESCE(k.WeaponId, s.WeaponId) AS WeaponId,
    ISNULL(k.KillCount, 0) AS KillCount,
    ISNULL(k.HeadshotKills, 0) AS HeadshotKills,
    ISNULL(s.ShotCount, 0) AS ShotCount,
    CAST(100.0 * ISNULL(k.HeadshotKills, 0) / NULLIF(k.KillCount, 0) AS decimal(5, 2)) AS HeadshotPct,
    CAST(1.0 * ISNULL(k.KillCount, 0) / NULLIF(s.ShotCount, 0) AS decimal(8, 4)) AS KillPerShot,
    k.AvgKillDistanceM,
    k.AvgTtkMs
FROM kills k
FULL OUTER JOIN shots s ON s.WeaponId = k.WeaponId
ORDER BY KillCount DESC;
