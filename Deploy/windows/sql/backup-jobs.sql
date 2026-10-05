-- HAREKÂT — Günlük veritabanı yedekleme (SQL Server Agent veya sqlcmd)
-- Hedef DB adı: Kuzgun (gerekirse değiştirin).
--
-- Kurulum (SQL Server Agent):
--   1) C:\harekat\backups\sql klasörünü oluşturun; SQL Service hesabına yazma izni verin.
--   2) Job Step = bu dosyanın içeriği (T-SQL, database: master).
--   3) Schedule: her gün 03:00.
--
-- Agent yoksa (Express): Task Scheduler +
--   sqlcmd -S localhost -E -i C:\harekat\repo\Deploy\windows\sql\backup-jobs.sql

SET NOCOUNT ON;

DECLARE @DbName     sysname = N'Kuzgun';
DECLARE @BackupRoot nvarchar(260) = N'C:\harekat\backups\sql';
DECLARE @Stamp      nvarchar(32)  = CONVERT(nvarchar(32), GETDATE(), 112) + N'_'
                                  + REPLACE(CONVERT(nvarchar(8), GETDATE(), 108), ':', '');
DECLARE @FullPath   nvarchar(400) = @BackupRoot + N'\' + @DbName + N'_FULL_' + @Stamp + N'.bak';
DECLARE @Sql        nvarchar(max);
DECLARE @RetainDays int = 14;
DECLARE @cleanup    nvarchar(600);

IF DB_ID(@DbName) IS NULL
BEGIN
    RAISERROR(N'Veritabanı bulunamadı: %s', 16, 1, @DbName);
    RETURN;
END;

-- Klasör oluştur (xp_cmdshell kapalıysa klasörü elle açın)
BEGIN TRY
    DECLARE @mkdir nvarchar(500) = N'mkdir "' + @BackupRoot + N'"';
    EXEC xp_cmdshell @mkdir, no_output;
END TRY
BEGIN CATCH
    PRINT N'mkdir atlandı — klasörün var olduğundan emin olun: ' + @BackupRoot;
END CATCH;

SET @Sql = N'BACKUP DATABASE [' + @DbName + N'] TO DISK = N''' + @FullPath
         + N''' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;';
PRINT @Sql;
EXEC (@Sql);

-- Eski .bak temizliği
BEGIN TRY
    SET @cleanup = N'forfiles /P "' + @BackupRoot + N'" /M *.bak /D -'
                 + CAST(@RetainDays AS nvarchar(8)) + N' /C "cmd /c del @path"';
    EXEC xp_cmdshell @cleanup, no_output;
END TRY
BEGIN CATCH
    PRINT N'Eski yedek temizliği atlandı (xp_cmdshell / forfiles).';
END CATCH;

PRINT N'HAREKÂT yedek tamam: ' + @FullPath;
GO

/*
=== SQL Server Agent Job iskeleti ===

USE msdb;
GO
EXEC sp_add_job @job_name = N'Harekat_DailyBackup';
EXEC sp_add_jobstep
    @job_name = N'Harekat_DailyBackup',
    @step_name = N'FullBackup',
    @subsystem = N'TSQL',
    @command = N'-- Deploy/windows/sql/backup-jobs.sql içeriğini buraya yapıştırın',
    @database_name = N'master';
EXEC sp_add_schedule
    @schedule_name = N'Harekat_Daily_0300',
    @freq_type = 4,
    @freq_interval = 1,
    @active_start_time = 30000;
EXEC sp_attach_schedule
    @job_name = N'Harekat_DailyBackup',
    @schedule_name = N'Harekat_Daily_0300';
EXEC sp_add_jobserver @job_name = N'Harekat_DailyBackup';
GO
*/
