using System;

namespace Project.Infrastructure.Audio.Weapons
{
    /// <summary>Namlu ucu aksesuarı.</summary>
    public enum MuzzleDevice { None = 0, FlashHider, Brake, Suppressor, IntegralSuppressor }

    /// <summary>Mühimmat hız sınıfı: sesüstü mermi balistik çatlama (crack) üretir, sesaltı üretmez.</summary>
    public enum AmmoSpeed { Supersonic = 0, Subsonic }

    /// <summary>Atış sinyalinin 1 m'deki dB seviyeleri (oyun ölçeği, SPL'ye yakın) ve türetilmiş bayraklar.</summary>
    public readonly struct ShotLevels
    {
        public readonly float BlastDb;
        public readonly float CrackDb;
        public readonly float MechDb;
        public readonly bool HasCrack;

        public ShotLevels(float blastDb, float crackDb, float mechDb, bool hasCrack)
        {
            BlastDb = blastDb;
            CrackDb = crackDb;
            MechDb = mechDb;
            HasCrack = hasCrack;
        }
    }

    /// <summary>Katman karışım ağırlıkları: 1 = susturucusuz referans.</summary>
    public readonly struct ShotLayerMix
    {
        public readonly float BodyGain;
        public readonly float CrackGain;
        public readonly float ThumpGain;
        public readonly float MechGain;
        public readonly float TailGain;
        public readonly float BodyCutoffScale;
        public readonly float BodyPitchScale;
        public readonly float TailCutoffScale;

        public ShotLayerMix(float body, float crack, float thump, float mech, float tail,
            float bodyCut, float bodyPitch, float tailCut)
        {
            BodyGain = body;
            CrackGain = crack;
            ThumpGain = thump;
            MechGain = mech;
            TailGain = tail;
            BodyCutoffScale = bodyCut;
            BodyPitchScale = bodyPitch;
            TailCutoffScale = tailCut;
        }
    }

    /// <summary>
    /// Susturucu gerçekçiliği (saf kurallar). Kaynak bulgular: susturucu namlu patlamasını ~20-35 dB azaltır
    /// ama sesüstü mermide balistik çatlama (crack, ~140+ dB) hiç değişmez; gerçek "sessiz" ancak sesaltı
    /// mühimmat + susturucu ile olur ve o zaman mekanik (sürgü/gaz dönüşü) katmanı öne çıkar.
    /// Susturucu ses hızını/silahı gizlemez: yakın mesafede hâlâ yüksektir, sadece menzil ciddi düşer.
    /// </summary>
    public static class SuppressorRules
    {
        /// <summary>Sesüstü kabul eşiği: ses hızının 1.1 katı (transonik çatlama zayıf, oyunda yok sayılır).</summary>
        public const float SupersonicMargin = 1.1f;

        /// <summary>Namlu çıkış hızı (m/s) sınıf varsayılanı.</summary>
        public static float DefaultMuzzleVelocity(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Pistol: return 360f;   // 9x19 standart
                case ShotClass.Smg: return 380f;
                case ShotClass.Shotgun: return 380f;  // saçma, hızla yavaşlar
                case ShotClass.Sniper: return 850f;
                default: return 900f;                 // 5.56/7.62 tüfek
            }
        }

        public static bool IsSupersonic(float muzzleVelocity, float speedOfSound = 343f)
            => muzzleVelocity >= speedOfSound * SupersonicMargin;

        /// <summary>Susturucusuz namlu patlaması (1 m, dB).</summary>
        public static float BaseBlastDb(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Pistol: return 160f;
                case ShotClass.Smg: return 159f;
                case ShotClass.Shotgun: return 162f;
                case ShotClass.Sniper: return 170f;
                default: return 165f;
            }
        }

        /// <summary>Balistik çatlama seviyesi (yalnızca sesüstü; mermi yolunun yakınında).</summary>
        public static float BaseCrackDb(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Sniper: return 152f;
                case ShotClass.Rifle: return 148f;
                case ShotClass.Smg: return 130f;
                case ShotClass.Pistol: return 128f;
                default: return 126f;
            }
        }

        /// <summary>Mekanik (sürgü, çekiç, gaz dönüşü) katmanı seviyesi (dB).</summary>
        public static float MechDb(ShotClass c)
        {
            switch (c)
            {
                case ShotClass.Pistol: return 98f;
                case ShotClass.Smg: return 96f;
                case ShotClass.Shotgun: return 92f;
                case ShotClass.Sniper: return 82f;
                default: return 93f;
            }
        }

        /// <summary>Aksesuarın namlu patlamasını azaltması (dB, öne doğru). Fren öne azaltmaz.</summary>
        public static float DeviceReductionDb(MuzzleDevice d)
        {
            switch (d)
            {
                case MuzzleDevice.FlashHider: return 0.5f;
                case MuzzleDevice.Brake: return 0f;
                case MuzzleDevice.Suppressor: return 26f;
                case MuzzleDevice.IntegralSuppressor: return 32f;
                default: return 0f;
            }
        }

        public static bool IsSuppressor(MuzzleDevice d) => d == MuzzleDevice.Suppressor || d == MuzzleDevice.IntegralSuppressor;

        /// <summary>Susturucu aşınması: bölme (baffle) erozyonu ve kurum, 1500 atışta 5 dB'ye kadar verim kaybı.</summary>
        public static float WearPenaltyDb(int roundsSinceNew)
        {
            if (roundsSinceNew <= 0) return 0f;
            var t = roundsSinceNew / 1500f;
            return 5f * (t > 1f ? 1f : t);
        }

        /// <summary>Sesaltı yük (daha az barut): tüfek sınıfında susturucuyla ek -4 dB.</summary>
        public static float SubsonicLoadBonusDb(ShotClass c, MuzzleDevice d, AmmoSpeed ammo)
        {
            if (ammo != AmmoSpeed.Subsonic || !IsSuppressor(d)) return 0f;
            return c == ShotClass.Rifle || c == ShotClass.Sniper ? 4f : 0f;
        }

        /// <summary>Net azalma (dB): aksesuar - aşınma + sesaltı bonusu; asla negatif olmaz.</summary>
        public static float NetReductionDb(ShotClass c, MuzzleDevice d, AmmoSpeed ammo, int roundsSinceNew)
        {
            var red = DeviceReductionDb(d);
            if (IsSuppressor(d)) red -= WearPenaltyDb(roundsSinceNew);
            red += SubsonicLoadBonusDb(c, d, ammo);
            return red < 0f ? 0f : red;
        }

        public static ShotLevels Levels(ShotClass c, MuzzleDevice d, AmmoSpeed ammo, int roundsSinceNew = 0, float muzzleVelocity = -1f)
        {
            var v = muzzleVelocity > 0f ? muzzleVelocity : DefaultMuzzleVelocity(c);
            if (ammo == AmmoSpeed.Subsonic && v > 330f) v = 320f;
            var crack = IsSupersonic(v);
            var blast = BaseBlastDb(c) - NetReductionDb(c, d, ammo, roundsSinceNew);
            return new ShotLevels(blast, crack ? BaseCrackDb(c) : 0f, MechDb(c), crack);
        }

        /// <summary>
        /// Namlu patlamasının yöne bağlı seviyesi (dB, bore eksenine göre 0=ön, 180=arka).
        /// Çıplak namlu öne ~9 dB fazla verir; fren yanlara +, susturucu yönselliği düzler.
        /// </summary>
        public static float DirectivityDb(MuzzleDevice d, float angleDeg)
        {
            var a = Math.Abs(angleDeg) % 360f;
            if (a > 180f) a = 360f - a;
            var rad = a * (float)Math.PI / 180f;
            var frontBack = d == MuzzleDevice.Brake ? 6f : IsSuppressor(d) ? 4f : 9f;
            var db = -frontBack * (1f - (float)Math.Cos(rad)) * 0.5f;
            if (d == MuzzleDevice.Brake)
            {
                var z = (a - 90f) / 35f;
                db += 6f * (float)Math.Exp(-z * z);
            }
            return db;
        }

        /// <summary>Atıcının kendi kulağında (arka-yan, ~120 derece) fren sonrası ek dB; susturucu atıcıyı da rahatlatır.</summary>
        public static float ShooterEarDb(ShotClass c, MuzzleDevice d, AmmoSpeed ammo, int rounds)
        {
            var lv = Levels(c, d, ammo, rounds);
            return lv.BlastDb + DirectivityDb(d, 120f);
        }

        /// <summary>
        /// Katman karışımı. Algısal ölçek: her -10 dB yarı yüksek (2^(-dB/10)). Alçak frekans (thump) daha az düşer
        /// (azalmanın %60'ı), çatlama sesüstüyse tam kalır, mekanik susturucuyla öne çıkar.
        /// </summary>
        public static ShotLayerMix Mix(ShotClass c, MuzzleDevice d, AmmoSpeed ammo, int rounds = 0, float muzzleVelocity = -1f)
        {
            var red = NetReductionDb(c, d, ammo, rounds);
            var lv = Levels(c, d, ammo, rounds, muzzleVelocity);
            var body = AcousticTransmission.PerceivedGain(red);
            var thump = AcousticTransmission.PerceivedGain(red * 0.6f);
            var crack = lv.HasCrack ? 1f : 0f;
            var supp = IsSuppressor(d);
            var mech = 0.3f + 0.7f * Math.Min(1f, red / 32f);
            var tail = AcousticTransmission.PerceivedGain(red * 0.85f);
            var bodyCut = supp ? (d == MuzzleDevice.IntegralSuppressor ? 0.5f : 0.55f) : 1f;
            var pitch = supp ? 0.8f : d == MuzzleDevice.Brake ? 1.05f : 1f;
            var tailCut = supp ? 0.7f : 1f;
            return new ShotLayerMix(body, crack, thump, mech, tail, bodyCut, pitch, tailCut);
        }

        /// <summary>Dinamik "ne kadar yüksek" özeti: susturulmuş atışın susturucusuza oranı (algısal, 0..1).</summary>
        public static float PerceivedLoudnessRatio(ShotClass c, MuzzleDevice d, AmmoSpeed ammo, int rounds = 0)
        {
            var lv = Levels(c, d, ammo, rounds);
            var ref0 = BaseBlastDb(c);
            var top = Math.Max(lv.BlastDb, lv.HasCrack ? lv.CrackDb : 0f);
            return AcousticTransmission.PerceivedGain(Math.Max(0f, ref0 - top));
        }
    }
}
