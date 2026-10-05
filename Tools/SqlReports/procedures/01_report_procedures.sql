-- ÖNERİ: Rapor saklı yordamları (salt okunur SELECT)
CREATE OR ALTER PROCEDURE dbo.usp_Report_ActivePlayers
    @AsOf datetimeoffset = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @AsOf = ISNULL(@AsOf, SYSUTCDATETIME());

    SELECT 'dau' AS Metric, COUNT(DISTINCT Id) AS Value
    FROM dbo.Players
    WHERE LastSeenAt >= CAST(CAST(@AsOf AS date) AS datetimeoffset) AND IsBanned = 0
    UNION ALL
    SELECT 'wau', COUNT(DISTINCT Id)
    FROM dbo.Players
    WHERE LastSeenAt >= DATEADD(day, -6, CAST(CAST(@AsOf AS date) AS datetimeoffset)) AND IsBanned = 0
    UNION ALL
    SELECT 'mau', COUNT(DISTINCT Id)
    FROM dbo.Players
    WHERE LastSeenAt >= DATEADD(day, -29, CAST(CAST(@AsOf AS date) AS datetimeoffset)) AND IsBanned = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Report_Retention
    @LookbackDays int = 60
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CreatedAt AS date) AS CohortDate,
        COUNT(*) AS CohortSize,
        CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 1, CreatedAt) THEN 1 ELSE 0 END)
             / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD1,
        CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 7, CreatedAt) THEN 1 ELSE 0 END)
             / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD7,
        CAST(100.0 * SUM(CASE WHEN LastSeenAt >= DATEADD(day, 30, CreatedAt) THEN 1 ELSE 0 END)
             / NULLIF(COUNT(*), 0) AS decimal(5, 2)) AS PctD30
    FROM dbo.Players
    WHERE CreatedAt >= DATEADD(day, -@LookbackDays, SYSUTCDATETIME())
      AND IsBanned = 0
    GROUP BY CAST(CreatedAt AS date)
    ORDER BY CohortDate;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Report_MatchmakingWait
    @Days int = 30
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.Region,
        COUNT(*) AS MatchedTickets,
        AVG(DATEDIFF(second, t.EnqueuedAt, m.CreatedAt) * 1.0) AS AvgWaitSec
    FROM dbo.MatchTickets t
    INNER JOIN dbo.Matches m ON m.Id = t.MatchId
    WHERE t.Status = 1
      AND t.EnqueuedAt >= DATEADD(day, -@Days, SYSUTCDATETIME())
    GROUP BY t.Region;
END;
GO
