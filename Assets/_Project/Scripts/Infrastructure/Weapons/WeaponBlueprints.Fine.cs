using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// silah-detay: AR ailesi (MPT-76/76K, MPT-55, SAR 223, KNT-76, SAR 7.62) için ince görsel detay — nişangâh netliği
    /// (siperlikli arpacık + diyoptrili gez), şarjör kabartması, kabza dokusu, flash hider yivleri/taç halkası, ön kayış halkası.
    /// Her öğe köşe bütçesine (WeaponDetailBudget) bakar; LOD1'de adetler yarıya iner. Ray dişleri Rail() içinde.
    /// Uzak/üçüncü-şahıs silahlar WeaponModelFactory üzerinden Build(style, 1) (LOD1) ile üretilir.
    /// </summary>
    internal static partial class WeaponBlueprints
    {
        private struct FineSpec
        {
            public float RailTop;      // ray üst yüzeyi (y)
            public float FrontSightZ;  // arpacık z
            public float RearSightZ;   // gez z
            public Vector3 MagTop; public float MagWidth, MagDepth, MagLength, MagStart, MagCurve;
            public Vector3 GripTop; public float GripRake, GripLength, GripWidth, GripDepth;
            public float HiderY, HiderZ0, HiderZ1, HiderR;
            public float SlingZ, SlingY;
        }

        private static bool TryFineSpec(WeaponStyle style, out FineSpec s)
        {
            s = default;
            switch (style)
            {
                case WeaponStyle.Mpt76:
                    s = new FineSpec { RailTop = 0.075f, FrontSightZ = 0.465f, RearSightZ = -0.045f,
                        MagTop = new Vector3(0f, -0.022f, 0.135f), MagWidth = 0.026f, MagDepth = 0.074f, MagLength = 0.142f, MagStart = 4f, MagCurve = 12f,
                        GripTop = new Vector3(0f, -0.012f, 0f), GripRake = 18f, GripLength = 0.102f, GripWidth = 0.03f, GripDepth = 0.042f,
                        HiderY = 0.035f, HiderZ0 = 0.622f, HiderZ1 = 0.674f, HiderR = 0.0128f, SlingZ = 0.485f, SlingY = 0.037f - 0.036f };
                    return true;
                case WeaponStyle.Mpt76K:
                    s = new FineSpec { RailTop = 0.075f, FrontSightZ = 0.3f, RearSightZ = -0.045f,
                        MagTop = new Vector3(0f, -0.022f, 0.135f), MagWidth = 0.026f, MagDepth = 0.074f, MagLength = 0.13f, MagStart = 4f, MagCurve = 12f,
                        GripTop = new Vector3(0f, -0.012f, 0f), GripRake = 18f, GripLength = 0.102f, GripWidth = 0.03f, GripDepth = 0.042f,
                        HiderY = 0.035f, HiderZ0 = 0.4f, HiderZ1 = 0.455f, HiderR = 0.0135f, SlingZ = 0.33f, SlingY = 0.0f };
                    return true;
                case WeaponStyle.Mpt55:
                case WeaponStyle.Sar223:
                    s = new FineSpec { RailTop = 0.075f, FrontSightZ = 0.42f, RearSightZ = -0.05f,
                        MagTop = new Vector3(0f, -0.022f, 0.125f), MagWidth = 0.023f, MagDepth = 0.064f, MagLength = 0.155f, MagStart = 2f, MagCurve = 22f,
                        GripTop = new Vector3(0f, -0.012f, 0f), GripRake = 18f, GripLength = 0.1f, GripWidth = 0.03f, GripDepth = 0.04f,
                        HiderY = 0.035f, HiderZ0 = 0.5f, HiderZ1 = 0.545f, HiderR = 0.0115f, SlingZ = 0.4f, SlingY = 0.0f };
                    return true;
                case WeaponStyle.Knt76:
                case WeaponStyle.Sar762Mt:
                    s = new FineSpec { RailTop = 0.075f, FrontSightZ = 0.62f, RearSightZ = -0.045f,
                        MagTop = new Vector3(0f, -0.022f, 0.135f), MagWidth = 0.027f, MagDepth = 0.074f, MagLength = 0.095f, MagStart = 2f, MagCurve = 2f,
                        GripTop = new Vector3(0f, -0.012f, 0f), GripRake = 18f, GripLength = 0.102f, GripWidth = 0.03f, GripDepth = 0.042f,
                        HiderY = 0.035f, HiderZ0 = 0.77f, HiderZ1 = 0.795f, HiderR = 0.0148f, SlingZ = 0.6f, SlingY = 0.0f };
                    return true;
                default:
                    return false;
            }
        }

        private static void FineDetail(Ctx c, WeaponStyle style)
        {
            if (!TryFineSpec(style, out var s))
            {
                ExtraFineDetail(c, style); // AR dışı silahlar (WeaponBlueprints.Fine2.cs)
                return;
            }
            // Sıra önemli: nişangâh netliği ve namlu ağzı bütçe kısılırsa bile önce gelir.
            SightPicture(c, s);
            HiderGrooves(c, s);
            MagazineEmboss(c, s);
            GripTexture(c, s);
            SlingLoop(c, s);
        }

        /// <summary>Siperlikli arpacık (kulaklar + trityum uç) ve diyoptrili gez (halka + kulaklar).</summary>
        private static void SightPicture(Ctx c, FineSpec s)
        {
            var p = c.P;
            var b = c.B;
            var cost = WeaponDetailBudget.FlatBoxVerts * 5 + WeaponDetailBudget.TubeVerts(6);
            if (!WeaponDetailBudget.CanAfford(b.VertexCount, cost, c.Lod)) return;

            var y = s.RailTop;
            var fz = s.FrontSightZ;
            b.FlatBox(p.DarkMetal, new Vector3(0f, y + 0.003f, fz), new Vector3(0.016f, 0.006f, 0.012f), Quaternion.identity);       // taban bloğu
            b.FlatBox(p.DarkMetal, new Vector3(0f, y + 0.0105f, fz), new Vector3(0.0022f, 0.009f, 0.004f), Quaternion.identity);     // arpacık direği
            b.FlatBox(p.DarkMetal, new Vector3(0.0045f, y + 0.0095f, fz), new Vector3(0.0014f, 0.011f, 0.006f), Quaternion.identity); // koruma kulağı
            b.FlatBox(p.DarkMetal, new Vector3(-0.0045f, y + 0.0095f, fz), new Vector3(0.0014f, 0.011f, 0.006f), Quaternion.identity);
            b.FlatBox(p.Tritium, new Vector3(0f, y + 0.0152f, fz - 0.0021f), new Vector3(0.0014f, 0.0014f, 0.0005f), Quaternion.identity); // uç noktası

            var rz = s.RearSightZ;
            b.Tube(p.DarkMetal, new Vector3(0f, y + 0.0085f, rz - 0.004f), new Vector3(0f, y + 0.0085f, rz + 0.004f), 0.0052f, 0.0024f, 6); // diyoptri
        }

        /// <summary>Alev gizleyici: taç halkası + radyal yivler (LOD1'de yarısı).</summary>
        private static void HiderGrooves(Ctx c, FineSpec s)
        {
            var p = c.P;
            var b = c.B;
            var prongs = WeaponDetailBudget.Count(6, c.Lod);
            var cost = WeaponDetailBudget.TubeVerts(8) + prongs * WeaponDetailBudget.FlatBoxVerts;
            if (!WeaponDetailBudget.CanAfford(b.VertexCount, cost, c.Lod)) return;

            var len = s.HiderZ1 - s.HiderZ0;
            var r = s.HiderR;
            // Taç halkası: uç yüzünde koyu halka (namlu deliği görünür).
            b.Tube(p.DarkMetal, new Vector3(0f, s.HiderY, s.HiderZ1 - 0.0025f), new Vector3(0f, s.HiderY, s.HiderZ1), r * 1.03f, r * 0.45f, 8);
            for (var i = 0; i < prongs; i++)
            {
                var angle = i * 360f / prongs + 15f;
                var rad = angle * Mathf.Deg2Rad;
                var center = new Vector3(Mathf.Cos(rad) * r * 0.98f, s.HiderY + Mathf.Sin(rad) * r * 0.98f, s.HiderZ1 - len * 0.3f);
                b.FlatBox(p.DarkMetal, center, new Vector3(r * 0.35f, 0.0028f, len * 0.4f), Quaternion.Euler(0f, 0f, angle));
            }
        }

        /// <summary>Şarjör gövdesi ön yüzünde yatay kabartma çizgileri (kavisi izler).</summary>
        private static void MagazineEmboss(Ctx c, FineSpec s)
        {
            var p = c.P;
            var b = c.B;
            var ribs = WeaponDetailBudget.Count(7, c.Lod);
            if (!WeaponDetailBudget.CanAfford(b.VertexCount, ribs * WeaponDetailBudget.FlatBoxVerts, c.Lod)) return;

            b.BeginGroup(WeaponModel.MagazinePart, s.MagTop);
            for (var i = 0; i < ribs; i++)
            {
                var t = (i + 1f) / (ribs + 1f);
                // Şarjör merkez çizgisi: açı start + curve * t, konum açı üzerinden entegre edilir (dört adım yeterli).
                var pos = s.MagTop;
                const int steps = 8;
                for (var k = 0; k < steps; k++)
                {
                    var a = s.MagStart + s.MagCurve * (t * (k + 0.5f) / steps);
                    pos += Quaternion.Euler(-a, 0f, 0f) * Vector3.down * (s.MagLength * t / steps);
                }

                var ang = s.MagStart + s.MagCurve * t;
                var rot = Quaternion.Euler(-ang, 0f, 0f);
                b.FlatBox(p.Polymer, pos + rot * new Vector3(0f, 0f, s.MagDepth * 0.5f + 0.0004f),
                    new Vector3(s.MagWidth * 0.8f, 0.0022f, 0.0012f), rot);
            }

            b.EndGroup();
        }

        /// <summary>Kabza yan yüzlerinde kaydırmaz çizgiler (iki yan x yatay şeritler).</summary>
        private static void GripTexture(Ctx c, FineSpec s)
        {
            var p = c.P;
            var b = c.B;
            var rows = WeaponDetailBudget.Count(8, c.Lod);
            if (!WeaponDetailBudget.CanAfford(b.VertexCount, rows * 2 * WeaponDetailBudget.FlatBoxVerts, c.Lod)) return;

            var rot = Quaternion.Euler(s.GripRake, 0f, 0f);
            var down = rot * Vector3.down;
            for (var i = 0; i < rows; i++)
            {
                var t = 0.12f + 0.76f * i / Mathf.Max(1, rows - 1);
                var pos = s.GripTop + down * (s.GripLength * t);
                for (var side = -1; side <= 1; side += 2)
                    b.FlatBox(p.Rubber, pos + new Vector3(side * (s.GripWidth * 0.5f + 0.0003f), 0f, 0f),
                        new Vector3(0.0012f, 0.0016f, s.GripDepth * 0.6f), rot);
            }
        }

        /// <summary>Ön kayış halkası: el kundağı altında menteşeli halka.</summary>
        private static void SlingLoop(Ctx c, FineSpec s)
        {
            var p = c.P;
            var b = c.B;
            var cost = WeaponDetailBudget.TubeVerts(6) + WeaponDetailBudget.FlatBoxVerts;
            if (c.Lod > 0 || !WeaponDetailBudget.CanAfford(b.VertexCount, cost, c.Lod)) return;

            var y = s.SlingY - 0.012f;
            b.FlatBox(p.Metal, new Vector3(0f, s.SlingY - 0.0045f, s.SlingZ), new Vector3(0.008f, 0.005f, 0.008f), Quaternion.identity);
            b.Tube(p.Metal, new Vector3(-0.0025f, y, s.SlingZ), new Vector3(0.0025f, y, s.SlingZ), 0.007f, 0.0045f, 6);
        }
    }
}
