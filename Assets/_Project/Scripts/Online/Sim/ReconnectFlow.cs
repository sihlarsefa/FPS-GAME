using System;
using System.Collections.Generic;

namespace Project.Online.Sim
{
    /// <summary>İstemci bağlantı akışı durumu.</summary>
    public enum ReconnectState : byte { Connected, Lost, Waiting, Reconnecting, Resyncing, Failed }

    /// <summary>
    /// İstemci tarafı yeniden bağlanma durum makinesi (saf, zaman dışarıdan verilir): üstel geri çekilme,
    /// deneme sınırı ve yeniden bağlanınca durum eşitleme (JoinState + tam snapshot) aşaması.
    /// </summary>
    public sealed class ReconnectFlow
    {
        public const int DefaultMaxAttempts = 6;
        public const float BaseDelay = 0.5f;
        public const float MaxDelay = 8f;

        private readonly int _maxAttempts;
        private float _nextAttemptAt;

        public ReconnectFlow(int maxAttempts = DefaultMaxAttempts) { _maxAttempts = Math.Max(1, maxAttempts); }

        public ReconnectState State { get; private set; } = ReconnectState.Connected;
        public int Attempts { get; private set; }
        /// <summary>Resync için sunucudan beklenen: tam snapshot alındı mı, JoinState alındı mı.</summary>
        public bool GotFullSnapshot { get; private set; }
        public bool GotJoinState { get; private set; }

        public static float BackoffDelay(int attempt) =>
            Math.Min(MaxDelay, BaseDelay * (float)Math.Pow(2, Math.Max(0, attempt - 1)));

        public void OnConnectionLost(float now)
        {
            if (State == ReconnectState.Failed) return;
            State = ReconnectState.Waiting;
            Attempts = 0;
            _nextAttemptAt = now + BackoffDelay(1);
        }

        /// <summary>Waiting durumunda süre dolduysa true döner ve deneme başlatır (çağıran bağlanmayı dener).</summary>
        public bool TryBeginAttempt(float now)
        {
            if (State != ReconnectState.Waiting || now < _nextAttemptAt) return false;
            Attempts++;
            State = ReconnectState.Reconnecting;
            return true;
        }

        public void OnAttemptFailed(float now)
        {
            if (State != ReconnectState.Reconnecting) return;
            if (Attempts >= _maxAttempts) { State = ReconnectState.Failed; return; }
            State = ReconnectState.Waiting;
            _nextAttemptAt = now + BackoffDelay(Attempts + 1);
        }

        /// <summary>Taşıma katmanı yeniden bağlandı: eşitleme aşamasına geç (delta baseline'ları geçersiz).</summary>
        public void OnTransportRestored()
        {
            if (State != ReconnectState.Reconnecting) return;
            State = ReconnectState.Resyncing;
            GotFullSnapshot = false;
            GotJoinState = false;
        }

        public void OnJoinStateReceived() { if (State == ReconnectState.Resyncing) { GotJoinState = true; Check(); } }
        public void OnFullSnapshotReceived() { if (State == ReconnectState.Resyncing) { GotFullSnapshot = true; Check(); } }

        private void Check()
        {
            if (GotJoinState && GotFullSnapshot) { State = ReconnectState.Connected; Attempts = 0; }
        }

        /// <summary>Eşitleme sırasında delta paketleri uygulanmaz (baseline yok): yalnızca tam snapshot kabul edilir.</summary>
        public bool AcceptDeltas => State == ReconnectState.Connected;
    }

    /// <summary>
    /// Sunucu tarafı: kopan oyuncunun yerini tolerans süresi boyunca tutar (token ile geri talep).
    /// Süre dolunca slot serbest kalır. Saf mantık, saat dışarıdan.
    /// </summary>
    public sealed class ReconnectGraceTable
    {
        public const float DefaultGraceSeconds = 30f;
        private readonly Dictionary<string, (int playerId, float expiresAt)> _held = new Dictionary<string, (int, float)>();
        private readonly float _grace;

        public ReconnectGraceTable(float graceSeconds = DefaultGraceSeconds) { _grace = Math.Max(1f, graceSeconds); }

        public int HeldCount => _held.Count;

        public void Hold(string token, int playerId, float now)
        {
            if (!string.IsNullOrEmpty(token)) _held[token] = (playerId, now + _grace);
        }

        /// <summary>Token geçerli ve süresi dolmamışsa oyuncu kimliğini verir ve kaydı tüketir (tek kullanımlık).</summary>
        public bool TryClaim(string token, float now, out int playerId)
        {
            playerId = -1;
            if (string.IsNullOrEmpty(token) || !_held.TryGetValue(token, out var e)) return false;
            _held.Remove(token);
            if (now > e.expiresAt) return false;
            playerId = e.playerId;
            return true;
        }

        /// <summary>Süresi dolanları temizler; serbest kalan oyuncu kimliklerini döner.</summary>
        public List<int> Expire(float now)
        {
            var freed = new List<int>();
            List<string> dead = null;
            foreach (var kv in _held)
                if (now > kv.Value.expiresAt) (dead ??= new List<string>()).Add(kv.Key);
            if (dead != null)
                foreach (var k in dead) { freed.Add(_held[k].playerId); _held.Remove(k); }
            return freed;
        }
    }
}
