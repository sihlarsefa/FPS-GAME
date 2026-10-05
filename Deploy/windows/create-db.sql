-- HAREKÂT — SQL Server: sadece Kuzgun DB (MobilDb'ye dokunma)
IF DB_ID(N'Kuzgun') IS NULL
    CREATE DATABASE Kuzgun;
GO

USE Kuzgun;
GO
