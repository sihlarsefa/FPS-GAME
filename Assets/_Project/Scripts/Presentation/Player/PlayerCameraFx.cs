using System;
using Project.Infrastructure.Player;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// CoD benzeri kamera hissi katmanı. <see cref="FirstPersonCameraController"/> ile aynı nesnede çalışır
    /// (yoksa çalışma anında otomatik eklenir): koşu FOV'u (+6°) ve güçlü sallantı, kayma alçalması/yatışı,
    /// tırmanma kamera yayı, silah değiştirme dürtmesi, yönlü hasar/patlama sarsıntısı, boşta nefes sallanması.
    /// Sarsıntı şiddeti ayardan (<c>GameSettings.CameraShakeIntensity</c>) okunur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraFx : MonoBehaviour
    {
        private FirstPersonCameraController _camera;
        private CharacterControllerMotor _motor;
        private float _sprint;
        private float _slideBlend;
        private float _mantleBlend;
        private float _nudge;
        private float _nudgeVelocity;
        private float _idle;
        private bool _subscribed;

        /// <summary>Sahnedeki yerel oyuncunun efekt katmanı (patlama/hasar bildirimleri için).</summary>
        public static PlayerCameraFx Local { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<FirstPersonCameraController>();
        }

        private void OnEnable()
        {
            Local = this;
            Subscribe();
        }

        private void OnDisable()
        {
            if (Local == this)
                Local = null;
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;
            _subscribed = true;
            try
            {
                if (GameSession.Settings != null)
                {
                    GameSession.Settings.Changed += OnSettingsChanged;
                    OnSettingsChanged(GameSession.Settings.Current);
                }
            }
            catch (Exception) { /* ayarlar henüz hazır değil */ }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;
            _subscribed = false;
            try
            {
                if (GameSession.Settings != null)
                    GameSession.Settings.Changed -= OnSettingsChanged;
            }
            catch (Exception) { }
        }

        private void OnSettingsChanged(Project.Core.Domain.GameSettings s)
        {
            if (s != null && _camera != null)
                _camera.ShakeScale = s.CameraShakeIntensity;
        }

        /// <summary>Silah değiştirirken kamera hafifçe aşağı iner ve toparlanır.</summary>
        public void NudgeWeaponSwitch()
        {
            _nudgeVelocity += 14f;
        }

        /// <summary>Yönlü hasar sarsıntısı: kaynak dünya konumu (null/sıfır = önden), hasar 0..100.</summary>
        public void Hit(Vector3 worldSource, float damage)
        {
            if (_camera == null)
                return;
            var strength = Mathf.Clamp01(damage / 60f);
            var local = transform.InverseTransformPoint(worldSource);
            CameraFeelMath.DirectionalPunch(local.x, local.z, strength, out var p, out var y, out var r);
            _camera.AddPunch(p, y, r);
            _camera.Shake(0.15f + 0.35f * strength, 0.25f);
        }

        /// <summary>Patlama sarsıntısı: mesafeyle azalır, yönlü yumruk + uzun Perlin sarsıntısı.</summary>
        public void Explosion(Vector3 worldPosition, float radius)
        {
            if (_camera == null)
                return;
            var distance = Vector3.Distance(transform.position, worldPosition);
            var k = CameraFeelMath.ExplosionIntensity(distance, Mathf.Max(radius * 3f, 12f));
            if (k <= 0.01f)
                return;
            var local = transform.InverseTransformPoint(worldPosition);
            CameraFeelMath.DirectionalPunch(local.x, local.z, k, out var p, out var y, out var r);
            _camera.AddPunch(p * 1.4f, y * 1.4f, r * 1.4f);
            _camera.Shake(Mathf.Clamp01(0.3f + k), 0.5f + 0.9f * k);
        }

        /// <summary>Statik kısayol: yerel oyuncu varsa patlamayı bildirir.</summary>
        public static void NotifyExplosion(Vector3 worldPosition, float radius)
        {
            if (Local != null)
                Local.Explosion(worldPosition, radius);
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;
            if (_motor == null)
                _motor = _camera.Motor != null ? _camera.Motor : GetComponentInParent<CharacterControllerMotor>();
            if (_motor == null)
                return;

            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f)
                return;

            var scale = Mathf.Max(0.0001f, _camera.ShakeScale);
            var shakeOn = _camera.ShakeScale > 0.001f;

            // Koşu: FOV +6° ve güçlü sallantı (ADS'te FOV ofseti zaten zoom'a bölünür).
            var targetSprint = _motor.IsSprinting && _motor.IsGrounded ? 1f : 0f;
            _sprint = Mathf.MoveTowards(_sprint, targetSprint, dt * (targetSprint > _sprint ? 4.5f : 3f));
            var fov = CameraFeelMath.SprintFovOffset(_sprint * _sprint * (3f - 2f * _sprint));

            // Kayma: kamera aşağı + hafif yatış.
            _slideBlend = Mathf.MoveTowards(_slideBlend, _motor.IsSliding ? 1f : 0f, dt * (_motor.IsSliding ? 8f : 3.5f));
            // Tırmanma: öne eğilip toparlanan yay.
            _mantleBlend = _motor.IsMantling ? 1f : Mathf.MoveTowards(_mantleBlend, 0f, dt * 4f);

            // Silah dürtmesi: sönümlü yay.
            _nudgeVelocity += (-160f * _nudge - 18f * _nudgeVelocity) * dt;
            _nudge += _nudgeVelocity * dt;

            _idle += dt;
            CameraFeelMath.IdleSway(_idle, _motor.IsGrounded && _motor.SpeedNormalized < 0.05f ? 1f : 0.25f, out var idleP, out var idleY);

            var euler = new Vector3(idleP, idleY, 0f) * scale;
            var position = Vector3.zero;

            euler.z += _slideBlend * 4f * scale;
            position.y -= _slideBlend * 0.12f;

            if (_mantleBlend > 0.001f)
            {
                var p = _motor.IsMantling ? _motor.MantleProgress : 1f;
                var arc = CameraFeelMath.MantleCameraPitch(p) * _mantleBlend;
                euler.x += arc * Mathf.Max(0.3f, scale);
                position.y += Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * 0.06f * _mantleBlend;
            }

            euler.x += _nudge * 0.35f * scale;
            position.y -= Mathf.Abs(_nudge) * 0.004f * scale;

            _camera.SetFxLayer(position, shakeOn ? euler : new Vector3(0f, 0f, euler.z * 0f), fov, Mathf.Lerp(1f, 1.5f, _sprint));
        }

        // ------------------------------------------------------------------ Otomatik ekleme

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("PlayerCameraFxBootstrap") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<FxAttacher>();
        }

        /// <summary>Saniyede bir, FPP kamerası olup efekt katmanı olmayan nesnelere <see cref="PlayerCameraFx"/> ekler.</summary>
        private sealed class FxAttacher : MonoBehaviour
        {
            private float _next;

            private void Update()
            {
                if (Time.unscaledTime < _next)
                    return;
                _next = Time.unscaledTime + 1f;
                var all = FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None);
                for (var i = 0; i < all.Length; i++)
                {
                    var c = all[i];
                    if (c != null && c.GetComponent<PlayerCameraFx>() == null)
                        c.gameObject.AddComponent<PlayerCameraFx>();
                }
            }
        }
    }
}
