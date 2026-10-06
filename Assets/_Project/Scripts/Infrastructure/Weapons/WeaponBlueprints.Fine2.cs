using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// silah-ince-detay-2: AR ailesi dışındaki silahlar için karakteristik ince detay (G3A7, SAR 109T, PMT-76/MG3, tabancalar).
    /// TryFineSpec/FineDetail desenini izler; her öğe WeaponDetailBudget'a bakar, LOD1'de adetler yarıya iner.
    /// </summary>
    internal static partial class WeaponBlueprints
    {
        private static void ExtraFineDetail(Ctx c, WeaponStyle style)
        {
            switch (style)
            {
                case WeaponStyle.G3a7:
                    G3Details(c);
                    break;
                case WeaponStyle.Sar109:
                    Sar109Details(c);
                    break;
                case WeaponStyle.Pmt76:
                case WeaponStyle.Mg3:
                    MachineGunDetails(c, style == WeaponStyle.Mg3);
                    break;
                case WeaponStyle.Sar9:
                case WeaponStyle.Tp9:
                case WeaponStyle.MeteSft:
                    PistolFine(c);
                    break;
            }
        }

        /// <summary>G3: delikli el kundağı (yan oluk sıraları), tambur gez tırtılı, şarjör kabartması, kabza dokusu.</summary>
        private static void G3Details(Ctx c)
        {
            var p = c.P;
            var b = c.B;

            // Tambur gez: silindir çevresinde tırtıl şeritleri (merkez x=0, yarıçap 0.0105, y=0.074, z=-0.07).
            var knurl = WeaponDetailBudget.Count(8, c.Lod);
            if (WeaponDetailBudget.CanAfford(b.VertexCount, knurl * WeaponDetailBudget.FlatBoxVerts, c.Lod))
            {
                for (var i = 0; i < knurl; i++)
                {
                    var a = i * 360f / knurl;
                    var rad = a * Mathf.Deg2Rad;
                    var pos = new Vector3(0f, 0.074f + Mathf.Sin(rad) * 0.0108f, -0.07f + Mathf.Cos(rad) * 0.0108f);
                    b.FlatBox(p.DarkMetal, pos, new Vector3(0.022f, 0.0012f, 0.0016f), Quaternion.Euler(-a, 0f, 0f));
                }
            }

            // Delikli el kundağı: iki yanda iki sıra yuvarlak (kare temsil) delik; LOD1'de seyrek.
            var perRow = WeaponDetailBudget.Count(5, c.Lod);
            var holes = perRow * 2 * 2;
            if (WeaponDetailBudget.CanAfford(b.VertexCount, holes * WeaponDetailBudget.FlatBoxVerts, c.Lod))
            {
                for (var side = -1; side <= 1; side += 2)
                    for (var row = 0; row < 2; row++)
                        for (var i = 0; i < perRow; i++)
                        {
                            var z = 0.235f + (i + 0.5f) * (0.2f / perRow);
                            var y = 0.026f + row * 0.017f;
                            b.FlatBox(p.DarkMetal, new Vector3(side * 0.0276f, y, z), new Vector3(0.0008f, 0.007f, 0.007f), Quaternion.identity);
                        }
            }

            var mag = new FineSpec { MagTop = new Vector3(0f, -0.006f, 0.135f), MagWidth = 0.028f, MagDepth = 0.07f, MagLength = 0.16f, MagStart = 2f, MagCurve = 7f,
                GripTop = new Vector3(0f, -0.006f, 0f), GripRake = 15f, GripLength = 0.1f, GripWidth = 0.031f, GripDepth = 0.043f };
            MagazineEmboss(c, mag);
            GripTexture(c, mag);
        }

        /// <summary>SAR 109T: kundak havalandırma yuvaları, şarjör kabartması, kabza dokusu.</summary>
        private static void Sar109Details(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            var slots = WeaponDetailBudget.Count(6, c.Lod);
            if (WeaponDetailBudget.CanAfford(b.VertexCount, slots * 2 * WeaponDetailBudget.FlatBoxVerts, c.Lod))
            {
                for (var i = 0; i < slots; i++)
                {
                    var z = 0.18f + (i + 0.5f) * (0.12f / slots);
                    b.FlatBox(p.DarkMetal, new Vector3(0.0312f, 0.037f, z), new Vector3(0.0008f, 0.012f, 0.008f), Quaternion.identity);
                    b.FlatBox(p.DarkMetal, new Vector3(-0.0312f, 0.037f, z), new Vector3(0.0008f, 0.012f, 0.008f), Quaternion.identity);
                }
            }

            var s = new FineSpec { MagTop = new Vector3(0f, -0.022f, 0.112f), MagWidth = 0.021f, MagDepth = 0.034f, MagLength = 0.17f, MagStart = 3f, MagCurve = 3f,
                GripTop = new Vector3(0f, -0.012f, 0f), GripRake = 18f, GripLength = 0.095f, GripWidth = 0.029f, GripDepth = 0.04f };
            MagazineEmboss(c, s);
            GripTexture(c, s);
        }

        /// <summary>PMT-76/MG3: namlu değiştirme kolu + mandalı, bipod bağlantı çaprazı, besleme kapağı perçin/mandal.</summary>
        private static void MachineGunDetails(Ctx c, bool mg3)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.045f;

            // Namlu değiştirme kolu: sağ yanda sap + parmak topuzu + kilit mandalı.
            var cost = WeaponDetailBudget.CylinderVerts(6) * 2 + WeaponDetailBudget.FlatBoxVerts * 2;
            if (WeaponDetailBudget.CanAfford(b.VertexCount, cost, c.Lod))
            {
                b.Cylinder(p.Metal, new Vector3(0.012f, bore, 0.5f), new Vector3(0.034f, bore + 0.006f, 0.5f), 0.0035f, 6);
                b.Cylinder(p.Polymer, new Vector3(0.034f, bore + 0.006f, 0.5f), new Vector3(0.04f, bore + 0.008f, 0.5f), 0.0058f, 6);
                b.FlatBox(p.DarkMetal, new Vector3(0.0125f, bore, 0.48f), new Vector3(0.003f, 0.012f, 0.012f), Quaternion.identity);
                b.FlatBox(p.DarkMetal, new Vector3(0.0125f, bore, 0.52f), new Vector3(0.003f, 0.012f, 0.012f), Quaternion.identity);
            }

            // İki ayak: bipod bacaklarını bağlayan çapraz bağ + menteşe pimleri.
            var braceCost = WeaponDetailBudget.CylinderVerts(6) + WeaponDetailBudget.FlatBoxVerts * 2;
            if (WeaponDetailBudget.CanAfford(b.VertexCount, braceCost, c.Lod))
            {
                b.Cylinder(p.Metal, new Vector3(-0.012f, 0.012f - 0.012f, 0.6f), new Vector3(0.012f, 0.012f - 0.012f, 0.6f), 0.0025f, 6);
                b.FlatBox(p.DarkMetal, new Vector3(0.0165f, -0.003f, 0.558f), new Vector3(0.004f, 0.006f, 0.006f), Quaternion.identity);
                b.FlatBox(p.DarkMetal, new Vector3(-0.0165f, -0.003f, 0.558f), new Vector3(0.004f, 0.006f, 0.006f), Quaternion.identity);
            }

            // Besleme kapağı (kayış beslemeli): kapak üstünde perçin sırası + kapak mandalı (kapakla birlikte hareket eder).
            var rivets = WeaponDetailBudget.Count(6, c.Lod);
            if (WeaponDetailBudget.CanAfford(b.VertexCount, (rivets * 2 + 1) * WeaponDetailBudget.FlatBoxVerts, c.Lod))
            {
                b.BeginGroup(WeaponModel.CoverPart, new Vector3(0f, 0.076f, 0.2f));
                for (var i = 0; i < rivets; i++)
                {
                    var z = -0.03f + i * (0.22f / rivets);
                    b.FlatBox(p.DarkMetal, new Vector3(0.02f, 0.0772f, z), new Vector3(0.004f, 0.0012f, 0.004f), Quaternion.identity);
                    b.FlatBox(p.DarkMetal, new Vector3(-0.02f, 0.0772f, z), new Vector3(0.004f, 0.0012f, 0.004f), Quaternion.identity);
                }

                b.FlatBox(p.Metal, new Vector3(0f, 0.0775f, -0.05f), new Vector3(0.014f, 0.003f, 0.01f), Quaternion.identity);
                b.EndGroup();
            }

            if (mg3)
            {
                // MG3: kapak yan kenarında kayış besleme oluğu çizgileri.
                var lines = WeaponDetailBudget.Count(4, c.Lod);
                if (WeaponDetailBudget.CanAfford(b.VertexCount, lines * WeaponDetailBudget.FlatBoxVerts, c.Lod))
                    for (var i = 0; i < lines; i++)
                        b.FlatBox(p.DarkMetal, new Vector3(0.0262f, 0.04f, -0.03f + i * 0.06f), new Vector3(0.0008f, 0.05f, 0.004f), Quaternion.identity);
            }
        }

        /// <summary>Tabanca: kızak yivleri (ön + arka) ve tetik korkuluğu (alt kemer + ön bağlantı).</summary>
        private static void PistolFine(Ctx c)
        {
            var p = c.P;
            var b = c.B;

            var grooves = WeaponDetailBudget.Count(6, c.Lod);
            if (WeaponDetailBudget.CanAfford(b.VertexCount, grooves * 2 * WeaponDetailBudget.FlatBoxVerts, c.Lod))
            {
                b.BeginGroup(WeaponModel.SlidePart, new Vector3(0f, 0.028f, 0f));
                for (var i = 0; i < grooves; i++)
                {
                    var z = 0.095f + i * (0.05f / grooves);
                    b.FlatBox(p.DarkMetal, new Vector3(0.0138f, 0.034f, z), new Vector3(0.0008f, 0.012f, 0.0018f), Quaternion.identity);
                    b.FlatBox(p.DarkMetal, new Vector3(-0.0138f, 0.034f, z), new Vector3(0.0008f, 0.012f, 0.0018f), Quaternion.identity);
                }

                b.EndGroup();
            }

            // Tetik korkuluğu: tetiğin altından geçen kemer + ön dikme.
            if (WeaponDetailBudget.CanAfford(b.VertexCount, WeaponDetailBudget.FlatBoxVerts * 4, c.Lod))
            {
                b.FlatBox(p.Metal, new Vector3(0f, -0.0125f, 0.062f), new Vector3(0.012f, 0.0028f, 0.05f), Quaternion.identity);
                b.FlatBox(p.Metal, new Vector3(0f, -0.007f, 0.089f), new Vector3(0.012f, 0.012f, 0.0028f), Quaternion.identity);
                b.FlatBox(p.DarkMetal, new Vector3(0.0132f, -0.002f, 0.09f), new Vector3(0.0008f, 0.004f, 0.012f), Quaternion.identity);
                b.FlatBox(p.DarkMetal, new Vector3(-0.0132f, -0.002f, 0.09f), new Vector3(0.0008f, 0.004f, 0.012f), Quaternion.identity);
            }
        }
    }
}
