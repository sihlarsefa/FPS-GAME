using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Project.Online.Lan
{
    /// <summary>IP:port ayrıştırma + Türkçe hata iletileri. Saf C#.</summary>
    public static class ServerEndpoint
    {
        public const ushort DefaultPort = 7777;

        /// <summary>"1.2.3.4", "1.2.3.4:7777", "host:port". Başarısızsa Türkçe hata.</summary>
        public static bool TryParse(string raw, out string host, out ushort port, out string error)
        {
            host = null;
            port = DefaultPort;
            error = null;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "Adres boş. Örnek: 192.168.1.20:7777";
                return false;
            }

            raw = raw.Trim();
            var colon = raw.LastIndexOf(':');
            var hostPart = raw;
            if (colon >= 0)
            {
                hostPart = raw.Substring(0, colon).Trim();
                var portPart = raw.Substring(colon + 1).Trim();
                if (!int.TryParse(portPart, NumberStyles.None, CultureInfo.InvariantCulture, out var p) || p < 1 || p > 65535)
                {
                    error = "Port geçersiz (1-65535).";
                    return false;
                }

                port = (ushort)p;
            }

            if (hostPart.Length == 0 || hostPart.Length > 253 || hostPart.IndexOfAny(new[] { ' ', '/', '\\', '|' }) >= 0)
            {
                error = "Sunucu adresi geçersiz.";
                return false;
            }

            host = hostPart;
            return true;
        }

        public static bool TryParsePort(string raw, out ushort port)
        {
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(raw))
                return true;
            if (!int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) || p < 1024 || p > 65535)
                return false;
            port = (ushort)p;
            return true;
        }

        public static string Format(string host, ushort port) => host + ":" + port.ToString(CultureInfo.InvariantCulture);

        /// <summary>Bağlantı hatası metnini kullanıcı dostu Türkçeye çevirir.</summary>
        public static string FriendlyError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Bağlantı kurulamadı.";
            var s = raw.ToLowerInvariant();
            if (s.Contains("refused")) return "Sunucu bağlantıyı reddetti (port kapalı olabilir).";
            if (s.Contains("timed out") || s.Contains("timeout")) return "Bağlantı zaman aşımına uğradı.";
            if (s.Contains("full")) return "Sunucu dolu.";
            if (s.Contains("version")) return "Sürüm uyuşmuyor.";
            if (s.Contains("resolve") || s.Contains("host")) return "Sunucu adresi çözümlenemedi.";
            return "Bağlantı hatası: " + raw;
        }
    }

    /// <summary>Son sunucular listesi (en yeni başta, tekilleştirilmiş, sınırlı). PlayerPrefs dizgesine serileşir.</summary>
    public static class RecentServers
    {
        public const int Max = 6;
        private const char Sep = ';';

        public static List<string> Parse(string stored)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(stored))
                return list;
            foreach (var part in stored.Split(Sep))
            {
                var t = part.Trim();
                if (t.Length == 0 || list.Contains(t))
                    continue;
                if (!ServerEndpoint.TryParse(t, out _, out _, out _))
                    continue;
                list.Add(t);
                if (list.Count >= Max)
                    break;
            }

            return list;
        }

        public static string Serialize(IEnumerable<string> items)
        {
            var sb = new StringBuilder();
            var n = 0;
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i) || i.IndexOf(Sep) >= 0)
                    continue;
                if (n++ > 0)
                    sb.Append(Sep);
                sb.Append(i.Trim());
                if (n >= Max)
                    break;
            }

            return sb.ToString();
        }

        /// <summary>Girişi başa alır; aynısı varsa taşır; Max'ı aşarsa kuyruğu atar.</summary>
        public static string Add(string stored, string endpoint)
        {
            var list = Parse(stored);
            if (string.IsNullOrWhiteSpace(endpoint))
                return Serialize(list);
            endpoint = endpoint.Trim();
            list.RemoveAll(e => string.Equals(e, endpoint, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, endpoint);
            if (list.Count > Max)
                list.RemoveRange(Max, list.Count - Max);
            return Serialize(list);
        }
    }

    /// <summary>Lobi oyuncu satırı.</summary>
    public readonly struct LobbyPlayer
    {
        public readonly string Name;
        public readonly int PingMs;   // < 0: bilinmiyor
        public readonly bool Ready;
        public readonly bool IsHost;

        public LobbyPlayer(string name, int pingMs, bool ready, bool isHost = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Oyuncu" : name;
            PingMs = pingMs;
            Ready = ready;
            IsHost = isHost;
        }
    }

    /// <summary>Ping / hazır / satır biçimlendirme ve lobi sıralaması. Saf C#.</summary>
    public static class LobbyRules
    {
        public enum PingQuality { Unknown, Good, Ok, Bad }

        public static PingQuality Quality(int pingMs)
        {
            if (pingMs < 0) return PingQuality.Unknown;
            if (pingMs <= 60) return PingQuality.Good;
            if (pingMs <= 130) return PingQuality.Ok;
            return PingQuality.Bad;
        }

        public static string PingText(int pingMs) =>
            pingMs < 0 ? "—" : Math.Min(pingMs, 999).ToString(CultureInfo.InvariantCulture) + " ms";

        public static string ReadyText(bool ready) => ready ? "HAZIR" : "BEKLİYOR";

        /// <summary>Host önce, sonra hazır olanlar, sonra ada göre.</summary>
        public static List<LobbyPlayer> Sort(IEnumerable<LobbyPlayer> players)
        {
            var l = new List<LobbyPlayer>(players ?? new LobbyPlayer[0]);
            l.Sort((a, b) =>
            {
                if (a.IsHost != b.IsHost) return a.IsHost ? -1 : 1;
                if (a.Ready != b.Ready) return a.Ready ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return l;
        }

        public static string Summary(IReadOnlyList<LobbyPlayer> players, int max)
        {
            var ready = 0;
            for (var i = 0; i < players.Count; i++)
                if (players[i].Ready) ready++;
            return players.Count + "/" + max + " oyuncu · " + ready + " hazır";
        }

        public static string ServerLine(in LanServerEntry e) =>
            e.Beacon.Name + "  " + Project.Core.Domain.MapCatalog.DisplayName(e.Beacon.MapId)
            + "  " + e.Beacon.Players + "/" + e.Beacon.MaxPlayers;
    }
}
