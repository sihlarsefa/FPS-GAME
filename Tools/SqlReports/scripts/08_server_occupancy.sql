-- Sunucu doluluğu ve sağlık
SET NOCOUNT ON;

SELECT
    g.Id,
    g.Host,
    g.Port,
    g.Region,
    g.Status,
    CASE g.Status
        WHEN 0 THEN N'Starting'
        WHEN 1 THEN N'Ready'
        WHEN 2 THEN N'Allocated'
        WHEN 3 THEN N'Draining'
        WHEN 4 THEN N'Offline'
        ELSE N'?'
    END AS StatusName,
    g.CurrentPlayers,
    g.MaxPlayers,
    CAST(100.0 * g.CurrentPlayers / NULLIF(g.MaxPlayers, 0) AS decimal(5, 2)) AS OccupancyPct,
    g.CurrentMatchId,
    g.LastHeartbeatAt,
    DATEDIFF(second, g.LastHeartbeatAt, SYSUTCDATETIME()) AS HeartbeatAgeSec
FROM dbo.GameServers g
ORDER BY OccupancyPct DESC, g.Region, g.Host;

SELECT
    Region,
    COUNT(*) AS ServerCount,
    SUM(CASE WHEN Status IN (1, 2) THEN 1 ELSE 0 END) AS HealthyCount,
    SUM(CurrentPlayers) AS TotalPlayers,
    SUM(MaxPlayers) AS TotalCapacity,
    CAST(100.0 * SUM(CurrentPlayers) / NULLIF(SUM(MaxPlayers), 0) AS decimal(5, 2)) AS FleetOccupancyPct
FROM dbo.GameServers
GROUP BY Region
ORDER BY Region;
