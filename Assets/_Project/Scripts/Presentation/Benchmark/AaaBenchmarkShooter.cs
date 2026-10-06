using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Vfx;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Presentation.Benchmark
{
    /// <summary>
    /// "Vitrin askeri": Vitrin planları 1 ve 6 için hedeflere bakan, MPT-76 tutan, görünür bir asker modeli.
    /// Oyuncu kamerası/görünüm modeli bu planlarda kullanılmaz (omuz üstü kamera); namlu alevi + mermi izi GameVfx ile tetiklenir.
    /// </summary>
    public sealed class AaaBenchmarkShooter : MonoBehaviour
    {
        private SoldierModel _model;
        private Vector3 _forward = Vector3.forward;
        private Vector3 _targetPoint;
        private float _nextShot;

        public SoldierModel Model => _model;
        public Vector3 Forward => _forward;

        /// <summary>Askerin zemindeki konumu (çapa).</summary>
        public Vector3 GroundPosition => transform.position;

        public static AaaBenchmarkShooter Create(Transform parent, Vector3 position, Vector3 toTargets, Vector3 targetPoint)
        {
            var flat = new Vector3(toTargets.x, 0f, toTargets.z);
            if (flat.sqrMagnitude < 1e-4f)
                flat = Vector3.forward;
            flat.Normalize();

            var go = new GameObject("Vitrin Askeri");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            var shooter = go.AddComponent<AaaBenchmarkShooter>();
            shooter._forward = flat;
            shooter._targetPoint = targetPoint;
            shooter.BuildModel();
            return shooter;
        }

        private void BuildModel()
        {
            // Satın alınan gerçek model (ContentOverrides soldier) SoldierModel.Build içinde otomatik kullanılır; yoksa prosedürel kalır.
            var look = SoldierLook.ForTeam(0, new System.Random(4242));
            _model = SoldierModel.Build(transform, look, null, false, GameLayers.Default);
            if (_model == null)
                return;

            _model.AutoSyncEquipment = false;
            _model.AutoPlayDeath = false;
            _model.AutoSyncWeapon = false;
            _model.ThrottleWhenOffscreen = false;
            _model.SetEquipment(2, 2, 1);
            _model.SetRank(MilitaryRank.Cavus);
            _model.SetSeated(false);
            _model.SetLocomotion(Vector3.zero, Stance.Standing, true);
            _model.SetAimPitch(0f);
            _model.SetVisible(true);

            if (WeaponCatalog.TryGet(WeaponIds.Mpt76, out var weapon) && weapon != null)
                BootstrapUtility.Try(() => _model.HoldWeapon(weapon), "Vitrin askeri: MPT-76");
        }

        /// <summary>Hedef noktasını günceller (nişan eğimi için).</summary>
        public void SetTarget(Vector3 targetPoint)
        {
            _targetPoint = targetPoint;
        }

        /// <summary>Plan başında: nişan alır, şarjör jesti isteğe bağlı.</summary>
        public void Aim()
        {
            if (_model == null)
                return;
            var muzzle = _model.MuzzlePosition;
            var d = _targetPoint - muzzle;
            var flat = new Vector2(d.x, d.z).magnitude;
            var pitch = flat > 0.1f ? -Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg : 0f;
            _model.SetAimPitch(pitch);
        }

        public void Reload(float seconds)
        {
            if (_model != null)
                _model.PlayReloadGesture(seconds);
        }

        /// <summary>Tek atış: sekme + namlu alevi + hedefe mermi izi (hız sınırlı).</summary>
        public void Fire()
        {
            if (_model == null || Time.unscaledTime < _nextShot)
                return;
            _nextShot = Time.unscaledTime + 0.09f;

            try
            {
                _model.PlayFire();
                var muzzle = _model.MuzzlePosition;
                var dir = _targetPoint - muzzle;
                if (dir.sqrMagnitude < 1e-3f)
                    dir = _forward;
                dir.Normalize();
                GameVfx.MuzzleFlash(muzzle, dir, 1.4f);
                var spread = UnityEngine.Random.insideUnitSphere * 0.15f;
                GameVfx.Tracer(muzzle + dir * 0.3f, _targetPoint + spread, 0.12f, 0.04f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Vitrin askeri ateş efekti: " + e.Message);
            }
        }
    }
}
