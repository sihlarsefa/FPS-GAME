-- HAREKÂT — Ek / doğrulama indeksleri (EF Core InitialSqlServer ile örtüşür)
-- Bu script IDEMPOTENT'tir: indeks varsa atlar.
-- EF migration zaten çoğu indeksi oluşturur; bu dosya:
--   1) Manuel EnsureCreated / eski DB'ler için güvenlik ağı
--   2) Operasyonel sorgular için birkaç ek covering indeks
--
-- Kullanım:
--   sqlcmd -S localhost -d Kuzgun -E -i indexes.sql
--   (veya Database=Harekat)

USE Kuzgun;
GO

SET NOCOUNT ON;

DECLARE @sql nvarchar(max);

-- Players
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Players_Username' AND object_id = OBJECT_ID(N'dbo.Players'))
    CREATE UNIQUE INDEX IX_Players_Username ON dbo.Players(Username);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Players_Email' AND object_id = OBJECT_ID(N'dbo.Players'))
    CREATE UNIQUE INDEX IX_Players_Email ON dbo.Players(Email) WHERE Email IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Players_SteamId' AND object_id = OBJECT_ID(N'dbo.Players'))
    CREATE INDEX IX_Players_SteamId ON dbo.Players(SteamId) WHERE SteamId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Players_Region_SeasonXp' AND object_id = OBJECT_ID(N'dbo.Players'))
    CREATE INDEX IX_Players_Region_SeasonXp ON dbo.Players(Region, SeasonXp DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Players_EloRating' AND object_id = OBJECT_ID(N'dbo.Players'))
    CREATE INDEX IX_Players_EloRating ON dbo.Players(EloRating DESC);

-- GameServers
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GameServers_Host_Status' AND object_id = OBJECT_ID(N'dbo.GameServers'))
    CREATE INDEX IX_GameServers_Host_Status ON dbo.GameServers(Host, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GameServers_LastHeartbeatAt' AND object_id = OBJECT_ID(N'dbo.GameServers'))
    CREATE INDEX IX_GameServers_LastHeartbeatAt ON dbo.GameServers(LastHeartbeatAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GameServers_Region' AND object_id = OBJECT_ID(N'dbo.GameServers'))
    CREATE INDEX IX_GameServers_Region ON dbo.GameServers(Region);

-- Matches (allocation poll + status)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Matches_Region_Status' AND object_id = OBJECT_ID(N'dbo.Matches'))
    CREATE INDEX IX_Matches_Region_Status ON dbo.Matches(Region, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Matches_Status' AND object_id = OBJECT_ID(N'dbo.Matches'))
    CREATE INDEX IX_Matches_Status ON dbo.Matches(Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Matches_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Matches'))
    CREATE INDEX IX_Matches_CreatedAt ON dbo.Matches(CreatedAt);

-- Matchmaking tickets
IF OBJECT_ID(N'dbo.MatchTickets') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MatchTickets_Region_Status' AND object_id = OBJECT_ID(N'dbo.MatchTickets'))
        CREATE INDEX IX_MatchTickets_Region_Status ON dbo.MatchTickets(Region, Status);
END

-- Squads
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Squads_InviteCode' AND object_id = OBJECT_ID(N'dbo.Squads'))
    CREATE UNIQUE INDEX IX_Squads_InviteCode ON dbo.Squads(InviteCode);

-- Friendships
IF OBJECT_ID(N'dbo.Friendships') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Friendships_RequesterId_AddresseeId' AND object_id = OBJECT_ID(N'dbo.Friendships'))
        CREATE INDEX IX_Friendships_RequesterId_AddresseeId ON dbo.Friendships(RequesterId, AddresseeId);
END

-- News / ClientVersions (F3-8)
IF OBJECT_ID(N'dbo.NewsItems') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_NewsItems_IsPublished_Language_PublishedAt' AND object_id = OBJECT_ID(N'dbo.NewsItems'))
        CREATE INDEX IX_NewsItems_IsPublished_Language_PublishedAt ON dbo.NewsItems(IsPublished, Language, PublishedAt DESC);
END

PRINT N'HAREKÂT indexes.sql tamam.';
GO
