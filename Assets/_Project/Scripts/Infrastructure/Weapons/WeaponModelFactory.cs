using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Prosedürel düşük poligonlu Türk silah modelleri (geçici iskelet — tam uygulama yazılıyor).</summary>
    public static class WeaponModelFactory
    {
        public static GameObject Build(WeaponDefinitionData weapon, Transform parent, int layer, bool forViewmodel, out Transform muzzle)
        {
            muzzle = null;
            if (weapon == null)
                return null;

            var root = new GameObject("Weapon_" + weapon.WeaponId);
            root.layer = layer;
            if (parent != null)
                root.transform.SetParent(parent, false);
            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.layer = layer;
            muzzleGo.transform.SetParent(root.transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0.035f, 0.6f);
            muzzle = muzzleGo.transform;
            return root;
        }
    }
}
