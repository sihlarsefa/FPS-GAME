-- ÖNERİ: Telemetri tabloları + indeksler (Cursor uygular)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Telemetry')
    EXEC(N'CREATE SCHEMA Telemetry');
GO

/*
CREATE TABLE Telemetry.MatchEvents (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    MatchId nvarchar(64) NOT NULL,
    PlayerId nvarchar(64) NOT NULL,
    EventType int NOT NULL,
    Timestamp datetimeoffset NOT NULL,
    TargetPlayerId nvarchar(64) NULL,
    WeaponId nvarchar(64) NULL,
    IsHeadshot bit NOT NULL CONSTRAINT DF_MatchEvents_IsHeadshot DEFAULT (0),
    ThroughWall bit NOT NULL CONSTRAINT DF_MatchEvents_ThroughWall DEFAULT (0),
    Damage real NULL,
    TimeToKillMs real NULL,
    DistanceMeters real NULL
);

CREATE TABLE Telemetry.PlayerSuspicionReports (
    Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    PlayerId nvarchar(64) NOT NULL,
    MatchId nvarchar(64) NOT NULL,
    TotalScore float NOT NULL,
    GeneratedAt datetimeoffset NOT NULL,
    FindingsJson nvarchar(max) NOT NULL
);
*/

IF OBJECT_ID(N'Telemetry.MatchEvents', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MatchEvents_Weapon_Event_Time'
      AND object_id = OBJECT_ID(N'Telemetry.MatchEvents')
)
CREATE INDEX IX_MatchEvents_Weapon_Event_Time
    ON Telemetry.MatchEvents (EventType, WeaponId, Timestamp)
    INCLUDE (IsHeadshot, DistanceMeters, TimeToKillMs);

IF OBJECT_ID(N'Telemetry.PlayerSuspicionReports', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Suspicion_GeneratedAt'
      AND object_id = OBJECT_ID(N'Telemetry.PlayerSuspicionReports')
)
CREATE INDEX IX_Suspicion_GeneratedAt
    ON Telemetry.PlayerSuspicionReports (GeneratedAt)
    INCLUDE (PlayerId, TotalScore, MatchId);
