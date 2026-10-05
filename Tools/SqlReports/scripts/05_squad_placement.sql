-- Tim yerleşimi ve galibiyet oranı (Matches.Teams JSON)
SET NOCOUNT ON;

;WITH team_rows AS (
    SELECT
        m.Id AS MatchId,
        m.SeasonNumber,
        m.Region,
        m.CompletedAt,
        CAST(j.SquadId AS uniqueidentifier) AS SquadId,
        j.SquadName,
        CAST(j.Placement AS int) AS Placement,
        CAST(j.BotCount AS int) AS BotCount
    FROM dbo.Matches m
    CROSS APPLY OPENJSON(m.Teams)
    WITH (
        SquadId uniqueidentifier '$.SquadId',
        SquadName nvarchar(128) '$.SquadName',
        Placement int '$.Placement',
        BotCount int '$.BotCount'
    ) j
    WHERE m.Status = 3 -- Completed
      AND m.CompletedAt >= DATEADD(day, -30, SYSUTCDATETIME())
),
by_squad AS (
    SELECT
        SquadId,
        MAX(SquadName) AS SquadName,
        COUNT(*) AS MatchesPlayed,
        SUM(CASE WHEN Placement = 1 THEN 1 ELSE 0 END) AS Wins,
        AVG(CAST(Placement AS float)) AS AvgPlacement,
        SUM(CASE WHEN Placement <= 3 THEN 1 ELSE 0 END) AS Top3
    FROM team_rows
    WHERE BotCount = 0
    GROUP BY SquadId
)
SELECT TOP (50)
    SquadId,
    SquadName,
    MatchesPlayed,
    Wins,
    CAST(100.0 * Wins / NULLIF(MatchesPlayed, 0) AS decimal(5, 2)) AS WinRatePct,
    CAST(AvgPlacement AS decimal(5, 2)) AS AvgPlacement,
    Top3,
    CAST(100.0 * Top3 / NULLIF(MatchesPlayed, 0) AS decimal(5, 2)) AS Top3Pct
FROM by_squad
WHERE MatchesPlayed >= 3
ORDER BY WinRatePct DESC, MatchesPlayed DESC;

-- Yerleşim histogramı (tüm tim slotları)
SELECT
    Placement,
    COUNT(*) AS TeamSlots
FROM team_rows
GROUP BY Placement
ORDER BY Placement;
