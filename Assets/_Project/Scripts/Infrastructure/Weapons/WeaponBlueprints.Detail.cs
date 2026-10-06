using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// silah-model: mevcut tariflerin üzerine ince detay katmanı (pahlı gövde, namlu cihazları, tetik/kabza detayları,
    /// besleme tepsisi, ahşap damarı, tritium nişan noktaları). Yalnızca Build sırasında, mesh toplanmadan önce çalışır;
    /// bütün parçalar küçük kutu/silindirlerdir (silah başına yaklaşık +400-900 üçgen).
    /// </summary>
    internal static partial class WeaponBlueprints
    {
        private static void ApplyDetail(Ctx c, WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.Mpt76:
                    ArDetail(c, -0.07f, 0.2f, 0.032f, -0.31f, 0.128f);
                    PortedCompensator(c, 0.035f, 0.622f, 0.674f, 0.0128f);
                    break;
                case WeaponStyle.Mpt76K:
                    ArDetail(c, -0.07f, 0.2f, 0.032f, -0.25f, 0.118f);
                    PortedCompensator(c, 0.035f, 0.4f, 0.455f, 0.0135f);
                    break;
                case WeaponStyle.Mpt55:
                case WeaponStyle.Sar223:
                    ArDetail(c, -0.066f, 0.185f, 0.031f, -0.275f, 0.115f);
                    PortedCompensator(c, 0.035f, 0.5f, 0.545f, 0.0115f);
                    break;
                case WeaponStyle.Sar109:
                    ArDetail(c, -0.06f, 0.16f, 0.03f, -0.226f, 0.115f);
                    SlottedMuzzle(c, 0.035f, 0.345f, 0.375f, 0.0115f);
                    break;
                case WeaponStyle.Knt76:
                case WeaponStyle.Sar762Mt:
                    ArDetail(c, -0.07f, 0.2f, 0.032f, -0.33f, 0.132f);
                    MuzzleBaffles(c, 0.035f, 0.77f, 0.795f, 0.0148f);
                    break;
                case WeaponStyle.Jng90:
                    MuzzleBaffles(c, 0.04f, 0.8f, 0.865f, 0.0163f);
                    c.B.Box(c.P.DarkMetal, new Vector3(0f, 0.0715f, 0.04f), new Vector3(0.018f, 0.002f, 0.12f)); // üst gövde pah şeridi
                    c.B.Cylinder(c.P.Metal, new Vector3(0.022f, 0.052f, 0.0f), new Vector3(0.029f, 0.052f, 0.0f), 0.0045f, 6); // emniyet
                    break;
                case WeaponStyle.G3a7:
                    SlottedMuzzle(c, 0.04f, 0.69f, 0.745f, 0.0138f);
                    WoodGrain(c);
                    break;
                case WeaponStyle.Pmt76:
                case WeaponStyle.Mg3:
                    BeltFeedTray(c);
                    SlottedMuzzle(c, 0.045f, 0.72f, 0.785f, 0.0155f);
                    break;
                case WeaponStyle.Sar9:
                case WeaponStyle.Tp9:
                case WeaponStyle.MeteSft:
                    PistolDetail(c);
                    break;
                case WeaponStyle.Escort:
                case WeaponStyle.EscortMagnum:
                    // Pompalı: namlu ucunda kaba kontra halkası, gövdede pah.
                    c.B.Cylinder(c.P.DarkMetal, new Vector3(0f, 0.045f, 0.675f), new Vector3(0f, 0.045f, 0.69f), 0.0128f, 8);
                    Bevel(c, c.P.Metal, 0.0155f, 0.064f, 0.0f, 0.15f, 0.006f);
                    Bevel(c, c.P.Metal, -0.0155f, 0.064f, 0.0f, 0.15f, 0.006f);
                    break;
            }

            FineDetail(c, style);
        }

        /// <summary>Z ekseni boyunca 45 derece döndürülmüş ince şerit: üst köşe pahı.</summary>
        private static void Bevel(Ctx c, Material m, float x, float y, float z0, float z1, float size)
        {
            c.B.Box(m, new Vector3(x, y, (z0 + z1) * 0.5f), new Vector3(size, size, z1 - z0), new Vector3(0f, 0f, 45f));
        }

        /// <summary>AR ailesi: pahlı üst/alt gövde, ileri itme düğmesi, toz kapağı menteşesi, şarjör yuvası huni, dipçik tabanı çizgileri.</summary>
        private static void ArDetail(Ctx c, float zBack, float zFront, float width, float buttZ, float buttHeight)
        {
            var p = c.P;
            var b = c.B;
            var half = width * 0.5f + 0.001f;
            Bevel(c, p.Metal, half, 0.065f, zBack, zFront, 0.007f);
            Bevel(c, p.Metal, -half, 0.065f, zBack, zFront, 0.007f);
            Bevel(c, p.Metal, half, 0.03f, zBack + 0.022f, zFront - 0.01f, 0.006f);
            Bevel(c, p.Metal, -half, 0.03f, zBack + 0.022f, zFront - 0.01f, 0.006f);

            // İleri itme (forward assist) + toz kapağı menteşesi + kapak bölme çizgisi.
            b.Cylinder(p.Metal, new Vector3(half, 0.052f, zBack + 0.12f), new Vector3(half + 0.01f, 0.05f, zBack + 0.128f), 0.0052f, 6);
            b.Box(p.DarkMetal, new Vector3(half + 0.0005f, 0.052f, zFront - 0.02f), new Vector3(0.0016f, 0.014f, 0.004f));
            b.Box(p.DarkMetal, new Vector3(0f, 0.0658f, (zBack + zFront) * 0.5f), new Vector3(0.003f, 0.0012f, zFront - zBack - 0.02f));

            // Tetik muhafazası altı pah + emniyet kolu (sol).
            b.Cylinder(p.Metal, new Vector3(-half, 0.006f, 0.0f), new Vector3(-half - 0.009f, 0.006f, 0.0f), 0.0045f, 6);
            b.Box(p.Metal, new Vector3(-half - 0.009f, 0.006f, 0.0f), new Vector3(0.004f, 0.004f, 0.016f), new Vector3(0f, 0f, 30f));

            // Dipçik tabanında kaydırmaz çizgiler + sapan yuvası.
            for (var i = 0; i < 4; i++)
            {
                var y = 0.03f - buttHeight * 0.3f + i * buttHeight * 0.2f;
                b.Box(p.DarkMetal, new Vector3(0f, y, buttZ - 0.0085f), new Vector3(0.036f, 0.0016f, 0.0012f));
            }

            b.Tube(p.Metal, new Vector3(0.018f, 0.03f - buttHeight * 0.4f, buttZ + 0.012f), new Vector3(0.018f, 0.03f - buttHeight * 0.4f, buttZ + 0.02f), 0.007f, 0.0042f, 8);
        }

        /// <summary>Yan portlu kompensatör: halka olukları + yanlarda 3 port.</summary>
        private static void PortedCompensator(Ctx c, float y, float z0, float z1, float r)
        {
            var p = c.P;
            var b = c.B;
            var len = z1 - z0;
            for (var i = 0; i < 3; i++)
            {
                var z = z0 + len * (0.2f + 0.2f * i);
                b.Cylinder(p.DarkMetal, new Vector3(0f, y, z - 0.0015f), new Vector3(0f, y, z + 0.0015f), r * 1.025f, 8);
            }

            for (var i = 0; i < 3; i++)
            {
                var z = z0 + len * (0.52f + 0.14f * i);
                b.Box(p.DarkMetal, new Vector3(r * 0.92f, y, z), new Vector3(0.004f, 0.006f, len * 0.07f));
                b.Box(p.DarkMetal, new Vector3(-r * 0.92f, y, z), new Vector3(0.004f, 0.006f, len * 0.07f));
            }

            b.Box(p.DarkMetal, new Vector3(0f, y + r * 0.92f, z0 + len * 0.7f), new Vector3(0.006f, 0.004f, len * 0.2f));
        }

        /// <summary>Yarıklı alev gizleyici (kuş kafesi): uçta dört yarık.</summary>
        private static void SlottedMuzzle(Ctx c, float y, float z0, float z1, float r)
        {
            var p = c.P;
            var b = c.B;
            var len = z1 - z0;
            for (var i = 0; i < 3; i++)
            {
                var angle = i * 60f;
                b.Box(p.DarkMetal, new Vector3(0f, y, z1 - len * 0.2f), new Vector3(r * 2.04f, 0.0035f, len * 0.3f), new Vector3(0f, 0f, angle));
            }

            b.Cylinder(p.Metal, new Vector3(0f, y, z0 - 0.004f), new Vector3(0f, y, z0 + 0.004f), r * 1.1f, 8);
        }

        /// <summary>Çok bölmeli namlu freni: ayrı baffle diskleri.</summary>
        private static void MuzzleBaffles(Ctx c, float y, float z0, float z1, float r)
        {
            var p = c.P;
            var b = c.B;
            const int baffles = 3;
            for (var i = 0; i < baffles; i++)
            {
                var z = z0 + (z1 - z0) * (i + 0.5f) / baffles;
                b.Cylinder(p.DarkMetal, new Vector3(0f, y, z - 0.0015f), new Vector3(0f, y, z + 0.0015f), r * 1.07f, 8);
            }

            b.Cylinder(p.Metal, new Vector3(0f, y, z0 - 0.006f), new Vector3(0f, y, z0), r * 0.95f, r * 1.1f, 8);
        }

        /// <summary>G3A7 ahşap damarları: dipçik yanlarında ince koyu şeritler + dipçik pahı.</summary>
        private static void WoodGrain(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            for (var side = -1; side <= 1; side += 2)
            {
                for (var k = -2; k <= 2; k++)
                {
                    var y = 0.014f + k * 0.017f;
                    var length = 0.16f + 0.03f * (k % 2 == 0 ? 1 : -1);
                    b.Box(p.WoodDark, new Vector3(side * 0.0208f, y, -0.25f + k * 0.012f), new Vector3(0.0009f, 0.0022f, length), new Vector3(-3f + k * 1.4f, 0f, 0f));
                }
            }

            // Dipçik tabanı vidaları + yanak çıkıntısı.
            b.Cylinder(p.Metal, new Vector3(0f, 0.045f, -0.4125f), new Vector3(0f, 0.045f, -0.4165f), 0.0035f, 6);
            b.Cylinder(p.Metal, new Vector3(0f, -0.055f, -0.4125f), new Vector3(0f, -0.055f, -0.4165f), 0.0035f, 6);
            b.Box(p.Wood, new Vector3(0f, 0.058f, -0.16f), new Vector3(0.036f, 0.01f, 0.08f));
        }

        /// <summary>PMT-76/MG3: soldan gelen fişek şeridi (kayış baklaları), besleme tepsisi ve tırnak.</summary>
        private static void BeltFeedTray(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            // Tepsi plakası + yan kenarlar.
            b.Box(p.Metal, new Vector3(-0.035f, 0.074f, 0.12f), new Vector3(0.03f, 0.004f, 0.075f), new Vector3(0f, 0f, -8f));
            b.Box(p.DarkMetal, new Vector3(-0.05f, 0.0765f, 0.12f), new Vector3(0.004f, 0.01f, 0.075f));
            b.Box(p.Metal, new Vector3(-0.026f, 0.0795f, 0.1f), new Vector3(0.012f, 0.007f, 0.012f)); // besleme tırnağı

            // Fişek şeridi: kutudan tepsiye kavisli yay (fişek + bakla çiftleri).
            var from = new Vector3(-0.055f, 0.03f, 0.128f);
            var to = new Vector3(-0.032f, 0.0755f, 0.128f);
            var ctrl = new Vector3(-0.075f, 0.058f, 0.128f);
            for (var i = 0; i < 6; i++)
            {
                var t = (i + 0.5f) / 6f;
                var pos = Quad(from, ctrl, to, t);
                var dir = (Quad(from, ctrl, to, Mathf.Min(1f, t + 0.08f)) - Quad(from, ctrl, to, Mathf.Max(0f, t - 0.08f))).normalized;
                var rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                // Fişek ekseni +Z; Z yönünde gövde, kayış bakla bandı yukarıda.
                b.Cylinder(p.Brass, pos + new Vector3(0f, 0f, -0.016f), pos + new Vector3(0f, 0f, 0.012f), 0.0045f, 0.0036f, 6);
                b.Box(p.DarkMetal, pos + new Vector3(0f, 0f, 0.0f), new Vector3(0.011f, 0.004f, 0.006f), rot);
            }

            // Kabza tarafında emniyet kolu ve tetik koruması pahı.
            b.Cylinder(p.Metal, new Vector3(0.026f, 0.012f, -0.012f), new Vector3(0.034f, 0.012f, -0.012f), 0.0045f, 6);
            Bevel(c, p.Metal, 0.0255f, 0.0755f, -0.1f, 0.16f, 0.007f);
            Bevel(c, p.Metal, -0.0255f, 0.0755f, -0.1f, 0.16f, 0.007f);
        }

        private static Vector3 Quad(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>Tabanca: kızak pahı, tritium nişan noktaları, tetik muhafazası pahı, çerçeve rayı yuvaları.</summary>
        private static void PistolDetail(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            // Kızakla birlikte hareket eden parçalar.
            b.BeginGroup(WeaponModel.SlidePart, new Vector3(0f, 0.028f, 0f));
            Bevel(c, p.DarkMetal, 0.0125f, 0.0425f, -0.022f, 0.162f, 0.004f);
            Bevel(c, p.DarkMetal, -0.0125f, 0.0425f, -0.022f, 0.162f, 0.004f);
            for (var i = 0; i < 4; i++)
                b.Box(p.DarkMetal, new Vector3(0f, 0.0465f, 0.044f + i * 0.0065f), new Vector3(0.012f, 0.0012f, 0.0028f)); // üst kızak oluğu
            b.Box(p.Tritium, new Vector3(0.0058f, 0.0498f, -0.0125f), new Vector3(0.0018f, 0.0018f, 0.0006f));
            b.Box(p.Tritium, new Vector3(-0.0058f, 0.0498f, -0.0125f), new Vector3(0.0018f, 0.0018f, 0.0006f));
            b.Box(p.Tritium, new Vector3(0f, 0.0498f, 0.1495f), new Vector3(0.0018f, 0.0018f, 0.0006f));
            b.EndGroup();

            // Çerçeve rayı yuvaları + kabza taban plakası vidası.
            for (var i = 0; i < 3; i++)
                b.Box(p.DarkMetal, new Vector3(0f, -0.0036f, 0.088f + i * 0.0085f), new Vector3(0.0235f, 0.0016f, 0.0035f));
            b.Cylinder(p.Metal, new Vector3(0.0145f, 0.004f, 0.033f), new Vector3(0.0155f, 0.004f, 0.033f), 0.0032f, 6); // takım pimi
        }
    }
}
