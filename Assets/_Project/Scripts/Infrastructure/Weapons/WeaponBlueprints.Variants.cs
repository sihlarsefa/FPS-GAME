using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// silahlar2: yeni Türk silahlarının geometri tarifleri. Mevcut tariflerin üzerine ayırt edici parçalar ekler
    /// (MPT-76K kısa namlulu olduğundan ayrı kurulur). Parçalar Build çağrısından önce Ctx.B'ye eklenir.
    /// </summary>
    internal static partial class WeaponBlueprints
    {
        /// <summary>Sarsılmaz SAR 223: MPT-55 gövdesi + zeytin yeşili el kundağı bantları ve gaz bloğu.</summary>
        private static void BuildSar223(Ctx c)
        {
            BuildMpt55(c);
            var p = c.P;
            var b = c.B;
            b.Cylinder(p.Olive, new Vector3(0f, 0.037f, 0.2f), new Vector3(0f, 0.037f, 0.22f), 0.0318f, 8);
            b.Cylinder(p.Olive, new Vector3(0f, 0.037f, 0.395f), new Vector3(0f, 0.037f, 0.415f), 0.0318f, 8);
            b.Box(p.DarkMetal, new Vector3(0f, 0.058f, 0.44f), new Vector3(0.016f, 0.012f, 0.03f));
        }

        /// <summary>MKE MPT-76K: MPT-76'nın kısa namlulu karabina sürümü; kısa kundak, kısa dipçik, kırmızı nokta.</summary>
        private static void BuildMpt76K(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.035f;
            var gripTop = new Vector3(0f, -0.012f, 0f);

            var darkMetal = p.Metal;
            p.Metal = p.Tan; // gövde kum/bej (ASKER_REFERANSI)
            ArReceiver(c, -0.07f, 0.2f, 0.135f, 0.085f, 0.032f);
            p.Metal = darkMetal;
            Rail(c, p.Metal, -0.068f, 0.198f, 0.065f);

            b.Cylinder(p.Tan, new Vector3(0f, 0.037f, 0.2f), new Vector3(0f, 0.037f, 0.36f), 0.031f, 8);
            Rail(c, p.Metal, 0.205f, 0.355f, 0.0655f);
            for (var i = 0; i < 2; i++)
            {
                var z = 0.25f + i * 0.07f;
                b.Box(p.Polymer, new Vector3(0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
                b.Box(p.Polymer, new Vector3(-0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
            }

            Barrel(c, p.Metal, bore, 0.36f, 0.4f, 0.0098f);
            FlashHider(c, bore, 0.4f, 0.455f, 0.0135f);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.458f));

            PistolGrip(c, p.Polymer, gripTop, 18f, 0.102f, 0.03f, 0.042f);
            TriggerGroup(c, p.Metal, p.Metal, 0.012f, 0.078f, -0.042f, -0.014f);
            AdjustableStock(c, p.Tan, 0.03f, -0.05f, -0.2f, -0.25f, 0.118f, true);

            Magazine(c, p.Polymer, p.Polymer, new Vector3(0f, -0.022f, 0.135f), 0.026f, 0.074f, 0.13f, 4f, 12f, 3);
            RedDot(c, 0.025f, 0.105f, 0.113f, 0.075f);
            c.Bp.EyeRelief = 0.15f;

            RightGripAnchor(c, gripTop, 18f, 0.03f);
            LeftSupportAnchor(c, 0.29f, 0.0084f);
        }

        /// <summary>Canik METE SFT: TP9 çatısı + alt ray ve kızak üstü ince arpacık/gez vurgusu.</summary>
        private static void BuildMeteSft(Ctx c)
        {
            BuildPistol(c, true);
            var p = c.P;
            c.B.Box(p.DarkMetal, new Vector3(0f, -0.0105f, 0.125f), new Vector3(0.02f, 0.004f, 0.05f));
            c.B.Box(p.Olive, new Vector3(0f, 0.0045f, 0.0f), new Vector3(0.028f, 0.012f, 0.012f));
        }

        /// <summary>Sarsılmaz SAR 762 MT: KNT-76 çatısı + zeytin yeşili el kundağı bantları.</summary>
        private static void BuildSar762Mt(Ctx c)
        {
            BuildKnt76(c);
            var p = c.P;
            var b = c.B;
            b.Cylinder(p.Olive, new Vector3(0f, 0.037f, 0.22f), new Vector3(0f, 0.037f, 0.24f), 0.0318f, 8);
            b.Cylinder(p.Olive, new Vector3(0f, 0.037f, 0.52f), new Vector3(0f, 0.037f, 0.54f), 0.0318f, 8);
        }

        /// <summary>MKE MG3: PMT-76 çatısı + üst kapakta havalandırma kaburgaları ve zeytin yeşili besleme bandı.</summary>
        private static void BuildMg3(Ctx c)
        {
            BuildPmt76(c);
            var p = c.P;
            var b = c.B;
            for (var i = 0; i < 5; i++)
                b.Box(p.DarkMetal, new Vector3(0f, 0.0775f, -0.03f + i * 0.045f), new Vector3(0.036f, 0.003f, 0.014f));
            b.Box(p.Olive, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.054f, 0.076f, 0.012f));
        }

        /// <summary>Escort Magnum: Escort çatısı + ısı kalkanı ve gaz bloğu (yarı otomatik); pompa animasyonu oynatılmaz.</summary>
        private static void BuildEscortMagnum(Ctx c)
        {
            BuildEscort(c);
            var p = c.P;
            var b = c.B;
            b.Cylinder(p.Polymer, new Vector3(0f, 0.058f, 0.2f), new Vector3(0f, 0.058f, 0.55f), 0.0135f, 8);
            b.Box(p.DarkMetal, new Vector3(0f, 0.014f, 0.22f), new Vector3(0.03f, 0.02f, 0.03f));
            b.Box(p.Metal, new Vector3(0.0215f, 0.046f, 0.03f), new Vector3(0.012f, 0.01f, 0.014f));
        }
    }
}
