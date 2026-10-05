using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Prosedürel bina üreticisi (geçici iskelet — tam uygulama yazılıyor).</summary>
    public static class BuildingGenerator
    {
        public static BuildingResult Build(BuildingSpec spec, Transform parent)
        {
            var result = new BuildingResult();
            var root = new GameObject(spec != null && !string.IsNullOrEmpty(spec.Name) ? spec.Name : "Bina");
            if (parent != null)
                root.transform.SetParent(parent, false);
            if (spec != null)
                root.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
            result.Root = root;
            result.Bounds = new Bounds(root.transform.position, Vector3.one);
            return result;
        }
    }
}
