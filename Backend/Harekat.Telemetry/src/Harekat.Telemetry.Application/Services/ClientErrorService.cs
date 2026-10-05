using System.Collections.Concurrent;
using System.Text.Json;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Application.Contracts;
using Harekat.Telemetry.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harekat.Telemetry.Application.Services;

public sealed class ClientErrorRateLimitOptions
{
    public int PermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class ClientErrorService
{
    private readonly IClientErrorStore _store;
    private readonly ILogger<ClientErrorService> _log;
    private readonly ClientErrorRateLimitOptions _limits;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public ClientErrorService(
        IClientErrorStore store,
        IOptions<ClientErrorRateLimitOptions> limits,
        ILogger<ClientErrorService> log)
    {
        _store = store;
        _log = log;
        _limits = limits.Value;
    }

    public async Task<(bool Accepted, ClientErrorDto? Dto, string? Error)> IngestAsync(
        ClientErrorRequest request,
        string? clientIp,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp!;
        if (IsRateLimited(key))
            return (false, null, "rate_limited");

        if (string.IsNullOrWhiteSpace(request.Message) && string.IsNullOrWhiteSpace(request.StackTrace)
            && (request.RecentLogs is null || request.RecentLogs.Count == 0))
            return (false, null, "empty_report");

        var id = Guid.TryParse(request.Id, out var parsed) ? parsed : Guid.NewGuid();
        var created = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.CreatedAtUtc)
            && DateTimeOffset.TryParse(request.CreatedAtUtc, out var parsedAt))
            created = parsedAt;

        var record = new ClientErrorRecord
        {
            Id = id,
            Trigger = Trunc(request.Trigger, 64) ?? "unknown",
            Version = Trunc(request.Version, 64) ?? "",
            Scene = Trunc(request.Scene, 128) ?? "",
            Platform = Trunc(request.Platform, 64) ?? "",
            DeviceModel = Trunc(request.DeviceModel, 128) ?? "",
            OperatingSystem = Trunc(request.OperatingSystem, 256) ?? "",
            ProcessorType = Trunc(request.ProcessorType, 128) ?? "",
            ProcessorCount = Math.Max(0, request.ProcessorCount),
            SystemMemoryMb = Math.Max(0, request.SystemMemoryMb),
            GraphicsDeviceName = Trunc(request.GraphicsDeviceName, 128) ?? "",
            GraphicsMemoryMb = Math.Max(0, request.GraphicsMemoryMb),
            UnityVersion = Trunc(request.UnityVersion, 64) ?? "",
            ExceptionType = Trunc(request.ExceptionType, 128) ?? "",
            Message = Trunc(request.Message, 4000) ?? "",
            StackTrace = Trunc(request.StackTrace, 16000) ?? "",
            RecentLogsJson = JsonSerializer.Serialize((request.RecentLogs ?? []).Take(500).ToList()),
            ClientIp = Trunc(clientIp, 64),
            CreatedAt = created
        };

        await _store.SaveAsync(record, ct).ConfigureAwait(false);
        _log.LogInformation("Client error kaydedildi {Id} trigger={Trigger} scene={Scene}", record.Id, record.Trigger, record.Scene);
        return (true, ToDto(record), null);
    }

    public async Task<ClientErrorListResponse> ListAsync(int skip, int take, CancellationToken ct = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        var items = await _store.ListAsync(skip, take, ct).ConfigureAwait(false);
        var total = await _store.CountAsync(ct).ConfigureAwait(false);
        return new ClientErrorListResponse
        {
            Items = items.Select(ToDto).ToList(),
            Total = total
        };
    }

    private bool IsRateLimited(string key)
    {
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromSeconds(Math.Max(1, _limits.WindowSeconds));
        var limit = Math.Max(1, _limits.PermitLimit);
        var win = _windows.AddOrUpdate(key,
            _ => new Window(now, 1),
            (_, existing) =>
            {
                if (now - existing.Started > window)
                    return new Window(now, 1);
                return existing with { Count = existing.Count + 1 };
            });
        return win.Count > limit;
    }

    private static ClientErrorDto ToDto(ClientErrorRecord r)
    {
        IReadOnlyList<string> logs = [];
        try
        {
            logs = JsonSerializer.Deserialize<List<string>>(r.RecentLogsJson) ?? [];
        }
        catch
        {
            // ignore
        }

        return new ClientErrorDto
        {
            Id = r.Id,
            Trigger = r.Trigger,
            Version = r.Version,
            Scene = r.Scene,
            Platform = r.Platform,
            DeviceModel = r.DeviceModel,
            OperatingSystem = r.OperatingSystem,
            ProcessorType = r.ProcessorType,
            ProcessorCount = r.ProcessorCount,
            SystemMemoryMb = r.SystemMemoryMb,
            GraphicsDeviceName = r.GraphicsDeviceName,
            GraphicsMemoryMb = r.GraphicsMemoryMb,
            UnityVersion = r.UnityVersion,
            ExceptionType = r.ExceptionType,
            Message = r.Message,
            StackTrace = r.StackTrace,
            RecentLogs = logs,
            ClientIp = r.ClientIp,
            CreatedAt = r.CreatedAt
        };
    }

    private static string? Trunc(string? value, int max)
    {
        if (value is null) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    private sealed record Window(DateTimeOffset Started, int Count);
}
