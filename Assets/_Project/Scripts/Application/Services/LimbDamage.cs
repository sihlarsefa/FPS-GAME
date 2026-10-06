using System;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>
    /// Uzuv hasarı saf kuralları (gerçekçi yaralanma): bacak yarası yavaşlatır, kol yarası nişanı bozar,
    /// korumasız gövde isabeti kanatır, kasklı kafa sıyrığı sersemletir. Dengeler DamageCalculator'a dokunmaz.
    /// </summary>
    public static class LimbDamageRules
    {
        /// <summary>Yara sayılması için asgari (zırh sonrası) hasar.</summary>
        public const float WoundMinDamage = 5f;

        /// <summary>Kanama başlatan asgari gövde hasarı (korumasız).</summary>
        public const float BleedMinDamage = 8f;

        public const int MaxLegWounds = 2;
        public const int MaxArmWounds = 2;

        /// <summary>Yaralı bacak başına hız kaybı; toplam üst sınır.</summary>
        public const float LegSlowPerWound = 0.25f;
        public const float LegSlowCap = 0.40f;

        /// <summary>Yaralı kol başına ek sarsıntı ve ADS yavaşlaması.</summary>
        public const float ArmSwayPerWound = 0.30f;
        public const float ArmAdsSlowPerWound = 0.30f;

        /// <summary>Kanama: her 3 sn'de 1 can, toplam 15 sn.</summary>
        public const float BleedTickSeconds = 3f;
        public const float BleedTickDamage = 1f;
        public const float BleedDurationSeconds = 15f;

        /// <summary>Kasklı kafa sıyrığı: asgari/azami hasar ve sersemlik süresi.</summary>
        public const float GrazeMaxDamage = 35f;
        public const float FlinchSeconds = 1.2f;

        /// <summary>Kanamayı yazan hasar kaynak kimliği (yaralanmayı yeniden tetiklemez).</summary>
        public const string BleedSourceId = "bleed";

        public static bool IsEnvironmentalSource(string sourceId)
        {
            return string.Equals(sourceId, BleedSourceId, StringComparison.Ordinal)
                   || string.Equals(sourceId, "bleedout", StringComparison.Ordinal)
                   || string.Equals(sourceId, "fall", StringComparison.Ordinal);
        }

        public static float MoveSpeedMultiplier(int legWounds)
        {
            var wounds = Math.Max(0, Math.Min(MaxLegWounds, legWounds));
            var slow = wounds * LegSlowPerWound;
            if (slow > LegSlowCap)
                slow = LegSlowCap;
            return 1f - slow;
        }

        public static float SwayMultiplier(int armWounds)
        {
            return 1f + Math.Max(0, Math.Min(MaxArmWounds, armWounds)) * ArmSwayPerWound;
        }

        /// <summary>ADS süresi çarpanı (1 = normal, büyük = yavaş).</summary>
        public static float AdsTimeMultiplier(int armWounds)
        {
            return 1f + Math.Max(0, Math.Min(MaxArmWounds, armWounds)) * ArmAdsSlowPerWound;
        }

        public static bool IsGraze(bool helmetWorn, float appliedDamage, bool lethal)
        {
            return helmetWorn && !lethal && appliedDamage > 0f && appliedDamage <= GrazeMaxDamage;
        }
    }

    /// <summary>Tek savaşçının uzuv yaraları (sunucu tarafı; Combatant tick eder). Saf, Unity'siz.</summary>
    public sealed class LimbDamageState
    {
        private float _bleedRemaining;
        private float _bleedClock;
        private float _flinchRemaining;

        public int LegWounds { get; private set; }
        public int ArmWounds { get; private set; }
        public bool IsBleeding => _bleedRemaining > 0f;
        public float BleedRemaining => _bleedRemaining;
        public float FlinchRemaining => _flinchRemaining;
        public bool IsFlinching => _flinchRemaining > 0f;
        public bool HasWounds => LegWounds > 0 || ArmWounds > 0 || IsBleeding;

        public float MoveSpeedMultiplier => LimbDamageRules.MoveSpeedMultiplier(LegWounds);
        public float SwayMultiplier => LimbDamageRules.SwayMultiplier(ArmWounds);
        public float AdsTimeMultiplier => LimbDamageRules.AdsTimeMultiplier(ArmWounds);

        /// <summary>Kafa sıyrığı olduğunda tetiklenir (ses boğma / ağır sarsıntı kancası).</summary>
        public event Action GrazeFlinched;

        /// <summary>İsabet kaydı (uygulanan, zırh sonrası hasar). Hayatta kalınan isabetlerde çağrılır.</summary>
        public void OnHit(BodyPart part, float appliedDamage, bool torsoUnprotected, bool helmetWorn, bool lethal)
        {
            if (float.IsNaN(appliedDamage) || appliedDamage <= 0f || lethal)
                return;

            switch (part)
            {
                case BodyPart.Leg:
                    if (appliedDamage >= LimbDamageRules.WoundMinDamage && LegWounds < LimbDamageRules.MaxLegWounds)
                        LegWounds++;
                    break;
                case BodyPart.Arm:
                    if (appliedDamage >= LimbDamageRules.WoundMinDamage && ArmWounds < LimbDamageRules.MaxArmWounds)
                        ArmWounds++;
                    break;
                case BodyPart.Torso:
                    if (torsoUnprotected && appliedDamage >= LimbDamageRules.BleedMinDamage)
                    {
                        if (_bleedRemaining <= 0f)
                            _bleedClock = 0f;
                        _bleedRemaining = LimbDamageRules.BleedDurationSeconds; // yenilenir, yığılmaz
                    }
                    break;
                case BodyPart.Head:
                    if (LimbDamageRules.IsGraze(helmetWorn, appliedDamage, lethal))
                    {
                        _flinchRemaining = LimbDamageRules.FlinchSeconds;
                        GrazeFlinched?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>Zamanı ilerletir; bu karede uygulanacak kanama hasarını döndürür.</summary>
        public float Tick(float dt)
        {
            if (float.IsNaN(dt) || dt <= 0f)
                return 0f;

            if (_flinchRemaining > 0f)
                _flinchRemaining = Math.Max(0f, _flinchRemaining - dt);

            if (_bleedRemaining <= 0f)
                return 0f;

            var step = Math.Min(dt, _bleedRemaining);
            _bleedRemaining -= step;
            _bleedClock += step;

            var damage = 0f;
            while (_bleedClock >= LimbDamageRules.BleedTickSeconds - 1e-3f)
            {
                _bleedClock = Math.Max(0f, _bleedClock - LimbDamageRules.BleedTickSeconds);
                damage += LimbDamageRules.BleedTickDamage;
            }

            if (_bleedRemaining <= 0f)
                _bleedClock = 0f;
            return damage;
        }

        /// <summary>Bandaj/ilk yardım: kanama ve uzuv yaraları temizlenir.</summary>
        public void Bandage()
        {
            LegWounds = 0;
            ArmWounds = 0;
            _bleedRemaining = 0f;
            _bleedClock = 0f;
        }

        public void Reset()
        {
            Bandage();
            _flinchRemaining = 0f;
        }
    }
}
