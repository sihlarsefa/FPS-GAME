using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Application.Catalogs
{
    /// <summary>Kalibre sınıfı: ses klasörü adı ve yedek (fallback) sınıfı belirler.</summary>
    public enum WeaponCaliber
    {
        Pistol9 = 0,
        Smg9,
        Rifle556,
        Rifle762,
        Dmr762,
        Sniper762,
        Lmg762,
        Shotgun12
    }

    /// <summary>Düşen kovan türü.</summary>
    public enum ShellDropType
    {
        None = 0,
        Brass9,
        Brass556,
        Brass762,
        Hull12
    }

    /// <summary>
    /// Silah başına ses profili (saf veri): kalibre sınıfı, mekanik/çekirdek/kuyruk set adları, alçak frekans
    /// vuruşu, bastırılmış varyant, atış hızına duyarlı kuyruk kesimi ve kovan türü.
    /// Set adları <c>Resources/Audio/Weapons/</c> altındaki klasör/dosya öneklerine karşılık gelir.
    /// </summary>
    public sealed class WeaponSoundProfile
    {
        public string WeaponId { get; }
        public WeaponCaliber Caliber { get; }
        /// <summary>Mekanik set (sürgü/kapak/tetik sesi), ör. "mech_rifle_steel".</summary>
        public string MechSet { get; }
        /// <summary>Çekirdek patlama seti, ör. "core_762".</summary>
        public string CoreSet { get; }
        /// <summary>Çevresel kuyruk seti öneki, ör. "tail_rifle" (+ _outdoor/_indoor/_valley).</summary>
        public string TailSet { get; }
        /// <summary>Bastırılmış varyant seti, ör. "supp_rifle".</summary>
        public string SuppressedSet { get; }
        /// <summary>Alçak frekans vuruş merkezi (Hz).</summary>
        public float ThumpHz { get; }
        /// <summary>Alçak frekans vuruş şiddeti 0..1.</summary>
        public float ThumpGain { get; }
        /// <summary>Kuyruğun en uzun süresi (sn) — yavaş atışta tam uzunluk.</summary>
        public float TailMaxSeconds { get; }
        public ShellDropType ShellDrop { get; }
        /// <summary>Kovan atışta mı düşer (sürgülülerde false: sürgü çekilince düşer).</summary>
        public bool EjectsOnFire { get; }
        /// <summary>Bastırıcı takılabilir mi (ses seti var).</summary>
        public bool CanSuppress { get; }

        public WeaponSoundProfile(string weaponId, WeaponCaliber caliber, string mechSet, string coreSet, string tailSet,
            string suppressedSet, float thumpHz, float thumpGain, float tailMaxSeconds, ShellDropType shellDrop,
            bool ejectsOnFire = true, bool canSuppress = true)
        {
            WeaponId = weaponId;
            Caliber = caliber;
            MechSet = mechSet;
            CoreSet = coreSet;
            TailSet = tailSet;
            SuppressedSet = suppressedSet;
            ThumpHz = thumpHz;
            ThumpGain = thumpGain;
            TailMaxSeconds = tailMaxSeconds;
            ShellDrop = shellDrop;
            EjectsOnFire = ejectsOnFire;
            CanSuppress = canSuppress;
        }

        /// <summary>
        /// Atış hızına duyarlı kuyruk süresi: kuyruk bir sonraki atıştan önce kesilir (üst üste yığılmaz).
        /// Yavaş silahta <see cref="TailMaxSeconds"/>, hızlıda aralığın ~1.6 katı (alt sınır 0.12 sn).
        /// </summary>
        public float TailCutSeconds(float fireIntervalSeconds)
        {
            if (!(fireIntervalSeconds > 0f))
                return TailMaxSeconds;
            var cut = fireIntervalSeconds * 1.6f;
            if (cut < 0.12f) cut = 0.12f;
            return cut > TailMaxSeconds ? TailMaxSeconds : cut;
        }

        /// <summary>Kalibre sınıfı klasör adı (yedek klasör ve prosedürel set için).</summary>
        public string CaliberFolder => WeaponSoundProfiles.CaliberFolder(Caliber);
    }

    /// <summary>Tüm silahların ses profilleri; katalogdaki her silah için açık kayıt vardır.</summary>
    public static class WeaponSoundProfiles
    {
        private static readonly Dictionary<string, WeaponSoundProfile> ById = Build();

        public static IReadOnlyCollection<WeaponSoundProfile> All => ById.Values;

        public static string CaliberFolder(WeaponCaliber c)
        {
            switch (c)
            {
                case WeaponCaliber.Pistol9: return "pistol9";
                case WeaponCaliber.Smg9: return "smg9";
                case WeaponCaliber.Rifle556: return "rifle556";
                case WeaponCaliber.Rifle762: return "rifle762";
                case WeaponCaliber.Dmr762: return "dmr762";
                case WeaponCaliber.Sniper762: return "sniper762";
                case WeaponCaliber.Lmg762: return "lmg762";
                default: return "shotgun12";
            }
        }

        public static bool TryGet(string weaponId, out WeaponSoundProfile profile)
        {
            profile = null;
            return !string.IsNullOrEmpty(weaponId) && ById.TryGetValue(weaponId, out profile);
        }

        /// <summary>Profil; yoksa kategoriye göre türetilmiş varsayılan (asla null dönmez).</summary>
        public static WeaponSoundProfile Get(string weaponId, WeaponCategory fallbackCategory = WeaponCategory.AssaultRifle)
        {
            if (TryGet(weaponId, out var p))
                return p;
            return DefaultFor(weaponId, fallbackCategory);
        }

        public static WeaponSoundProfile DefaultFor(string weaponId, WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Pistol9, "mech_pistol", "core_9", "tail_pistol", "supp_pistol", 110f, 0.25f, 0.9f, ShellDropType.Brass9);
                case WeaponCategory.Smg:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Smg9, "mech_smg", "core_9", "tail_smg", "supp_smg", 120f, 0.3f, 1.0f, ShellDropType.Brass9);
                case WeaponCategory.Sniper:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Sniper762, "mech_bolt", "core_762", "tail_sniper", "supp_sniper", 55f, 0.9f, 2.6f, ShellDropType.Brass762, false);
                case WeaponCategory.Dmr:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Dmr762, "mech_dmr", "core_762", "tail_dmr", "supp_dmr", 65f, 0.75f, 2.0f, ShellDropType.Brass762);
                case WeaponCategory.Lmg:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Lmg762, "mech_lmg", "core_762", "tail_lmg", "supp_lmg", 60f, 0.85f, 1.8f, ShellDropType.Brass762);
                case WeaponCategory.Shotgun:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Shotgun12, "mech_shotgun", "core_12", "tail_shotgun", "supp_shotgun", 70f, 1f, 1.6f, ShellDropType.Hull12, true, false);
                default:
                    return new WeaponSoundProfile(weaponId, WeaponCaliber.Rifle556, "mech_rifle", "core_556", "tail_rifle", "supp_rifle", 80f, 0.5f, 1.5f, ShellDropType.Brass556);
            }
        }

        private static Dictionary<string, WeaponSoundProfile> Build()
        {
            var d = new Dictionary<string, WeaponSoundProfile>(StringComparer.Ordinal);
            void Add(WeaponSoundProfile p) => d[p.WeaponId] = p;

            // Tabancalar
            Add(new WeaponSoundProfile(WeaponIds.Sar9, WeaponCaliber.Pistol9, "mech_pistol_polymer", "core_9", "tail_pistol", "supp_pistol", 115f, 0.25f, 0.9f, ShellDropType.Brass9));
            Add(new WeaponSoundProfile(WeaponIds.Tp9, WeaponCaliber.Pistol9, "mech_pistol_polymer", "core_9", "tail_pistol", "supp_pistol", 110f, 0.25f, 0.9f, ShellDropType.Brass9));
            Add(new WeaponSoundProfile(WeaponIds.Mete, WeaponCaliber.Pistol9, "mech_pistol_steel", "core_9", "tail_pistol", "supp_pistol", 105f, 0.28f, 0.95f, ShellDropType.Brass9));
            // SMG
            Add(new WeaponSoundProfile(WeaponIds.Sar109, WeaponCaliber.Smg9, "mech_smg", "core_9", "tail_smg", "supp_smg", 125f, 0.3f, 1.0f, ShellDropType.Brass9));
            // 5.56 tüfekler
            Add(new WeaponSoundProfile(WeaponIds.Mpt55, WeaponCaliber.Rifle556, "mech_rifle_steel", "core_556", "tail_rifle", "supp_rifle", 85f, 0.5f, 1.5f, ShellDropType.Brass556));
            Add(new WeaponSoundProfile(WeaponIds.Sar223, WeaponCaliber.Rifle556, "mech_rifle_polymer", "core_556", "tail_rifle", "supp_rifle", 88f, 0.48f, 1.5f, ShellDropType.Brass556));
            // 7.62 tüfekler
            Add(new WeaponSoundProfile(WeaponIds.Mpt76, WeaponCaliber.Rifle762, "mech_rifle_steel", "core_762", "tail_battle", "supp_rifle", 70f, 0.7f, 1.8f, ShellDropType.Brass762));
            Add(new WeaponSoundProfile(WeaponIds.Mpt76K, WeaponCaliber.Rifle762, "mech_rifle_steel", "core_762", "tail_battle", "supp_rifle", 78f, 0.72f, 1.6f, ShellDropType.Brass762));
            Add(new WeaponSoundProfile(WeaponIds.G3, WeaponCaliber.Rifle762, "mech_g3_roller", "core_762", "tail_battle", "supp_rifle", 66f, 0.8f, 1.9f, ShellDropType.Brass762));
            // Nişancı
            Add(new WeaponSoundProfile(WeaponIds.Knt76, WeaponCaliber.Dmr762, "mech_dmr", "core_762", "tail_dmr", "supp_dmr", 62f, 0.78f, 2.1f, ShellDropType.Brass762));
            Add(new WeaponSoundProfile(WeaponIds.Sar762Mt, WeaponCaliber.Dmr762, "mech_dmr", "core_762", "tail_dmr", "supp_dmr", 64f, 0.76f, 2.0f, ShellDropType.Brass762));
            Add(new WeaponSoundProfile(WeaponIds.Jng90, WeaponCaliber.Sniper762, "mech_bolt", "core_762", "tail_sniper", "supp_sniper", 52f, 0.95f, 2.8f, ShellDropType.Brass762, false));
            // Makineli
            Add(new WeaponSoundProfile(WeaponIds.Pmt76, WeaponCaliber.Lmg762, "mech_lmg", "core_762", "tail_lmg", "supp_lmg", 58f, 0.85f, 1.8f, ShellDropType.Brass762));
            Add(new WeaponSoundProfile(WeaponIds.Mg3, WeaponCaliber.Lmg762, "mech_mg3_belt", "core_762", "tail_lmg", "supp_lmg", 62f, 0.9f, 1.4f, ShellDropType.Brass762));
            // Pompalılar
            Add(new WeaponSoundProfile(WeaponIds.Escort, WeaponCaliber.Shotgun12, "mech_shotgun_pump", "core_12", "tail_shotgun", "supp_shotgun", 68f, 1f, 1.7f, ShellDropType.Hull12, false, false));
            Add(new WeaponSoundProfile(WeaponIds.EscortMagnum, WeaponCaliber.Shotgun12, "mech_shotgun_semi", "core_12", "tail_shotgun", "supp_shotgun", 64f, 1f, 1.7f, ShellDropType.Hull12, true, false));
            return d;
        }
    }
}
