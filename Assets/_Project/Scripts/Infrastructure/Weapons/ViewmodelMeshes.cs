using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Görünüm modelinde ellerde tutulan eşya türü.</summary>
    public enum ViewmodelProp
    {
        None = 0,
        FragGrenade,
        SmokeGrenade,
        Bandage,
        FirstAid,
        EnergyDrink,
        Painkiller,
        RifleShell,
        ShotgunShell
    }

    /// <summary>
    /// FPP kollar (kamuflaj kol, eldiven/ten), eller (kavrama ve yumruk), eldeki eşyalar, namlu alevi ve kovan mesh'leri.
    /// Bir kez üretilir ve tüm görünüm modelleri arasında paylaşılır (mesh'ler okunabilir kalır).
    /// El uzayı: orijin bilek, +Z parmak yönü, +Y elin sırtı, -Y avuç; sağ elde başparmak -X tarafında (sol el aynalanır).
    /// Kol uzayı: orijin omuz/dirsek, +Z uzanma yönü.
    /// </summary>
    internal static class ViewmodelMeshes
    {
        public const float UpperArmLength = 0.29f;
        public const float ForearmLength = 0.27f;

        public static BuiltMeshPart UpperArm { get; private set; }
        public static BuiltMeshPart Forearm { get; private set; }
        public static BuiltMeshPart HandGripRight { get; private set; }
        public static BuiltMeshPart HandGripLeft { get; private set; }
        /// <summary>Sağ el, işaret parmağı tetikte (ateş anı ~80 ms).</summary>
        public static BuiltMeshPart HandFireRight { get; private set; }
        public static BuiltMeshPart FistRight { get; private set; }
        public static BuiltMeshPart FistLeft { get; private set; }
        public static BuiltMeshPart Flash { get; private set; }

        private static readonly Dictionary<ViewmodelProp, BuiltMeshPart> Props = new Dictionary<ViewmodelProp, BuiltMeshPart>();
        private static bool _built;

        /// <summary>Mesh'leri (gerekirse yeniden) üretir. Başarısızsa false.</summary>
        public static bool Ensure()
        {
            if (_built && IsAlive())
                return true;

            try
            {
                Build();
                _built = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Silah] Görünüm modeli kol mesh'leri üretilemedi: " + e.Message);
                _built = false;
            }

            return _built;
        }

        public static BuiltMeshPart GetProp(ViewmodelProp prop)
        {
            if (!Ensure())
                return null;

            return Props.TryGetValue(prop, out var part) ? part : null;
        }

        private static bool IsAlive()
        {
            return Valid(UpperArm) && Valid(Forearm) && Valid(HandGripRight) && Valid(HandGripLeft) && Valid(HandFireRight) && Valid(FistRight)
                   && Valid(FistLeft) && Valid(Flash);
        }

        private static bool Valid(BuiltMeshPart part)
        {
            if (part == null || !part.IsValid)
                return false;

            for (var i = 0; i < part.Materials.Length; i++)
            {
                if (part.Materials[i] == null)
                    return false;
            }

            return true;
        }

        private static Material Safe(Material m) => m != null ? m : MaterialLibrary.Get(MaterialId.Gray);

        private static void Build()
        {
            var sleeve = Safe(MaterialLibrary.Get(MaterialId.CamoWoodland));
            var skin = Safe(MaterialLibrary.Get(MaterialId.Skin));
            var glove = Safe(MaterialLibrary.Lit(new Color(0.16f, 0.15f, 0.105f), 0.12f));
            var sleeveDark = Safe(MaterialLibrary.Lit(new Color(0.12f, 0.14f, 0.09f), 0.15f));
            var gloveDark = Safe(MaterialLibrary.Lit(new Color(0.09f, 0.085f, 0.06f), 0.1f));
            var metal = Safe(MaterialLibrary.Get(MaterialId.GunMetal));
            var olive = Safe(MaterialLibrary.Lit(new Color(0.25f, 0.28f, 0.17f), 0.25f));
            var smokeBody = Safe(MaterialLibrary.Lit(new Color(0.42f, 0.45f, 0.4f), 0.3f, 0.2f));
            var white = Safe(MaterialLibrary.Lit(new Color(0.9f, 0.9f, 0.86f), 0.15f));
            var red = Safe(MaterialLibrary.Lit(new Color(0.78f, 0.06f, 0.07f), 0.35f));
            var brass = Safe(MaterialLibrary.Lit(new Color(0.75f, 0.58f, 0.26f), 0.65f, 0.85f));
            var orange = Safe(MaterialLibrary.Lit(new Color(0.8f, 0.45f, 0.12f), 0.6f));
            var flash = Safe(MaterialLibrary.Get(MaterialId.MuzzleFlash));

            var b = new WeaponMeshBuilder();

            // --- Üst kol (omuz → dirsek).
            b.BeginGroup("UpperArm", Vector3.zero);
            b.Ellipsoid(sleeve, Vector3.zero, new Vector3(0.062f, 0.062f, 0.062f), 3, 8);
            b.Cylinder(sleeve, new Vector3(0f, 0f, -0.02f), new Vector3(0f, 0f, UpperArmLength + 0.02f), 0.057f, 0.048f, 8);
            b.Box(sleeve, new Vector3(0.045f, 0.01f, UpperArmLength * 0.45f), new Vector3(0.022f, 0.06f, 0.09f));
            // Omuz yaması (bayrak) + kol dikişi + biceps bandı.
            b.Box(red, new Vector3(0.0f, 0.054f, UpperArmLength * 0.35f), new Vector3(0.036f, 0.006f, 0.05f), new Vector3(0f, 0f, 0f));
            b.Box(white, new Vector3(0.0f, 0.0575f, UpperArmLength * 0.35f), new Vector3(0.012f, 0.002f, 0.012f));
            b.Cylinder(sleeveDark, new Vector3(0f, 0f, UpperArmLength * 0.72f), new Vector3(0f, 0f, UpperArmLength * 0.78f), 0.0535f, 0.0525f, 8);

            // --- Ön kol (dirsek → bilek): kol yeni, ucunda katlanmış kol ağzı, bilekte ten.
            b.BeginGroup("Forearm", Vector3.zero);
            b.Ellipsoid(sleeve, Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), 3, 8);
            b.Cylinder(sleeve, new Vector3(0f, 0f, -0.03f), new Vector3(0f, 0f, ForearmLength - 0.06f), 0.048f, 0.04f, 8);
            b.Cylinder(sleeve, new Vector3(0f, 0f, ForearmLength - 0.085f), new Vector3(0f, 0f, ForearmLength - 0.045f), 0.0435f, 0.042f, 8);
            b.Box(sleeve, new Vector3(0f, 0.04f, ForearmLength - 0.065f), new Vector3(0.03f, 0.008f, 0.03f));
            // Dirsek koruması + kol ağzı lastiği + cep kapağı.
            b.Ellipsoid(sleeveDark, new Vector3(0f, 0.012f, 0.004f), new Vector3(0.045f, 0.038f, 0.034f), 2, 8);
            b.Cylinder(sleeveDark, new Vector3(0f, 0f, ForearmLength - 0.09f), new Vector3(0f, 0f, ForearmLength - 0.082f), 0.0443f, 0.0441f, 8);
            b.Box(sleeveDark, new Vector3(0.036f, 0.016f, ForearmLength * 0.45f), new Vector3(0.006f, 0.026f, 0.05f));
            b.Cylinder(skin, new Vector3(0f, 0f, ForearmLength - 0.07f), new Vector3(0f, 0f, ForearmLength - 0.015f), 0.031f, 0.0285f, 8);

            // --- Eller.
            b.BeginGroup("HandGripR", Vector3.zero);
            Hand(b, glove, gloveDark, skin, false, IndexRest);
            b.BeginGroup("HandFireR", Vector3.zero);
            Hand(b, glove, gloveDark, skin, false, IndexPress);
            b.BeginGroup("FistR", Vector3.zero);
            Hand(b, glove, gloveDark, skin, true, IndexWrap);
            b.MirrorX = true;
            b.BeginGroup("HandGripL", Vector3.zero);
            Hand(b, glove, gloveDark, skin, false, IndexWrap);
            b.BeginGroup("FistL", Vector3.zero);
            Hand(b, glove, gloveDark, skin, true, IndexWrap);
            b.MirrorX = false;

            // --- Eşyalar (orijin: tutma noktası).
            b.BeginGroup("FragGrenade", Vector3.zero);
            b.Ellipsoid(olive, Vector3.zero, new Vector3(0.029f, 0.035f, 0.029f), 4, 8);
            b.Cylinder(metal, new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.05f, 0f), 0.011f, 8);
            b.Box(metal, new Vector3(0.012f, 0.028f, 0f), new Vector3(0.008f, 0.05f, 0.012f), new Vector3(0f, 0f, -12f));
            b.Tube(metal, new Vector3(-0.022f, 0.048f, 0f), new Vector3(-0.022f, 0.048f, 0.003f), 0.011f, 0.008f, 8);

            b.BeginGroup("SmokeGrenade", Vector3.zero);
            b.Cylinder(smokeBody, new Vector3(0f, -0.055f, 0f), new Vector3(0f, 0.045f, 0f), 0.029f, 10);
            b.Cylinder(white, new Vector3(0f, -0.012f, 0f), new Vector3(0f, 0.012f, 0f), 0.0295f, 10);
            b.Cylinder(metal, new Vector3(0f, 0.045f, 0f), new Vector3(0f, 0.06f, 0f), 0.012f, 8);
            b.Box(metal, new Vector3(0.014f, 0.03f, 0f), new Vector3(0.008f, 0.05f, 0.012f), new Vector3(0f, 0f, -10f));

            b.BeginGroup("Bandage", Vector3.zero);
            b.Cylinder(white, new Vector3(-0.035f, 0f, 0f), new Vector3(0.035f, 0f, 0f), 0.028f, 10);
            b.Cylinder(skin, new Vector3(-0.036f, 0f, 0f), new Vector3(0.036f, 0f, 0f), 0.009f, 8);
            b.Box(white, new Vector3(0f, -0.04f, 0.03f), new Vector3(0.068f, 0.002f, 0.07f), new Vector3(-35f, 0f, 0f));

            b.BeginGroup("FirstAid", Vector3.zero);
            b.Box(white, Vector3.zero, new Vector3(0.11f, 0.055f, 0.08f));
            b.Box(white, new Vector3(0f, 0.032f, 0f), new Vector3(0.04f, 0.01f, 0.012f));
            Crescent(b, red, new Vector3(0f, 0.0285f, 0f), 0.018f);

            b.BeginGroup("EnergyDrink", Vector3.zero);
            b.Cylinder(red, new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.055f, 0f), 0.026f, 10);
            b.Cylinder(white, new Vector3(0f, -0.01f, 0f), new Vector3(0f, 0.012f, 0f), 0.0263f, 10);
            b.Cylinder(metal, new Vector3(0f, 0.055f, 0f), new Vector3(0f, 0.065f, 0f), 0.026f, 0.021f, 10);

            b.BeginGroup("Painkiller", Vector3.zero);
            b.Cylinder(orange, new Vector3(0f, -0.035f, 0f), new Vector3(0f, 0.03f, 0f), 0.019f, 10);
            b.Cylinder(white, new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.046f, 0f), 0.0205f, 10);
            b.Box(white, new Vector3(0f, -0.003f, 0.0185f), new Vector3(0.026f, 0.03f, 0.002f));

            // Kovanlar: +Z boyunca, orijin merkez.
            b.BeginGroup("RifleShell", Vector3.zero);
            b.Cylinder(brass, new Vector3(0f, 0f, -0.024f), new Vector3(0f, 0f, 0.016f), 0.0058f, 6);
            b.Cylinder(brass, new Vector3(0f, 0f, 0.016f), new Vector3(0f, 0f, 0.026f), 0.0058f, 0.0042f, 6);

            b.BeginGroup("ShotgunShell", Vector3.zero);
            b.Cylinder(red, new Vector3(0f, 0f, -0.022f), new Vector3(0f, 0f, 0.035f), 0.0105f, 8);
            b.Cylinder(brass, new Vector3(0f, 0f, -0.035f), new Vector3(0f, 0f, -0.022f), 0.0112f, 8);

            // Namlu alevi: +Z boyunca çapraz düzlemler + önden bakan yıldız.
            b.BeginGroup("Flash", Vector3.zero);
            for (var i = 0; i < 3; i++)
            {
                var angle = i * 60f * Mathf.Deg2Rad;
                var u = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.045f;
                b.DoubleSidedQuad(flash, -u, new Vector3(-u.x, -u.y, 0.17f), new Vector3(u.x, u.y, 0.17f), u);
            }

            b.DoubleSidedQuad(flash, new Vector3(-0.06f, -0.06f, 0.012f), new Vector3(-0.06f, 0.06f, 0.012f),
                new Vector3(0.06f, 0.06f, 0.012f), new Vector3(0.06f, -0.06f, 0.012f));
            b.EndGroup();

            var parts = b.Build("Viewmodel", true);
            Props.Clear();
            UpperArm = Forearm = HandGripRight = HandGripLeft = HandFireRight = FistRight = FistLeft = Flash = null;
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                switch (part.Name)
                {
                    case "UpperArm": UpperArm = part; break;
                    case "Forearm": Forearm = part; break;
                    case "HandGripR": HandGripRight = part; break;
                    case "HandGripL": HandGripLeft = part; break;
                    case "HandFireR": HandFireRight = part; break;
                    case "FistR": FistRight = part; break;
                    case "FistL": FistLeft = part; break;
                    case "Flash": Flash = part; break;
                    case "FragGrenade": Props[ViewmodelProp.FragGrenade] = part; break;
                    case "SmokeGrenade": Props[ViewmodelProp.SmokeGrenade] = part; break;
                    case "Bandage": Props[ViewmodelProp.Bandage] = part; break;
                    case "FirstAid": Props[ViewmodelProp.FirstAid] = part; break;
                    case "EnergyDrink": Props[ViewmodelProp.EnergyDrink] = part; break;
                    case "Painkiller": Props[ViewmodelProp.Painkiller] = part; break;
                    case "RifleShell": Props[ViewmodelProp.RifleShell] = part; break;
                    case "ShotgunShell": Props[ViewmodelProp.ShotgunShell] = part; break;
                }
            }

            if (!IsAlive())
                throw new InvalidOperationException("Görünüm modeli mesh'leri eksik.");
        }

        /// <summary>
        /// Eldivenli (parmak uçları açık) sağ el; kavrama: parmaklar avuç tarafına (-Y) kıvrılır; yumruk: parmaklar avuca kapanır.
        /// </summary>
        private const int IndexWrap = 0;
        private const int IndexRest = 1;
        private const int IndexPress = 2;

        private static void Hand(WeaponMeshBuilder b, Material glove, Material gloveDark, Material skin, bool fist, int indexMode)
        {
            // Bilek manşeti + avuç.
            b.Cylinder(gloveDark, new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.012f), 0.035f, 0.032f, 8);
            b.Taper(glove, new Vector3(0f, 0f, 0.002f), new Vector2(0.064f, 0.03f), new Vector3(0f, -0.002f, 0.086f), new Vector2(0.083f, 0.026f));
            b.Box(gloveDark, new Vector3(0f, 0.0155f, 0.068f), new Vector3(0.072f, 0.006f, 0.032f));
            b.Ellipsoid(glove, new Vector3(-0.022f, -0.01f, 0.026f), new Vector3(0.017f, 0.012f, 0.025f), 3, 6);

            // Boğum koruması (sert kauçuk), bilek kayışı + cırt, avuç dikişi.
            b.Box(gloveDark, new Vector3(0f, 0.0185f, 0.082f), new Vector3(0.066f, 0.005f, 0.012f));
            for (var k = 0; k < 4; k++)
                b.Box(gloveDark, new Vector3(-0.0285f + k * 0.019f, 0.021f, 0.084f), new Vector3(0.013f, 0.003f, 0.007f));
            b.Box(gloveDark, new Vector3(0f, 0.0168f, 0.004f), new Vector3(0.05f, 0.004f, 0.016f));
            b.Box(glove, new Vector3(0f, 0.0190f, 0.004f), new Vector3(0.022f, 0.002f, 0.01f));
            b.Box(gloveDark, new Vector3(0f, -0.0145f, 0.045f), new Vector3(0.05f, 0.002f, 0.004f));
            // Bilek körüğü: manşet üzerinde üç kıvrım halkası.
            for (var ring = 0; ring < 3; ring++)
            {
                var z = -0.046f + ring * 0.016f;
                b.Cylinder(gloveDark, new Vector3(0f, 0f, z), new Vector3(0f, 0f, z + 0.006f), 0.0368f, 0.0368f, 8);
            }

            // Parmaklar (işaret parmağı başparmak tarafında, -X).
            for (var i = 0; i < 4; i++)
            {
                var x = -0.0285f + i * 0.019f;
                var length = i == 1 ? 1.06f : i == 3 ? 0.82f : 1f;
                var radius = i == 3 ? 0.0074f : 0.0084f;
                var k = new Vector3(x, -0.002f, 0.084f);
                Vector3 p1, p2, p3;
                if (fist)
                {
                    p1 = k + new Vector3(0f, -0.019f, 0.017f) * length;
                    p2 = p1 + new Vector3(0f, -0.024f, -0.009f) * length;
                    p3 = p2 + new Vector3(0f, 0.002f, -0.021f) * length;
                }
                else if (i == 0 && indexMode == IndexRest)
                {
                    // İşaret parmağı tetik korkuluğu üstünde düz (yan tarafa yaslı).
                    p1 = k + new Vector3(0.003f, -0.002f, 0.032f);
                    p2 = p1 + new Vector3(0.003f, -0.002f, 0.028f);
                    p3 = p2 + new Vector3(0.001f, -0.002f, 0.022f);
                }
                else if (i == 0 && indexMode == IndexPress)
                {
                    // Tetikte: ilk boğum öne-aşağı, ikinci boğum tetiğe iner, uç boğum hafif geri.
                    p1 = k + new Vector3(0f, -0.010f, 0.030f);
                    p2 = p1 + new Vector3(0f, -0.019f, 0.012f);
                    p3 = p2 + new Vector3(0f, -0.014f, -0.007f);
                }
                else
                {
                    // Kabzayı saran 3 boğum: kök boğum ~50, orta ~95, uç boğum avuca dönük.
                    p1 = k + new Vector3(0f, -0.017f, 0.024f) * length;
                    p2 = p1 + new Vector3(0f, -0.027f, -0.006f) * length;
                    p3 = p2 + new Vector3(0f, -0.006f, -0.021f) * length;
                }

                b.Cylinder(glove, k, p1, radius, radius * 0.97f, 6);
                b.Ellipsoid(glove, p1, new Vector3(radius, radius, radius), 2, 6);
                b.Cylinder(glove, p1, p2, radius * 0.97f, radius * 0.92f, 6);
                b.Ellipsoid(skin, p2, new Vector3(radius * 0.92f, radius * 0.92f, radius * 0.92f), 2, 6);
                b.Cylinder(skin, p2, p3, radius * 0.9f, radius * 0.8f, 6);
                b.Cylinder(gloveDark, p1 + (p2 - p1) * 0.35f, p1 + (p2 - p1) * 0.45f, radius * 1.08f, radius * 1.08f, 6); // eldiven dikiş halkası
            }

            // Başparmak.
            var t0 = new Vector3(-0.03f, -0.006f, 0.014f);
            var t1 = new Vector3(-0.046f, -0.017f, 0.043f);
            var t2 = fist ? new Vector3(-0.026f, -0.036f, 0.07f) : new Vector3(-0.043f, -0.042f, 0.063f);
            b.Cylinder(glove, t0, t1, 0.0105f, 0.0098f, 6);
            b.Ellipsoid(glove, t1, new Vector3(0.0098f, 0.0098f, 0.0098f), 2, 6);
            b.Cylinder(skin, t1, t2, 0.0094f, 0.0082f, 6);
        }

        /// <summary>Kızılay hilali (yatay düzlemde, küçük kutulardan yay).</summary>
        private static void Crescent(WeaponMeshBuilder b, Material m, Vector3 center, float radius)
        {
            const int segments = 7;
            for (var i = 0; i < segments; i++)
            {
                var t = (i + 0.5f) / segments;
                var angle = Mathf.Lerp(40f, 320f, t) * Mathf.Deg2Rad;
                var thickness = Mathf.Lerp(0.35f, 1f, Mathf.Sin(t * Mathf.PI)) * radius * 0.42f;
                var pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                var yaw = -angle * Mathf.Rad2Deg;
                b.Box(m, pos, new Vector3(thickness, 0.0016f, radius * 0.95f * Mathf.PI * 2f * (280f / 360f) / segments), new Vector3(0f, yaw, 0f));
            }
        }
    }
}
