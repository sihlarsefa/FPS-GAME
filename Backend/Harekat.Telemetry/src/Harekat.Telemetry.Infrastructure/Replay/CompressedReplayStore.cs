using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;

namespace Harekat.Telemetry.Infrastructure.Replay;

/// <summary>
/// harekat-replay-v1 formatı: JSON + GZip sıkıştırma.
/// </summary>
public sealed class CompressedReplayStore : IReplayStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public Task SaveCompressedAsync(MatchReplay replay, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(replay);
        var json = JsonSerializer.SerializeToUtf8Bytes(replay, JsonOptions);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(json);
        _blobs[replay.MatchId] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<MatchReplay?> LoadAsync(string matchId, CancellationToken ct = default)
    {
        if (!_blobs.TryGetValue(matchId, out var blob))
            return Task.FromResult<MatchReplay?>(null);

        using var input = new MemoryStream(blob);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        var replay = JsonSerializer.Deserialize<MatchReplay>(output.ToArray(), JsonOptions);
        return Task.FromResult(replay);
    }

    public Task<long> GetCompressedSizeAsync(string matchId, CancellationToken ct = default) =>
        Task.FromResult(_blobs.TryGetValue(matchId, out var b) ? b.LongLength : 0L);

    public static byte[] Compress(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(raw);
        return ms.ToArray();
    }
}
