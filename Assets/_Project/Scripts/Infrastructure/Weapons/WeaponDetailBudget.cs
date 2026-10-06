using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Silah mesh detayı için saf köşe bütçesi mantığı (Unity nesnesi gerektirmez). Silah başına tam detay (LOD0) üst sınırı
    /// 12 000 köşe, LOD1 yarısı (6 000). Maliyet tahminleri WeaponMeshBuilder şekillerinin gerçek köşe sayılarıyla uyumludur.
    /// </summary>
    public static class WeaponDetailBudget
    {
        public const int MaxVerticesLod0 = 12000;
        public const int MaxVerticesLod1 = 6000;

        /// <summary>Pahlı Box: 3 eksen x 2 yön x 9 dörtgen x 4 köşe.</summary>
        public const int BoxVerts = 216;
        /// <summary>Pahsız FlatBox: 6 dörtgen x 4 köşe.</summary>
        public const int FlatBoxVerts = 24;

        public static int Limit(int lod) => lod > 0 ? MaxVerticesLod1 : MaxVerticesLod0;

        /// <summary>Mevcut köşe + eklenecek maliyet bu LOD'un bütçesine sığıyor mu.</summary>
        public static bool CanAfford(int currentVertices, int cost, int lod) =>
            cost >= 0 && currentVertices + cost <= Limit(lod);

        /// <summary>Ray dişi sayısı: LOD0 her 1 cm, LOD1 her 2 cm (yarım detay). En az 1.</summary>
        public static int RailTeeth(float length, int lod)
        {
            var step = lod > 0 ? 0.02f : 0.01f;
            return Mathf.Max(1, Mathf.FloorToInt(length / step));
        }

        /// <summary>Tekrarlı detay adedi: LOD1'de yarıya iner (en az 1, baz 0 ise 0).</summary>
        public static int Count(int baseCount, int lod)
        {
            if (baseCount <= 0) return 0;
            return lod > 0 ? Mathf.Max(1, baseCount / 2) : baseCount;
        }

        /// <summary>WeaponMeshBuilder.Cylinder köşe tahmini (küçük yarıçapta yan sayısı en az 20'ye çıkar).</summary>
        public static int CylinderVerts(int sides, bool capA = true, bool capB = true)
        {
            if (sides >= 8) sides = Mathf.Max(sides, 20);
            sides = Mathf.Clamp(sides, 3, 32);
            return sides * 4 + (capA ? sides * 3 : 0) + (capB ? sides * 3 : 0);
        }

        /// <summary>WeaponMeshBuilder.Tube köşe tahmini (yan sayısı 2 ile çarpılıp 16-48 arasına sıkıştırılır).</summary>
        public static int TubeVerts(int sides)
        {
            sides = Mathf.Clamp(sides * 2, 16, 48);
            return sides * 16;
        }

        /// <summary>Ray köşe tahmini: diş başına 2 FlatBox + taban Box.</summary>
        public static int RailVerts(float length, int lod) =>
            BoxVerts + RailTeeth(length, lod) * 2 * FlatBoxVerts;

        /// <summary>Bu stil için ek ince detay var mı (saf mantık; testlerde kullanılır).</summary>
        public static bool HasExtraFineDetail(WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.G3a7:
                case WeaponStyle.Sar109:
                case WeaponStyle.Pmt76:
                case WeaponStyle.Mg3:
                case WeaponStyle.Sar9:
                case WeaponStyle.Tp9:
                case WeaponStyle.MeteSft:
                    return true;
                default:
                    return false;
            }
        }
    }
}
