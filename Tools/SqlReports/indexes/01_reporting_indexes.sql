-- ÖNERİ: Rapor / canlı sorgu indeksleri
-- Mevcut: Username, Email, RefreshTokenHash (Players); InviteCode (Squads);
--         SquadId + (Region,Status) (MatchTickets); Region (GameServers)

-- Aktif oyuncu / tutma
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Players_LastSeenAt' AND object_id = OBJECT_ID(N'dbo.Players')
)
CREATE INDEX IX_Players_LastSeenAt
    ON dbo.Players (LastSeenAt)
    INCLUDE (Id, Region, IsBanned, IsOnline);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Players_CreatedAt' AND object_id = OBJECT_ID(N'dbo.Players')
)
CREATE INDEX IX_Players_CreatedAt
    ON dbo.Players (CreatedAt)
    INCLUDE (Id, LastSeenAt, IsBanned);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Players_Stats_Rank' AND object_id = OBJECT_ID(N'dbo.Players')
)
CREATE INDEX IX_Players_Stats_Rank
    ON dbo.Players (Stats_Rank)
    INCLUDE (Stats_Experience, SeasonXp, IsBanned);

-- Maç analitiği
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Matches_Status_CompletedAt' AND object_id = OBJECT_ID(N'dbo.Matches')
)
CREATE INDEX IX_Matches_Status_CompletedAt
    ON dbo.Matches (Status, CompletedAt)
    INCLUDE (StartedAt, Region, SeasonNumber);

-- Eşleştirme bekleme
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MatchTickets_Status_EnqueuedAt' AND object_id = OBJECT_ID(N'dbo.MatchTickets')
)
CREATE INDEX IX_MatchTickets_Status_EnqueuedAt
    ON dbo.MatchTickets (Status, EnqueuedAt)
    INCLUDE (Region, MatchId, AverageElo);

-- Moderasyon
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Reports_CreatedAt_Status' AND object_id = OBJECT_ID(N'dbo.Reports')
)
CREATE INDEX IX_Reports_CreatedAt_Status
    ON dbo.Reports (CreatedAt, Status)
    INCLUDE (Reason, ReportedPlayerId);

-- Sunucu filosu
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_GameServers_Status_Heartbeat' AND object_id = OBJECT_ID(N'dbo.GameServers')
)
CREATE INDEX IX_GameServers_Status_Heartbeat
    ON dbo.GameServers (Status, LastHeartbeatAt)
    INCLUDE (Region, CurrentPlayers, MaxPlayers);
