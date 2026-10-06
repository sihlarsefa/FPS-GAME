using System;

namespace Project.Infrastructure.Audio.HdrMix
{
    /// <summary>
    /// Frostbite tarzı HDR ses penceresi (saf mantık, Unity bağımlılığı yok). Çok yüksek bir olay (patlama, kendi
    /// makineli tüfeğimiz) pencerenin tepesini yukarı iter; pencerenin altında kalan sessiz sesler kırpılmak yerine
    /// aşağı itilir (kısılır). Olay bitince pencere yavaşça gevşer ve sessiz sesler geri gelir.
    /// Seviyeler dB cinsindendir: 0 dB = en yüksek olay (yakın patlama), tipik sessiz ortam -45 dB civarıdır.
    /// </summary>
    public static class HdrAudioMath
    {
        public const float ReferenceDb = 0f;
        public const float MaxTopDb = 6f;
        /// <summary>Sessiz durumda pencere tepesi (hiçbir ses itilmez).</summary>
        public const float IdleTopDb = -30f;
        public const float WindowHeightDb = 40f;
        public const float HoldSeconds = 0.25f;
        public const float ReleaseDbPerSecond = 45f;
        /// <summary>Pencere altına düşen her dB için uygulanan ek kısma oranı.</summary>
        public const float PushRatio = 0.75f;
        public const float MaxPushDb = 30f;
        /// <summary>Tepe bu değeri aşınca ana kazanç hafifçe kısılır (kırpma önleme).</summary>
        public const float HeadroomKneeDb = -3f;

        /// <summary>Silah kimliğinden (lmg_, sr_, smg_ ...) HDR olay türü.</summary>
        public static HdrEventKind KindForWeaponId(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return HdrEventKind.Generic;
            if (weaponId.StartsWith("lmg_", StringComparison.Ordinal))
                return HdrEventKind.OwnMachineGun;
            if (weaponId.StartsWith("sr_", StringComparison.Ordinal) || weaponId.StartsWith("dmr_", StringComparison.Ordinal))
                return HdrEventKind.Sniper;
            if (weaponId.StartsWith("pistol_", StringComparison.Ordinal) || weaponId.StartsWith("smg_", StringComparison.Ordinal))
                return HdrEventKind.OwnPistol;
            return HdrEventKind.OwnRifle;
        }

        public static float DbToLinear(float db) => (float)Math.Pow(10.0, db / 20.0);

        public static float LinearToDb(float linear) => linear <= 0.000001f ? -120f : 20f * (float)Math.Log10(linear);

        /// <summary>Kaynak seviyesi (dB) için pencere altı itme kazancı (dB, ≤ 0).</summary>
        public static float PushDb(float sourceDb, float windowTopDb, float windowHeightDb = WindowHeightDb)
        {
            var bottom = windowTopDb - windowHeightDb;
            if (sourceDb >= bottom)
                return 0f;
            var push = (bottom - sourceDb) * PushRatio;
            return -Math.Min(MaxPushDb, push);
        }

        /// <summary>İki seviyeyi enerji olarak toplar (iki eş zamanlı patlama +3 dB).</summary>
        public static float PowerSumDb(float aDb, float bDb)
        {
            var hi = Math.Max(aDb, bDb);
            var lo = Math.Min(aDb, bDb);
            return hi + 10f * (float)Math.Log10(1.0 + Math.Pow(10.0, (lo - hi) / 10.0));
        }

        /// <summary>Mesafeye bağlı seviye (ters kare yasası, 1 m'de kaynak seviyesi). Gökyüzü emilimi ihmal edilir.</summary>
        public static float DistanceAttenuatedDb(float sourceDb, float distanceMeters)
        {
            var d = Math.Max(1f, distanceMeters);
            return sourceDb - 20f * (float)Math.Log10(d);
        }

        /// <summary>Olay türü için tipik 1 m seviye (dB, yakın patlama = 0 dB'ye göre ölçeklenmiş değil; mesafe öncesi).</summary>
        public static float SourceLevelDb(HdrEventKind kind)
        {
            switch (kind)
            {
                case HdrEventKind.Explosion: return 52f;
                case HdrEventKind.OwnMachineGun: return 30f;
                case HdrEventKind.OwnRifle: return 26f;
                case HdrEventKind.OwnPistol: return 20f;
                case HdrEventKind.Sniper: return 34f;
                case HdrEventKind.Vehicle: return 24f;
                case HdrEventKind.Footstep: return -8f;
                default: return 18f;
            }
        }

        /// <summary>Olay için pencereye bildirilecek seviye (dB); kaynak seviyesi mesafe ile sönümlenir ve 0 dB tavanına oturtulur.</summary>
        public static float EventLevelDb(HdrEventKind kind, float distanceMeters)
        {
            var level = DistanceAttenuatedDb(SourceLevelDb(kind), distanceMeters) - 40f;
            return Math.Min(MaxTopDb, level);
        }
    }

    public enum HdrEventKind
    {
        Generic = 0,
        Explosion,
        OwnMachineGun,
        OwnRifle,
        OwnPistol,
        Sniper,
        Vehicle,
        Footstep
    }

    /// <summary>Durumlu HDR penceresi: <see cref="Report"/> ile olay bildirilir, <see cref="Tick"/> ile gevşer.</summary>
    public sealed class HdrWindow
    {
        private float _peakDb = HdrAudioMath.IdleTopDb;
        private float _hold;

        public float TopDb => Math.Max(HdrAudioMath.IdleTopDb, _peakDb);
        public float BottomDb => TopDb - HdrAudioMath.WindowHeightDb;

        /// <summary>Pencere tepesi ne kadar yukarıda (0 = boşta).</summary>
        public float Excitement01 =>
            Math.Min(1f, Math.Max(0f, (TopDb - HdrAudioMath.IdleTopDb) / (HdrAudioMath.MaxTopDb - HdrAudioMath.IdleTopDb)));

        public void Report(float levelDb)
        {
            if (float.IsNaN(levelDb))
                return;
            var level = Math.Min(HdrAudioMath.MaxTopDb, levelDb);
            if (level <= HdrAudioMath.IdleTopDb)
                return;

            // Pencere zaten yüksekse aynı anda gelen olaylar enerji olarak toplanır; değilse tepe yükselir.
            _peakDb = _hold > 0f && _peakDb > HdrAudioMath.IdleTopDb
                ? Math.Min(HdrAudioMath.MaxTopDb, HdrAudioMath.PowerSumDb(_peakDb, level))
                : Math.Max(_peakDb, level);
            _hold = HdrAudioMath.HoldSeconds;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;
            if (_hold > 0f)
            {
                _hold -= dt;
                return;
            }

            if (_peakDb > HdrAudioMath.IdleTopDb)
                _peakDb = Math.Max(HdrAudioMath.IdleTopDb, _peakDb - HdrAudioMath.ReleaseDbPerSecond * dt);
        }

        /// <summary>Kaynak seviyesi için doğrusal kazanç (1 = dokunma).</summary>
        public float GainLinear(float sourceDb) => HdrAudioMath.DbToLinear(HdrAudioMath.PushDb(sourceDb, TopDb));

        /// <summary>Ana kazanç: tepe diz noktasını aşınca hafifçe kısılır (kırpma yerine pay bırakır).</summary>
        public float HeadroomLinear
        {
            get
            {
                var over = Math.Max(0f, TopDb - HdrAudioMath.HeadroomKneeDb);
                return HdrAudioMath.DbToLinear(-over * 0.5f);
            }
        }

        public void Reset()
        {
            _peakDb = HdrAudioMath.IdleTopDb;
            _hold = 0f;
        }
    }
}
