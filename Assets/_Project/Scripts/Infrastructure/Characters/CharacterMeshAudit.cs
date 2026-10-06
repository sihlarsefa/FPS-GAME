using System.Collections.Generic;
using UnityEngine;
using R = Project.Infrastructure.Characters.CharacterMeshes.Ring;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Test kancası: asker ağ üreticilerini Mesh nesnesi (yerel köprü) oluşturmadan, ham köşe/normal listeleriyle çalıştırır.
    /// Profiller SoldierModel'deki çağrılarla birebir aynı tutulmalıdır (yeni uzuv/ekipman eklenince buraya da eklenir).
    /// </summary>
    public static class CharacterMeshAudit
    {
        public sealed class Entry
        {
            public string Key;
            public Vector3[] Verts;
            public Vector3[] Normals;
            public Vector2[] Uvs;
        }

        public static List<Entry> BuildAll()
        {
            var list = new List<Entry>(96);
            void Add(System.Action build)
            {
                var s = CharacterMeshes.AuditBuild(build);
                if (s != null)
                    list.Add(new Entry { Key = s.Key, Verts = s.Verts, Normals = s.Normals, Uvs = s.Uvs });
            }

            // Cloth (uzuv/gövde)
            Add(() => CharacterMeshes.Cloth("pelvisLSculpt", new[] { new R(-0.12f, 0.125f, 0.088f), new R(-0.03f, 0.155f, 0.1f), new R(0.08f, 0.165f, 0.105f) }));
            Add(() => CharacterMeshes.Cloth("abdomenLSculpt", new[] { new R(0f, 0.155f, 0.1f), new R(0.1f, 0.146f, 0.097f), new R(0.21f, 0.17f, 0.104f) }));
            Add(() => CharacterMeshes.Cloth("chestL2Sculpt", new[] { new R(0f, 0.17f, 0.103f), new R(0.1f, 0.185f, 0.112f),
                new R(0.17f, 0.2f, 0.112f), new R(0.22f, 0.19f, 0.1f), new R(0.255f, 0.13f, 0.082f), new R(0.285f, 0.075f, 0.068f) }));
            Add(() => CharacterMeshes.Cloth("thighL2Sculpt", new[] { new R(-0.435f, 0.052f, 0.056f), new R(-0.34f, 0.06f, 0.065f),
                new R(-0.2f, 0.076f, 0.082f), new R(-0.08f, 0.088f, 0.092f), new R(0.03f, 0.085f, 0.088f) }));
            Add(() => CharacterMeshes.Cloth("shinL2Sculpt", new[] { new R(-0.38f, 0.05f, 0.054f), new R(-0.31f, 0.064f, 0.068f, -0.002f),
                new R(-0.27f, 0.052f, 0.058f, -0.004f), new R(-0.2f, 0.056f, 0.064f, -0.012f), new R(-0.1f, 0.052f, 0.058f, -0.006f),
                new R(-0.02f, 0.056f, 0.061f), new R(0.02f, 0.058f, 0.062f) }));
            Add(() => CharacterMeshes.Cloth("upperArmL2Sculpt", new[] { new R(-0.28f, 0.04f, 0.043f), new R(-0.2f, 0.047f, 0.049f),
                new R(-0.1f, 0.052f, 0.054f), new R(-0.02f, 0.056f, 0.056f), new R(0f, 0.05f, 0.05f) }));
            Add(() => CharacterMeshes.Cloth("forearmL2Sculpt", new[] { new R(-0.26f, 0.032f, 0.032f), new R(-0.18f, 0.038f, 0.037f),
                new R(-0.08f, 0.045f, 0.044f), new R(0.01f, 0.046f, 0.045f) }));
            Add(() => CharacterMeshes.SculptedHead());

            // Lathe
            Add(() => CharacterMeshes.Lathe("collarL", new[] { new R(0.235f, 0.125f, 0.098f), new R(0.275f, 0.088f, 0.078f), new R(0.31f, 0.07f, 0.07f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("neckL", new[] { new R(-0.05f, 0.075f, 0.065f), new R(0f, 0.058f, 0.056f),
                new R(0.045f, 0.052f, 0.054f, 0.005f), new R(0.09f, 0.05f, 0.052f, 0.01f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("bootShaftL", new[] { new R(-0.055f, 0.05f, 0.058f), new R(0f, 0.049f, 0.056f, -0.002f),
                new R(0.05f, 0.054f, 0.06f, -0.002f), new R(0.09f, 0.057f, 0.064f, -0.003f) }, 10, false, true));
            Add(() => CharacterMeshes.Lathe("armbandL", new[] { new R(-0.1425f, 0.0555f, 0.0575f), new R(-0.11f, 0.0575f, 0.0595f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("vest1L", new[] { new R(0f, 0.182f, 0.115f), new R(0.1f, 0.197f, 0.124f), new R(0.17f, 0.212f, 0.124f),
                new R(0.22f, 0.2f, 0.108f), new R(0.245f, 0.165f, 0.092f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("vest1LowL", new[] { new R(0.07f, 0.165f, 0.115f), new R(0.21f, 0.18f, 0.12f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("cummerbundL", new[] { new R(0f, 0.186f, 0.118f), new R(0.13f, 0.2f, 0.123f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("cummerbundLowL", new[] { new R(0.1f, 0.172f, 0.116f), new R(0.21f, 0.185f, 0.12f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("vestCollarL", new[] { new R(0.24f, 0.15f, 0.125f), new R(0.31f, 0.1f, 0.095f) }, 10, false, false));
            Add(() => CharacterMeshes.Lathe("helmetRimV1", new[] { new R(0.102f, 0.106f, 0.131f), new R(0.112f, 0.11f, 0.1355f), new R(0.126f, 0.1035f, 0.1285f) }, 14, false, false));
            // Kapaklı varyant (kapak üçgenleri de denetlensin)
            Add(() => CharacterMeshes.Lathe("auditCapped", new[] { new R(0f, 0.05f, 0.05f), new R(0.1f, 0.06f, 0.06f) }, 12, true, true));

            // Eller
            Add(() => CharacterMeshes.Hand("handL", 1f));
            Add(() => CharacterMeshes.Hand("handR", -1f));

            // Yuvarlatılmış kutular (donanım)
            var boxes = new (string, Vector3, Vector3)[]
            {
                ("beltPouchRounded", Vector3.zero, new Vector3(0.07f, 0.09f, 0.05f)), ("cargoSculpt", Vector3.zero, new Vector3(0.03f, 0.12f, 0.1f)),
                ("bootToeCap2", new Vector3(0f, -0.05f, 0.195f), new Vector3(0.088f, 0.044f, 0.07f)),
                ("bootSoleSculpt", new Vector3(0f, -0.07f, 0.08f), new Vector3(0.104f, 0.02f, 0.3f)),
                ("plateF3Sculpt", Vector3.zero, new Vector3(0.33f, 0.33f, 0.06f)), ("plateB3Sculpt", Vector3.zero, new Vector3(0.33f, 0.34f, 0.06f)),
                ("plateF2Sculpt", Vector3.zero, new Vector3(0.3f, 0.3f, 0.05f)), ("plateB2Sculpt", Vector3.zero, new Vector3(0.3f, 0.32f, 0.05f)),
                ("vestStrapSculpt", Vector3.zero, new Vector3(0.065f, 0.025f, 0.25f)), ("radioPouchRounded", Vector3.zero, new Vector3(0.05f, 0.12f, 0.07f)),
                ("magPouchRounded", Vector3.zero, new Vector3(0.07f, 0.11f, 0.04f)), ("holsterDropPlate", Vector3.zero, new Vector3(0.03f, 0.06f, 0.07f)),
                ("holsterBody", Vector3.zero, new Vector3(0.046f, 0.17f, 0.072f)), ("beltSmallPouch", Vector3.zero, new Vector3(0.05f, 0.07f, 0.04f)),
                ("sidePouchRounded", Vector3.zero, new Vector3(0.05f, 0.09f, 0.09f)), ("utilityPouchRounded", Vector3.zero, new Vector3(0.12f, 0.07f, 0.04f)),
                ("adminPouchRounded", Vector3.zero, new Vector3(0.15f, 0.075f, 0.022f)), ("carrierHandleSculpt", Vector3.zero, new Vector3(0.115f, 0.018f, 0.022f)),
                ("carrierBuckleSculpt", Vector3.zero, new Vector3(0.047f, 0.033f, 0.012f)),
                ("pack1RoundedV2", new Vector3(0f, 0.115f, -0.0525f), new Vector3(0.27f, 0.27f, 0.105f)), ("packMiniPouch", Vector3.zero, new Vector3(0.045f, 0.12f, 0.08f)),
                ("pack2RoundedV2", new Vector3(0f, 0.1f, -0.0725f), new Vector3(0.32f, 0.36f, 0.145f)), ("packSideRounded", Vector3.zero, new Vector3(0.05f, 0.2f, 0.12f)),
                ("pack3RoundedV2", new Vector3(0f, 0.12f, -0.0875f), new Vector3(0.36f, 0.48f, 0.175f)), ("packSide3Rounded", Vector3.zero, new Vector3(0.06f, 0.26f, 0.14f)),
            };
            foreach (var b in boxes)
            {
                var bb = b;
                Add(() => CharacterMeshes.RoundedBox(bb.Item1, bb.Item2, bb.Item3));
            }

            // Elipsoitler
            var ell = new (string, Vector3, Vector3, int, int, float, float, float)[]
            {
                ("hair2", new Vector3(0f, 0.095f, -0.006f), new Vector3(0.083f, 0.12f, 0.104f), 16, 6, 12f, 90f, 0f),
                ("ear3", Vector3.zero, new Vector3(0.006f, 0.021f, 0.015f), 10, 4, -90f, 90f, 0f),
                ("kneepadL2", Vector3.zero, new Vector3(0.036f, 0.048f, 0.014f), 10, 4, -90f, 90f, 0f),
                ("bootFoot2", new Vector3(0f, -0.042f, 0.08f), new Vector3(0.05f, 0.04f, 0.15f), 10, 5, -90f, 90f, 0f),
                ("shoulderCap2", new Vector3(0f, -0.015f, 0f), new Vector3(0.062f, 0.058f, 0.062f), 8, 5, -90f, 90f, 0f),
                ("deltoidPadV1", new Vector3(0f, -0.028f, 0f), new Vector3(0.071f, 0.085f, 0.071f), 10, 4, -38f, 90f, 0f),
                ("elbowPad", Vector3.zero, new Vector3(0.045f, 0.04f, 0.03f), 6, 3, -90f, 90f, 0f),
                ("stubbleShade", new Vector3(0f, 0.09f, 0f), new Vector3(0.0835f, 0.1205f, 0.1035f), 12, 4, -90f, -30f, 0f),
                ("micCapsule", Vector3.zero, new Vector3(0.014f, 0.01f, 0.008f), 8, 4, -90f, 90f, 0f),
                ("lipLineSculpt", Vector3.zero, new Vector3(0.022f, 0.0017f, 0.002f), 16, 4, -90f, 90f, 0f),
                ("balaclava2", new Vector3(0f, 0.09f, 0f), new Vector3(0.083f, 0.12f, 0.103f), 10, 3, -90f, -16f, 0f),
                ("shemaghFace2", new Vector3(0f, 0.09f, 0f), new Vector3(0.087f, 0.123f, 0.107f), 10, 3, -90f, -12f, 0f),
                ("eyelidSculpt", Vector3.zero, new Vector3(0.017f, 0.0075f, 0.0065f), 16, 6, -90f, 90f, 0f),
                ("eyeSculpt", Vector3.zero, new Vector3(0.0095f, 0.0028f, 0.003f), 16, 6, -90f, 90f, 0f),
                ("irisSculpt", Vector3.zero, new Vector3(0.0038f, 0.0035f, 0.0012f), 12, 6, -90f, 90f, 0f),
                ("browSculpt", Vector3.zero, new Vector3(0.026f, 0.0048f, 0.0032f), 16, 4, -90f, 90f, 0f),
                ("cap2", new Vector3(0f, 0.125f, -0.006f), new Vector3(0.088f, 0.082f, 0.108f), 10, 3, 0f, 90f, 0f),
                ("helmet2", new Vector3(0f, 0.115f, -0.01f), new Vector3(0.1f, 0.105f, 0.125f), 20, 8, 0f, 90f, 0.03f),
                ("beret2", Vector3.zero, new Vector3(0.094f, 0.045f, 0.108f), 16, 6, -90f, 90f, 0f),
                ("pack1DomeV2", Vector3.zero, new Vector3(0.132f, 0.05f, 0.05f), 10, 3, 0f, 90f, 0f),
                ("pack2DomeV2", Vector3.zero, new Vector3(0.157f, 0.06f, 0.0705f), 12, 3, 0f, 90f, 0f),
                ("pack3DomeV2", Vector3.zero, new Vector3(0.177f, 0.07f, 0.0855f), 12, 3, 0f, 90f, 0f),
                ("dummyHead", new Vector3(0f, 0.11f, 0f), new Vector3(0.1f, 0.12f, 0.11f), 8, 6, -90f, 90f, 0f),
            };
            foreach (var e in ell)
            {
                var ee = e;
                Add(() => CharacterMeshes.Ellipsoid(ee.Item1, ee.Item2, ee.Item3, ee.Item4, ee.Item5, ee.Item6, ee.Item7, ee.Item8));
            }

            // Silindirler
            var cyl = new (string, float, float, float, float, int, bool, float)[]
            {
                ("cuff3", -0.275f, -0.22f, 0.0345f, 0.041f, 10, false, 1f), ("antenna", 0f, 0.3f, 0.005f, 0.003f, 4, false, 1f),
                ("helmetBand2", 0.112f, 0.14f, 0.107f, 0.101f, 12, false, 1.2f), ("earCup", -0.022f, 0.022f, 0.04f, 0.038f, 8, true, 1f),
                ("beretBand2", 0.13f, 0.155f, 0.084f, 0.082f, 10, false, 1.19f), ("bedroll2", -0.17f, 0.17f, 0.055f, 0.055f, 10, true, 1f),
                ("bedroll3", -0.19f, 0.19f, 0.062f, 0.062f, 10, true, 1f), ("beretBadgeRound", -0.005f, 0.005f, 0.017f, 0.017f, 12, true, 1f),
                ("headsetCup", -0.009f, 0.009f, 0.028f, 0.026f, 10, true, 1f), ("balaclavaNeck2", -0.03f, 0.05f, 0.066f, 0.06f, 10, false, 1f),
                ("shemaghNeck2", -0.045f, 0.05f, 0.088f, 0.076f, 10, false, 1f), ("nvgTube", -0.03f, 0.03f, 0.016f, 0.016f, 6, true, 1f),
                ("bottle", -0.1f, 0.1f, 0.036f, 0.034f, 8, true, 1f), ("matRoll", -0.15f, 0.15f, 0.05f, 0.05f, 8, true, 1f),
                ("radioKnob", 0f, 0.02f, 0.014f, 0.014f, 6, true, 1f), ("whipAntenna", 0f, 0.42f, 0.006f, 0.0025f, 4, false, 1f),
                ("dummyPost", 0f, 0.36f, 0.035f, 0.03f, 6, false, 1f), ("dummyRing0", 0f, 0.008f, 0.16f, 0.16f, 16, true, 1f),
            };
            foreach (var c in cyl)
            {
                var cc = c;
                Add(() => CharacterMeshes.Cylinder(cc.Item1, cc.Item2, cc.Item3, cc.Item4, cc.Item5, cc.Item6, cc.Item7, cc.Item8));
            }

            // Kutular, frustum, quad, kayış, bağcık
            Add(() => CharacterMeshes.Box("belt", Vector3.zero, new Vector3(0.345f, 0.055f, 0.225f)));
            Add(() => CharacterMeshes.Box("molleStrap", Vector3.zero, new Vector3(0.2f, 0.007f, 0.006f)));
            Add(() => CharacterMeshes.Frustum("pauldron", -0.11f, 0.035f, new Vector2(0.13f, 0.128f), new Vector2(0.142f, 0.14f)));
            Add(() => CharacterMeshes.Quad("flagPatch", 0.075f, 0.05f));
            Add(() => CharacterMeshes.Webbing("auditWebbing", 5, 3, 0.2f, 0.06f));
            Add(() => CharacterMeshes.BootLacing());
            return list;
        }
    }
}
