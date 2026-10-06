using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>
    /// Kirpi / Cobra prosedürel gövdelerine görsel detay: pahlı zırh plakaları, pencere çerçeveleri, aynalar, emissive farlar,
    /// çeki kancaları, antenler, rakam plakası ve özgün (TSK benzeri) stilize işaretler, jant + diş detayı, kule kalkanı.
    /// Hiçbir detay collider taşımaz; koltuk/kamera/gövde collider'ına dokunulmaz.
    /// </summary>
    public static class VehicleDetailKit
    {
        public struct Dims
        {
            public float HalfWidth;   // gövde yarı genişlik
            public float FrontZ;      // ön tampon z
            public float RearZ;       // arka z
            public float SideBottomY; // yan plaka alt
            public float SideTopY;    // yan plaka üst
            public float CabinFrontZ; // ön cam z
            public float CabinY;      // cam merkez y
            public float RoofY;       // tavan y
            public float WheelRadius;
        }

        public static readonly Dims KirpiDims = new Dims
        {
            HalfWidth = 1.175f, FrontZ = 2.7f, RearZ = -2.55f, SideBottomY = 0.7f, SideTopY = 1.7f, CabinFrontZ = 1.55f,
            CabinY = 2.05f, RoofY = 2.35f, WheelRadius = 0.48f
        };

        public static readonly Dims CobraDims = new Dims
        {
            HalfWidth = 1.05f, FrontZ = 2.35f, RearZ = -2.2f, SideBottomY = 0.65f, SideTopY = 1.5f, CabinFrontZ = 1.35f,
            CabinY = 1.85f, RoofY = 2.1f, WheelRadius = 0.42f
        };

        public static void Decorate(Transform root, Dims d, Transform[] wheels, Transform turretYaw, bool kirpi)
        {
            if (root == null || StructureKit.CollidersOnly)
                return;

            var detail = new GameObject("Detay").transform;
            detail.SetParent(root, false);

            var headlight = MaterialLibrary.Unlit(new Color(1f, 0.93f, 0.72f));
            var tail = MaterialLibrary.Unlit(new Color(0.9f, 0.08f, 0.05f));
            var amber = MaterialLibrary.Unlit(new Color(1f, 0.6f, 0.08f));

            for (var s = -1; s <= 1; s += 2)
            {
                var sx = s * (d.HalfWidth + 0.03f);

                // Pahlı yan zırh plakaları: orta gövdede iki panel + cıvata sırası
                var zMid = (d.FrontZ + d.RearZ) * 0.5f - 0.2f;
                var third = (d.FrontZ - d.RearZ) * 0.22f;
                for (var i = -1; i <= 0; i++)
                {
                    var pz = zMid + i * (third * 1.1f) - third * 0.15f;
                    Plate(detail, "ZirhPlaka", new Vector3(sx, (d.SideBottomY + d.SideTopY) * 0.5f, pz), third, d.SideTopY - d.SideBottomY - 0.15f, s, MaterialId.VehicleDark);
                }

                // Pencere çerçevesi (yan)
                Box(detail, "PencereCerceve", new Vector3(sx, d.CabinY + 0.28f, d.CabinFrontZ - 1.1f), new Vector3(0.05f, 0.05f, 1.5f), MaterialId.MetalDark);
                Box(detail, "PencereCerceve", new Vector3(sx, d.CabinY - 0.12f, d.CabinFrontZ - 1.1f), new Vector3(0.05f, 0.05f, 1.5f), MaterialId.MetalDark);
                Box(detail, "PencereCam", new Vector3(sx - s * 0.01f, d.CabinY + 0.08f, d.CabinFrontZ - 1.1f), new Vector3(0.03f, 0.3f, 1.4f), MaterialId.Windshield);

                // Ayna: kol + gövde + yansıtıcı
                var mx = s * (d.HalfWidth + 0.2f);
                Box(detail, "AynaKol", new Vector3(s * (d.HalfWidth + 0.1f), d.CabinY, d.CabinFrontZ - 0.1f), new Vector3(0.24f, 0.04f, 0.04f), MaterialId.MetalDark);
                Box(detail, "Ayna", new Vector3(mx, d.CabinY + 0.1f, d.CabinFrontZ - 0.1f), new Vector3(0.07f, 0.28f, 0.18f), MaterialId.VehicleDark);
                Box(detail, "AynaCam", new Vector3(mx - s * 0.04f, d.CabinY + 0.1f, d.CabinFrontZ - 0.1f), new Vector3(0.012f, 0.24f, 0.14f), MaterialId.Windshield);

                // Farlar (emissive) + koruma çerçevesi, sinyal, arka stop
                Lamp(detail, "Far", new Vector3(s * (d.HalfWidth - 0.3f), d.SideTopY - 0.15f, d.FrontZ + 0.01f), new Vector3(0.26f, 0.14f, 0.04f), headlight);
                Box(detail, "FarCerceve", new Vector3(s * (d.HalfWidth - 0.3f), d.SideTopY - 0.15f, d.FrontZ - 0.01f), new Vector3(0.32f, 0.2f, 0.03f), MaterialId.MetalDark);
                Lamp(detail, "Sinyal", new Vector3(s * (d.HalfWidth - 0.08f), d.SideTopY - 0.45f, d.FrontZ + 0.01f), new Vector3(0.12f, 0.08f, 0.04f), amber);
                Lamp(detail, "Stop", new Vector3(s * (d.HalfWidth - 0.25f), d.SideTopY - 0.3f, d.RearZ - 0.02f), new Vector3(0.2f, 0.14f, 0.04f), tail);
                Box(detail, "StopCerceve", new Vector3(s * (d.HalfWidth - 0.25f), d.SideTopY - 0.3f, d.RearZ + 0.0f), new Vector3(0.26f, 0.2f, 0.03f), MaterialId.MetalDark);

                // Çeki kancaları (ön/arka)
                TowHook(detail, new Vector3(s * 0.65f, d.SideBottomY - 0.05f, d.FrontZ + 0.1f), 1);
                TowHook(detail, new Vector3(s * 0.65f, d.SideBottomY - 0.05f, d.RearZ - 0.1f), -1);

                // Anten (kırbaç)
                Whip(detail, new Vector3(s * (d.HalfWidth - 0.35f), d.RoofY + (kirpi ? 0.1f : 0.05f), d.RearZ + 0.5f), kirpi ? 1.7f : 1.3f);

                // Yan işaret: kırmızı-beyaz stilize şerit (özgün, TSK benzeri ama kopya değil)
                Insignia(detail, new Vector3(sx, d.SideTopY - 0.45f, zMid - third * 0.9f), s);
            }

            // Rakam plakaları
            NumberPlate(detail, new Vector3(0f, d.SideBottomY + 0.18f, d.FrontZ + 0.04f), 1);
            NumberPlate(detail, new Vector3(0f, d.SideBottomY + 0.2f, d.RearZ - 0.04f), -1);

            // Kaput nervürleri + ön cam siperliği
            for (var i = 0; i < 3; i++)
                Box(detail, "KaputNervur", new Vector3(0f, d.CabinY - 0.5f, d.CabinFrontZ + 0.3f + i * 0.28f), new Vector3(d.HalfWidth * 1.1f, 0.03f, 0.05f), MaterialId.VehicleDark);
            Box(detail, "Siperlik", new Vector3(0f, d.CabinY + 0.35f, d.CabinFrontZ + 0.03f), new Vector3(d.HalfWidth * 1.5f, 0.04f, 0.18f), MaterialId.VehicleDark, Quaternion.Euler(-14f, 0f, 0f));

            // Teker jantı + diş detayı
            if (wheels != null)
                for (var i = 0; i < wheels.Length; i++)
                    DecorateWheel(wheels[i], i % 2 == 0 ? -1 : 1, d.WheelRadius);

            // Kule kalkanı detayı
            if (turretYaw != null)
                DecorateTurret(turretYaw);
        }

        // ------------------------------------------------------------------ parçalar

        private static void Plate(Transform parent, string name, Vector3 center, float length, float height, int side, MaterialId mat)
        {
            // Orta panel + iki pahlı uç (eğik küçük kutular)
            Box(parent, name, center, new Vector3(0.06f, height, length * 0.8f), mat);
            var bevelZ = length * 0.4f;
            Box(parent, name + "Pah", center + new Vector3(0f, 0f, bevelZ), new Vector3(0.05f, height * 0.9f, length * 0.12f), mat, Quaternion.Euler(0f, 0f, side * 10f));
            Box(parent, name + "Pah", center - new Vector3(0f, 0f, bevelZ), new Vector3(0.05f, height * 0.9f, length * 0.12f), mat, Quaternion.Euler(0f, 0f, -side * 10f));
            for (var k = -1; k <= 1; k += 2)
                for (var j = -1; j <= 1; j += 2)
                    Box(parent, "Civata", center + new Vector3(side * 0.035f, j * (height * 0.5f - 0.07f), k * (length * 0.4f - 0.04f)), new Vector3(0.03f, 0.05f, 0.05f), MaterialId.MetalDark);
        }

        private static void Insignia(Transform parent, Vector3 pos, int side)
        {
            // Kırmızı yama + beyaz yıldız yerine üç bar (özgün stilize)
            var p = pos + new Vector3(side * 0.01f, 0f, 0f);
            Box(parent, "IsaretZemin", p, new Vector3(0.03f, 0.32f, 0.5f), MaterialId.TurkishFlag);
            Box(parent, "IsaretSerit", p + new Vector3(0f, -0.2f, 0f), new Vector3(0.025f, 0.04f, 0.6f), MaterialId.White);
            Box(parent, "IsaretSerit", p + new Vector3(0f, 0.2f, 0f), new Vector3(0.025f, 0.04f, 0.6f), MaterialId.Black);
        }

        private static void NumberPlate(Transform parent, Vector3 pos, int facing)
        {
            Box(parent, "PlakaCerceve", pos, new Vector3(0.54f, 0.16f, 0.03f), MaterialId.MetalDark);
            Box(parent, "Plaka", pos + new Vector3(0f, 0f, facing * 0.02f), new Vector3(0.5f, 0.12f, 0.012f), MaterialId.White);
            for (var i = 0; i < 5; i++)
                Box(parent, "PlakaRakam", pos + new Vector3(-0.19f + i * 0.095f, 0f, facing * 0.03f), new Vector3(0.04f, i % 2 == 0 ? 0.08f : 0.06f, 0.01f), MaterialId.Black);
        }

        private static void TowHook(Transform parent, Vector3 pos, int facing)
        {
            Box(parent, "KancaTaban", pos, new Vector3(0.16f, 0.14f, 0.1f), MaterialId.MetalDark);
            var ring = StructureKit.CreateCylinder(parent, "KancaHalka", pos + new Vector3(0f, 0f, facing * 0.12f), 0.07f, 0.03f, MaterialId.GunMetal, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void Whip(Transform parent, Vector3 basePos, float height)
        {
            Box(parent, "AntenTaban", basePos, new Vector3(0.1f, 0.1f, 0.1f), MaterialId.VehicleDark);
            var rod = StructureKit.CreateCylinder(parent, "Anten", basePos + new Vector3(0f, height * 0.5f, 0f), 0.01f, height, MaterialId.Black, false);
            rod.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
        }

        private static void DecorateWheel(Transform wheel, int side, float radius)
        {
            if (wheel == null)
                return;
            var sc = wheel.localScale;
            if (sc.x < 0.001f || sc.y < 0.001f || sc.z < 0.001f)
                return;

            // Tekerlek yerel Y ekseni dingil; dış yüz = yerel +Y (dış taraf: side'a göre) — iki yüze de detay
            for (var f = -1; f <= 1; f += 2)
            {
                var faceY = f * 0.5f; // birim silindir yarısı
                WheelPart(wheel, "JantDudak", new Vector3(0f, faceY + f * 0.03f, 0f), radius * 0.66f, 0.06f, MaterialId.MetalDark);
                WheelPart(wheel, "JantIc", new Vector3(0f, faceY + f * 0.06f, 0f), radius * 0.45f, 0.06f, MaterialId.Black);
                WheelPart(wheel, "JantGobek", new Vector3(0f, faceY + f * 0.1f, 0f), radius * 0.16f, 0.08f, MaterialId.GunMetal);
                for (var k = 0; k < 6; k++)
                {
                    var a = k / 6f * Mathf.PI * 2f;
                    var lug = StructureKit.CreateBox(wheel, "Bijon", Vector3.zero, new Vector3(1f, 1f, 1f), Quaternion.identity, MaterialId.GunMetal, false);
                    PlaceOnWheel(lug.transform, new Vector3(Mathf.Cos(a) * radius * 0.3f, faceY + f * 0.1f * 0.9f, Mathf.Sin(a) * radius * 0.3f), new Vector3(0.05f, 0.05f, 0.05f), sc);
                }
            }

            // Diş blokları (çevre)
            const int treads = 14;
            for (var k = 0; k < treads; k++)
            {
                var a = k / (float)treads * Mathf.PI * 2f;
                var t = StructureKit.CreateBox(wheel, "Dis", Vector3.zero, Vector3.one, Quaternion.identity, MaterialId.Tire, false);
                PlaceOnWheel(t.transform, new Vector3(Mathf.Cos(a) * (radius + 0.015f), 0f, Mathf.Sin(a) * (radius + 0.015f)), new Vector3(0.12f, 0.26f, 0.07f), sc);
                // Diş yönü: radyal dışa bakacak şekilde y ekseni etrafında döndür
                t.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
        }

        private static void WheelPart(Transform wheel, string name, Vector3 unitPos, float radius, float thickness, MaterialId mat)
        {
            var sc = wheel.localScale;
            var go = StructureKit.CreateCylinder(wheel, name, Vector3.zero, 0.5f, 1f, mat, false);
            PlaceOnWheel(go.transform, unitPos, new Vector3(radius * 2f, thickness, radius * 2f), sc);
        }

        /// <summary>Dünya ölçüsünde (tekerlek birimi) istenen konum/boyutu üst ölçeğe bölerek yerel uzaya çevirir. unitPos.y birim silindir (-0.5..0.5) cinsindendir.</summary>
        private static void PlaceOnWheel(Transform t, Vector3 pos, Vector3 size, Vector3 parentScale)
        {
            // pos.x/z gerçek metre; pos.y birim silindir yüksekliği çarpanı (parent ölçeğiyle zaten çarpılır)
            t.localPosition = new Vector3(pos.x / parentScale.x, pos.y, pos.z / parentScale.z);
            t.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
        }

        private static void DecorateTurret(Transform yaw)
        {
            // Pahlı kalkan çerçevesi, görüş yarığı camı, cıvatalar, nişan dürbünü
            Box(yaw, "KalkanCerceve", new Vector3(0f, 0.57f, 0.42f), new Vector3(0.95f, 0.04f, 0.05f), MaterialId.MetalDark, Quaternion.Euler(-14f, 0f, 0f));
            Box(yaw, "KalkanPahSol", new Vector3(-0.5f, 0.3f, 0.32f), new Vector3(0.05f, 0.5f, 0.22f), MaterialId.VehicleDark, Quaternion.Euler(0f, -25f, 0f));
            Box(yaw, "KalkanPahSag", new Vector3(0.5f, 0.3f, 0.32f), new Vector3(0.05f, 0.5f, 0.22f), MaterialId.VehicleDark, Quaternion.Euler(0f, 25f, 0f));
            Box(yaw, "KalkanCam", new Vector3(0f, 0.38f, 0.425f), new Vector3(0.4f, 0.08f, 0.012f), MaterialId.Windshield);
            for (var s = -1; s <= 1; s += 2)
                Box(yaw, "KalkanCivata", new Vector3(s * 0.4f, 0.12f, 0.425f), new Vector3(0.04f, 0.04f, 0.02f), MaterialId.GunMetal);
            Box(yaw, "KuleAnten", new Vector3(-0.35f, 0.55f, -0.1f), new Vector3(0.015f, 0.5f, 0.015f), MaterialId.Black);
        }

        // ------------------------------------------------------------------ yardımcılar

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, MaterialId mat)
        {
            return StructureKit.CreateBox(parent, name, pos, size, Quaternion.identity, mat, false);
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, MaterialId mat, Quaternion rot)
        {
            return StructureKit.CreateBox(parent, name, pos, size, rot, mat, false);
        }

        private static void Lamp(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
        {
            var go = StructureKit.CreateBox(parent, name, pos, size, Quaternion.identity, MaterialId.White, false);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && material != null)
                mr.sharedMaterial = material;
        }
    }
}
