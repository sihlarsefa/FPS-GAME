using System.Collections.Concurrent;
using Harekat.Telemetry.Application.Abstractions;
using Harekat.Telemetry.Domain.Entities;

namespace Harekat.Telemetry.Infrastructure.Storage;

public sealed class InMemoryClientErrorStore : IClientErrorStore
{
    private readonly ConcurrentDictionary<Guid, ClientErrorRecord> _items = new();

    public Task SaveAsync(ClientErrorRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _items[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ClientErrorRecord>> ListAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var list = _items.Values
            .OrderByDescending(r => r.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToList();
        return Task.FromResult<IReadOnlyList<ClientErrorRecord>>(list);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_items.Count);
}
