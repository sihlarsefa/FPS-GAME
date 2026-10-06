using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Silah modeli tipi (görsel). Her Türk silahının kendine özgü bir modeli vardır.</summary>
    public enum WeaponStyle
    {
        /// <summary>Model yok (çıplak el / yakın dövüş).</summary>
        None = 0,
        Sar9,
        Tp9,
        Sar109,
        Mpt55,
        Mpt76,
        G3a7,
        Knt76,
        Jng90,
        Pmt76,
        Escort,
        Sar223,
        Mpt76K,
        MeteSft,
        Sar762Mt,
        Mg3,
        EscortMagnum
    }

    /// <summary>Silah tanımından model tipini çözer (bilinmeyen kimlikler kategoriye göre en yakın modele düşer).</summary>
    public static class WeaponStyles
    {
        public static WeaponStyle Resolve(WeaponDefinitionData weapon)
        {
            return weapon == null ? WeaponStyle.None : Resolve(weapon.WeaponId, weapon.Category);
        }

        public static WeaponStyle Resolve(string weaponId, WeaponCategory category)
        {
            switch (weaponId)
            {
                case WeaponIds.Sar9: return WeaponStyle.Sar9;
                case WeaponIds.Tp9: return WeaponStyle.Tp9;
                case WeaponIds.Sar109: return WeaponStyle.Sar109;
                case WeaponIds.Mpt55: return WeaponStyle.Mpt55;
                case WeaponIds.Mpt76: return WeaponStyle.Mpt76;
                case WeaponIds.G3: return WeaponStyle.G3a7;
                case WeaponIds.Knt76: return WeaponStyle.Knt76;
                case WeaponIds.Jng90: return WeaponStyle.Jng90;
                case WeaponIds.Pmt76: return WeaponStyle.Pmt76;
                case WeaponIds.Escort: return WeaponStyle.Escort;
                case WeaponIds.Sar223: return WeaponStyle.Sar223;
                case WeaponIds.Mpt76K: return WeaponStyle.Mpt76K;
                case WeaponIds.Mete: return WeaponStyle.MeteSft;
                case WeaponIds.Sar762Mt: return WeaponStyle.Sar762Mt;
                case WeaponIds.Mg3: return WeaponStyle.Mg3;
                case WeaponIds.EscortMagnum: return WeaponStyle.EscortMagnum;
            }

            switch (category)
            {
                case WeaponCategory.Pistol: return WeaponStyle.Sar9;
                case WeaponCategory.Smg: return WeaponStyle.Sar109;
                case WeaponCategory.AssaultRifle: return WeaponStyle.Mpt55;
                case WeaponCategory.Dmr: return WeaponStyle.Knt76;
                case WeaponCategory.Sniper: return WeaponStyle.Jng90;
                case WeaponCategory.Lmg: return WeaponStyle.Pmt76;
                case WeaponCategory.Shotgun: return WeaponStyle.Escort;
                default: return WeaponStyle.None;
            }
        }

        public static bool IsPistol(WeaponStyle style) => style == WeaponStyle.Sar9 || style == WeaponStyle.Tp9 || style == WeaponStyle.MeteSft;

        public static bool IsBoltAction(WeaponStyle style) => style == WeaponStyle.Jng90;

        public static bool IsPumpAction(WeaponStyle style) => style == WeaponStyle.Escort;

        public static bool IsBeltFed(WeaponStyle style) => style == WeaponStyle.Pmt76 || style == WeaponStyle.Mg3;

        public static bool IsHeavy(WeaponStyle style) =>
            style == WeaponStyle.Pmt76 || style == WeaponStyle.Mg3 || style == WeaponStyle.Jng90 ||
            style == WeaponStyle.Knt76 || style == WeaponStyle.Sar762Mt;
    }
}
