using UnityEngine;

namespace Project.Infrastructure.Weapons.Skins
{
    public enum SkinPattern { Digital = 0, Striped = 1, Solid = 2 }

    /// <summary>Silah kaplaması (saf veri). Wear 0..1: kenar aşınma şiddeti.</summary>
    public readonly struct WeaponSkin
    {
        public readonly string Id, Name;
        public readonly Color Primary, Secondary;
        public readonly SkinPattern Pattern;
        public readonly float Wear, Metallic, Smoothness;
        public WeaponSkin(string id, string name, Color primary, Color secondary, SkinPattern pattern, float wear, float metallic, float smoothness)
        { Id = id; Name = name; Primary = primary; Secondary = secondary; Pattern = pattern; Wear = wear; Metallic = metallic; Smoothness = smoothness; }
    }

    /// <summary>Kozmetik kaplama kataloğu. Ana renk parlaklığı GunMaterials sınırları içinde kalır (siluet okunur).</summary>
    public static class WeaponSkinCatalog
    {
        public const string DefaultId = "none";

        public static readonly WeaponSkin[] All =
        {
            new WeaponSkin("col_kamuflaj", "Çöl Kamuflaj", new Color(0.60f, 0.50f, 0.36f), new Color(0.42f, 0.34f, 0.22f), SkinPattern.Digital, 0.30f, 0.05f, 0.30f),
            new WeaponSkin("kurt_grisi", "Kurt Grisi", new Color(0.40f, 0.42f, 0.44f), new Color(0.26f, 0.28f, 0.30f), SkinPattern.Solid, 0.45f, 0.40f, 0.45f),
            new WeaponSkin("bordo_bere", "Bordo Bere", new Color(0.30f, 0.30f, 0.31f), new Color(0.60f, 0.10f, 0.14f), SkinPattern.Striped, 0.20f, 0.20f, 0.40f),
            new WeaponSkin("gece_operasyonu", "Gece Operasyonu", new Color(0.19f, 0.21f, 0.24f), new Color(0.24f, 0.27f, 0.31f), SkinPattern.Digital, 0.25f, 0.25f, 0.30f),
            new WeaponSkin("ege_mavisi", "Ege Mavisi", new Color(0.18f, 0.42f, 0.58f), new Color(0.50f, 0.62f, 0.68f), SkinPattern.Striped, 0.15f, 0.30f, 0.55f),
            new WeaponSkin("anadolu_pas", "Anadolu Pas", new Color(0.50f, 0.30f, 0.18f), new Color(0.32f, 0.20f, 0.12f), SkinPattern.Digital, 0.75f, 0.35f, 0.20f),
        };

        public static bool TryGet(string id, out WeaponSkin skin)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == id) { skin = All[i]; return true; }
            skin = default; return false;
        }
    }
}
