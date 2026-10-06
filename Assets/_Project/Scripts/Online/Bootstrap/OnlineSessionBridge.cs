using System;
using System.Collections.Generic;
using Project.Online.Lan;

namespace Project.Online.Bootstrap
{
    /// <summary>Sunucu kurma seçenekleri (UI -> Netcode köprüsü).</summary>
    public readonly struct HostOptions
    {
        public readonly string Name;
        public readonly ushort Port;
        public readonly int MaxPlayers;
        public readonly bool FillWithBots;
        public readonly string MapId;

        public HostOptions(string name, ushort port, int maxPlayers, bool fillWithBots, string mapId)
        {
            Name = name; Port = port; MaxPlayers = maxPlayers; FillWithBots = fillWithBots; MapId = mapId;
        }
    }

    /// <summary>
    /// Project.Online, Netcode assembly'sine bağlı değildir (paket yokken derlenmez). Netcode varsa
    /// <c>NetcodeBridgeRegistration</c> bu kancaları doldurur; yoksa panel "Netcode paketi gerekli" gösterir.
    /// </summary>
    public static class OnlineSessionBridge
    {
        /// <summary>Netcode kancaları kayıtlı mı.</summary>
        public static bool NetcodeAvailable => StartHost != null && StartClient != null;

        /// <summary>Hata metni (null = başarı).</summary>
        public static Func<HostOptions, string> StartHost;
        public static Func<string, ushort, string> StartClient;
        public static Action Stop;
        /// <summary>Bağlı mı (host dinliyor / istemci bağlandı).</summary>
        public static Func<bool> IsConnected;
        public static Func<IReadOnlyList<LobbyPlayer>> Roster;
        /// <summary>Yerel oyuncunun sunucuya ping'i (ms), bilinmiyorsa -1.</summary>
        public static Func<int> LocalPingMs;

        /// <summary>Son bağlantı kopma/hata nedeni (Netcode kaydı yazar, panel okur).</summary>
        public static string LastDisconnectReason;

        /// <summary>Sunucu kurulurken seçilen harita kimliği ve bot doldurma (maç kurulumu okuyabilir).</summary>
        public static string HostedMapId = "kuzgun";
        public static bool HostedFillWithBots = true;
        public static int HostedMaxPlayers;

        public static void Clear()
        {
            StartHost = null; StartClient = null; Stop = null;
            IsConnected = null; Roster = null; LocalPingMs = null;
        }
    }
}
