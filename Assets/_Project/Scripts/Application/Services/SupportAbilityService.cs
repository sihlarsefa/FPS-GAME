using System;
using System.Collections.Generic;

namespace Project.Application.Services
{
    /// <summary>
    /// "T-129 ATAK Desteği" saf mantığı (Unity yok; zaman çağıran verir). Tim 8 öldürmeye ulaşınca ya da bekleme
    /// (300 sn) dolunca açılır; kullanılınca öldürme sayacı sıfırlanır, bekleme yeniden başlar ve helikopter
    /// görevdeyken ikinci çağrı reddedilir.
    /// </summary>
    public sealed class SupportAbilityService
    {
        public const int KillsToUnlock = 8;
        public const float CooldownSeconds = 300f;
        public const float OrbitSeconds = 25f;
        public const float HelicopterHealth = 1500f;
        public const int RocketSalvos = 2;

        private sealed class TeamState
        {
            public int Kills;
            public float ReadyAt;
            public bool Active;
            public bool Notified;
        }

        private readonly Dictionary<int, TeamState> _teams = new();
        private readonly int _killsToUnlock;
        private readonly float _cooldown;

        public SupportAbilityService(int killsToUnlock = KillsToUnlock, float cooldown = CooldownSeconds)
        {
            _killsToUnlock = killsToUnlock > 0 ? killsToUnlock : KillsToUnlock;
            _cooldown = cooldown > 0f ? cooldown : CooldownSeconds;
        }

        public int KillsRequired => _killsToUnlock;

        private TeamState Get(int team, float now)
        {
            if (!_teams.TryGetValue(team, out var s))
            {
                s = new TeamState { ReadyAt = now + _cooldown };
                _teams[team] = s;
            }

            return s;
        }

        /// <summary>Tim bir düşman öldürdü (dost ateşi/çevresel ölüm çağıran tarafından elenir).</summary>
        public void RegisterKill(int team, float now)
        {
            if (team < 0) return;
            Get(team, now).Kills++;
        }

        public int GetKills(int team, float now) => Get(team, now).Kills;

        public bool IsActive(int team) => _teams.TryGetValue(team, out var s) && s.Active;

        public bool IsReady(int team, float now)
        {
            if (team < 0) return false;
            var s = Get(team, now);
            return !s.Active && (s.Kills >= _killsToUnlock || now >= s.ReadyAt);
        }

        /// <summary>Hazır değilse en az kaç saniye (bekleme) kaldığı; öldürmeyle de açılabilir. Hazırsa 0.</summary>
        public float GetCooldownRemaining(int team, float now)
        {
            if (team < 0) return 0f;
            var s = Get(team, now);
            if (s.Kills >= _killsToUnlock) return 0f;
            var r = s.ReadyAt - now;
            return r > 0f ? r : 0f;
        }

        /// <summary>Hazırsa kullanır: sayaç sıfırlanır, bekleme başlar, görev aktif olur.</summary>
        public bool TryActivate(int team, float now)
        {
            if (!IsReady(team, now)) return false;
            var s = Get(team, now);
            s.Kills = 0;
            s.ReadyAt = now + _cooldown;
            s.Active = true;
            s.Notified = false;
            return true;
        }

        /// <summary>Helikopter görevi bitti ya da düşürüldü.</summary>
        public void EndActive(int team)
        {
            if (_teams.TryGetValue(team, out var s)) s.Active = false;
        }

        /// <summary>Hazır olduğu ilk karede bir kez true döner ("hazır" bildirimi için).</summary>
        public bool ConsumeReadyNotice(int team, float now)
        {
            if (!IsReady(team, now)) return false;
            var s = Get(team, now);
            if (s.Notified) return false;
            s.Notified = true;
            return true;
        }

        public void Reset() => _teams.Clear();

        // ------------------------------------------------------------------ saf yardımcılar

        /// <summary>Yörünge noktası (yatay daire, sabit irtifa). Açı radyan.</summary>
        public static void OrbitPoint(float cx, float cz, float radius, float angle, out float x, out float z)
        {
            x = cx + (float)Math.Cos(angle) * radius;
            z = cz + (float)Math.Sin(angle) * radius;
        }

        /// <summary>Füze salvosu başlangıç zamanları (yörünge süresinin ~%30 ve ~%70'i).</summary>
        public static float SalvoTime(int index, float orbitSeconds)
        {
            var count = RocketSalvos;
            if (index < 0 || index >= count || orbitSeconds <= 0f) return -1f;
            return orbitSeconds * (0.3f + 0.4f * index / Math.Max(1, count - 1));
        }
    }

    /// <summary>Helikopter gövde canı (saf): hasar alır, 0'da düşer.</summary>
    public sealed class SupportHeliHealth
    {
        public SupportHeliHealth(float max = SupportAbilityService.HelicopterHealth)
        {
            Max = max > 0f ? max : SupportAbilityService.HelicopterHealth;
            Current = Max;
        }

        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDown => Current <= 0f;

        /// <summary>Hasar uygular; bu çağrıda yok olduysa true döner.</summary>
        public bool Damage(float amount)
        {
            if (IsDown || float.IsNaN(amount) || amount <= 0f) return false;
            Current = Math.Max(0f, Current - amount);
            return IsDown;
        }
    }
}
