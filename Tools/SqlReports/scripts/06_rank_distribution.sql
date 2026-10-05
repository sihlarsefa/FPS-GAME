-- Rütbe dağılımı (Stats_Rank = MilitaryRank 0..18)
SET NOCOUNT ON;

;WITH rank_names AS (
    SELECT * FROM (VALUES
        (0,  N'Er'),
        (1,  N'Onbaşı'),
        (2,  N'Çavuş'),
        (3,  N'Sözleşmeli Er'),
        (4,  N'Uzman Onbaşı'),
        (5,  N'Uzman Çavuş'),
        (6,  N'Astsubay Çavuş'),
        (7,  N'Astsubay Kıdemli Çavuş'),
        (8,  N'Astsubay Üstçavuş'),
        (9,  N'Astsubay Kıdemli Üstçavuş'),
        (10, N'Astsubay Başçavuş'),
        (11, N'Astsubay Kıdemli Başçavuş'),
        (12, N'Asteğmen'),
        (13, N'Teğmen'),
        (14, N'Üsteğmen'),
        (15, N'Yüzbaşı'),
        (16, N'Binbaşı'),
        (17, N'Yarbay'),
        (18, N'Albay')
    ) v(RankCode, RankName)
),
counts AS (
    SELECT
        p.Stats_Rank AS RankCode,
        COUNT(*) AS PlayerCount,
        AVG(CAST(p.Stats_Experience AS float)) AS AvgXp,
        AVG(CAST(p.SeasonXp AS float)) AS AvgSeasonXp
    FROM dbo.Players p
    WHERE p.IsBanned = 0
    GROUP BY p.Stats_Rank
)
SELECT
    r.RankCode,
    r.RankName,
    ISNULL(c.PlayerCount, 0) AS PlayerCount,
    CAST(100.0 * ISNULL(c.PlayerCount, 0)
         / NULLIF((SELECT SUM(PlayerCount) FROM counts), 0) AS decimal(5, 2)) AS Pct,
    c.AvgXp,
    c.AvgSeasonXp
FROM rank_names r
LEFT JOIN counts c ON c.RankCode = r.RankCode
ORDER BY r.RankCode;
