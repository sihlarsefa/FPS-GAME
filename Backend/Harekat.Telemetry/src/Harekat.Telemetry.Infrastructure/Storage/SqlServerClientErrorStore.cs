using System.Data;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Harekat.Telemetry.Infrastructure.Storage;

/// <summary>
/// MSSQL istemci hata deposu. Bağlantı yoksa veya tablo yoksa oluşturmayı dener.
/// </summary>
public sealed class SqlServerClientErrorStore : IClientErrorStore
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerClientErrorStore> _log;
    private readonly SemaphoreSlim _init = new(1, 1);
    private volatile bool _ready;

    public SqlServerClientErrorStore(IConfiguration config, ILogger<SqlServerClientErrorStore> log)
    {
        _connectionString = config.GetConnectionString("SqlServer")
            ?? config["Storage:SqlServer"]
            ?? throw new InvalidOperationException("SqlServer bağlantı dizesi yok.");
        _log = log;
    }

    public async Task SaveAsync(ClientErrorRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            MERGE dbo.ClientErrors AS t
            USING (SELECT @Id AS Id) AS s ON t.Id = s.Id
            WHEN NOT MATCHED THEN INSERT (
                Id, Trigger, Version, Scene, Platform, DeviceModel, OperatingSystem, ProcessorType,
                ProcessorCount, SystemMemoryMb, GraphicsDeviceName, GraphicsMemoryMb, UnityVersion,
                ExceptionType, Message, StackTrace, RecentLogsJson, ClientIp, CreatedAt)
            VALUES (
                @Id, @Trigger, @Version, @Scene, @Platform, @DeviceModel, @OperatingSystem, @ProcessorType,
                @ProcessorCount, @SystemMemoryMb, @GraphicsDeviceName, @GraphicsMemoryMb, @UnityVersion,
                @ExceptionType, @Message, @StackTrace, @RecentLogsJson, @ClientIp, @CreatedAt);
            """;
        AddParams(cmd, record);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClientErrorRecord>> ListAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Trigger, Version, Scene, Platform, DeviceModel, OperatingSystem, ProcessorType,
                   ProcessorCount, SystemMemoryMb, GraphicsDeviceName, GraphicsMemoryMb, UnityVersion,
                   ExceptionType, Message, StackTrace, RecentLogsJson, ClientIp, CreatedAt
            FROM dbo.ClientErrors
            ORDER BY CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;
        cmd.Parameters.AddWithValue("@Skip", Math.Max(0, skip));
        cmd.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 200));

        var list = new List<ClientErrorRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add(Read(reader));
        return list;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM dbo.ClientErrors;";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _init.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ready) return;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                IF OBJECT_ID(N'dbo.ClientErrors', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ClientErrors (
                        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        Trigger NVARCHAR(64) NOT NULL,
                        Version NVARCHAR(64) NOT NULL,
                        Scene NVARCHAR(128) NOT NULL,
                        Platform NVARCHAR(64) NOT NULL,
                        DeviceModel NVARCHAR(128) NOT NULL,
                        OperatingSystem NVARCHAR(256) NOT NULL,
                        ProcessorType NVARCHAR(128) NOT NULL,
                        ProcessorCount INT NOT NULL,
                        SystemMemoryMb INT NOT NULL,
                        GraphicsDeviceName NVARCHAR(128) NOT NULL,
                        GraphicsMemoryMb INT NOT NULL,
                        UnityVersion NVARCHAR(64) NOT NULL,
                        ExceptionType NVARCHAR(128) NOT NULL,
                        Message NVARCHAR(MAX) NOT NULL,
                        StackTrace NVARCHAR(MAX) NOT NULL,
                        RecentLogsJson NVARCHAR(MAX) NOT NULL,
                        ClientIp NVARCHAR(64) NULL,
                        CreatedAt DATETIMEOFFSET NOT NULL
                    );
                    CREATE INDEX IX_ClientErrors_CreatedAt ON dbo.ClientErrors (CreatedAt DESC);
                END
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _ready = true;
            _log.LogInformation("ClientErrors MSSQL şeması hazır.");
        }
        finally
        {
            _init.Release();
        }
    }

    private static void AddParams(SqlCommand cmd, ClientErrorRecord r)
    {
        cmd.Parameters.AddWithValue("@Id", r.Id);
        cmd.Parameters.AddWithValue("@Trigger", r.Trigger);
        cmd.Parameters.AddWithValue("@Version", r.Version);
        cmd.Parameters.AddWithValue("@Scene", r.Scene);
        cmd.Parameters.AddWithValue("@Platform", r.Platform);
        cmd.Parameters.AddWithValue("@DeviceModel", r.DeviceModel);
        cmd.Parameters.AddWithValue("@OperatingSystem", r.OperatingSystem);
        cmd.Parameters.AddWithValue("@ProcessorType", r.ProcessorType);
        cmd.Parameters.AddWithValue("@ProcessorCount", r.ProcessorCount);
        cmd.Parameters.AddWithValue("@SystemMemoryMb", r.SystemMemoryMb);
        cmd.Parameters.AddWithValue("@GraphicsDeviceName", r.GraphicsDeviceName);
        cmd.Parameters.AddWithValue("@GraphicsMemoryMb", r.GraphicsMemoryMb);
        cmd.Parameters.AddWithValue("@UnityVersion", r.UnityVersion);
        cmd.Parameters.AddWithValue("@ExceptionType", r.ExceptionType);
        cmd.Parameters.AddWithValue("@Message", r.Message);
        cmd.Parameters.AddWithValue("@StackTrace", r.StackTrace);
        cmd.Parameters.AddWithValue("@RecentLogsJson", r.RecentLogsJson);
        cmd.Parameters.AddWithValue("@ClientIp", (object?)r.ClientIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", r.CreatedAt);
    }

    private static ClientErrorRecord Read(SqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Trigger = reader.GetString(1),
        Version = reader.GetString(2),
        Scene = reader.GetString(3),
        Platform = reader.GetString(4),
        DeviceModel = reader.GetString(5),
        OperatingSystem = reader.GetString(6),
        ProcessorType = reader.GetString(7),
        ProcessorCount = reader.GetInt32(8),
        SystemMemoryMb = reader.GetInt32(9),
        GraphicsDeviceName = reader.GetString(10),
        GraphicsMemoryMb = reader.GetInt32(11),
        UnityVersion = reader.GetString(12),
        ExceptionType = reader.GetString(13),
        Message = reader.GetString(14),
        StackTrace = reader.GetString(15),
        RecentLogsJson = reader.GetString(16),
        ClientIp = reader.IsDBNull(17) ? null : reader.GetString(17),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(18)
    };
}
