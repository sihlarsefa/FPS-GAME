-- UZATMA: Silah denge telemetrisini BalanceCalc tahminleriyle karşılaştırma
-- Beklenen TTK (saniye) BalanceCalc/out — body, 10m, armor0/helmet0 referansı
-- Gerçek AvgTtkMs Telemetry.MatchEvents'ten gelir
SET NOCOUNT ON;

IF OBJECT_ID(N'Telemetry.MatchEvents', N'U') IS NULL
BEGIN
    SELECT CAST(N'schema_missing' AS nvarchar(40)) AS Status;
    RETURN;
END;

;WITH expected AS (
    SELECT * FROM (VALUES
        (N'pistol_sar9',  0.48),
        (N'pistol_tp9',   0.45),
        (N'smg_sar109t',  0.30),
        (N'ar_mpt55',     0.28),
        (N'ar_mpt76',     0.32),
        (N'ar_g3a7',      0.36),
        (N'dmr_knt76',    0.40),
        (N'sr_jng90',     0.00), -- bolt; tek atış modeli
        (N'lmg_pmt76',    0.35),
        (N'sg_escort',    0.20)
    ) v(WeaponId, ExpectedTtkSec)
),
actual AS (
    SELECT
        WeaponId,
        COUNT(*) AS KillSample,
        AVG(CAST(TimeToKillMs AS float)) / 1000.0 AS ActualTtkSec,
        AVG(CAST(DistanceMeters AS float)) AS AvgDistM
    FROM Telemetry.MatchEvents
    WHERE EventType = 0
      AND WeaponId IS NOT NULL
      AND TimeToKillMs IS NOT NULL
      AND Timestamp >= DATEADD(day, -14, SYSUTCDATETIME())
    GROUP BY WeaponId
)
SELECT
    e.WeaponId,
    e.ExpectedTtkSec,
    a.ActualTtkSec,
    a.KillSample,
    a.AvgDistM,
    CAST(a.ActualTtkSec - e.ExpectedTtkSec AS decimal(8, 3)) AS DeltaSec,
    CASE
        WHEN a.ActualTtkSec IS NULL THEN N'veri_yok'
        WHEN ABS(a.ActualTtkSec - e.ExpectedTtkSec) <= 0.08 THEN N'uyumlu'
        WHEN a.ActualTtkSec < e.ExpectedTtkSec - 0.08 THEN N'guclu_outlier'
        ELSE N'zayif_outlier'
    END AS Verdict
FROM expected e
LEFT JOIN actual a ON a.WeaponId = e.WeaponId
ORDER BY ABS(ISNULL(a.ActualTtkSec, 0) - e.ExpectedTtkSec) DESC;
