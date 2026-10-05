using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Model uzayında bir bağlantı noktası (namlu, nişan, el bileği...).</summary>
    internal sealed class AnchorSpec
    {
        public string Name;
        public string Parent;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>Bir silah tipinin önbelleğe alınmış, paylaşılan mesh + bağlantı tarifi.</summary>
    internal sealed class WeaponBlueprint
    {
        public WeaponStyle Style;
        public List<BuiltMeshPart> Parts = new List<BuiltMeshPart>();
        public readonly List<AnchorSpec> Anchors = new List<AnchorSpec>(8);
        public float EyeRelief = 0.14f;
        public bool HasScope;
        public bool HasOptic;
        public Vector3 MagazineEject = Vector3.down;
        public float BoltTravel = 0.08f;
        public float BoltLift = 60f;
        public float PumpTravel = 0.085f;
        public float SlideTravel = 0.026f;

        public bool IsValid
        {
            get
            {
                if (Parts == null || Parts.Count == 0)
                    return false;

                for (var i = 0; i < Parts.Count; i++)
                {
                    if (Parts[i] == null || !Parts[i].IsValid)
                        return false;
                }

                return true;
            }
        }

        public Vector3 PivotOf(string group)
        {
            for (var i = 0; i < Parts.Count; i++)
            {
                if (Parts[i].Name == group)
                    return Parts[i].Pivot;
            }

            return Vector3.zero;
        }
    }

    /// <summary>
    /// Türk silahlarının düşük poligonlu geometri tarifleri (gerçek ölçülere yakın, metre). Orijin kabzanın üstüdür,
    /// +Z namlu yönü. Her tarif gövde + hareketli parçaları (şarjör, kurma kolu, kızak, pompa, kapak) ve el/nişan/namlu
    /// bağlantılarını üretir.
    /// </summary>
    internal static class WeaponBlueprints
    {
        /// <summary>El modelinde bilekten avuç merkezine uzaklık (ViewmodelHands ile aynı).</summary>
        public const float PalmCenter = 0.045f;

        /// <summary>Avuç yarı kalınlığı.</summary>
        public const float PalmHalfThickness = 0.015f;

        private sealed class Palette
        {
            public Material Metal;
            public Material DarkMetal;
            public Material Polymer;
            public Material Tan;
            public Material Wood;
            public Material Olive;
            public Material Rubber;
            public Material Glass;
            public Material Reticle;
            public Material Brass;
            public Material ShellRed;

            public static Palette Create()
            {
                return new Palette
                {
                    Metal = Safe(MaterialLibrary.Get(MaterialId.GunMetal)),
                    Polymer = Safe(MaterialLibrary.Get(MaterialId.GunPolymer)),
                    Tan = Safe(MaterialLibrary.Get(MaterialId.GunTan)),
                    Wood = Safe(MaterialLibrary.Get(MaterialId.GunWood)),
                    DarkMetal = Safe(MaterialLibrary.Lit(new Color(0.05f, 0.05f, 0.055f), 0.35f, 0.5f)),
                    Olive = Safe(MaterialLibrary.Lit(new Color(0.24f, 0.27f, 0.17f), 0.22f)),
                    Rubber = Safe(MaterialLibrary.Lit(new Color(0.045f, 0.045f, 0.045f), 0.08f)),
                    Glass = Safe(MaterialLibrary.Lit(new Color(0.05f, 0.09f, 0.13f), 0.95f, 0.4f)),
                    Reticle = Safe(MaterialLibrary.Unlit(new Color(1f, 0.12f, 0.06f))),
                    Brass = Safe(MaterialLibrary.Lit(new Color(0.75f, 0.58f, 0.26f), 0.65f, 0.85f)),
                    ShellRed = Safe(MaterialLibrary.Lit(new Color(0.55f, 0.08f, 0.06f), 0.35f))
                };
            }

            private static Material Safe(Material m) => m != null ? m : MaterialLibrary.Get(MaterialId.Gray);
        }

        private sealed class Ctx
        {
            public readonly WeaponMeshBuilder B = new WeaponMeshBuilder();
            public readonly WeaponBlueprint Bp = new WeaponBlueprint();
            public Palette P;

            public void Anchor(string name, Vector3 position, Quaternion rotation, string parent = WeaponModel.BodyPart)
            {
                Bp.Anchors.Add(new AnchorSpec { Name = name, Parent = parent, Position = position, Rotation = rotation });
            }

            public void Anchor(string name, Vector3 position, string parent = WeaponModel.BodyPart) =>
                Anchor(name, position, Quaternion.identity, parent);
        }

        public static WeaponBlueprint Build(WeaponStyle style)
        {
            var c = new Ctx { P = Palette.Create() };
            c.Bp.Style = style;

            switch (style)
            {
                case WeaponStyle.Sar9: BuildPistol(c, false); break;
                case WeaponStyle.Tp9: BuildPistol(c, true); break;
                case WeaponStyle.Sar109: BuildSar109(c); break;
                case WeaponStyle.Mpt55: BuildMpt55(c); break;
                case WeaponStyle.Mpt76: BuildMpt76(c); break;
                case WeaponStyle.G3a7: BuildG3(c); break;
                case WeaponStyle.Knt76: BuildKnt76(c); break;
                case WeaponStyle.Jng90: BuildJng90(c); break;
                case WeaponStyle.Pmt76: BuildPmt76(c); break;
                case WeaponStyle.Escort: BuildEscort(c); break;
                default: return null;
            }

            c.Bp.Parts = c.B.Build("Weapon_" + style);
            return c.Bp;
        }

        // =====================================================================================================
        //  Ortak alt montajlar
        // =====================================================================================================

        /// <summary>AR tipi alt + üst gövde (MPT-55/76, KNT-76, SAR 109T). Üst gövde üstü y = 0.065.</summary>
        private static void ArReceiver(Ctx c, float zBack, float zFront, float wellZ, float wellLength, float width)
        {
            var p = c.P;
            var b = c.B;
            var lowerBack = zBack + 0.022f;
            var lowerFront = zFront - 0.01f;
            b.Box(p.Metal, new Vector3(0f, 0.008f, (lowerBack + lowerFront) * 0.5f), new Vector3(width, 0.044f, lowerFront - lowerBack));
            b.Box(p.Metal, new Vector3(0f, 0.02f, zBack + 0.012f), new Vector3(width - 0.004f, 0.03f, 0.024f));
            b.Box(p.Metal, new Vector3(0f, -0.021f, wellZ), new Vector3(width + 0.005f, 0.026f, wellLength));
            b.Box(p.Metal, new Vector3(0f, 0.047f, (zBack + zFront) * 0.5f), new Vector3(width + 0.002f, 0.036f, zFront - zBack));

            // Kovan atma penceresi, sürgü yardımcısı, kurma kolu.
            b.Box(p.DarkMetal, new Vector3(width * 0.5f + 0.0012f, 0.045f, wellZ - 0.045f), new Vector3(0.002f, 0.014f, 0.05f));
            b.Cylinder(p.Metal, new Vector3(width * 0.5f, 0.055f, zBack + 0.085f), new Vector3(width * 0.5f + 0.011f, 0.055f, zBack + 0.062f), 0.0065f, 6);
            b.Box(p.Polymer, new Vector3(0f, 0.068f, zBack - 0.006f), new Vector3(0.016f, 0.008f, 0.026f));
            b.Box(p.Polymer, new Vector3(0f, 0.068f, zBack - 0.022f), new Vector3(0.05f, 0.008f, 0.01f));

            // Şarjör bırakma düğmesi (sağ).
            b.Box(p.Metal, new Vector3(width * 0.5f + 0.002f, 0.0f, wellZ - wellLength * 0.5f - 0.006f), new Vector3(0.004f, 0.01f, 0.01f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(width * 0.5f + 0.004f, 0.045f, wellZ - 0.045f));
        }

        /// <summary>Picatinny ray: taban + dişler. Üst yüzey yBase + 0.01.</summary>
        private static void Rail(Ctx c, Material m, float z0, float z1, float yBase, float width = 0.021f)
        {
            var b = c.B;
            var length = z1 - z0;
            if (length <= 0.005f)
                return;

            b.Box(m, new Vector3(0f, yBase + 0.003f, (z0 + z1) * 0.5f), new Vector3(width * 0.8f, 0.006f, length));
            var teeth = Mathf.Max(1, Mathf.FloorToInt(length / 0.01f));
            var step = length / teeth;
            for (var i = 0; i < teeth; i++)
            {
                var z = z0 + step * (i + 0.5f);
                b.Box(m, new Vector3(0f, yBase + 0.008f, z), new Vector3(width, 0.004f, step * 0.55f));
            }
        }

        /// <summary>Eğik tabanca kabzası (üstten aşağı, alt kısım geriye yatık).</summary>
        private static void PistolGrip(Ctx c, Material m, Vector3 top, float rake, float length, float width, float depth)
        {
            var b = c.B;
            var down = Quaternion.Euler(rake, 0f, 0f) * Vector3.down;
            var bottom = top + down * length;
            b.VerticalTaper(m, top, new Vector2(width, depth), bottom, new Vector2(width * 1.06f, depth * 1.1f));
            b.Box(m, bottom + new Vector3(0f, -0.004f, 0f), new Vector3(width * 1.12f, 0.008f, depth * 1.2f), new Vector3(rake * 0.5f, 0f, 0f));

            // Parmak yuvaları (öne doğru küçük çıkıntılar).
            var front = Quaternion.Euler(rake, 0f, 0f) * Vector3.forward;
            for (var i = 1; i <= 2; i++)
            {
                var pos = top + down * (length * (0.3f * i)) + front * (depth * 0.5f);
                b.Box(m, pos, new Vector3(width * 0.9f, 0.006f, 0.006f), new Vector3(rake, 0f, 0f));
            }
        }

        private static void TriggerGroup(Ctx c, Material guard, Material trigger, float zBack, float zFront, float yBottom, float yTop)
        {
            var b = c.B;
            b.Box(guard, new Vector3(0f, yBottom, (zBack + zFront) * 0.5f), new Vector3(0.011f, 0.005f, zFront - zBack));
            b.Box(guard, new Vector3(0f, (yBottom + yTop) * 0.5f, zFront), new Vector3(0.011f, yTop - yBottom, 0.006f));
            var height = (yTop - yBottom) * 0.6f;
            b.Box(trigger, new Vector3(0f, yTop - height * 0.5f, zBack + (zFront - zBack) * 0.35f), new Vector3(0.005f, height, 0.006f), new Vector3(18f, 0f, 0f));
        }

        private static void Barrel(Ctx c, Material m, float y, float z0, float z1, float r, int sides = 8)
        {
            c.B.Cylinder(m, new Vector3(0f, y, z0), new Vector3(0f, y, z1), r, sides);
        }

        private static void FlashHider(Ctx c, float y, float z0, float z1, float r)
        {
            var p = c.P;
            c.B.Cylinder(p.Metal, new Vector3(0f, y, z0), new Vector3(0f, y, z1), r, 6);
            c.B.Cylinder(p.DarkMetal, new Vector3(0f, y, z1 - 0.006f), new Vector3(0f, y, z1), r * 1.06f, 6);
            var mid = (z0 + z1) * 0.5f + 0.004f;
            c.B.Box(p.DarkMetal, new Vector3(r * 0.9f, y, mid), new Vector3(0.003f, 0.004f, (z1 - z0) * 0.55f));
            c.B.Box(p.DarkMetal, new Vector3(-r * 0.9f, y, mid), new Vector3(0.003f, 0.004f, (z1 - z0) * 0.55f));
            c.B.Box(p.DarkMetal, new Vector3(0f, y + r * 0.9f, mid), new Vector3(0.004f, 0.003f, (z1 - z0) * 0.55f));
        }

        /// <summary>Kompakt kırmızı nokta nişangâhı (içi boş tüp + kızıl nokta). Nişan hattı: yCenter.</summary>
        private static void RedDot(Ctx c, float zRear, float zFront, float yCenter, float railTop)
        {
            var p = c.P;
            var b = c.B;
            const float outer = 0.021f;
            const float inner = 0.0172f;
            var mid = (zRear + zFront) * 0.5f;
            b.Box(p.Polymer, new Vector3(0f, railTop + 0.004f, mid), new Vector3(0.028f, 0.008f, zFront - zRear + 0.01f));
            var riserTop = yCenter - outer + 0.003f;
            b.Box(p.Polymer, new Vector3(0f, (railTop + 0.008f + riserTop) * 0.5f, mid), new Vector3(0.022f, riserTop - railTop - 0.008f, (zFront - zRear) * 0.7f));
            b.Tube(p.Polymer, new Vector3(0f, yCenter, zRear), new Vector3(0f, yCenter, zFront), outer, inner, 12);
            // Ayar kuleleri.
            b.Cylinder(p.Polymer, new Vector3(0f, yCenter + outer - 0.002f, mid), new Vector3(0f, yCenter + outer + 0.008f, mid), 0.0085f, 8);
            b.Cylinder(p.Polymer, new Vector3(outer - 0.002f, yCenter, mid), new Vector3(outer + 0.008f, yCenter, mid), 0.0085f, 8);
            // Ön mercek halkası (koyu, biraz geniş).
            b.Tube(p.DarkMetal, new Vector3(0f, yCenter, zFront - 0.004f), new Vector3(0f, yCenter, zFront + 0.002f), outer + 0.0015f, inner, 12);
            // Kızıl nokta (ön mercekte).
            b.Box(p.Reticle, new Vector3(0f, yCenter, zFront - 0.006f), new Vector3(0.0018f, 0.0018f, 0.0008f));

            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, yCenter, zRear));
            c.Bp.HasOptic = true;
        }

        /// <summary>Holografik nişangâh (açık pencere + nokta/halka). Nişan hattı yüksekliğini döndürür.</summary>
        private static float Holo(Ctx c, float zRear, float zFront, float railTop)
        {
            var p = c.P;
            var b = c.B;
            var baseTop = railTop + 0.012f;
            const float windowHeight = 0.04f;
            const float halfWidth = 0.023f;
            var yCenter = baseTop + windowHeight * 0.5f;
            var mid = (zRear + zFront) * 0.5f;

            b.Box(p.Polymer, new Vector3(0f, railTop + 0.006f, mid - 0.006f), new Vector3(0.036f, 0.012f, zFront - zRear + 0.03f));
            // Pencere çerçevesi.
            b.Box(p.Polymer, new Vector3(halfWidth, yCenter, zFront - 0.012f), new Vector3(0.004f, windowHeight + 0.004f, 0.026f));
            b.Box(p.Polymer, new Vector3(-halfWidth, yCenter, zFront - 0.012f), new Vector3(0.004f, windowHeight + 0.004f, 0.026f));
            b.Box(p.Polymer, new Vector3(0f, baseTop + windowHeight + 0.002f, zFront - 0.012f), new Vector3(halfWidth * 2f + 0.004f, 0.004f, 0.026f));
            // Yan düğmeler.
            b.Box(p.Rubber, new Vector3(halfWidth - 0.002f, railTop + 0.007f, zRear - 0.01f), new Vector3(0.008f, 0.008f, 0.012f));
            // Retikül: nokta + 4 halka parçası.
            var z = zFront - 0.004f;
            b.Box(p.Reticle, new Vector3(0f, yCenter, z), new Vector3(0.0016f, 0.0016f, 0.0006f));
            const float ring = 0.0075f;
            b.Box(p.Reticle, new Vector3(0f, yCenter + ring, z), new Vector3(0.0045f, 0.0008f, 0.0006f));
            b.Box(p.Reticle, new Vector3(0f, yCenter - ring, z), new Vector3(0.0045f, 0.0008f, 0.0006f));
            b.Box(p.Reticle, new Vector3(ring, yCenter, z), new Vector3(0.0008f, 0.0045f, 0.0006f));
            b.Box(p.Reticle, new Vector3(-ring, yCenter, z), new Vector3(0.0008f, 0.0045f, 0.0006f));

            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, yCenter, zRear));
            c.Bp.HasOptic = true;
            return yCenter;
        }

        /// <summary>Dürbün: göz merceği, gövde tüpü, objektif, kuleler, halkalar, cam yüzeyler.</summary>
        private static void Scope(Ctx c, float zRear, float zFront, float y, float tubeR, float objR, float objLen, float ocuR,
            float ocuLen, float ring0, float ring1, float railTop)
        {
            var p = c.P;
            var b = c.B;
            // Göz merceği lastiği (içi boş) + cam.
            b.Tube(p.Rubber, new Vector3(0f, y, zRear - 0.008f), new Vector3(0f, y, zRear), ocuR * 1.03f, ocuR * 0.8f, 12);
            b.Cylinder(p.Polymer, new Vector3(0f, y, zRear), new Vector3(0f, y, zRear + ocuLen), ocuR, tubeR * 1.08f, 12, false, true, true);
            b.Cylinder(p.Glass, new Vector3(0f, y, zRear + 0.003f), new Vector3(0f, y, zRear + 0.006f), ocuR * 0.97f, 12);
            // Ana tüp.
            b.Cylinder(p.Polymer, new Vector3(0f, y, zRear + ocuLen), new Vector3(0f, y, zFront - objLen), tubeR, tubeR, 12, false, false, true);
            // Objektif çanı + cam.
            var bellStart = zFront - objLen;
            var bellEnd = zFront - objLen * 0.45f;
            b.Cylinder(p.Polymer, new Vector3(0f, y, bellStart), new Vector3(0f, y, bellEnd), tubeR, objR, 12, false, false, true);
            b.Cylinder(p.Polymer, new Vector3(0f, y, bellEnd), new Vector3(0f, y, zFront), objR, objR, 12, false, false, true);
            b.Tube(p.Polymer, new Vector3(0f, y, zFront - 0.002f), new Vector3(0f, y, zFront + 0.004f), objR * 1.04f, objR * 0.85f, 12);
            b.Cylinder(p.Glass, new Vector3(0f, y, zFront - 0.006f), new Vector3(0f, y, zFront - 0.003f), objR * 0.97f, 12);

            // Kuleler (yükseliş, rüzgâr, paralaks).
            var turretZ = (zRear + ocuLen + bellStart) * 0.5f;
            b.Cylinder(p.Polymer, new Vector3(0f, y + tubeR * 0.8f, turretZ), new Vector3(0f, y + tubeR + 0.016f, turretZ), 0.011f, 10);
            b.Cylinder(p.Metal, new Vector3(0f, y + tubeR + 0.016f, turretZ), new Vector3(0f, y + tubeR + 0.019f, turretZ), 0.0085f, 10);
            b.Cylinder(p.Polymer, new Vector3(tubeR * 0.8f, y, turretZ), new Vector3(tubeR + 0.016f, y, turretZ), 0.011f, 10);
            b.Cylinder(p.Polymer, new Vector3(-tubeR * 0.8f, y, turretZ), new Vector3(-tubeR - 0.011f, y, turretZ), 0.009f, 10);
            b.Box(p.Polymer, new Vector3(0f, y - tubeR * 0.3f, turretZ), new Vector3(tubeR * 2.4f, tubeR * 1.5f, 0.03f));

            // Halkalar ve tabanları.
            ScopeRing(c, ring0, y, tubeR, railTop);
            ScopeRing(c, ring1, y, tubeR, railTop);

            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, y, zRear - 0.008f));
            c.Bp.HasScope = true;
        }

        private static void ScopeRing(Ctx c, float z, float y, float tubeR, float railTop)
        {
            var p = c.P;
            var b = c.B;
            b.Tube(p.Metal, new Vector3(0f, y, z - 0.008f), new Vector3(0f, y, z + 0.008f), tubeR + 0.004f, tubeR * 0.9f, 12);
            var baseTop = y - tubeR;
            var height = Mathf.Max(0.006f, baseTop - railTop + 0.003f);
            b.Box(p.Metal, new Vector3(0f, railTop + height * 0.5f - 0.001f, z), new Vector3(0.022f, height, 0.016f));
            b.Box(p.Metal, new Vector3(-0.014f, railTop + 0.006f, z), new Vector3(0.008f, 0.01f, 0.012f));
        }

        /// <summary>Delikli gez (halka). Nişan hattı yCenter; taban halkanın altında kalır (görüşü kapatmaz).</summary>
        private static void IronRearAperture(Ctx c, Material m, float z, float yCenter, float railTop, float outer, float inner)
        {
            var b = c.B;
            var baseTop = yCenter - outer * 0.55f;
            b.Box(m, new Vector3(0f, (railTop + baseTop) * 0.5f, z), new Vector3(0.02f, Mathf.Max(0.004f, baseTop - railTop), 0.012f));
            b.Tube(m, new Vector3(0f, yCenter, z - 0.004f), new Vector3(0f, yCenter, z + 0.004f), outer, inner, 10);
            b.Box(m, new Vector3(outer + 0.003f, yCenter - 0.002f, z), new Vector3(0.004f, outer * 2f + 0.002f, 0.012f));
            b.Box(m, new Vector3(-outer - 0.003f, yCenter - 0.002f, z), new Vector3(0.004f, outer * 2f + 0.002f, 0.012f));
            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, yCenter, z - 0.004f));
        }

        /// <summary>Gez yarığı (iki kulak). Nişan hattı yTop - 0.0015.</summary>
        private static void IronRearNotch(Ctx c, Material m, float z, float yTop, float baseY)
        {
            var b = c.B;
            var h = Mathf.Max(0.004f, yTop - baseY);
            b.Box(m, new Vector3(0.0058f, baseY + h * 0.5f, z), new Vector3(0.0062f, h, 0.008f));
            b.Box(m, new Vector3(-0.0058f, baseY + h * 0.5f, z), new Vector3(0.0062f, h, 0.008f));
            b.Box(m, new Vector3(0f, baseY + 0.002f, z), new Vector3(0.018f, 0.004f, 0.008f));
            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, yTop - 0.0015f, z - 0.004f));
        }

        /// <summary>Arpacık; tepesi yTop (nişan hattı).</summary>
        private static void IronFrontPost(Ctx c, Material m, float z, float yTop, float baseY, bool hooded)
        {
            var b = c.B;
            var h = Mathf.Max(0.004f, yTop - baseY);
            b.Box(m, new Vector3(0f, baseY + h * 0.5f, z), new Vector3(0.0032f, h, 0.004f));
            b.Box(m, new Vector3(0f, baseY - 0.003f, z), new Vector3(0.02f, 0.008f, 0.016f));
            var wingH = h + 0.006f;
            b.Box(m, new Vector3(0.0095f, baseY + wingH * 0.5f, z), new Vector3(0.003f, wingH, 0.014f));
            b.Box(m, new Vector3(-0.0095f, baseY + wingH * 0.5f, z), new Vector3(0.003f, wingH, 0.014f));
            if (hooded)
                b.Box(m, new Vector3(0f, baseY + wingH + 0.0015f, z), new Vector3(0.022f, 0.003f, 0.014f));
        }

        /// <summary>Katlanmış iki ayak (bipod), bacaklar öne yatık.</summary>
        private static void Bipod(Ctx c, float z, float yMount, float legLength, float spread)
        {
            var p = c.P;
            var b = c.B;
            b.Box(p.Metal, new Vector3(0f, yMount - 0.007f, z), new Vector3(0.032f, 0.014f, 0.028f));
            for (var side = -1; side <= 1; side += 2)
            {
                var x = spread * side;
                b.Cylinder(p.Metal, new Vector3(x, yMount - 0.016f, z - 0.004f), new Vector3(x, yMount - 0.02f, z + legLength), 0.0055f, 6);
                b.Cylinder(p.Metal, new Vector3(x, yMount - 0.02f, z + legLength * 0.55f), new Vector3(x, yMount - 0.021f, z + legLength), 0.0045f, 6);
                b.Box(p.Rubber, new Vector3(x, yMount - 0.021f, z + legLength + 0.007f), new Vector3(0.012f, 0.011f, 0.014f));
            }
        }

        /// <summary>
        /// (Kavisli) şarjör, "Magazine" grubunda (pivot = üst merkez). startAngle: ilk eğim (pozitif = alt uç öne),
        /// curve: toplam kavis. Sol el tutma bağlantısını da ekler.
        /// </summary>
        private static void Magazine(Ctx c, Material m, Material baseMat, Vector3 top, float width, float depth, float length,
            float startAngle, float curve, int segments)
        {
            var b = c.B;
            segments = Mathf.Max(1, segments);
            b.BeginGroup(WeaponModel.MagazinePart, top);

            var corners = new Vector3[8];
            var point = top;
            var segLength = length / segments;
            var hw = width * 0.5f;
            var hd = depth * 0.5f;
            Quaternion rotA = Quaternion.Euler(-startAngle, 0f, 0f);
            var gripPoint = top;
            var gripRot = rotA;
            for (var i = 0; i < segments; i++)
            {
                var a0 = startAngle + curve * i / segments;
                var a1 = startAngle + curve * (i + 1) / segments;
                var r0 = Quaternion.Euler(-a0, 0f, 0f);
                var r1 = Quaternion.Euler(-a1, 0f, 0f);
                var dir = Quaternion.Euler(-(a0 + a1) * 0.5f, 0f, 0f) * Vector3.down;
                var next = point + dir * segLength;
                corners[0] = next + r1 * new Vector3(-hw, 0f, -hd);
                corners[1] = next + r1 * new Vector3(hw, 0f, -hd);
                corners[2] = point + r0 * new Vector3(hw, 0f, -hd);
                corners[3] = point + r0 * new Vector3(-hw, 0f, -hd);
                corners[4] = next + r1 * new Vector3(-hw, 0f, hd);
                corners[5] = next + r1 * new Vector3(hw, 0f, hd);
                corners[6] = point + r0 * new Vector3(hw, 0f, hd);
                corners[7] = point + r0 * new Vector3(-hw, 0f, hd);
                b.Hull(m, corners);

                // Yan nervür.
                var ribCenter = (point + next) * 0.5f;
                b.Box(m, ribCenter + r0 * new Vector3(hw + 0.0008f, 0f, 0f), new Vector3(0.0016f, segLength * 0.7f, depth * 0.45f), Quaternion.Euler(-(a0 + a1) * 0.5f, 0f, 0f));
                b.Box(m, ribCenter + r0 * new Vector3(-hw - 0.0008f, 0f, 0f), new Vector3(0.0016f, segLength * 0.7f, depth * 0.45f), Quaternion.Euler(-(a0 + a1) * 0.5f, 0f, 0f));

                if (i == segments / 2)
                {
                    gripPoint = (point + next) * 0.5f;
                    gripRot = Quaternion.Euler(-(a0 + a1) * 0.5f, 0f, 0f);
                }

                point = next;
                rotA = r1;
            }

            // Taban plakası.
            b.Box(baseMat, point + rotA * new Vector3(0f, -0.004f, 0.002f), new Vector3(width + 0.006f, 0.008f, depth + 0.01f), rotA);

            // Sol el: avuç şarjörün sol yüzünde, parmaklar öne (ön yüzü sarar).
            var handRot = gripRot * Quaternion.LookRotation(new Vector3(0.1f, 0.25f, 1f), Vector3.left);
            var palm = gripPoint + gripRot * new Vector3(-(hw + PalmHalfThickness + 0.001f), -0.01f, -0.012f);
            var wrist = palm - handRot * new Vector3(0f, 0f, PalmCenter);
            c.Anchor(WeaponModel.MagazineHandAnchor, wrist, handRot, WeaponModel.MagazinePart);
            c.Bp.MagazineEject = Quaternion.Euler(-startAngle, 0f, 0f) * Vector3.down;

            b.EndGroup();
        }

        /// <summary>Sağ el kabzada: avuç kabzanın sağında, parmaklar önden sola sarar, başparmak üstte.</summary>
        private static void RightGripAnchor(Ctx c, Vector3 gripTop, float rake, float gripWidth)
        {
            var rakeRot = Quaternion.Euler(rake, 0f, 0f);
            var rot = rakeRot * Quaternion.LookRotation(new Vector3(-0.16f, 0f, 1f), Vector3.right);
            var palm = gripTop + rakeRot * Vector3.down * 0.045f + Vector3.right * (gripWidth * 0.5f + PalmHalfThickness + 0.001f)
                       + rakeRot * Vector3.back * 0.006f;
            var wrist = palm - rot * new Vector3(0f, 0f, PalmCenter);
            c.Anchor(WeaponModel.RightHandAnchor, wrist, rot);
        }

        /// <summary>Sol el kundağın altında: avuç yukarı, parmaklar sağ-öne uzanıp sağ yandan yukarı sarar.</summary>
        private static void LeftSupportAnchor(Ctx c, float z, float yBottom, string parent = WeaponModel.BodyPart)
        {
            var rot = Quaternion.LookRotation(new Vector3(0.55f, 0.08f, 0.83f), new Vector3(0f, -1f, 0.1f));
            var palm = new Vector3(-0.008f, yBottom - PalmHalfThickness - 0.001f, z);
            var wrist = palm - rot * new Vector3(0f, 0f, PalmCenter);
            c.Anchor(WeaponModel.LeftHandAnchor, wrist, rot, parent);
        }

        /// <summary>Tabancada destek eli: kabzanın solunda, sağ elin parmaklarını önden sarar.</summary>
        private static void LeftPistolAnchor(Ctx c, Vector3 gripTop, float rake, float gripWidth)
        {
            var rakeRot = Quaternion.Euler(rake, 0f, 0f);
            var rot = rakeRot * Quaternion.LookRotation(new Vector3(0.35f, 0f, 1f), Vector3.left);
            var palm = gripTop + rakeRot * Vector3.down * 0.062f + Vector3.left * (gripWidth * 0.5f + 0.012f + PalmHalfThickness);
            var wrist = palm - rot * new Vector3(0f, 0f, PalmCenter);
            c.Anchor(WeaponModel.LeftHandAnchor, wrist, rot);
        }

        private static void AdjustableStock(Ctx c, Material stock, float tubeY, float zStart, float tubeEnd, float buttZ, float buttHeight,
            bool cheekRiser)
        {
            var p = c.P;
            var b = c.B;
            b.Cylinder(p.Metal, new Vector3(0f, tubeY, zStart), new Vector3(0f, tubeY, tubeEnd), 0.0155f, 8);
            var frontZ = Mathf.Lerp(tubeEnd, buttZ, 0.15f) + 0.07f;
            b.Taper(stock, new Vector3(0f, tubeY - buttHeight * 0.5f + 0.045f, buttZ), new Vector2(0.044f, buttHeight),
                new Vector3(0f, tubeY + 0.002f, frontZ), new Vector2(0.04f, 0.058f));
            b.Box(p.Rubber, new Vector3(0f, tubeY - buttHeight * 0.5f + 0.045f, buttZ - 0.008f), new Vector3(0.046f, buttHeight + 0.004f, 0.016f));
            if (cheekRiser)
                b.Box(stock, new Vector3(0f, tubeY + 0.038f, (frontZ + buttZ) * 0.5f), new Vector3(0.034f, 0.014f, (frontZ - buttZ) * 0.7f));
            // Ayar kolu.
            b.Box(p.Polymer, new Vector3(0f, tubeY - 0.026f, frontZ - 0.012f), new Vector3(0.012f, 0.012f, 0.03f));
        }

        // =====================================================================================================
        //  Silahlar
        // =====================================================================================================

        /// <summary>MKE MPT-76: uzun 7,62 piyade tüfeği, kum rengi/siyah polimer, ray üstü kırmızı nokta.</summary>
        private static void BuildMpt76(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.035f;
            var gripTop = new Vector3(0f, -0.012f, 0f);

            ArReceiver(c, -0.07f, 0.2f, 0.135f, 0.085f, 0.032f);
            Rail(c, p.Metal, -0.068f, 0.198f, 0.065f);

            // Kum rengi sekizgen el kundağı + ray + M-LOK yuvaları.
            b.Cylinder(p.Tan, new Vector3(0f, 0.037f, 0.2f), new Vector3(0f, 0.037f, 0.5f), 0.031f, 8);
            b.Cylinder(p.Polymer, new Vector3(0f, 0.037f, 0.5f), new Vector3(0f, 0.037f, 0.508f), 0.0305f, 8);
            Rail(c, p.Metal, 0.205f, 0.495f, 0.0655f);
            for (var i = 0; i < 3; i++)
            {
                var z = 0.27f + i * 0.07f;
                b.Box(p.Polymer, new Vector3(0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
                b.Box(p.Polymer, new Vector3(-0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
            }

            Barrel(c, p.Metal, bore, 0.508f, 0.622f, 0.0098f);
            FlashHider(c, bore, 0.622f, 0.674f, 0.0128f);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.677f));

            // Katlanmış yedek nişangâhlar.
            b.Box(p.Polymer, new Vector3(0f, 0.0795f, 0.465f), new Vector3(0.018f, 0.008f, 0.035f));
            b.Box(p.Polymer, new Vector3(0f, 0.0795f, -0.045f), new Vector3(0.02f, 0.008f, 0.03f));

            PistolGrip(c, p.Polymer, gripTop, 18f, 0.102f, 0.03f, 0.042f);
            TriggerGroup(c, p.Metal, p.Metal, 0.012f, 0.078f, -0.042f, -0.014f);
            AdjustableStock(c, p.Tan, 0.03f, -0.05f, -0.255f, -0.31f, 0.128f, true);

            Magazine(c, p.Polymer, p.Polymer, new Vector3(0f, -0.022f, 0.135f), 0.026f, 0.074f, 0.142f, 4f, 12f, 3);
            RedDot(c, 0.025f, 0.105f, 0.113f, 0.075f);
            c.Bp.EyeRelief = 0.15f;

            RightGripAnchor(c, gripTop, 18f, 0.03f);
            LeftSupportAnchor(c, 0.37f, 0.0084f);
        }

        /// <summary>MKE MPT-55: daha kısa 5,56 tüfek, siyah polimer, kum rengi kavisli şarjör, holografik nişangâh.</summary>
        private static void BuildMpt55(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.035f;
            var gripTop = new Vector3(0f, -0.012f, 0f);

            ArReceiver(c, -0.066f, 0.185f, 0.125f, 0.078f, 0.031f);
            Rail(c, p.Metal, -0.064f, 0.183f, 0.065f);

            b.Cylinder(p.Polymer, new Vector3(0f, 0.037f, 0.185f), new Vector3(0f, 0.037f, 0.42f), 0.031f, 8);
            Rail(c, p.Metal, 0.19f, 0.415f, 0.0655f);
            for (var i = 0; i < 2; i++)
            {
                var z = 0.25f + i * 0.08f;
                b.Box(p.DarkMetal, new Vector3(0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.032f));
                b.Box(p.DarkMetal, new Vector3(-0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.032f));
            }

            Barrel(c, p.Metal, bore, 0.42f, 0.5f, 0.0088f);
            b.Box(p.Metal, new Vector3(0f, bore + 0.002f, 0.46f), new Vector3(0.018f, 0.024f, 0.018f));
            FlashHider(c, bore, 0.5f, 0.545f, 0.0115f);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.548f));

            b.Box(p.Polymer, new Vector3(0f, 0.0795f, 0.395f), new Vector3(0.018f, 0.008f, 0.032f));

            PistolGrip(c, p.Polymer, gripTop, 18f, 0.098f, 0.029f, 0.04f);
            TriggerGroup(c, p.Metal, p.Metal, 0.012f, 0.074f, -0.04f, -0.014f);
            AdjustableStock(c, p.Polymer, 0.03f, -0.046f, -0.225f, -0.275f, 0.115f, false);

            Magazine(c, p.Tan, p.Polymer, new Vector3(0f, -0.022f, 0.125f), 0.023f, 0.064f, 0.155f, 2f, 22f, 4);
            Holo(c, 0.05f, 0.09f, 0.075f);
            c.Bp.EyeRelief = 0.17f;

            RightGripAnchor(c, gripTop, 18f, 0.029f);
            LeftSupportAnchor(c, 0.32f, 0.0084f);
        }

        /// <summary>MKE G3A7: klasik 7,62 tüfek; yeşil el kundağı, ahşap dipçik, döner tamburlu gez, korumalı arpacık.</summary>
        private static void BuildG3(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.04f;
            var gripTop = new Vector3(0f, -0.006f, 0f);

            // Preslenmiş gövde (yuvarlak üst).
            b.Box(p.Metal, new Vector3(0f, 0.04f, 0.05f), new Vector3(0.036f, 0.046f, 0.3f));
            b.Cylinder(p.Metal, new Vector3(0f, 0.055f, -0.1f), new Vector3(0f, 0.055f, 0.2f), 0.0185f, 8);
            b.Box(p.DarkMetal, new Vector3(0.0185f, 0.045f, 0.07f), new Vector3(0.002f, 0.016f, 0.07f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(0.021f, 0.045f, 0.07f));
            b.Box(p.Metal, new Vector3(0f, 0.04f, -0.094f), new Vector3(0.037f, 0.044f, 0.014f));

            // Tetik muhafazası + kabza.
            b.Box(p.Polymer, new Vector3(0f, 0.006f, 0.03f), new Vector3(0.032f, 0.026f, 0.15f));
            PistolGrip(c, p.Polymer, gripTop, 15f, 0.1f, 0.031f, 0.043f);
            TriggerGroup(c, p.Polymer, p.Metal, 0.01f, 0.08f, -0.038f, -0.007f);

            // Şarjör yuvası + düz metal şarjör.
            b.Box(p.Metal, new Vector3(0f, 0.004f, 0.135f), new Vector3(0.034f, 0.03f, 0.075f));
            Magazine(c, p.Metal, p.Metal, new Vector3(0f, -0.006f, 0.135f), 0.028f, 0.07f, 0.16f, 2f, 7f, 2);

            // Kurma tüpü + kolu.
            b.Cylinder(p.Metal, new Vector3(0f, 0.075f, 0.2f), new Vector3(0f, 0.075f, 0.535f), 0.0105f, 8);
            b.Box(p.Metal, new Vector3(-0.02f, 0.076f, 0.235f), new Vector3(0.026f, 0.007f, 0.008f), new Vector3(0f, -15f, 0f));
            b.Box(p.Polymer, new Vector3(-0.033f, 0.076f, 0.232f), new Vector3(0.008f, 0.01f, 0.012f));

            // Yeşil el kundağı (havalandırma delikli).
            b.Taper(p.Olive, new Vector3(0f, 0.035f, 0.205f), new Vector2(0.056f, 0.052f), new Vector3(0f, 0.035f, 0.45f), new Vector2(0.05f, 0.046f));
            for (var i = 0; i < 4; i++)
            {
                var z = 0.25f + i * 0.05f;
                b.Box(p.Polymer, new Vector3(0.027f, 0.035f, z), new Vector3(0.002f, 0.012f, 0.02f));
                b.Box(p.Polymer, new Vector3(-0.027f, 0.035f, z), new Vector3(0.002f, 0.012f, 0.02f));
            }

            Barrel(c, p.Metal, bore, 0.45f, 0.69f, 0.0105f);

            // Korumalı arpacık (tepesi y = 0.092).
            b.Box(p.Metal, new Vector3(0f, 0.068f, 0.545f), new Vector3(0.028f, 0.03f, 0.035f));
            b.Box(p.Metal, new Vector3(0f, 0.05f, 0.545f), new Vector3(0.02f, 0.02f, 0.02f));
            b.Box(p.Metal, new Vector3(0.0115f, 0.096f, 0.545f), new Vector3(0.003f, 0.026f, 0.016f));
            b.Box(p.Metal, new Vector3(-0.0115f, 0.096f, 0.545f), new Vector3(0.003f, 0.026f, 0.016f));
            b.Box(p.Metal, new Vector3(0f, 0.1105f, 0.545f), new Vector3(0.026f, 0.003f, 0.016f));
            b.Box(p.Metal, new Vector3(0f, 0.0875f, 0.545f), new Vector3(0.003f, 0.009f, 0.004f));

            FlashHider(c, bore, 0.69f, 0.745f, 0.0135f);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.748f));

            // Tamburlu delikli gez.
            b.Box(p.Metal, new Vector3(0f, 0.073f, -0.07f), new Vector3(0.024f, 0.008f, 0.03f));
            b.Cylinder(p.Metal, new Vector3(-0.013f, 0.074f, -0.07f), new Vector3(0.013f, 0.074f, -0.07f), 0.0105f, 8);
            b.Tube(p.Metal, new Vector3(0f, 0.092f, -0.075f), new Vector3(0f, 0.092f, -0.067f), 0.0105f, 0.0058f, 10);
            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, 0.092f, -0.075f));

            // Ahşap sabit dipçik.
            b.Taper(p.Wood, new Vector3(0f, -0.004f, -0.4f), new Vector2(0.044f, 0.13f), new Vector3(0f, 0.032f, -0.1f), new Vector2(0.038f, 0.062f));
            b.Box(p.Rubber, new Vector3(0f, -0.004f, -0.408f), new Vector3(0.046f, 0.134f, 0.016f));

            c.Bp.EyeRelief = 0.11f;
            RightGripAnchor(c, gripTop, 15f, 0.031f);
            LeftSupportAnchor(c, 0.33f, 0.01f);
        }

        /// <summary>MKE KNT-76: 7,62 nişancı tüfeği; kum rengi, ağır namlu, namlu freni, 3x dürbün, katlı bipod.</summary>
        private static void BuildKnt76(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.035f;
            var gripTop = new Vector3(0f, -0.012f, 0f);

            ArReceiver(c, -0.07f, 0.2f, 0.135f, 0.085f, 0.032f);
            Rail(c, p.Metal, -0.068f, 0.198f, 0.065f);

            b.Cylinder(p.Tan, new Vector3(0f, 0.037f, 0.2f), new Vector3(0f, 0.037f, 0.56f), 0.031f, 8);
            Rail(c, p.Metal, 0.205f, 0.4f, 0.0655f);
            for (var i = 0; i < 4; i++)
            {
                var z = 0.26f + i * 0.07f;
                b.Box(p.Polymer, new Vector3(0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
                b.Box(p.Polymer, new Vector3(-0.0287f, 0.037f, z), new Vector3(0.003f, 0.009f, 0.03f));
            }

            Barrel(c, p.Metal, bore, 0.56f, 0.745f, 0.0115f);
            b.Cylinder(p.Metal, new Vector3(0f, bore, 0.745f), new Vector3(0f, bore, 0.795f), 0.0145f, 6);
            b.Box(p.DarkMetal, new Vector3(0.0128f, bore, 0.762f), new Vector3(0.003f, 0.007f, 0.01f));
            b.Box(p.DarkMetal, new Vector3(-0.0128f, bore, 0.762f), new Vector3(0.003f, 0.007f, 0.01f));
            b.Box(p.DarkMetal, new Vector3(0.0128f, bore, 0.78f), new Vector3(0.003f, 0.007f, 0.01f));
            b.Box(p.DarkMetal, new Vector3(-0.0128f, bore, 0.78f), new Vector3(0.003f, 0.007f, 0.01f));
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.798f));

            Bipod(c, 0.5f, 0.0084f, 0.2f, 0.013f);

            PistolGrip(c, p.Polymer, gripTop, 18f, 0.102f, 0.03f, 0.042f);
            TriggerGroup(c, p.Metal, p.Metal, 0.012f, 0.078f, -0.042f, -0.014f);
            AdjustableStock(c, p.Tan, 0.03f, -0.05f, -0.27f, -0.33f, 0.132f, false);
            b.Box(p.Polymer, new Vector3(0f, 0.07f, -0.25f), new Vector3(0.036f, 0.016f, 0.12f));
            b.Box(p.Tan, new Vector3(0f, -0.055f, -0.29f), new Vector3(0.03f, 0.025f, 0.05f));

            Magazine(c, p.Polymer, p.Polymer, new Vector3(0f, -0.022f, 0.135f), 0.027f, 0.074f, 0.095f, 2f, 2f, 2);
            Scope(c, -0.05f, 0.215f, 0.12f, 0.0127f, 0.021f, 0.06f, 0.019f, 0.05f, 0.02f, 0.135f, 0.075f);
            c.Bp.EyeRelief = 0.09f;

            RightGripAnchor(c, gripTop, 18f, 0.03f);
            LeftSupportAnchor(c, 0.4f, 0.0084f);
        }

        /// <summary>MKE JNG-90 Bora-12: sürgülü keskin nişancı; uzun ağır namlu, büyük dürbün, başparmak delikli dipçik, bipod.</summary>
        private static void BuildJng90(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.04f;
            const float boltY = 0.046f;
            var gripTop = new Vector3(0f, -0.017f, 0f);

            // Şasi (kum rengi) + yuvarlak gövde + ray.
            b.Taper(p.Tan, new Vector3(0f, 0.012f, -0.085f), new Vector2(0.05f, 0.058f), new Vector3(0f, 0.02f, 0.53f), new Vector2(0.046f, 0.044f));
            b.Cylinder(p.Metal, new Vector3(0f, boltY, -0.08f), new Vector3(0f, boltY, 0.19f), 0.019f, 10);
            b.Box(p.DarkMetal, new Vector3(0.0185f, 0.05f, 0.04f), new Vector3(0.002f, 0.014f, 0.065f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(0.021f, 0.05f, 0.04f));
            Rail(c, p.Metal, -0.07f, 0.185f, 0.064f);

            // Kurma kolu grubu (pivot sürgü ekseninde).
            b.BeginGroup(WeaponModel.BoltPart, new Vector3(0f, boltY, -0.06f));
            b.Cylinder(p.Metal, new Vector3(0f, boltY, -0.115f), new Vector3(0f, boltY, -0.075f), 0.012f, 8);
            b.Cylinder(p.Metal, new Vector3(0f, boltY, -0.075f), new Vector3(0f, boltY, 0.06f), 0.0092f, 8);
            b.Cylinder(p.Metal, new Vector3(0.008f, boltY, -0.06f), new Vector3(0.054f, boltY - 0.023f, -0.074f), 0.0042f, 6);
            var knob = new Vector3(0.06f, boltY - 0.027f, -0.077f);
            b.Ellipsoid(p.Polymer, knob, new Vector3(0.011f, 0.011f, 0.011f), 3, 8);
            var boltHandRot = Quaternion.LookRotation(new Vector3(-0.45f, 0.05f, 1f), new Vector3(0.7f, 0.7f, 0f));
            var boltPalm = knob + new Vector3(0.02f, 0.006f, -0.018f);
            c.Anchor(WeaponModel.BoltHandAnchor, boltPalm - boltHandRot * new Vector3(0f, 0f, PalmCenter), boltHandRot, WeaponModel.BoltPart);
            b.EndGroup();
            c.Bp.BoltTravel = 0.085f;
            c.Bp.BoltLift = 62f;

            // Ağır, yivli namlu + namlu freni.
            b.Cylinder(p.Metal, new Vector3(0f, bore, 0.19f), new Vector3(0f, bore, 0.8f), 0.0128f, 0.0112f, 10);
            b.Box(p.DarkMetal, new Vector3(0f, bore + 0.0117f, 0.45f), new Vector3(0.003f, 0.0016f, 0.28f));
            b.Box(p.DarkMetal, new Vector3(0.0117f, bore, 0.45f), new Vector3(0.0016f, 0.003f, 0.28f));
            b.Box(p.DarkMetal, new Vector3(-0.0117f, bore, 0.45f), new Vector3(0.0016f, 0.003f, 0.28f));
            b.Cylinder(p.Metal, new Vector3(0f, bore, 0.8f), new Vector3(0f, bore, 0.865f), 0.016f, 6);
            for (var i = 0; i < 2; i++)
            {
                var z = 0.82f + i * 0.026f;
                b.Box(p.DarkMetal, new Vector3(0.0142f, bore, z), new Vector3(0.004f, 0.009f, 0.012f));
                b.Box(p.DarkMetal, new Vector3(-0.0142f, bore, z), new Vector3(0.004f, 0.009f, 0.012f));
            }

            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.868f));

            // Başparmak delikli dipçik.
            b.Box(p.Tan, new Vector3(0f, 0.052f, -0.235f), new Vector3(0.04f, 0.03f, 0.29f));
            b.Box(p.Tan, new Vector3(0f, -0.088f, -0.2f), new Vector3(0.038f, 0.026f, 0.34f));
            b.Box(p.Tan, new Vector3(0f, -0.012f, -0.36f), new Vector3(0.044f, 0.17f, 0.05f));
            b.Box(p.Rubber, new Vector3(0f, -0.012f, -0.392f), new Vector3(0.046f, 0.175f, 0.014f));
            b.Box(p.Polymer, new Vector3(0f, 0.076f, -0.25f), new Vector3(0.034f, 0.018f, 0.15f));
            b.Box(p.Metal, new Vector3(0f, 0.076f, -0.18f), new Vector3(0.008f, 0.012f, 0.012f));

            PistolGrip(c, p.Polymer, gripTop, 12f, 0.095f, 0.03f, 0.042f);
            TriggerGroup(c, p.Tan, p.Metal, 0.012f, 0.08f, -0.045f, -0.017f);

            Magazine(c, p.Polymer, p.Polymer, new Vector3(0f, -0.012f, 0.085f), 0.03f, 0.085f, 0.07f, 0f, 0f, 1);
            Bipod(c, 0.47f, -0.002f, 0.22f, 0.015f);
            Scope(c, -0.085f, 0.27f, 0.118f, 0.015f, 0.027f, 0.085f, 0.021f, 0.06f, 0.0f, 0.145f, 0.074f);
            c.Bp.EyeRelief = 0.09f;

            RightGripAnchor(c, gripTop, 12f, 0.03f);
            LeftSupportAnchor(c, 0.3f, -0.007f);
        }

        /// <summary>MKE PMT-76: 7,62 makineli tüfek; kutulu fişek şeridi, besleme kapağı, taşıma kolu, katlı bipod.</summary>
        private static void BuildPmt76(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.045f;
            var gripTop = new Vector3(0f, 0.004f, 0f);

            b.Box(p.Metal, new Vector3(0f, 0.04f, 0.06f), new Vector3(0.05f, 0.072f, 0.29f));
            b.Box(p.DarkMetal, new Vector3(0.0255f, 0.045f, 0.08f), new Vector3(0.002f, 0.03f, 0.12f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(0.028f, 0.045f, 0.08f));
            b.Box(p.DarkMetal, new Vector3(-0.0255f, 0.03f, 0.12f), new Vector3(0.002f, 0.02f, 0.05f));
            b.Box(p.Metal, new Vector3(0.03f, 0.05f, 0.01f), new Vector3(0.012f, 0.012f, 0.03f));

            // Besleme kapağı (ön menteşeli).
            b.BeginGroup(WeaponModel.CoverPart, new Vector3(0f, 0.076f, 0.2f));
            b.Box(p.Metal, new Vector3(0f, 0.084f, 0.08f), new Vector3(0.052f, 0.016f, 0.24f));
            for (var i = 0; i < 3; i++)
                b.Box(p.DarkMetal, new Vector3(0f, 0.0925f, 0.02f + i * 0.06f), new Vector3(0.044f, 0.002f, 0.008f));
            b.Box(p.Metal, new Vector3(0f, 0.084f, -0.045f), new Vector3(0.03f, 0.012f, 0.012f));
            // Sol el kapağın arka ucunu üstten kavrar (avuç aşağı, parmaklar öne).
            var coverHandRot = Quaternion.LookRotation(new Vector3(0.25f, -0.15f, 1f), Vector3.up);
            var coverPalm = new Vector3(-0.004f, 0.0925f + PalmHalfThickness + 0.001f, -0.01f);
            c.Anchor(WeaponModel.CoverHandAnchor, coverPalm - coverHandRot * new Vector3(0f, 0f, PalmCenter), coverHandRot, WeaponModel.CoverPart);
            b.EndGroup();

            // Gez.
            b.Box(p.Metal, new Vector3(0f, 0.081f, -0.068f), new Vector3(0.024f, 0.01f, 0.026f));
            IronRearNotch(c, p.Metal, -0.068f, 0.108f, 0.086f);

            // El kundağı, namlu, gaz tüpü, taşıma kolu.
            b.Box(p.Polymer, new Vector3(0f, 0.02f, 0.29f), new Vector3(0.05f, 0.04f, 0.17f));
            Barrel(c, p.Metal, bore, 0.205f, 0.72f, 0.012f);
            b.Cylinder(p.Metal, new Vector3(0f, 0.018f, 0.375f), new Vector3(0f, 0.018f, 0.56f), 0.009f, 8);
            b.Box(p.Metal, new Vector3(0f, 0.032f, 0.56f), new Vector3(0.026f, 0.04f, 0.025f));
            b.Box(p.Metal, new Vector3(0f, 0.065f, 0.3f), new Vector3(0.01f, 0.03f, 0.008f));
            b.Box(p.Metal, new Vector3(0f, 0.065f, 0.36f), new Vector3(0.01f, 0.03f, 0.008f));
            b.Box(p.Polymer, new Vector3(0f, 0.085f, 0.33f), new Vector3(0.016f, 0.016f, 0.1f));

            // Arpacık (tepesi 0.1055).
            b.Box(p.Metal, new Vector3(0f, 0.063f, 0.69f), new Vector3(0.022f, 0.016f, 0.02f));
            b.Box(p.Metal, new Vector3(0f, 0.088f, 0.69f), new Vector3(0.004f, 0.035f, 0.004f));
            b.Box(p.Metal, new Vector3(0.009f, 0.085f, 0.69f), new Vector3(0.003f, 0.028f, 0.012f));
            b.Box(p.Metal, new Vector3(-0.009f, 0.085f, 0.69f), new Vector3(0.003f, 0.028f, 0.012f));

            // Konik alev gizleyici.
            b.Cylinder(p.Metal, new Vector3(0f, bore, 0.72f), new Vector3(0f, bore, 0.785f), 0.0135f, 0.019f, 8);
            b.Cylinder(p.DarkMetal, new Vector3(0f, bore, 0.784f), new Vector3(0f, bore, 0.786f), 0.0175f, 8);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.788f));

            Bipod(c, 0.56f, 0.012f, 0.2f, 0.016f);

            // Dipçik + kabza + tetik.
            b.Taper(p.Polymer, new Vector3(0f, 0.012f, -0.37f), new Vector2(0.05f, 0.13f), new Vector3(0f, 0.04f, -0.085f), new Vector2(0.046f, 0.066f));
            b.Box(p.Rubber, new Vector3(0f, 0.012f, -0.378f), new Vector3(0.052f, 0.135f, 0.016f));
            PistolGrip(c, p.Polymer, gripTop, 15f, 0.1f, 0.032f, 0.045f);
            TriggerGroup(c, p.Metal, p.Metal, 0.012f, 0.08f, -0.03f, 0.004f);

            // Fişek kutusu (Magazine grubu) + fişek şeridi.
            var boxTop = new Vector3(-0.01f, 0f, 0.15f);
            b.BeginGroup(WeaponModel.MagazinePart, boxTop);
            b.Box(p.Olive, new Vector3(-0.01f, -0.055f, 0.15f), new Vector3(0.065f, 0.11f, 0.11f));
            b.Box(p.Olive, new Vector3(-0.01f, 0.002f, 0.15f), new Vector3(0.068f, 0.006f, 0.114f));
            b.Box(p.Metal, new Vector3(-0.044f, -0.02f, 0.15f), new Vector3(0.004f, 0.02f, 0.03f));
            b.Box(p.Olive, new Vector3(-0.01f, -0.055f, 0.0955f), new Vector3(0.05f, 0.08f, 0.003f));
            b.Box(p.Brass, new Vector3(-0.03f, 0.03f, 0.135f), new Vector3(0.01f, 0.05f, 0.045f));
            for (var i = 0; i < 4; i++)
            {
                var z = 0.118f + i * 0.011f;
                b.Cylinder(p.Brass, new Vector3(-0.022f, 0.045f, z), new Vector3(-0.058f, 0.045f, z), 0.0045f, 0.003f, 6);
            }

            var boxHandRot = Quaternion.LookRotation(new Vector3(0.05f, 0.2f, 1f), Vector3.left);
            var boxPalm = new Vector3(-0.01f - 0.0325f - PalmHalfThickness - 0.001f, -0.06f, 0.15f);
            c.Anchor(WeaponModel.MagazineHandAnchor, boxPalm - boxHandRot * new Vector3(0f, 0f, PalmCenter), boxHandRot, WeaponModel.MagazinePart);
            b.EndGroup();
            c.Bp.MagazineEject = Vector3.down;

            c.Bp.EyeRelief = 0.13f;
            RightGripAnchor(c, gripTop, 15f, 0.032f);
            LeftSupportAnchor(c, 0.29f, 0f);
        }

        /// <summary>Sarsılmaz SAR 109T: kompakt 9 mm hafif makineli; kısa kundak, katlanır gez/arpacık, uzun düz şarjör.</summary>
        private static void BuildSar109(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.035f;
            var gripTop = new Vector3(0f, -0.012f, 0f);

            ArReceiver(c, -0.06f, 0.16f, 0.112f, 0.06f, 0.03f);
            Rail(c, p.Metal, -0.058f, 0.158f, 0.065f);

            b.Cylinder(p.Polymer, new Vector3(0f, 0.037f, 0.16f), new Vector3(0f, 0.037f, 0.31f), 0.031f, 8);
            Rail(c, p.Metal, 0.165f, 0.305f, 0.0655f);
            b.Box(p.DarkMetal, new Vector3(0.0287f, 0.037f, 0.235f), new Vector3(0.003f, 0.009f, 0.05f));
            b.Box(p.DarkMetal, new Vector3(-0.0287f, 0.037f, 0.235f), new Vector3(0.003f, 0.009f, 0.05f));

            Barrel(c, p.Metal, bore, 0.31f, 0.345f, 0.0085f);
            b.Cylinder(p.Metal, new Vector3(0f, bore, 0.345f), new Vector3(0f, bore, 0.375f), 0.0115f, 6);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.378f));

            IronRearAperture(c, p.Polymer, -0.04f, 0.095f, 0.075f, 0.0105f, 0.0056f);
            IronFrontPost(c, p.Polymer, 0.29f, 0.095f, 0.079f, false);

            PistolGrip(c, p.Polymer, gripTop, 18f, 0.095f, 0.029f, 0.04f);
            TriggerGroup(c, p.Metal, p.Metal, 0.01f, 0.07f, -0.04f, -0.014f);
            b.Cylinder(p.Metal, new Vector3(0f, 0.03f, -0.04f), new Vector3(0f, 0.03f, -0.2f), 0.014f, 8);
            b.Taper(p.Polymer, new Vector3(0f, 0.012f, -0.22f), new Vector2(0.038f, 0.11f), new Vector3(0f, 0.03f, -0.15f), new Vector2(0.034f, 0.05f));
            b.Box(p.Rubber, new Vector3(0f, 0.012f, -0.226f), new Vector3(0.04f, 0.115f, 0.012f));

            Magazine(c, p.Polymer, p.Polymer, new Vector3(0f, -0.022f, 0.112f), 0.021f, 0.034f, 0.17f, 3f, 3f, 2);
            c.Bp.EyeRelief = 0.12f;

            RightGripAnchor(c, gripTop, 18f, 0.029f);
            LeftSupportAnchor(c, 0.235f, 0.0084f);
        }

        /// <summary>SAR 9 (siyah gövde, metal kızak) / Canik TP9 (kum rengi gövde, siyah kızak) tabancaları.</summary>
        private static void BuildPistol(Ctx c, bool tp9)
        {
            var p = c.P;
            var b = c.B;
            var frame = tp9 ? p.Tan : p.Polymer;
            var slideMat = tp9 ? p.Polymer : p.Metal;
            const float rake = 20f;
            var gripTop = new Vector3(0f, -0.004f, 0f);

            b.Box(frame, new Vector3(0f, 0.004f, 0.055f), new Vector3(0.026f, 0.02f, 0.13f));
            b.Box(frame, new Vector3(0f, -0.009f, 0.1f), new Vector3(0.022f, 0.008f, 0.04f));
            b.Box(frame, new Vector3(0f, 0.005f, -0.022f), new Vector3(0.026f, 0.014f, 0.026f));
            PistolGrip(c, frame, gripTop, rake, 0.1f, 0.028f, 0.048f);
            TriggerGroup(c, frame, p.Metal, 0.012f, 0.07f, -0.03f, -0.006f);
            b.Box(p.Metal, new Vector3(0.0135f, 0.008f, 0.03f), new Vector3(0.002f, 0.006f, 0.012f));

            // Kızak (Slide grubu).
            b.BeginGroup(WeaponModel.SlidePart, new Vector3(0f, 0.028f, 0f));
            b.Box(slideMat, new Vector3(0f, 0.0285f, 0.07f), new Vector3(0.027f, 0.029f, 0.185f));
            b.Box(slideMat, new Vector3(0f, 0.044f, 0.07f), new Vector3(0.02f, 0.004f, 0.18f));
            for (var i = 0; i < 4; i++)
            {
                var z = -0.016f + i * 0.0065f;
                b.Box(p.DarkMetal, new Vector3(0.0137f, 0.03f, z), new Vector3(0.0012f, 0.02f, 0.0028f));
                b.Box(p.DarkMetal, new Vector3(-0.0137f, 0.03f, z), new Vector3(0.0012f, 0.02f, 0.0028f));
                if (tp9)
                {
                    var zf = 0.13f + i * 0.0065f;
                    b.Box(p.DarkMetal, new Vector3(0.0137f, 0.03f, zf), new Vector3(0.0012f, 0.02f, 0.0028f));
                    b.Box(p.DarkMetal, new Vector3(-0.0137f, 0.03f, zf), new Vector3(0.0012f, 0.02f, 0.0028f));
                }
            }

            b.Box(p.Metal, new Vector3(0.006f, 0.0462f, 0.065f), new Vector3(0.013f, 0.0012f, 0.035f));
            b.Box(p.Metal, new Vector3(0.0058f, 0.0465f, -0.012f), new Vector3(0.0062f, 0.007f, 0.008f));
            b.Box(p.Metal, new Vector3(-0.0058f, 0.0465f, -0.012f), new Vector3(0.0062f, 0.007f, 0.008f));
            b.Box(p.Metal, new Vector3(0f, 0.0465f, 0.152f), new Vector3(0.0035f, 0.007f, 0.005f));
            b.Cylinder(p.DarkMetal, new Vector3(0f, 0.028f, 0.1625f), new Vector3(0f, 0.028f, 0.164f), 0.0065f, 8);
            b.EndGroup();
            c.Bp.SlideTravel = 0.026f;

            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, 0.028f, 0.167f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(0.012f, 0.045f, 0.06f));
            c.Anchor(WeaponModel.SightAnchor, new Vector3(0f, 0.0485f, -0.016f));

            // Şarjör kabzanın içinde; yalnızca taban plakası görünür.
            Magazine(c, p.Metal, frame, gripTop + new Vector3(0f, -0.004f, 0f), 0.021f, 0.032f, 0.104f, -rake, 0f, 1);

            c.Bp.EyeRelief = 0.3f;
            RightGripAnchor(c, gripTop, rake, 0.028f);
            LeftPistolAnchor(c, gripTop, rake, 0.028f);
        }

        /// <summary>Escort pompalı: boru şarjör, kaburgalı pompa kundağı (Pump grubu), fiber arpacık, tabanca kabzalı dipçik.</summary>
        private static void BuildEscort(Ctx c)
        {
            var p = c.P;
            var b = c.B;
            const float bore = 0.045f;
            var gripTop = new Vector3(0f, -0.011f, 0f);

            b.Box(p.Metal, new Vector3(0f, 0.036f, 0.075f), new Vector3(0.036f, 0.058f, 0.21f));
            b.Box(p.DarkMetal, new Vector3(0.0182f, 0.046f, 0.085f), new Vector3(0.002f, 0.018f, 0.055f));
            c.Anchor(WeaponModel.EjectAnchor, new Vector3(0.021f, 0.046f, 0.085f));
            b.Box(p.DarkMetal, new Vector3(0f, 0.0065f, 0.12f), new Vector3(0.022f, 0.002f, 0.07f));
            Rail(c, p.Metal, 0.0f, 0.17f, 0.065f);

            Barrel(c, p.Metal, bore, 0.18f, 0.69f, 0.0115f, 10);
            c.Anchor(WeaponModel.MuzzleAnchor, new Vector3(0f, bore, 0.692f));
            b.Cylinder(p.Metal, new Vector3(0f, 0.014f, 0.18f), new Vector3(0f, 0.014f, 0.625f), 0.012f, 8);
            b.Cylinder(p.Metal, new Vector3(0f, 0.014f, 0.625f), new Vector3(0f, 0.014f, 0.64f), 0.0135f, 8);
            b.Box(p.Metal, new Vector3(0f, 0.03f, 0.6f), new Vector3(0.026f, 0.046f, 0.014f));

            IronRearAperture(c, p.Metal, 0.012f, 0.09f, 0.075f, 0.011f, 0.0058f);
            IronFrontPost(c, p.Metal, 0.67f, 0.09f, 0.057f, false);
            b.Box(p.Reticle, new Vector3(0f, 0.0885f, 0.6675f), new Vector3(0.0036f, 0.003f, 0.0015f));

            // Pompa (pivot boru şarjör ekseninde).
            b.BeginGroup(WeaponModel.PumpPart, new Vector3(0f, 0.016f, 0.37f));
            b.Cylinder(p.Polymer, new Vector3(0f, 0.016f, 0.29f), new Vector3(0f, 0.016f, 0.45f), 0.022f, 0.021f, 8);
            for (var z = 0.31f; z < 0.44f; z += 0.03f)
                b.Cylinder(p.Polymer, new Vector3(0f, 0.016f, z - 0.004f), new Vector3(0f, 0.016f, z + 0.004f), 0.0238f, 8);
            b.EndGroup();
            LeftSupportAnchor(c, 0.37f, 0.016f - 0.0203f, WeaponModel.PumpPart);
            c.Bp.PumpTravel = 0.085f;

            b.Box(p.Polymer, new Vector3(0f, -0.002f, 0.03f), new Vector3(0.032f, 0.018f, 0.11f));
            TriggerGroup(c, p.Polymer, p.Metal, 0.015f, 0.075f, -0.035f, -0.011f);
            PistolGrip(c, p.Polymer, gripTop, 20f, 0.1f, 0.03f, 0.045f);
            b.Taper(p.Polymer, new Vector3(0f, 0.012f, -0.34f), new Vector2(0.044f, 0.135f), new Vector3(0f, 0.035f, -0.03f), new Vector2(0.034f, 0.055f));
            b.Box(p.Rubber, new Vector3(0f, 0.012f, -0.348f), new Vector3(0.046f, 0.14f, 0.016f));

            c.Anchor(WeaponModel.LoadingPortAnchor, new Vector3(0f, 0.004f, 0.12f));
            c.Bp.EyeRelief = 0.12f;
            RightGripAnchor(c, gripTop, 20f, 0.03f);
        }
    }
}
