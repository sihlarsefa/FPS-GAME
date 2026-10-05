using UnityEngine;

namespace Project.Presentation.World
{
    /// <summary>ESKİ prototip blok haritası. Gerçek harita WorldGenerator (Kuzgun Vadisi) ile üretilir.</summary>
    [System.Obsolete("WorldGenerator.Generate kullanın.")]
    public static class MapBlockoutBuilder
    {
        public static void BuildVerdantValleyPrototype()
        {
            if (GameObject.Find("[MapBlockout]") != null)
                return;

            var root = new GameObject("[MapBlockout]");

            CreateBuilding(root.transform, "CentralTown_A", new Vector3(12f, 0f, 8f), new Vector3(14f, 6f, 10f));
            CreateBuilding(root.transform, "CentralTown_B", new Vector3(-10f, 0f, 14f), new Vector3(10f, 5f, 8f));
            CreateBuilding(root.transform, "Factory", new Vector3(28f, 0f, -18f), new Vector3(18f, 8f, 14f));
            CreateBuilding(root.transform, "Warehouse", new Vector3(-24f, 0f, -20f), new Vector3(12f, 4f, 20f));

            CreateCoverCluster(root.transform, "NorthForest", new Vector3(-30f, 0f, 30f), 8);
            CreateCoverCluster(root.transform, "SouthField", new Vector3(20f, 0f, -35f), 5);
            CreateHill(root.transform, "EastHill", new Vector3(40f, 0f, 10f));
        }

        private static void CreateBuilding(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = name;
            building.transform.SetParent(parent);
            building.transform.position = position + new Vector3(0f, size.y * 0.5f, 0f);
            building.transform.localScale = size;

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = $"{name}_Roof";
            roof.transform.SetParent(building.transform);
            roof.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            roof.transform.localScale = new Vector3(1.05f, 0.08f, 1.05f);
        }

        private static void CreateCoverCluster(Transform parent, string name, Vector3 center, int count)
        {
            var cluster = new GameObject(name);
            cluster.transform.SetParent(parent);
            cluster.transform.position = center;

            for (var i = 0; i < count; i++)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"Cover_{i}";
                rock.transform.SetParent(cluster.transform);
                rock.transform.localPosition = new Vector3(
                    Random.Range(-8f, 8f),
                    0.75f,
                    Random.Range(-8f, 8f));
                rock.transform.localScale = new Vector3(
                    Random.Range(1f, 2.5f),
                    Random.Range(1f, 2f),
                    Random.Range(1f, 2.5f));
            }
        }

        private static void CreateHill(Transform parent, string name, Vector3 position)
        {
            var hill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hill.name = name;
            hill.transform.SetParent(parent);
            hill.transform.position = position + new Vector3(0f, 2f, 0f);
            hill.transform.localScale = new Vector3(12f, 4f, 12f);
        }
    }
}
