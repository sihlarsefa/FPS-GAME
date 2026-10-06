using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Yol kenarı ek propları: tabela (direk + dokulu levha) ve yanmış araç kasası. Mesh yazıcıya kutu ekler.</summary>
    public static class RoadsideProps
    {
        public const float SignW = 2.0f;
        public const float SignH = 0.5f;
        public const float SignPostH = 2.3f;

        /// <summary>Tabela: direk + arka metal levha ana builder'a, dokulu ön yüz sign builder'a (alt mesh 0).</summary>
        public static void AddSign(MeshBuilder main, int subMetal, MeshBuilder signs, Vector3 basePos, float yaw, int kind)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var fwd = rot * Vector3.forward;
            var uAxis = rot * Vector3.left; // izleyicinin sağı
            var center = basePos + Vector3.up * (SignPostH + SignH * 0.5f);
            MeshFactory.AddBox(main, subMetal, basePos + Vector3.up * (SignPostH * 0.5f), new Vector3(0.08f, SignPostH, 0.08f), rot);
            MeshFactory.AddBox(main, subMetal, center - fwd * 0.03f, new Vector3(SignW + 0.08f, SignH + 0.08f, 0.04f), rot);

            var v = RoadsideSignSet.VRange(kind);
            var p = center + fwd * 0.012f;
            var hw = SignW * 0.5f;
            var hh = SignH * 0.5f;
            var up = Vector3.up;
            var i0 = signs.AddVertex(p - uAxis * hw - up * hh, fwd, new Vector2(0f, v.x));
            var i1 = signs.AddVertex(p - uAxis * hw + up * hh, fwd, new Vector2(0f, v.y));
            var i2 = signs.AddVertex(p + uAxis * hw + up * hh, fwd, new Vector2(1f, v.y));
            var i3 = signs.AddVertex(p + uAxis * hw - up * hh, fwd, new Vector2(1f, v.x));
            signs.AddTriangle(0, i0, i1, i2);
            signs.AddTriangle(0, i0, i2, i3);
        }

        /// <summary>Yanmış araç kasası: pas gövde + is kabin, tekerleksiz (yanık jant), hafif yan yatık. Çarpıştırıcı boyutu döner.</summary>
        public static Bounds AddWreck(MeshBuilder b, int subRust, int subChar, Vector3 basePos, float yaw, int variant)
        {
            var roll = (variant & 1) == 0 ? 5f : -7f;
            var rot = Quaternion.Euler(0f, yaw, roll);
            var yawOnly = Quaternion.Euler(0f, yaw, 0f);
            var lift = Vector3.up * 0.35f;
            // Gövde (şasi + kaporta)
            MeshFactory.AddBox(b, subRust, basePos + lift + rot * new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.7f, 4.4f), rot);
            // Kabin (is rengi, ön camlar boş)
            MeshFactory.AddBox(b, subChar, basePos + lift + rot * new Vector3(0f, 1.05f, -0.3f), new Vector3(1.7f, 0.55f, 2.0f), rot);
            // Çökmüş kaporta ucu
            MeshFactory.AddBox(b, subChar, basePos + lift + rot * new Vector3(0f, 0.85f, 1.8f), new Vector3(1.8f, 0.12f, 0.9f),
                Quaternion.Euler(0f, yaw, roll + 8f));
            // Yanık jantlar (tekerlek yok, yere oturmuş)
            for (var i = 0; i < 4; i++)
            {
                var sx = i < 2 ? -0.95f : 0.95f;
                var sz = (i & 1) == 0 ? 1.4f : -1.4f;
                MeshFactory.AddBox(b, subChar, basePos + yawOnly * new Vector3(sx, 0.18f, sz), new Vector3(0.25f, 0.36f, 0.36f), yawOnly);
            }

            return new Bounds(basePos + Vector3.up * 0.9f, new Vector3(2f, 1.5f, 4.4f));
        }
    }
}
