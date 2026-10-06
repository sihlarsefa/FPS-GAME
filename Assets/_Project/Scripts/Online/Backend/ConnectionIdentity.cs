using System;
using System.Collections.Generic;
using System.Globalization;

namespace Project.Online.Backend
{
    /// <summary>
    /// Netcode ConnectionApproval yükü: <c>harekat|{playerId}|{jwt}|{ticketId}</c> (son iki alan opsiyonel).
    /// JWT base64url olduğundan '|' içermez. Eski biçim <c>harekat|{playerId}</c> hâlâ okunur (kimliksiz = anonim).
    /// </summary>
    public static class ConnectionIdentity
    {
        public const string Prefix = "harekat";
        public const int MaxPayloadBytes = 2048;

        /// <summary>İstemci tarafında JWT sağlayıcısı (OnlineServices bağlar); null = anonim bağlantı.</summary>
        public static Func<string> TokenProvider;
        /// <summary>İstemci tarafında aktif matchmaking bilet kimliği (varsa).</summary>
        public static Func<Guid> TicketProvider;

        public readonly struct Payload
        {
            public readonly bool Valid;
            public readonly int PlayerId;
            public readonly string Jwt;
            public readonly Guid TicketId;

            public Payload(bool valid, int playerId, string jwt, Guid ticketId)
            {
                Valid = valid;
                PlayerId = playerId;
                Jwt = jwt ?? string.Empty;
                TicketId = ticketId;
            }

            public bool HasToken => !string.IsNullOrEmpty(Jwt);
        }

        public static string Build(int playerId, string jwt, Guid ticketId)
        {
            var s = Prefix + "|" + playerId.ToString(CultureInfo.InvariantCulture);
            var hasJwt = !string.IsNullOrEmpty(jwt) && jwt.IndexOf('|') < 0;
            if (hasJwt)
            {
                s += "|" + jwt;
                if (ticketId != Guid.Empty)
                    s += "|" + ticketId.ToString("D");
            }

            return s;
        }

        /// <summary>Yerel sağlayıcılardan (token/bilet) yük metni üretir; sağlayıcı hatası anonim yüke düşer.</summary>
        public static string BuildLocal(int playerId)
        {
            string jwt = null;
            var ticket = Guid.Empty;
            try { jwt = TokenProvider?.Invoke(); } catch (Exception) { /* anonim */ }
            try { if (TicketProvider != null) ticket = TicketProvider(); } catch (Exception) { /* bilet yok */ }
            return Build(playerId, jwt, ticket);
        }

        public static Payload Parse(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxPayloadBytes)
                return default;
            return Parse(System.Text.Encoding.UTF8.GetString(data));
        }

        public static Payload Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return default;
            var parts = text.Split('|');
            if (parts.Length < 2 || parts[0] != Prefix
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                return default;

            var jwt = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var ticket = Guid.Empty;
            if (parts.Length > 3)
                Guid.TryParse(parts[3].Trim(), out ticket);
            return new Payload(true, pid, jwt, ticket);
        }
    }

    /// <summary>
    /// Sunucuda doğrulanmış kimlikler: PlayerId.Value ↔ backend profil/squad Guid. Eşlenmemiş savaşanlar bot sayılır.
    /// Bir profil aynı anda tek PlayerId'ye, bir PlayerId tek profile bağlanabilir (kimlik çalma/çift giriş engeli).
    /// </summary>
    public sealed class IdentityRegistry
    {
        private readonly Dictionary<int, Guid> _profileByPlayer = new Dictionary<int, Guid>();
        private readonly Dictionary<int, Guid> _squadByPlayer = new Dictionary<int, Guid>();
        private readonly Dictionary<Guid, int> _playerByProfile = new Dictionary<Guid, int>();

        public int Count => _profileByPlayer.Count;

        public bool TryBind(int playerId, Guid profileId, Guid? squadId, out string reason)
        {
            reason = null;
            if (profileId == Guid.Empty)
            {
                reason = "Geçersiz profil.";
                return false;
            }

            if (_playerByProfile.TryGetValue(profileId, out var existing) && existing != playerId)
            {
                reason = "Bu hesap zaten bağlı.";
                return false;
            }

            if (_profileByPlayer.TryGetValue(playerId, out var owner) && owner != profileId)
            {
                reason = "Oyuncu kimliği kullanımda.";
                return false;
            }

            _profileByPlayer[playerId] = profileId;
            _playerByProfile[profileId] = playerId;
            if (squadId.HasValue && squadId.Value != Guid.Empty)
                _squadByPlayer[playerId] = squadId.Value;
            return true;
        }

        public void Release(int playerId)
        {
            // Maç sonucu için profil eşlemesi korunur; yalnızca yeniden bağlanma kilidi (profil→oyuncu) kalkar.
            if (_profileByPlayer.TryGetValue(playerId, out var profile)
                && _playerByProfile.TryGetValue(profile, out var p) && p == playerId)
                _playerByProfile.Remove(profile);
        }

        public bool IsHuman(int playerId) => _profileByPlayer.ContainsKey(playerId);

        public bool TryGetProfile(int playerId, out string profileId)
        {
            if (_profileByPlayer.TryGetValue(playerId, out var g))
            {
                profileId = g.ToString("D");
                return true;
            }

            profileId = null;
            return false;
        }

        public bool TryGetSquad(int playerId, out string squadId)
        {
            if (_squadByPlayer.TryGetValue(playerId, out var g))
            {
                squadId = g.ToString("D");
                return true;
            }

            squadId = null;
            return false;
        }

        public void Clear()
        {
            _profileByPlayer.Clear();
            _squadByPlayer.Clear();
            _playerByProfile.Clear();
        }
    }
}
