using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// Kirpi / T-70 görsel detay eklentileri (zırh plakaları, aynalar, ışıklar, çeki kancaları, rakam plakaları, TSK tarzı
    /// özgün işaretler, jant ve diş detayı, rotor göbeği). Yalnızca mesh üretir; collider/koltuk/pivot'lara dokunmaz.
    /// </summary>
    internal static class TransportDetailKit
    {
        /// <summary>Eğik kenarlı zırh plakası (iki yan pahlı); x ekseni boyunca ince, z-y düzleminde pahlı çokgen.</summary>
        public static void BeveledPlateX(TransportMeshBuilder b, int slot, float x, float thickness, float z0, float z1, float y0, float y1, float bevel)
        {
            bevel = Mathf.Min(bevel, Mathf.Min((z1 - z0), (y1 - y0)) * 0.45f);
            var profile = new[]
            {
                new Vector2(z0 + bevel, y0), new Vector2(z1 - bevel, y0), new Vector2(z1, y0 + bevel), new Vector2(z1, y1 - bevel),
                new Vector2(z1 - bevel, y1), new Vector2(z0 + bevel, y1), new Vector2(z0, y1 - bevel), new Vector2(z0, y0 + bevel)
            };
            b.PrismX(slot, profile, x - thickness * 0.5f, x + thickness * 0.5f);
        }

        /// <summary>Plaka dört köşe cıvatası.</summary>
        public static void Bolts(TransportMeshBuilder b, int slot, float x, float z0, float z1, float y0, float y1, int side)
        {
            const float inset = 0.07f;
            var bx = x + side * 0.012f;
            b.Box(slot, new Vector3(bx, y0 + inset, z0 + inset), new Vector3(0.03f, 0.04f, 0.04f));
            b.Box(slot, new Vector3(bx, y0 + inset, z1 - inset), new Vector3(0.03f, 0.04f, 0.04f));
            b.Box(slot, new Vector3(bx, y1 - inset, z0 + inset), new Vector3(0.03f, 0.04f, 0.04f));
            b.Box(slot, new Vector3(bx, y1 - inset, z1 - inset), new Vector3(0.03f, 0.04f, 0.04f));
        }

        /// <summary>Düz renkli rakam plakası: koyu zemin + açık şerit + sembolik bloklar (gerçek yazı değil).</summary>
        public static void NumberPlateZ(TransportMeshBuilder b, int plate, int ink, int back, Vector3 center, float width, float height, int facing)
        {
            var z = center.z;
            b.Box(back, center, new Vector3(width + 0.04f, height + 0.04f, 0.025f));
            b.Box(plate, new Vector3(center.x, center.y, z + facing * 0.014f), new Vector3(width, height, 0.012f));
            var step = width / 7f;
            for (var i = 0; i < 5; i++)
            {
                var h = height * (i % 2 == 0 ? 0.6f : 0.42f);
                b.Box(ink, new Vector3(center.x - width * 0.38f + i * step * 1.5f, center.y, z + facing * 0.022f), new Vector3(step * 0.55f, h, 0.01f));
            }
        }

        /// <summary>Özgün stilize işaret: bayrak yaması + iki şerit (TSK benzeri ama kopya değil).</summary>
        public static void InsigniaX(TransportMeshBuilder b, int flag, int stripe, Vector3 center, float width, float height, int side)
        {
            var x = center.x + side * 0.006f;
            var hw = width * 0.5f;
            var hh = height * 0.5f;
            b.QuadUv(flag,
                new Vector3(x, center.y - hh, center.z - hw), new Vector3(x, center.y - hh, center.z + hw),
                new Vector3(x, center.y + hh, center.z + hw), new Vector3(x, center.y + hh, center.z - hw),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector3(side, 0f, 0f));
            b.Box(stripe, new Vector3(x, center.y - hh - 0.05f, center.z), new Vector3(0.01f, 0.03f, width * 1.15f));
            b.Box(stripe, new Vector3(x, center.y + hh + 0.05f, center.z), new Vector3(0.01f, 0.03f, width * 1.15f));
        }

        public static void TowHookZ(TransportMeshBuilder b, int metal, int dark, Vector3 pos, int facing)
        {
            b.Box(dark, pos, new Vector3(0.16f, 0.14f, 0.1f));
            var ringCenter = pos + new Vector3(0f, 0f, facing * 0.12f);
            for (var k = 0; k < 8; k++)
            {
                var a0 = k / 8f * Mathf.PI * 2f;
                var a1 = (k + 1) / 8f * Mathf.PI * 2f;
                b.CylinderBetween(metal, ringCenter + new Vector3(Mathf.Cos(a0) * 0.07f, Mathf.Sin(a0) * 0.07f, 0f),
                    ringCenter + new Vector3(Mathf.Cos(a1) * 0.07f, Mathf.Sin(a1) * 0.07f, 0f), 0.014f, 4, false);
            }
        }

        public static void WhipAntenna(TransportMeshBuilder b, int baseSlot, int rod, Vector3 pos, float height, float lean)
        {
            b.Cylinder(baseSlot, pos + Vector3.up * 0.04f, Quaternion.identity, 0.06f, 0.045f, 0.08f, 8);
            b.CylinderBetween(rod, pos + Vector3.up * 0.08f, pos + new Vector3(0f, height, lean), 0.009f, 4, false);
            b.Sphere(rod, pos + new Vector3(0f, height, lean), new Vector3(0.018f, 0.018f, 0.018f), 4, 3);
        }

        // ------------------------------------------------------------------ KİRPİ

        public static void KirpiBody(TransportMeshBuilder b)
        {
            const int Paint = KirpiModel.Paint, Metal = KirpiModel.Metal, Dark = KirpiModel.Dark, Glass = KirpiModel.Glass;
            const int Flag = KirpiModel.Flag, Black = KirpiModel.Black, Light = KirpiModel.Light, Red = KirpiModel.RedLight;

            for (var side = -1; side <= 1; side += 2)
            {
                var px = side * 1.285f;

                // Yan zırh plakaları (kapı altı hizası; pencerelerin altında)
                float[] z0 = { -3.0f, -1.7f, -0.4f };
                float[] z1 = { -1.8f, -0.5f, 0.75f };
                for (var i = 0; i < 3; i++)
                {
                    BeveledPlateX(b, Paint, px, 0.05f, z0[i], z1[i], 1.4f, 1.98f, 0.14f);
                    Bolts(b, Metal, px, z0[i], z1[i], 1.4f, 1.98f, side);
                }

                // Pencere çerçeveleri (cam kenarı)
                b.Box(Dark, new Vector3(side * 1.275f, 2.1f, -1.2f), new Vector3(0.04f, 0.05f, 1.6f));
                b.Box(Dark, new Vector3(side * 1.275f, 2.53f, -1.2f), new Vector3(0.04f, 0.05f, 1.6f));

                // Ayna: kol + gövde + cam
                var mx = side * 1.42f;
                b.CylinderBetween(Metal, new Vector3(side * 1.22f, 2.05f, 1.45f), new Vector3(mx, 2.2f, 1.5f), 0.02f, 4, false);
                b.Box(Dark, new Vector3(mx, 2.25f, 1.5f), new Vector3(0.06f, 0.3f, 0.2f));
                b.Box(Glass, new Vector3(mx - side * 0.034f, 2.25f, 1.5f), new Vector3(0.01f, 0.26f, 0.16f));

                // Işıklar: sinyal (turuncu yerine Light), kenar pozisyon, arka stop/ters
                b.Box(Light, new Vector3(side * 1.12f, 1.12f, 3.36f), new Vector3(0.14f, 0.07f, 0.04f));
                b.Box(Red, new Vector3(side * 1.05f, 1.35f, -3.34f), new Vector3(0.2f, 0.12f, 0.04f));
                b.Box(Red, new Vector3(side * 1.05f, 1.55f, -3.34f), new Vector3(0.2f, 0.06f, 0.04f));
                b.Box(Metal, new Vector3(side * 1.05f, 1.45f, -3.33f), new Vector3(0.26f, 0.32f, 0.025f));
                b.Box(Red, new Vector3(side * 1.255f, 1.3f, 3.0f), new Vector3(0.03f, 0.05f, 0.12f));

                // Far koruma ızgarası
                for (var k = -1; k <= 1; k++)
                    b.Box(Dark, new Vector3(side * 0.85f + k * 0.08f, 1.48f, 3.425f), new Vector3(0.02f, 0.2f, 0.02f));

                // Tow kancası (ön/arka)
                TowHookZ(b, Metal, Dark, new Vector3(side * 0.7f, 0.98f, 3.55f), 1);
                TowHookZ(b, Metal, Dark, new Vector3(side * 0.7f, 1.0f, -3.4f), -1);

                // Çamurluk basamak ve el tutamağı
                b.Box(Metal, new Vector3(side * 1.31f, 1.12f, 0.0f), new Vector3(0.12f, 0.03f, 0.5f));
                b.CylinderBetween(Metal, new Vector3(side * 1.3f, 2.25f, 0.55f), new Vector3(side * 1.3f, 2.25f, 0.95f), 0.015f, 4, false);

                // Yan işaret (bayrak yaması) ve şerit
                InsigniaX(b, Flag, Dark, new Vector3(side * 1.288f, 1.2f, -2.1f), 0.5f, 0.33f, side);

                // Anten (kısa kırbaç) + dengeleyici
                WhipAntenna(b, Dark, Black, new Vector3(side * 0.7f, 2.9f, -3.0f), 1.3f, side * -0.1f);
            }

            // Rakam plakaları
            NumberPlateZ(b, Light, Black, Metal, new Vector3(0f, 0.9f, 3.5f), 0.5f, 0.12f, 1);
            NumberPlateZ(b, Light, Black, Metal, new Vector3(0f, 0.8f, -3.5f), 0.5f, 0.12f, -1);

            // Kaput üstü zırh nervürleri
            for (var i = 0; i < 3; i++)
                b.Box(Paint, new Vector3(0f, 1.99f, 1.9f + i * 0.45f), new Vector3(1.2f, 0.03f, 0.06f));

            // Ön cam siperliği
            b.Box(Dark, new Vector3(0f, RoofYVisor, 0.98f), new Vector3(2.0f, 0.04f, 0.2f), Quaternion.Euler(-12f, 0f, 0f));
        }

        private const float RoofYVisor = 2.88f;

        public static void KirpiWheel(TransportMeshBuilder b)
        {
            const int Metal = KirpiModel.Metal, Dark = KirpiModel.Dark, Tire = KirpiModel.Tire, Black = KirpiModel.Black;
            var axis = Quaternion.Euler(0f, 0f, 90f);
            for (var face = -1; face <= 1; face += 2)
            {
                // Jant dudağı + iç halka
                b.Cylinder(Metal, new Vector3(face * 0.215f, 0f, 0f), axis, 0.33f, 0.33f, 0.02f, 14);
                b.Cylinder(Black, new Vector3(face * 0.225f, 0f, 0f), axis, 0.22f, 0.22f, 0.02f, 12);
                b.Cylinder(Metal, new Vector3(face * 0.24f, 0f, 0f), axis, 0.12f, 0.12f, 0.03f, 8);
                // Yan duvar kabartması
                b.Cylinder(Tire, new Vector3(face * 0.2f, 0f, 0f), axis, WheelRadiusInner, WheelRadiusInner, 0.03f, 14);
                // Jant kolları
                for (var k = 0; k < 5; k++)
                {
                    var rot = Quaternion.Euler(k * 72f, 0f, 0f);
                    b.Box(Dark, rot * new Vector3(face * 0.23f, 0.2f, 0f), new Vector3(0.025f, 0.12f, 0.05f), rot);
                }
            }

            // Çift sıra diş blokları (omuz) + yan diş
            for (var k = 0; k < 14; k++)
            {
                var rot = Quaternion.Euler(k * (360f / 14f) + 8f, 0f, 0f);
                b.Box(Tire, rot * new Vector3(-0.17f, KirpiModel.WheelRadius + 0.004f, 0f), new Vector3(0.1f, 0.03f, 0.08f), rot);
                b.Box(Tire, rot * new Vector3(0.17f, KirpiModel.WheelRadius + 0.004f, 0f), new Vector3(0.1f, 0.03f, 0.08f), rot);
                var side = Quaternion.Euler(k * (360f / 14f), 0f, 0f);
                b.Box(Tire, side * new Vector3(-0.215f, 0.5f, 0f), new Vector3(0.02f, 0.1f, 0.05f), side);
                b.Box(Tire, side * new Vector3(0.215f, 0.5f, 0f), new Vector3(0.02f, 0.1f, 0.05f), side);
            }
        }

        private const float WheelRadiusInner = 0.46f;

        public static void KirpiTurret(TransportMeshBuilder b)
        {
            const int Paint = KirpiModel.Paint, Metal = KirpiModel.Metal, Dark = KirpiModel.Dark, Glass = KirpiModel.Glass, Black = KirpiModel.Black;

            // Pahlı kalkan çerçevesi ve görüş yarığı
            b.Box(Dark, new Vector3(0f, 0.3f, 0.44f), new Vector3(1.04f, 0.04f, 0.03f));
            b.Box(Paint, new Vector3(-0.27f, 0.77f, 0.42f), new Vector3(0.44f, 0.05f, 0.05f), Quaternion.Euler(-14f, 0f, 0f));
            b.Box(Paint, new Vector3(0.27f, 0.77f, 0.42f), new Vector3(0.44f, 0.05f, 0.05f), Quaternion.Euler(-14f, 0f, 0f));
            b.Box(Glass, new Vector3(-0.27f, 0.58f, 0.445f), new Vector3(0.28f, 0.08f, 0.01f));
            b.Box(Glass, new Vector3(0.27f, 0.58f, 0.445f), new Vector3(0.28f, 0.08f, 0.01f));
            b.Box(Dark, new Vector3(-0.27f, 0.645f, 0.447f), new Vector3(0.3f, 0.02f, 0.015f));
            b.Box(Dark, new Vector3(0.27f, 0.645f, 0.447f), new Vector3(0.3f, 0.02f, 0.015f));

            // Cıvatalar
            for (var s = -1; s <= 1; s += 2)
                for (var r = 0; r < 2; r++)
                    b.Box(Metal, new Vector3(s * 0.45f, 0.35f + r * 0.28f, 0.445f), new Vector3(0.035f, 0.035f, 0.02f));

            // Nişan dürbünü ve mühimmat sandığı
            b.Cylinder(Black, new Vector3(-0.1f, 0.62f, 0.05f), Quaternion.Euler(90f, 0f, 0f), 0.035f, 0.035f, 0.16f, 6);
            b.Box(Dark, new Vector3(-0.2f, 0.4f, 0.06f), new Vector3(0.14f, 0.16f, 0.22f));
        }

        // ------------------------------------------------------------------ T-70

        public static void HeliBody(TransportMeshBuilder b)
        {
            const int Paint = 0, Metal = 1, Dark = 3, Flag = 5, Black = 6, NavRed = 7, NavGreen = 8;

            for (var side = -1; side <= 1; side += 2)
            {
                // Silah/yakıt sponsonları (iniş takımı dikmesi üstü) + yan dış panel
                b.Box(Paint, new Vector3(side * 1.3f, 0.82f, 0.2f), new Vector3(0.3f, 0.2f, 2.0f));
                b.Sphere(Paint, new Vector3(side * 1.3f, 0.82f, 1.25f), new Vector3(0.15f, 0.1f, 0.22f), 8, 4);
                b.Sphere(Paint, new Vector3(side * 1.3f, 0.82f, -0.85f), new Vector3(0.15f, 0.1f, 0.22f), 8, 4);
                b.Box(Dark, new Vector3(side * 1.456f, 0.82f, 0.2f), new Vector3(0.012f, 0.05f, 1.6f));

                // Kokpit pencere çerçevesi (yan ön)
                var gx = side * 1.17f;
                b.Box(Dark, new Vector3(gx, 1.54f, 2.075f), new Vector3(0.03f, 0.04f, 0.5f));
                b.Box(Dark, new Vector3(gx, 2.07f, 2.075f), new Vector3(0.03f, 0.04f, 0.5f));
                b.Box(Dark, new Vector3(gx, 1.8f, 1.83f), new Vector3(0.03f, 0.55f, 0.04f));
                b.Box(Dark, new Vector3(gx, 1.8f, 2.32f), new Vector3(0.03f, 0.55f, 0.04f));

                // Kapı penceresi çerçevesi
                var dx = side * 1.26f;
                b.Box(Dark, new Vector3(dx, 1.6f, -2.325f), new Vector3(0.02f, 0.04f, 0.7f));
                b.Box(Dark, new Vector3(dx, 2.07f, -2.325f), new Vector3(0.02f, 0.04f, 0.7f));
                b.Box(Dark, new Vector3(dx, 1.83f, -1.99f), new Vector3(0.02f, 0.5f, 0.04f));
                b.Box(Dark, new Vector3(dx, 1.83f, -2.67f), new Vector3(0.02f, 0.5f, 0.04f));

                // Kapı kolu, yan pozisyon ışığı, sert nokta
                b.Box(Metal, new Vector3(side * 1.26f, 1.3f, -2.9f), new Vector3(0.03f, 0.05f, 0.18f));
                b.Box(side < 0 ? NavRed : NavGreen, new Vector3(side * 1.19f, 2.58f, 2.4f), new Vector3(0.04f, 0.04f, 0.1f));
                b.CylinderBetween(Metal, new Vector3(side * 1.18f, 1.0f, 2.2f), new Vector3(side * 1.45f, 1.0f, 2.2f), 0.04f, 6);
                b.CylinderBetween(Metal, new Vector3(side * 1.45f, 1.0f, 2.2f), new Vector3(side * 1.45f, 1.0f, 1.6f), 0.025f, 6, false);

                // Yan işaret yaması (kuyruk kirişi)
                InsigniaX(b, Flag, Dark, new Vector3(side * 0.36f, 2.35f, -5.2f), 0.9f, 0.6f, side);

                // Burun yan zırh plakası
                BeveledPlateX(b, Paint, side * 1.0f, 0.04f, 3.0f, 4.2f, 0.7f, 1.15f, 0.12f);
            }

            // Kokpit cam üstü ışık çubuğu ve sensör topu
            b.Box(Black, new Vector3(0f, 1.09f, 4.7f), new Vector3(0.5f, 0.05f, 0.08f));
            b.Sphere(Dark, new Vector3(0f, 0.9f, 4.65f), new Vector3(0.2f, 0.18f, 0.2f), 8, 5);
            b.Sphere(Black, new Vector3(0f, 0.9f, 4.82f), new Vector3(0.1f, 0.1f, 0.06f), 6, 4);

            // Antenler: tavan çubukları, pervane, alt kırbaç
            WhipAntenna(b, Dark, Black, new Vector3(0.35f, 2.68f, 0.9f), 0.9f, -0.2f);
            WhipAntenna(b, Dark, Black, new Vector3(-0.3f, 2.68f, -2.4f), 0.8f, -0.25f);
            b.CylinderBetween(Black, new Vector3(0f, 0.7f, -1.9f), new Vector3(0f, 0.45f, -2.1f), 0.012f, 4, false);
            b.Box(Dark, new Vector3(0f, 2.28f, -6.2f), new Vector3(0.05f, 0.1f, 0.4f));

            // Kuyruk tamponu ve alt ışık
            b.Box(Metal, new Vector3(0f, 1.9f, -8.5f), new Vector3(0.1f, 0.12f, 0.2f));
            b.Box(NavRed, new Vector3(0f, 1.86f, -8.78f), new Vector3(0.06f, 0.06f, 0.04f));

            // Kapı üstü kanal, gövde kenar bandı ve kuyruk plakası
            b.Box(Paint, new Vector3(0f, 2.7f, 2.0f), new Vector3(1.5f, 0.05f, 0.4f));
            b.Box(Dark, new Vector3(0f, 0.62f, 4.1f), new Vector3(0.6f, 0.04f, 0.5f));

            // Tavan göbek kapağı ve ana rotor mili halkası
            b.Cylinder(Metal, new Vector3(0f, 3.32f, 0.2f), Quaternion.identity, 0.26f, 0.2f, 0.06f, 10);
            b.Cylinder(Dark, new Vector3(0f, 3.4f, 0.2f), Quaternion.identity, 0.22f, 0.22f, 0.04f, 10);
        }

        /// <summary>Ana rotor göbek detayı (çift halka, bağlantı çubukları, kanat kökleri). Slotlar: 0 kanat, 1 göbek, 2 uç.</summary>
        public static void HeliMainRotorHub(TransportMeshBuilder b)
        {
            const int Hub = 1;
            b.Cylinder(Hub, new Vector3(0f, 0.16f, 0f), Quaternion.identity, 0.34f, 0.3f, 0.06f, 10);
            b.Cylinder(Hub, new Vector3(0f, -0.04f, 0f), Quaternion.identity, 0.36f, 0.36f, 0.05f, 10);
            for (var k = 0; k < 4; k++)
            {
                var rotation = Quaternion.Euler(0f, k * 90f + 45f, 0f);
                b.Cylinder(Hub, rotation * new Vector3(0f, 0.02f, 0.78f), Quaternion.Euler(90f, k * 90f + 45f, 0f), 0.06f, 0.05f, 0.34f, 6);
                b.Box(Hub, rotation * new Vector3(0.1f, 0.12f, 0.45f), new Vector3(0.025f, 0.2f, 0.025f), rotation);
            }
        }
    }
}
