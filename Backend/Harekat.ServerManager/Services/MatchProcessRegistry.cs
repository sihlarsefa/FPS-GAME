using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

public sealed class TrackedMatchProcess
{
    public Guid MatchId { get; init; }
    public Guid ServerId { get; set; }
    public int Port { get; init; }
    public Process? Process { get; set; }
    public string Status { get; set; } = "Allocated";
    public int CurrentPlayers { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public Queue<DateTimeOffset> RestartTimestamps { get; } = new();
    public string? LogFile { get; set; }
}

/// <summary>
/// Spawn edilen oyun süreçlerini izler; çökmede rate-limited yeniden başlatır.
/// </summary>
public sealed class MatchProcessRegistry
{
    private readonly ConcurrentDictionary<Guid, TrackedMatchProcess> _byMatch = new();
    private readonly ConcurrentDictionary<Guid, TrackedMatchProcess> _byServer = new();
    private readonly ServerManagerOptions _options;
    private readonly PortPool _ports;
    private readonly ProcessResourceMonitor _resources;
    private readonly ILogger<MatchProcessRegistry> _log;

    public MatchProcessRegistry(
        IOptions<ServerManagerOptions> options,
        PortPool ports,
        ProcessResourceMonitor resources,
        ILogger<MatchProcessRegistry> log)
    {
        _options = options.Value;
        _ports = ports;
        _resources = resources;
        _log = log;
    }

    public IReadOnlyCollection<TrackedMatchProcess> All => _byMatch.Values.ToList();
    public int ActiveCount => _byMatch.Count;

    public TrackedMatchProcess? GetByServerId(Guid serverId) =>
        _byServer.TryGetValue(serverId, out var t) ? t : null;

    public bool TryAdd(TrackedMatchProcess entry)
    {
        if (!_byMatch.TryAdd(entry.MatchId, entry))
            return false;
        _byServer[entry.ServerId] = entry;
        return true;
    }

    public void UpdateServerId(TrackedMatchProcess entry, Guid serverId)
    {
        if (entry.ServerId != Guid.Empty)
            _byServer.TryRemove(entry.ServerId, out _);
        entry.ServerId = serverId;
        _byServer[serverId] = entry;
    }

    public bool TryRemove(Guid matchId, out TrackedMatchProcess? entry)
    {
        if (!_byMatch.TryRemove(matchId, out entry))
            return false;
        _byServer.TryRemove(entry.ServerId, out _);
        if (entry.Process is { HasExited: false } p)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            try { p.Dispose(); } catch { /* ignore */ }
        }
        if (entry.Process is not null)
            _resources.Forget(entry.Process.Id);
        _ports.Release(entry.Port);
        return true;
    }

    public ProcessResources AggregateResources()
    {
        var alive = _byMatch.Values
            .Select(t => t.Process)
            .Where(p => p is { HasExited: false })
            .Cast<Process>();
        return _resources.SampleAggregate(alive);
    }

    public bool IsOverResourceLimit()
    {
        var agg = AggregateResources();
        return agg.CpuPercent >= _options.MaxCpuPercent
               || agg.WorkingSetMb >= _options.MaxRamMb;
    }

    /// <summary>
    /// Çöken süreçleri yeniden başlatır. Saatlik restart limiti aşılırsa port bırakılır.
    /// </summary>
    public IReadOnlyList<TrackedMatchProcess> ReapAndRestartCrashed(Func<TrackedMatchProcess, Process?> startProcess)
    {
        var removed = new List<TrackedMatchProcess>();
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddHours(-1);

        foreach (var entry in _byMatch.Values.ToList())
        {
            if (entry.Process is null || !entry.Process.HasExited)
                continue;

            _log.LogWarning(
                "Oyun süreci çöktü match={MatchId} port={Port} exit={Code}",
                entry.MatchId, entry.Port, entry.Process.ExitCode);

            while (entry.RestartTimestamps.Count > 0 && entry.RestartTimestamps.Peek() < windowStart)
                entry.RestartTimestamps.Dequeue();

            if (entry.RestartTimestamps.Count >= _options.MaxRestartsPerHour)
            {
                _log.LogError(
                    "Restart limiti aşıldı match={MatchId} ({Max}/saat) — süreç bırakılıyor",
                    entry.MatchId, _options.MaxRestartsPerHour);
                TryRemove(entry.MatchId, out _);
                removed.Add(entry);
                continue;
            }

            try { entry.Process.Dispose(); } catch { /* ignore */ }
            entry.Process = null;

            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(1, _options.RestartDelaySeconds)));

            var proc = startProcess(entry);
            if (proc is null)
            {
                _log.LogError("Yeniden başlatma başarısız match={MatchId}", entry.MatchId);
                TryRemove(entry.MatchId, out _);
                removed.Add(entry);
                continue;
            }

            entry.Process = proc;
            entry.StartedAt = DateTimeOffset.UtcNow;
            entry.RestartTimestamps.Enqueue(now);
            entry.Status = "Allocated";
            _log.LogInformation("Süreç yeniden başlatıldı match={MatchId} pid={Pid}", entry.MatchId, proc.Id);
        }

        return removed;
    }

    public Process? StartGameProcess(int port, Guid matchId, Guid serverId)
    {
        var exe = _options.GameServerExe;
        if (!File.Exists(exe))
        {
            _log.LogError("GameServerExe bulunamadı: {Exe}", exe);
            return null;
        }

        Directory.CreateDirectory(_options.LogDirectory);
        var logFile = Path.Combine(
            _options.LogDirectory,
            $"match-{matchId:N}-{port}-{DateTime.UtcNow:yyyyMMddHHmmss}.log");

        var args =
            $"-batchmode -nographics -server -port {port} " +
            $"-region {_options.Region} -backend \"{_options.BackendBaseUrl}\" " +
            $"-serverKey \"{_options.ServerKey}\" -maxPlayers {_options.MaxPlayersPerMatch} " +
            $"-matchId {matchId:D} -serverId {serverId:D}";

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        try
        {
            var logStream = new StreamWriter(logFile) { AutoFlush = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) logStream.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) logStream.WriteLine("[err] " + e.Data); };
            process.Exited += (_, _) =>
            {
                try { logStream.Dispose(); } catch { /* ignore */ }
            };

            if (!process.Start())
            {
                logStream.Dispose();
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _log.LogInformation(
                "Oyun süreci başlatıldı match={MatchId} port={Port} pid={Pid} log={Log}",
                matchId, port, process.Id, logFile);
            return process;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Süreç başlatılamadı: {Exe} {Args}", exe, args);
            try { process.Dispose(); } catch { /* ignore */ }
            return null;
        }
    }
}
