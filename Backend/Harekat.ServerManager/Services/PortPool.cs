using Microsoft.Extensions.Options;

namespace Harekat.ServerManager.Services;

/// <summary>
/// UDP oyun sunucusu port havuzu (varsayılan 7777–7900). Lease / release thread-safe.
/// </summary>
public sealed class PortPool
{
    private readonly object _gate = new();
    private readonly SortedSet<int> _free = new();
    private readonly HashSet<int> _leased = new();

    public PortPool(IOptions<ServerManagerOptions> options)
    {
        var opt = options.Value;
        var min = Math.Min(opt.PortMin, opt.PortMax);
        var max = Math.Max(opt.PortMin, opt.PortMax);
        for (var p = min; p <= max; p++)
            _free.Add(p);
    }

    public int Total => _free.Count + _leased.Count;
    public int Available
    {
        get { lock (_gate) return _free.Count; }
    }
    public int Leased
    {
        get { lock (_gate) return _leased.Count; }
    }

    public bool TryLease(out int port)
    {
        lock (_gate)
        {
            if (_free.Count == 0)
            {
                port = 0;
                return false;
            }

            port = _free.Min;
            _free.Remove(port);
            _leased.Add(port);
            return true;
        }
    }

    public bool TryLeaseSpecific(int port)
    {
        lock (_gate)
        {
            if (!_free.Remove(port))
                return false;
            _leased.Add(port);
            return true;
        }
    }

    public void Release(int port)
    {
        lock (_gate)
        {
            if (!_leased.Remove(port))
                return;
            _free.Add(port);
        }
    }
}
