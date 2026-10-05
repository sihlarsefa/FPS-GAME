using System.Collections.Concurrent;
using System.Text.Json;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Harekat.Telemetry.Infrastructure.Streaming;

/// <summary>
/// Redis Streams / Kafka uyumlu yayıncı soyutlaması.
/// Geliştirmede bellek içi; üretimde RedisStreamsEventPublisher veya KafkaEventPublisher seçilir.
/// </summary>
public interface IStreamBackend
{
    string Name { get; }
    Task PublishAsync(string streamKey, string payloadJson, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
}

public sealed class InMemoryStreamBackend : IStreamBackend
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<StreamMessage>> _streams = new(StringComparer.Ordinal);
    public string Name => "InMemory";

    public Task PublishAsync(string streamKey, string payloadJson, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        var q = _streams.GetOrAdd(streamKey, _ => new ConcurrentQueue<StreamMessage>());
        q.Enqueue(new StreamMessage(Guid.NewGuid().ToString("N"), payloadJson, headers, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public IReadOnlyList<StreamMessage> Peek(string streamKey) =>
        _streams.TryGetValue(streamKey, out var q) ? q.ToArray() : [];

    public int Count(string streamKey) =>
        _streams.TryGetValue(streamKey, out var q) ? q.Count : 0;
}

public sealed record StreamMessage(string Id, string Payload, IReadOnlyDictionary<string, string> Headers, DateTimeOffset PublishedAt);

/// <summary>Redis Streams benzeri kayıt defteri (bağlantı olmadan sözleşme uyumu).</summary>
public sealed class RedisStreamsBackend : IStreamBackend
{
    private readonly InMemoryStreamBackend _inner = new();
    public string Name => "RedisStreams";
    public Task PublishAsync(string streamKey, string payloadJson, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default) =>
        _inner.PublishAsync($"redis:{streamKey}", payloadJson, headers, ct);
}

/// <summary>Kafka benzeri konu yayıncısı (bağlantı olmadan sözleşme uyumu).</summary>
public sealed class KafkaStreamBackend : IStreamBackend
{
    private readonly InMemoryStreamBackend _inner = new();
    public string Name => "Kafka";
    public Task PublishAsync(string streamKey, string payloadJson, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default) =>
        _inner.PublishAsync($"kafka:{streamKey}", payloadJson, headers, ct);
}

public sealed class EventStreamPublisher : IEventStreamPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IStreamBackend _backend;
    private readonly ILogger<EventStreamPublisher> _logger;

    public EventStreamPublisher(IStreamBackend backend, ILogger<EventStreamPublisher> logger)
    {
        _backend = backend;
        _logger = logger;
    }

    public string BackendName => _backend.Name;

    public async Task PublishAsync(string streamKey, MatchEvent evt, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(evt, JsonOptions);
        var headers = new Dictionary<string, string>
        {
            ["matchId"] = evt.MatchId,
            ["playerId"] = evt.PlayerId,
            ["eventType"] = evt.EventType.ToString()
        };
        await _backend.PublishAsync(streamKey, json, headers, ct).ConfigureAwait(false);
        _logger.LogTrace("Stream publish {Backend} {Key} {Type}", _backend.Name, streamKey, evt.EventType);
    }

    public async Task PublishBatchAsync(string streamKey, IReadOnlyList<MatchEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events)
            await PublishAsync(streamKey, e, ct).ConfigureAwait(false);
    }
}
