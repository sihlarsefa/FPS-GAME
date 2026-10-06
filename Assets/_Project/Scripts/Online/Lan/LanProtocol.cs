using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Project.Online.Lan
{
    /// <summary>LAN keşif işareti (beacon): sunucunun UDP yayınında taşıdığı bilgi. Saf C#.</summary>
    public readonly struct LanBeacon : IEquatable<LanBeacon>
    {
        public readonly string Name;
        public readonly ushort GamePort;
        public readonly string MapId;
        public readonly int Players;
        public readonly int MaxPlayers;
        public readonly int Version;

        public LanBeacon(string name, ushort gamePort, string mapId, int players, int maxPlayers, int version = LanProtocol.Version)
        {
            Name = LanProtocol.Clean(name, "Sunucu");
            GamePort = gamePort;
            MapId = LanProtocol.Clean(mapId, "kuzgun");
            Players = Math.Max(0, players);
            MaxPlayers = Math.Max(1, maxPlayers);
            Version = version;
        }

        public bool Equals(LanBeacon o) =>
            Name == o.Name && GamePort == o.GamePort && MapId == o.MapId
            && Players == o.Players && MaxPlayers == o.MaxPlayers && Version == o.Version;

        public override bool Equals(object obj) => obj is LanBeacon o && Equals(o);
        public override int GetHashCode() => (Name ?? "").GetHashCode() ^ GamePort;
    }

    /// <summary>Beacon kodlama/çözme: "HAREKAT|v|ad|port|harita|oyuncu|maks". Ayraç '|', alanlarda yasak.</summary>
    public static class LanProtocol
    {
        public const string Magic = "HAREKAT";
        public const int Version = 1;
        public const int DiscoveryPort = 7778;
        public const int MaxPacketBytes = 256;
        public const int MaxNameLength = 24;

        public static string Clean(string s, string fallback)
        {
            if (string.IsNullOrWhiteSpace(s))
                return fallback;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s.Trim())
            {
                if (c == '|' || char.IsControl(c))
                    continue;
                sb.Append(c);
                if (sb.Length >= MaxNameLength)
                    break;
            }

            return sb.Length == 0 ? fallback : sb.ToString();
        }

        public static byte[] Encode(in LanBeacon b)
        {
            var text = string.Join("|", Magic, b.Version.ToString(CultureInfo.InvariantCulture), b.Name,
                b.GamePort.ToString(CultureInfo.InvariantCulture), b.MapId,
                b.Players.ToString(CultureInfo.InvariantCulture), b.MaxPlayers.ToString(CultureInfo.InvariantCulture));
            return Encoding.UTF8.GetBytes(text);
        }

        public static bool TryDecode(byte[] data, int length, out LanBeacon beacon)
        {
            beacon = default;
            if (data == null || length <= 0 || length > MaxPacketBytes || length > data.Length)
                return false;

            string text;
            try { text = Encoding.UTF8.GetString(data, 0, length); }
            catch (Exception) { return false; }

            var p = text.Split('|');
            if (p.Length != 7 || p[0] != Magic)
                return false;
            if (!int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ver) || ver != Version)
                return false;
            if (!ushort.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port == 0)
                return false;
            if (!int.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out var players)
                || !int.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out var max)
                || max <= 0 || players > 1000 || max > 1000)
                return false;
            if (string.IsNullOrWhiteSpace(p[2]) || string.IsNullOrWhiteSpace(p[4]))
                return false;

            beacon = new LanBeacon(p[2], port, p[4], players, max, ver);
            return true;
        }
    }

    /// <summary>Bulunan LAN sunucusu (gönderen adres + beacon).</summary>
    public readonly struct LanServerEntry
    {
        public readonly string Address;
        public readonly LanBeacon Beacon;
        public readonly double LastSeen;

        public LanServerEntry(string address, LanBeacon beacon, double lastSeen)
        {
            Address = address;
            Beacon = beacon;
            LastSeen = lastSeen;
        }

        public string Endpoint => Address + ":" + Beacon.GamePort.ToString(CultureInfo.InvariantCulture);
        public bool IsFull => Beacon.Players >= Beacon.MaxPlayers;
    }

    /// <summary>Tarama listesi: beacon geldikçe güncellenir, TTL sonunda düşer. Zamanı çağıran verir (test edilebilir).</summary>
    public sealed class LanServerList
    {
        public const double DefaultTtl = 3.0;
        public const int MaxEntries = 32;

        private readonly Dictionary<string, LanServerEntry> _map = new Dictionary<string, LanServerEntry>();
        private readonly double _ttl;

        public LanServerList(double ttlSeconds = DefaultTtl) { _ttl = ttlSeconds <= 0 ? DefaultTtl : ttlSeconds; }

        public int Count => _map.Count;

        /// <summary>Yeni sunucu mu eklendi / içerik değişti mi true döner.</summary>
        public bool Report(string address, in LanBeacon beacon, double now)
        {
            if (string.IsNullOrEmpty(address))
                return false;
            var key = address + ":" + beacon.GamePort.ToString(CultureInfo.InvariantCulture);
            var changed = !_map.TryGetValue(key, out var old) || !old.Beacon.Equals(beacon);
            if (changed && !_map.ContainsKey(key) && _map.Count >= MaxEntries)
                return false;
            _map[key] = new LanServerEntry(address, beacon, now);
            return changed;
        }

        /// <summary>TTL'i dolanları siler; silinen varsa true.</summary>
        public bool Prune(double now)
        {
            List<string> dead = null;
            foreach (var kv in _map)
                if (now - kv.Value.LastSeen > _ttl)
                    (dead ??= new List<string>()).Add(kv.Key);
            if (dead == null)
                return false;
            foreach (var k in dead)
                _map.Remove(k);
            return true;
        }

        public void Clear() => _map.Clear();

        /// <summary>Ada göre sıralı kopya.</summary>
        public List<LanServerEntry> Snapshot()
        {
            var list = new List<LanServerEntry>(_map.Values);
            list.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(a.Beacon.Name, b.Beacon.Name);
                return c != 0 ? c : string.CompareOrdinal(a.Endpoint, b.Endpoint);
            });
            return list;
        }
    }
}
