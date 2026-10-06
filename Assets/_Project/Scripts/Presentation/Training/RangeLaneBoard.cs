using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Presentation.Training
{
    /// <summary>
    /// Poligon şerit levhaları: atış çizgisinde numaralı şerit tabelaları, her şeritte 100-600 m mesafe levhaları.
    /// Harita sınırından uzak mesafeler için kalıcı bir uzak zemin şeridi döşenir. Statik, tek seferlik kurulum.
    /// </summary>
    public static class RangeLaneBoard
    {
        /// <summary>Levhaları kurar; origin atış çizgisi, forward atış yönü (yatay).</summary>
        public static Transform Build(Transform parent, Vector3 origin, Vector3 forward)
        {
            var root = new GameObject("[Şerit Levhaları]").transform;
            if (parent != null)
                root.SetParent(parent, false);

            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var yaw = StructureKit.YawOf(forward);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            origin.y = 0f;

            BuildFarGround(root, origin, forward, right, rot);

            for (var lane = 0; lane < RangeScoring.LaneCount; lane++)
            {
                var lateral = RangeScoring.LaneLateral(lane);
                BuildLaneSign(root, origin + right * lateral + forward * -2f, rot, lane);

                for (var i = 0; i < RangeScoring.LaneMarkerDistances.Length; i++)
                {
                    var d = RangeScoring.LaneMarkerDistances[i];
                    BuildDistanceBoard(root, origin + right * (lateral + 3.5f) + forward * d, rot, d, lane);
                }
            }

            return root;
        }

        private static void BuildFarGround(Transform root, Vector3 origin, Vector3 forward, Vector3 right, Quaternion rot)
        {
            // Yalnızca +Z'ye bakan hat için: harita kenarından 620 m'ye kadar toprak şerit.
            if (Vector3.Dot(forward, Vector3.forward) < 0.9f)
                return;

            var startZ = TrainingRangeBuilder.HalfSize;
            var endZ = origin.z + RangeScoring.LaneMarkerDistances[RangeScoring.LaneMarkerDistances.Length - 1] + 20f;
            var length = endZ - startZ;
            if (length < 10f)
                return;

            var slab = StructureKit.CreateBox(root, "UzakZeminKalici", new Vector3(origin.x, -0.07f, startZ + length * 0.5f),
                new Vector3(120f, 0.1f, length), Quaternion.identity, MaterialId.Dirt);
            StructureKit.MarkStatic(slab);
        }

        private static void BuildLaneSign(Transform root, Vector3 position, Quaternion rot, int lane)
        {
            var post = StructureKit.CreateBox(root, "SeritDirek", position + Vector3.up * 1.3f, new Vector3(0.12f, 2.6f, 0.12f), rot, MaterialId.MetalDark, false);
            StructureKit.MarkStatic(post);
            var board = StructureKit.CreateBox(root, "SeritTabela", position + Vector3.up * 2.9f, new Vector3(1.6f, 0.9f, 0.08f), rot, MaterialId.Blue, false);
            StructureKit.MarkStatic(board);
            AddText(board.transform, (lane + 1).ToString(), 0f, 0.1f, Color.white);
        }

        private static void BuildDistanceBoard(Transform root, Vector3 position, Quaternion rot, float meters, int lane)
        {
            var h = 2.4f;
            var post = StructureKit.CreateBox(root, "MesafeDirek", position + Vector3.up * h * 0.5f, new Vector3(0.1f, h, 0.1f), rot, MaterialId.MetalDark, false);
            StructureKit.MarkStatic(post);
            var board = StructureKit.CreateBox(root, "MesafeLevha", position + Vector3.up * (h + 0.35f), new Vector3(1.9f, 0.7f, 0.06f), rot, MaterialId.Yellow, false);
            StructureKit.MarkStatic(board);
            AddText(board.transform, RangeScoring.DistanceLabel(meters), 0.18f, 0.045f, Color.black);
            AddText(board.transform, "Ş" + (lane + 1), -0.32f, 0.026f, Color.black);
        }

        private static void AddText(Transform board, string text, float unitY, float size, Color color)
        {
            var go = new GameObject("Yazi");
            go.transform.SetParent(board, false);
            // Levha ölçeği (1.9x0.7x0.06) ters çevrilir ki yazı bozulmasın.
            var s = board.localScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            go.transform.localPosition = new Vector3(0f, unitY, -0.55f);
            go.transform.localRotation = Quaternion.identity;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 64;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var font = UiFactory.DefaultFont;
            if (font != null)
            {
                mesh.font = font;
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null && font.material != null)
                    renderer.sharedMaterial = font.material;
            }
        }
    }
}
