using Project.Core.Domain;
using Project.Infrastructure.Config;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>FPP silah görünümü (geçici iskelet — tam uygulama yazılıyor).</summary>
    public sealed class WeaponViewModel : MonoBehaviour
    {
        private Transform _muzzle;

        public WeaponDefinitionData Current { get; private set; }
        public bool IsEquipping { get; private set; }
        public float AimBlend { get; private set; }
        public Transform Muzzle => _muzzle != null ? _muzzle : transform;
        public Vector3 MuzzleWorldPosition => Muzzle.position;

        public static WeaponViewModel Create(Transform cameraTransform, int layer)
        {
            var go = new GameObject("WeaponViewModel");
            go.layer = layer;
            if (cameraTransform != null)
                go.transform.SetParent(cameraTransform, false);
            return go.AddComponent<WeaponViewModel>();
        }

        public static WeaponViewModel CreateDefault(Transform cameraTransform, WeaponConfig config)
        {
            var vm = Create(cameraTransform, cameraTransform != null ? cameraTransform.gameObject.layer : 0);
            vm.Equip(config != null ? config.ToDefinition() : WeaponDefinitionData.AssaultRifle);
            return vm;
        }

        public void Equip(WeaponDefinitionData weapon) { Current = weapon; }
        public void SetAim(bool aiming) { }
        public void OnFire() { }
        public void ApplyRecoil(float kickback, float recoverySpeed) { }
        public void PlayReload(float durationSeconds) { }
        public void StopReload() { }
        public void PlayMelee() { }
        public void PlayThrow() { }
        public void PlayUse(float durationSeconds) { }
        public void StopUse() { }
        public void SetMotion(float speed01, bool sprinting, bool grounded, float lookYawDelta, float lookPitchDelta) { }
        public void SetHidden(bool hidden) { }
    }
}
