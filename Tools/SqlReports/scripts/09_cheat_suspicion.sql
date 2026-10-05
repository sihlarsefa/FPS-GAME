-- Hile şüphelisi trendi
-- Kaynak: Telemetry.PlayerSuspicionReports + dbo.Reports (oyuncu şikayetleri)
SET NOCOUNT ON;

-- Moderation rapor kuyruğu (EF: Reports)
SELECT
    CAST(CreatedAt AS date) AS ReportDate,
    Status,
    COUNT(*) AS ReportCount
FROM dbo.Reports
WHERE CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY CAST(CreatedAt AS date), Status
ORDER BY ReportDate, Status;

SELECT
    Reason,
    COUNT(*) AS Count
FROM dbo.Reports
WHERE CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY Reason
ORDER BY Count DESC;

IF OBJECT_ID(N'Telemetry.PlayerSuspicionReports', N'U') IS NULL
BEGIN
    SELECT
        CAST('schema_missing' AS nvarchar(40)) AS Status,
        CAST(N'Telemetry.PlayerSuspicionReports yok' AS nvarchar(200)) AS Message;
    RETURN;
END;

SELECT
    CAST(GeneratedAt AS date) AS DayUtc,
    COUNT(*) AS Reports,
    SUM(CASE WHEN TotalScore >= 50 THEN 1 ELSE 0 END) AS SuspectCount,
    AVG(TotalScore) AS AvgScore,
    MAX(TotalScore) AS MaxScore
FROM Telemetry.PlayerSuspicionReports
WHERE GeneratedAt >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY CAST(GeneratedAt AS date)
ORDER BY DayUtc;
