using System;
using System.Net;
using System.Net.Sockets;

namespace Project.Online.Lan
{
    /// <summary>
    /// UDP yayın (broadcast) beacon'ı + dinleyici. İş parçacığı yok: <see cref="Tick"/> panelin Update'inden çağrılır,
    /// soketler engellemesizdir. Hatalar yutulur (LAN yoksa sessizce çalışmaz); <see cref="LastError"/> okunabilir.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        public const double BeaconInterval = 1.0;

        private UdpClient _listener;
        private UdpClient _sender;
        private double _nextBeacon;
        private Func<LanBeacon> _beaconSource;
        private readonly int _port;

        public LanServerList Servers { get; } = new LanServerList();
        public bool Listening => _listener != null;
        public bool Broadcasting => _beaconSource != null;
        public string LastError { get; private set; }

        public LanDiscovery(int discoveryPort = LanProtocol.DiscoveryPort) { _port = discoveryPort; }

        /// <summary>Dinlemeyi başlatır (kendi beacon'ımız dahil gelir; aynı makinede test için bilinçli).</summary>
        public bool StartListening()
        {
            if (_listener != null)
                return true;
            try
            {
                var c = new UdpClient(AddressFamily.InterNetwork);
                c.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                c.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
                c.Client.Blocking = false;
                _listener = c;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                _listener = null;
                return false;
            }
        }

        public void StartBroadcasting(Func<LanBeacon> source)
        {
            _beaconSource = source;
            _nextBeacon = 0;
            if (_sender != null)
                return;
            try
            {
                _sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            }
            catch (Exception e)
            {
                LastError = e.Message;
                _sender = null;
            }
        }

        public void StopBroadcasting()
        {
            _beaconSource = null;
            try { _sender?.Close(); } catch (Exception) { }
            _sender = null;
        }

        public void StopListening()
        {
            try { _listener?.Close(); } catch (Exception) { }
            _listener = null;
            Servers.Clear();
        }

        /// <summary>now = saniye (örn. Time.realtimeSinceStartupAsDouble). Liste değiştiyse true.</summary>
        public bool Tick(double now)
        {
            var changed = false;

            if (_beaconSource != null && _sender != null && now >= _nextBeacon)
            {
                _nextBeacon = now + BeaconInterval;
                try
                {
                    var bytes = LanProtocol.Encode(_beaconSource());
                    _sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
                }
                catch (Exception e) { LastError = e.Message; }
            }

            if (_listener != null)
            {
                try
                {
                    var guard = 16;
                    while (guard-- > 0 && _listener.Available > 0)
                    {
                        IPEndPoint from = null;
                        var data = _listener.Receive(ref from);
                        if (from != null && LanProtocol.TryDecode(data, data.Length, out var beacon))
                            changed |= Servers.Report(from.Address.ToString(), beacon, now);
                    }
                }
                catch (Exception e) { LastError = e.Message; }
            }

            changed |= Servers.Prune(now);
            return changed;
        }

        public void Dispose()
        {
            StopBroadcasting();
            StopListening();
        }
    }
}
