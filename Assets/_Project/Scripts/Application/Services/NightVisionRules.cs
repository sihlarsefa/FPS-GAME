using System;
using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// Gece Görüş Gözlüğü saf mantığı (Unity yok): pil modeli, teçhizat dağıtımı ve botların gece algı çarpanı.
    /// Pil 120 sn kullanım süresi; kapalıyken aynı hızda dolar (tam dolum 120 sn). Boşalınca kapanır,
    /// yeniden açmak için en az <see cref="RestartThreshold"/> şarj gerekir.
    /// </summary>
    public sealed class NightVisionBattery
    {
        public const float CapacitySeconds = 120f;
        public const float RechargeSeconds = 120f;
        public const float RestartThreshold = 0.15f;

        private float _charge = 1f;

        /// <summary>0..1 şarj.</summary>
        public float Charge => _charge;

        public bool IsOn { get; private set; }

        public bool CanTurnOn => _charge >= RestartThreshold;

        /// <summary>Açık/kapalı geçiş; açılamıyorsa (pil zayıf) false döner ve kapalı kalır.</summary>
        public bool Toggle()
        {
            if (IsOn)
            {
                IsOn = false;
                return true;
            }

            if (!CanTurnOn)
                return false;

            IsOn = true;
            return true;
        }

        public void ForceOff() => IsOn = false;

        public void Refill() => _charge = 1f;

        /// <summary>Açıkken boşalır, kapalıyken dolar. Pil biterse kapanır ve true döner.</summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f)
                return false;

            if (IsOn)
            {
                _charge -= dt / CapacitySeconds;
                if (_charge <= 0f)
                {
                    _charge = 0f;
                    IsOn = false;
                    return true;
                }
            }
            else
            {
                _charge = Math.Min(1f, _charge + dt / RechargeSeconds);
            }

            return false;
        }
    }

    public static class NightVisionRules
    {
        /// <summary>Gece botların görüş mesafesi çarpanı: gözlüksüz kısalır, gözlüklü etkilenmez.</summary>
        public static float PerceptionMultiplier(TimeOfDay time, bool hasGoggles)
        {
            if (hasGoggles)
                return 1f;

            switch (time)
            {
                case TimeOfDay.Gece: return 0.55f;
                case TimeOfDay.Safak: return 0.85f;
                case TimeOfDay.Aksam: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>Gece görevinde Tim Komutanı ve Telsizci gözlükle başlar.</summary>
        public static bool IssuedAtStart(TeamRole role, TimeOfDay time) =>
            time == TimeOfDay.Gece && (role == TeamRole.Leader || role == TeamRole.Radioman);

        /// <summary>Teçhizata gözlüğü ekler (yinelenen çağrıda ikinci kez eklemez).</summary>
        public static Loadout IssueFor(Loadout loadout, TimeOfDay time)
        {
            if (loadout != null && IssuedAtStart(loadout.Role, time) && loadout.CountOf(ItemIds.NightVision) == 0)
                loadout.Add(ItemIds.NightVision, 1);
            return loadout;
        }
    }
}
