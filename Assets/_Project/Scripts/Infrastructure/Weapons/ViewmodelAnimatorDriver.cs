using System;
using Project.Infrastructure.Content;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Override prefab'ın Animator'ünü sürer (WeaponAnimationOverride) ve sol el (Grip_L) IK hedefini iletir.
    /// Animator yoksa ya da override tanımlı değilse hiçbir şey yapmaz (prosedürel viewmodel aynen çalışır).
    /// Animation Rigging varsa (Two Bone IK) yalnızca bilgi günlüğü; yoksa Animator IK (OnAnimatorIK) kullanılır.
    /// Parametreler (varsa): Aim (float), Speed (float), Sprint (bool), Reload (bool), Fire (trigger), ReloadEmpty (bool), ReloadPhase (int), ReloadSpeed (float), MagCheck (trigger).
    /// Cursor doğrulaması: Animator IK Pass katmanı ve gerçek prefab ile görsel kontrol gerekir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelAnimatorDriver : MonoBehaviour
    {
        private static bool _rigLogged;

        private Animator _animator;
        private Transform _gripL;
        private float _ikWeight;
        private bool _hasAim, _hasSpeed, _hasSprint, _hasReload, _hasFire, _hasReloadEmpty, _hasReloadPhase, _hasReloadSpeed, _hasMagCheck;
        private static readonly int AimId = Animator.StringToHash("Aim");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int SprintId = Animator.StringToHash("Sprint");
        private static readonly int ReloadId = Animator.StringToHash("Reload");
        private static readonly int FireId = Animator.StringToHash("Fire");
        private static readonly int ReloadEmptyId = Animator.StringToHash("ReloadEmpty");
        private static readonly int ReloadPhaseId = Animator.StringToHash("ReloadPhase");
        private static readonly int ReloadSpeedId = Animator.StringToHash("ReloadSpeed");
        private static readonly int MagCheckId = Animator.StringToHash("MagCheck");

        public bool IsActive => _animator != null && _animator.runtimeAnimatorController != null;

        /// <summary>Model altında Animator + weaponAnimations girdisi varsa sürücüyü bağlar; yoksa null.</summary>
        public static ViewmodelAnimatorDriver TryAttach(WeaponModel model, string weaponId)
        {
            if (model == null || string.IsNullOrEmpty(weaponId))
                return null;
            try
            {
                var existing = model.GetComponent<ViewmodelAnimatorDriver>();
                if (existing != null)
                    return existing;
                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator == null || !ContentOverrides.TryGetWeaponAnimation(weaponId, out var controller))
                    return null;

                var driver = model.gameObject.AddComponent<ViewmodelAnimatorDriver>();
                driver.Bind(animator, controller, model.LeftHandGrip);
                return driver;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Silah] Animator sürücüsü bağlanamadı: " + e.Message);
                return null;
            }
        }

        private void Bind(Animator animator, RuntimeAnimatorController controller, Transform gripL)
        {
            _animator = animator;
            _gripL = gripL;
            _animator.runtimeAnimatorController = controller;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ps = _animator.parameters;
            for (var i = 0; i < ps.Length; i++)
            {
                var h = ps[i].nameHash;
                if (h == AimId) _hasAim = true;
                else if (h == SpeedId) _hasSpeed = true;
                else if (h == SprintId) _hasSprint = true;
                else if (h == ReloadId) _hasReload = true;
                else if (h == FireId) _hasFire = true;
                else if (h == ReloadEmptyId) _hasReloadEmpty = true;
                else if (h == ReloadPhaseId) _hasReloadPhase = true;
                else if (h == ReloadSpeedId) _hasReloadSpeed = true;
                else if (h == MagCheckId) _hasMagCheck = true;
            }

            if (!_rigLogged && Type.GetType("UnityEngine.Animations.Rigging.TwoBoneIKConstraint, Unity.Animation.Rigging") != null)
            {
                _rigLogged = true;
                Debug.Log("[Silah] Animation Rigging bulundu; sol el için prefab'a Two Bone IK eklenebilir (Grip_L hedefi).");
            }
        }

        public void Drive(float aim01, float speed01, bool sprinting, bool reloading)
        {
            if (!IsActive)
                return;
            if (_hasAim) _animator.SetFloat(AimId, aim01);
            if (_hasSpeed) _animator.SetFloat(SpeedId, speed01);
            if (_hasSprint) _animator.SetBool(SprintId, sprinting);
            if (_hasReload) _animator.SetBool(ReloadId, reloading);
        }

        /// <summary>Doldurma evresi (ReloadPhase int), boş/taktik ve klip hız çarpanı. Parametre yoksa yok sayılır.</summary>
        public void SetReloadInfo(int phase, bool empty, float speedMultiplier)
        {
            if (!IsActive)
                return;
            if (_hasReloadPhase) _animator.SetInteger(ReloadPhaseId, phase);
            if (_hasReloadEmpty) _animator.SetBool(ReloadEmptyId, empty);
            if (_hasReloadSpeed) _animator.SetFloat(ReloadSpeedId, speedMultiplier);
        }

        public void TriggerMagCheck()
        {
            if (IsActive && _hasMagCheck)
                _animator.SetTrigger(MagCheckId);
        }

        public void SetLeftHandWeight(float w) => _ikWeight = Mathf.Clamp01(w);

        public void TriggerFire()
        {
            if (IsActive && _hasFire)
                _animator.SetTrigger(FireId);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || _gripL == null)
                return;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _ikWeight);
            if (_ikWeight > 0f)
            {
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _gripL.position);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand, _gripL.rotation);
            }
        }
    }
}
