using System.Collections.Concurrent;
using Harekat.LoadTest.Client;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;

namespace Harekat.LoadTest.Scenarios;

/// <summary>
/// Tek sanal oyuncu yaşam döngüsü: kayıt → giriş → tim → kuyruk → (komutan) sahte maç sonucu.
/// </summary>
public sealed class VirtualPlayerScenario
{
    private readonly LoadTestOptions _options;
    private readonly MetricsCollector _metrics;

    public VirtualPlayerScenario(LoadTestOptions options, MetricsCollector metrics)
    {
        _options = options;
        _metrics = metrics;
    }

    public async Task RunPlayerAsync(int playerIndex, CancellationToken ct)
    {
        var username = $"{_options.UsernamePrefix}_{_options.PlayerIdOffset + playerIndex:D6}";
        var email = $"{username}@loadtest.harekat.local";
        var password = _options.Password;

        await using var client = new HarekatApiClient(_options, _metrics);

        ApiModels.AuthResponse auth;
        try
        {
            auth = await client.RegisterAsync(username, email, password, ct);
        }
        catch (ApiException)
        {
            // Zaten kayıtlı olabilir — giriş dene
            auth = await client.LoginAsync(username, password, ct);
        }

        if (_options.ThinkTimeMs > 0)
        {
            await Task.Delay(_options.ThinkTimeMs, ct);
        }

        auth = await client.LoginAsync(username, password, ct);
        _ = await client.GetMeAsync(auth.AccessToken, ct);

        if (_options.ThinkTimeMs > 0)
        {
            await Task.Delay(_options.ThinkTimeMs, ct);
        }

        // Tim oluşturma: her SquadSize oyuncudan biri komutan
        var isLeader = playerIndex % _options.SquadSize == 0;
        string squadId;
        string matchId;

        if (isLeader)
        {
            var squad = await client.CreateSquadAsync(auth.AccessToken, $"Tim-{username}", ct);
            squadId = squad.Id;

            // Aynı süreçte diğer üyeleri simüle etmek yerine leader kuyruğa girer;
            // join ayrı worker'larda playerIndex ile eşleşir (SquadJoinRegistry).
            SquadJoinRegistry.Publish(playerIndex / _options.SquadSize, squadId, auth.AccessToken);

            if (_options.ThinkTimeMs > 0)
            {
                await Task.Delay(_options.ThinkTimeMs, ct);
            }

            var queue = await client.EnqueueAsync(auth.AccessToken, squadId, _options.Region, ct);
            matchId = queue.MatchId ?? $"match-{queue.TicketId}";

            // Sahte maç sonucu (server key)
            var result = BuildFakeResult(matchId, squadId, auth.PlayerId, playerIndex);
            await client.SubmitMatchResultAsync(matchId, result, ct);
        }
        else
        {
            var squadKey = playerIndex / _options.SquadSize;
            var joined = await SquadJoinRegistry.WaitAsync(squadKey, TimeSpan.FromSeconds(30), ct);
            squadId = joined.SquadId;

            try
            {
                await client.JoinSquadAsync(auth.AccessToken, squadId, auth.PlayerId, ct);
            }
            catch (ApiException)
            {
                // Join endpoint yoksa veya zaten üye — yoksay (backend henüz hazır olmayabilir)
            }

            if (_options.ThinkTimeMs > 0)
            {
                await Task.Delay(_options.ThinkTimeMs, ct);
            }

            // Üyeler de kuyruk çağrısı yapabilir (idempotent beklenir)
            try
            {
                _ = await client.EnqueueAsync(auth.AccessToken, squadId, _options.Region, ct);
            }
            catch (ApiException)
            {
                // Leader zaten kuyruğa aldı
            }
        }
    }

    private ApiModels.MatchResultRequest BuildFakeResult(string matchId, string squadId, string playerId, int seed)
    {
        var rng = new Random(seed);
        var kills = rng.Next(0, 12);
        var headshots = rng.Next(0, Math.Max(1, kills));
        var placement = rng.Next(1, 8);
        var won = placement == 1;

        return new ApiModels.MatchResultRequest(
            matchId,
            new[]
            {
                new ApiModels.TeamResult(
                    squadId,
                    placement,
                    won,
                    new[]
                    {
                        new ApiModels.PlayerMatchStats(playerId, kills, rng.Next(0, 5), headshots)
                    })
            },
            _options.Region);
    }
}

/// <summary>
/// Aynı süreçteki tim üyelerinin squadId paylaşımı.
/// </summary>
internal static class SquadJoinRegistry
{
    private sealed record Entry(string SquadId, string LeaderToken);

    private static readonly ConcurrentDictionary<int, TaskCompletionSource<Entry>> Map = new();

    public static void Publish(int squadKey, string squadId, string leaderToken)
    {
        var tcs = Map.GetOrAdd(squadKey, static _ => new TaskCompletionSource<Entry>(TaskCreationOptions.RunContinuationsAsynchronously));
        tcs.TrySetResult(new Entry(squadId, leaderToken));
    }

    public static async Task<(string SquadId, string LeaderToken)> WaitAsync(int squadKey, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = Map.GetOrAdd(squadKey, static _ => new TaskCompletionSource<Entry>(TaskCreationOptions.RunContinuationsAsynchronously));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            var entry = await tcs.Task.WaitAsync(cts.Token);
            return (entry.SquadId, entry.LeaderToken);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout — sahte squadId ile devam (dry-run / backend yok)
            var fake = $"sq-timeout-{squadKey}";
            return (fake, "");
        }
    }

    public static void Reset() => Map.Clear();
}
