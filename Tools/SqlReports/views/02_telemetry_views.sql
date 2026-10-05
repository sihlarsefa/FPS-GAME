-- ÖNERİ: Telemetri / oturum view'ları (kalıcı tablolar Cursor tarafında)
-- Varsayılan şema: Telemetry
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Telemetry')
    EXEC(N'CREATE SCHEMA Telemetry');
GO

-- Oturum tablosu yoksa oluşturulmalı; view yalnızca tablo varken anlamlıdır.
-- CREATE TABLE dbo.PlayerSessions (
--   Id uniqueidentifier NOT NULL PRIMARY KEY,
--   PlayerId uniqueidentifier NOT NULL,
--   StartedAt datetimeoffset NOT NULL,
--   EndedAt datetimeoffset NULL,
--   Region nvarchar(16) NOT NULL
-- );

CREATE OR ALTER VIEW dbo.vw_RankDistribution
AS
SELECT
    Stats_Rank AS RankCode,
    COUNT(*) AS PlayerCount,
    AVG(CAST(Stats_Experience AS float)) AS AvgXp
FROM dbo.Players
WHERE IsBanned = 0
GROUP BY Stats_Rank;
GO

CREATE OR ALTER VIEW Telemetry.vw_WeaponKillStats
AS
SELECT
    WeaponId,
    COUNT(*) AS KillCount,
    SUM(CASE WHEN IsHeadshot = 1 THEN 1 ELSE 0 END) AS HeadshotKills,
    AVG(CAST(DistanceMeters AS float)) AS AvgDistanceM,
    AVG(CAST(TimeToKillMs AS float)) AS AvgTtkMs
FROM Telemetry.MatchEvents
WHERE EventType = 0
  AND WeaponId IS NOT NULL
GROUP BY WeaponId;
GO
