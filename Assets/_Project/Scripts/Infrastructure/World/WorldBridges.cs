using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yol–dere kesişimlerindeki (<see cref="MapLayout.Bridges"/>) köprüler. Arazi köprü başlarını tabliye yüksekliğinde düz
    /// bırakır; yapı üreticisi (LocationBuilder) o noktada bir yapı kurmadıysa burada basit betonarme köprü kurulur:
    /// tabliye (yol kaplamalı), korkuluk duvarları, iki ayak. Tüm parçalar çarpıştırıcılı, Default katman.
    /// </summary>
    public static class WorldBridges
    {
        public const string RootName = "Köprüler";
        public const float DeckThickness = 0.7f;
        public const float ParapetHeight = 0.95f;
        public const float ParapetThickness = 0.3f;

        /// <summary>
        /// Yapı listesinde karşılığı olmayan köprüleri kurar; kurulan köprülerin sınırlarını structuresOut'a ekler (null olabilir).
        /// Kurulan köprü sayısını döner.
        /// </summary>
        public static int BuildMissing(MapLayout layout, IReadOnlyList<Bounds> existingStructures, Transform parent, List<Bounds> structuresOut)
        {
            if (layout == null || layout.Bridges.Count == 0)
                return 0;

            GameObject root = null;
            var built = 0;
            for (var i = 0; i < layout.Bridges.Count; i++)
            {
                var bridge = layout.Bridges[i];
                if (bridge == null || bridge.DeckHeight < 0f || bridge.Length < 2f)
                    continue;
                if (HasStructureAt(existingStructures, bridge.Center))
                    continue;

                if (root == null)
                {
                    root = new GameObject(RootName);
                    root.layer = GameLayers.Default;
                    if (parent != null)
                        root.transform.SetParent(parent, false);
                }

                var bounds = Build(bridge, layout, root.transform);
                structuresOut?.Add(bounds);
                built++;
            }

            return built;
        }

        /// <summary>Tek bir köprü kurar ve dünya sınırlarını döner.</summary>
        public static Bounds Build(BridgeSpec bridge, MapLayout layout, Transform parent)
        {
            var go = new GameObject(string.IsNullOrEmpty(bridge.Name) ? "Köprü" : bridge.Name);
            go.layer = GameLayers.Default;
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(bridge.Center.x, 0f, bridge.Center.y), Quaternion.Euler(0f, bridge.Yaw, 0f));

            var length = bridge.Length + 2f; // köprü başlarında araziye gömülsün
            var width = Mathf.Max(4f, bridge.Width);
            var deckTop = bridge.DeckHeight;
            var bed = layout.WaterLevel - 3f;

            var builder = new MeshBuilder(2);
            const int structure = 0;
            const int surface = 1;

            // Tabliye.
            var deckCenter = new Vector3(0f, deckTop - DeckThickness * 0.5f, 0f);
            MeshFactory.AddBox(builder, structure, deckCenter - new Vector3(0f, 0.02f, 0f), new Vector3(width, DeckThickness - 0.04f, length));
            builder.AddFlatQuad(surface,
                new Vector3(-width * 0.5f + ParapetThickness, deckTop + 0.01f, -length * 0.5f),
                new Vector3(-width * 0.5f + ParapetThickness, deckTop + 0.01f, length * 0.5f),
                new Vector3(width * 0.5f - ParapetThickness, deckTop + 0.01f, length * 0.5f),
                new Vector3(width * 0.5f - ParapetThickness, deckTop + 0.01f, -length * 0.5f), 0.25f);

            // Korkuluk duvarları (uçlarda 3 m kısa → yol girişi açık).
            var parapetLength = Mathf.Max(2f, length - 6f);
            var parapetY = deckTop + ParapetHeight * 0.5f;
            var parapetX = width * 0.5f - ParapetThickness * 0.5f;
            var parapetSize = new Vector3(ParapetThickness, ParapetHeight, parapetLength);
            MeshFactory.AddBox(builder, structure, new Vector3(-parapetX, parapetY, 0f), parapetSize);
            MeshFactory.AddBox(builder, structure, new Vector3(parapetX, parapetY, 0f), parapetSize);

            // Ayaklar (dere yatağına kadar).
            var pierTop = deckTop - DeckThickness;
            var pierHeight = Mathf.Max(1f, pierTop - bed);
            var pierSize = new Vector3(width * 0.8f, pierHeight, 1.2f);
            var pierOffset = length * 0.22f;
            MeshFactory.AddBox(builder, structure, new Vector3(0f, bed + pierHeight * 0.5f, -pierOffset), pierSize);
            MeshFactory.AddBox(builder, structure, new Vector3(0f, bed + pierHeight * 0.5f, pierOffset), pierSize);

            var mesh = builder.ToMesh("HK_Bridge");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                MaterialLibrary.Get(MaterialId.Concrete),
                MaterialLibrary.Get(bridge.Kind == RoadKind.Asphalt ? MaterialId.Asphalt : MaterialId.ConcreteDark)
            };

            AddBox(go, deckCenter, new Vector3(width, DeckThickness, length));
            AddBox(go, new Vector3(-parapetX, parapetY, 0f), parapetSize);
            AddBox(go, new Vector3(parapetX, parapetY, 0f), parapetSize);
            AddBox(go, new Vector3(0f, bed + pierHeight * 0.5f, -pierOffset), pierSize);
            AddBox(go, new Vector3(0f, bed + pierHeight * 0.5f, pierOffset), pierSize);

            var bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < 0.01f)
                bounds = new Bounds(go.transform.TransformPoint(mesh.bounds.center), mesh.bounds.size);
            return bounds;
        }

        private static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static bool HasStructureAt(IReadOnlyList<Bounds> structures, Vector2 point)
        {
            if (structures == null)
                return false;
            for (var i = 0; i < structures.Count; i++)
            {
                var b = structures[i];
                // Bölge çapında dev sınırlar köprü sayılmaz.
                if (b.size.x > 140f || b.size.z > 140f)
                    continue;
                if (point.x >= b.min.x && point.x <= b.max.x && point.y >= b.min.z && point.y <= b.max.z)
                    return true;
            }

            return false;
        }
    }
}
