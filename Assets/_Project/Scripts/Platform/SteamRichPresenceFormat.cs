using System;
using System.Globalization;

namespace Project.Platform
{
    /// <summary>Oyuncunun Steam arkadaş listesinde görünen durum (rich presence).</summary>
    public enum SteamPresenceState
    {
        Menu = 0,
        Training = 1,
        Preparing = 2,
        Insertion = 3,
        InMatch = 4,
        MatchEnded = 5
    }

    /// <summary>
    /// Rich presence anahtarları, yerelleştirme token'ları ve metin biçimi (saf C#; test edilebilir).
    /// Token'lar Partner → Rich Presence'a yüklenen <c>Steamworks~/rich_presence.vdf</c> ile birebir aynı olmalı.
    /// </summary>
    public static class SteamRichPresenceFormat
    {
        // Steam'in tanıdığı özel anahtarlar
        public const string KeyDisplay = "steam_display";
        public const string KeyStatus = "status";
        public const string KeyGroup = "steam_player_group";
        public const string KeyGroupSize = "steam_player_group_size";

        // HAREKÂT anahtarları (token içinde %anahtar% olarak ikame edilir)
        public const string KeyMap = "map";
        public const string KeySoldiers = "soldiers";

        public const string TokenMenu = "#Status_Menu";
        public const string TokenTraining = "#Status_Training";
        public const string TokenPreparing = "#Status_Preparing";
        public const string TokenInsertion = "#Status_Insertion";
        public const string TokenMatch = "#Status_Match";
        public const string TokenMatchEnded = "#Status_MatchEnded";

        /// <summary>Harita adı yoksa kullanılan ad.</summary>
        public const string DefaultMapName = "Kuzgun Vadisi";

        public static string TokenFor(SteamPresenceState state)
        {
            switch (state)
            {
                case SteamPresenceState.Training: return TokenTraining;
                case SteamPresenceState.Preparing: return TokenPreparing;
                case SteamPresenceState.Insertion: return TokenInsertion;
                case SteamPresenceState.InMatch: return TokenMatch;
                case SteamPresenceState.MatchEnded: return TokenMatchEnded;
                default: return TokenMenu;
            }
        }

        /// <summary>
        /// "status" anahtarı (Steam "Oyun bilgisini görüntüle" penceresi) için Türkçe düz metin.
        /// Örnek: <c>"Kuzgun Vadisi — 23 asker kaldı"</c>.
        /// </summary>
        public static string StatusText(SteamPresenceState state, string mapName, int soldiersRemaining)
        {
            var map = NormalizeMapName(mapName);
            switch (state)
            {
                case SteamPresenceState.Training:
                    return "Atış poligonunda";
                case SteamPresenceState.Preparing:
                    return map + " — harekâta hazırlanıyor";
                case SteamPresenceState.Insertion:
                    return map + " — intikal";
                case SteamPresenceState.InMatch:
                    return MatchStatus(map, soldiersRemaining);
                case SteamPresenceState.MatchEnded:
                    return map + " — harekât sona erdi";
                default:
                    return "Karargâhta (ana menü)";
            }
        }

        /// <summary>Görev metnindeki biçim: <c>"Kuzgun Vadisi — 23 asker kaldı"</c>.</summary>
        public static string MatchStatus(string mapName, int soldiersRemaining)
        {
            return NormalizeMapName(mapName) + " — " + SoldiersValue(soldiersRemaining) + " asker kaldı";
        }

        public static string SoldiersValue(int soldiersRemaining)
        {
            return Math.Max(0, soldiersRemaining).ToString(CultureInfo.InvariantCulture);
        }

        public static string NormalizeMapName(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                return DefaultMapName;

            var trimmed = mapName.Trim();
            // Steam rich presence değerleri en fazla 256 bayt (UTF-8). Türkçe karakterler 2 bayt.
            return trimmed.Length > 96 ? trimmed.Substring(0, 96) : trimmed;
        }

        /// <summary>
        /// Aynı durumun tekrar tekrar Steam'e gönderilmesini önlemek için imza.
        /// Asker sayısı yalnızca maç içinde imzaya girer.
        /// </summary>
        public static string Signature(SteamPresenceState state, string mapName, int soldiersRemaining)
        {
            var map = state == SteamPresenceState.Menu || state == SteamPresenceState.Training
                ? string.Empty
                : NormalizeMapName(mapName);
            var soldiers = state == SteamPresenceState.InMatch ? SoldiersValue(soldiersRemaining) : string.Empty;
            return ((int)state).ToString(CultureInfo.InvariantCulture) + "|" + map + "|" + soldiers;
        }
    }
}
