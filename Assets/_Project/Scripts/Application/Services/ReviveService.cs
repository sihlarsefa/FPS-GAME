using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Yaralı (DBNO) durumunun saf mantığı: kanama süresi (45 sn), ayağa kaldırma ilerlemesi (6 sn, Sıhhiyeci 3 sn).
    /// Unity'ye bağlı değildir; Combatant/BotReviveBehaviour/oyuncu etkileşimi bu servisi çağırır.
    /// Takımda hayatta (yaralı olmayan) müttefik yoksa yaralı olunmaz → TryDown false döner (doğrudan ölüm).
    /// </summary>
    public sealed class ReviveService
    {
        public const float DefaultBleedOutSeconds = 45f;
        public const float DefaultReviveSeconds = 6f;
        public const float DefaultMedicReviveSeconds = 3f;
        /// <summary>Ayağa kalkınca dolan can oranı.</summary>
        public const float ReviveHealthFraction = 0.3f;
        /// <summary>Kaldırma için azami mesafe (m).</summary>
        public const float ReviveRange = 2.2f;

        private sealed class Entry
        {
            public PlayerId Id;
            public PlayerId Attacker;
            public int Team;
            public float Bleed;
            public PlayerId Reviver;
            public float Progress;
            public float Required;
        }

        private readonly IEventBus _bus;
        private readonly Dictionary<int, Entry> _downed = new();
        private readonly List<int> _scratch = new();

        public float BleedOutSeconds { get; }
        public float ReviveSeconds { get; }
        public float MedicReviveSeconds { get; }

        /// <summary>Kanamadan ölen (süre biten) savaşan; çağıran gerçek ölümü uygular.</summary>
        public event Action<PlayerId, PlayerId> BledOut;

        /// <summary>Ayağa kaldırma tamamlandı (kurban, kaldıran).</summary>
        public event Action<PlayerId, PlayerId> Revived;

        public ReviveService(IEventBus bus = null, float bleedOutSeconds = DefaultBleedOutSeconds,
            float reviveSeconds = DefaultReviveSeconds, float medicReviveSeconds = DefaultMedicReviveSeconds)
        {
            _bus = bus;
            BleedOutSeconds = bleedOutSeconds > 0f ? bleedOutSeconds : DefaultBleedOutSeconds;
            ReviveSeconds = reviveSeconds > 0f ? reviveSeconds : DefaultReviveSeconds;
            MedicReviveSeconds = medicReviveSeconds > 0f ? medicReviveSeconds : DefaultMedicReviveSeconds;
        }

        /// <summary>Kaldıranın hâlâ geçerli (sağ, yaralı değil) olduğunu söyler; false ise ilerleme bırakılır.</summary>
        public Func<PlayerId, bool> ReviverValidator { get; set; }

        public int DownedCount => _downed.Count;

        public float RequiredSecondsFor(TeamRole reviverRole) =>
            reviverRole == TeamRole.Medic ? MedicReviveSeconds : ReviveSeconds;

        /// <summary>Takımda yaralı olmayan sağ müttefik varsa yaralı olunabilir.</summary>
        public static bool CanBeDowned(int aliveTeammatesNotDowned) => aliveTeammatesNotDowned > 0;

        public bool IsDowned(PlayerId id) => id.IsValid && _downed.ContainsKey(id.Value);

        /// <summary>Kalan kanama süresi (yaralı değilse 0).</summary>
        public float BleedRemaining(PlayerId id) => _downed.TryGetValue(id.Value, out var e) ? e.Bleed : 0f;

        /// <summary>0..1 kaldırma ilerlemesi.</summary>
        public float ReviveProgress(PlayerId id) =>
            _downed.TryGetValue(id.Value, out var e) && e.Required > 0f ? Math.Min(1f, e.Progress / e.Required) : 0f;

        /// <summary>Yaralıyı bu duruma düşüren saldırgan (yaralı değilse/çevreselse geçersiz).</summary>
        public PlayerId AttackerOf(PlayerId id) => _downed.TryGetValue(id.Value, out var e) ? e.Attacker : PlayerId.Invalid;

        public PlayerId CurrentReviver(PlayerId id) => _downed.TryGetValue(id.Value, out var e) ? e.Reviver : PlayerId.Invalid;

        /// <summary>Yaralı durumuna sok. Sağ müttefik yoksa ya da zaten yaralıysa false.</summary>
        public bool TryDown(PlayerId victim, PlayerId attacker, int team, int aliveTeammatesNotDowned)
        {
            if (!victim.IsValid || _downed.ContainsKey(victim.Value) || !CanBeDowned(aliveTeammatesNotDowned))
                return false;

            _downed[victim.Value] = new Entry
            {
                Id = victim, Attacker = attacker, Team = team, Bleed = BleedOutSeconds, Reviver = PlayerId.Invalid
            };
            _bus?.Publish(new DownedEvent(victim, attacker, team));
            return true;
        }

        /// <summary>Kaldırmaya başla/sürdür. Başka biri kaldırıyorsa false. Kaldıran değişirse ilerleme sıfırlanır.</summary>
        public bool BeginRevive(PlayerId reviver, TeamRole reviverRole, PlayerId victim)
        {
            if (!_downed.TryGetValue(victim.Value, out var e) || !reviver.IsValid || reviver == victim)
                return false;

            if (e.Reviver.IsValid && e.Reviver != reviver)
                return false;

            if (e.Reviver != reviver)
            {
                e.Reviver = reviver;
                e.Progress = 0f;
            }

            e.Required = RequiredSecondsFor(reviverRole);
            return true;
        }

        /// <summary>Kaldırma iptal (tuş bırakıldı, mesafe/öldü). İlerleme sıfırlanır.</summary>
        public void CancelRevive(PlayerId reviver)
        {
            foreach (var e in _downed.Values)
            {
                if (e.Reviver == reviver)
                {
                    e.Reviver = PlayerId.Invalid;
                    e.Progress = 0f;
                }
            }
        }

        /// <summary>Yaralıyı kayıttan çıkar (öldü/bitirildi). True ise yaralıydı.</summary>
        public bool Remove(PlayerId id) => id.IsValid && _downed.Remove(id.Value);

        public void Clear() => _downed.Clear();

        /// <summary>Kanama ve kaldırma ilerlemesini işletir; biten kaldırma/kanama olaylarını yayınlar.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f || _downed.Count == 0)
                return;

            _scratch.Clear();
            foreach (var key in _downed.Keys)
                _scratch.Add(key);

            for (var i = 0; i < _scratch.Count; i++)
            {
                if (!_downed.TryGetValue(_scratch[i], out var e))
                    continue;

                if (e.Reviver.IsValid && ReviverValidator != null && !ReviverValidator(e.Reviver))
                {
                    e.Reviver = PlayerId.Invalid;
                    e.Progress = 0f;
                }

                if (e.Reviver.IsValid)
                {
                    // Kaldırılırken kanama durur.
                    e.Progress += dt;
                    if (e.Progress >= e.Required)
                    {
                        var reviver = e.Reviver;
                        _downed.Remove(_scratch[i]);
                        _bus?.Publish(new RevivedEvent(e.Id, reviver));
                        Revived?.Invoke(e.Id, reviver);
                    }

                    continue;
                }

                e.Bleed -= dt;
                if (e.Bleed <= 0f)
                {
                    _downed.Remove(_scratch[i]);
                    BledOut?.Invoke(e.Id, e.Attacker);
                }
            }
        }
    }
}
