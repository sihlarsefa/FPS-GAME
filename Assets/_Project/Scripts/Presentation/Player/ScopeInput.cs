using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Dürbün girdi/sürücü kancası (PlayerWeaponHandler'a dokunmaz): fare tekerleği = değişken zoom,
    /// PageUp/PageDown = sıfırlama menzili, nefes tutma (Shift = Sprint tuşu, handler zaten yönetir) verisini
    /// ScopeController'a aktarır. Handler her karede <see cref="Tick"/> çağırır.
    /// </summary>
    public sealed class ScopeInput : IDisposable
    {
        public const float DefaultBaseFov = 70f;

        private ScopeController _controller;
        private GameObject _host;
        private string _weaponId;
        private float _magnification = 1f;
        private float _min = 1f;
        private float _max = 1f;
        private float _zeroingToastUntil;
        private Vector2 _sway;
        private ScopeGlint _glint;

        /// <summary>true iken handler fare tekerleğiyle silah değiştirmeyi yok saymalı (zoom için kullanılıyor).</summary>
        public bool ConsumesWheel { get; private set; }

        /// <summary>Hedef büyütme: handler CurrentZoom yerine bunu kullanabilir (PiP'te ana kamera 1x kalır).</summary>
        public float Magnification => _magnification;

        /// <summary>PiP aktifse ana kamera zoomu 1 olmalı; değilse verilen varsayılanı döndürür.</summary>
        public float MainCameraZoom(float handlerZoom) => Scope.PipActive ? 1f : (_magnification > 1.05f ? Mathf.Max(handlerZoom, 1f) : handlerZoom);

        /// <summary>HUD için "Sıfırlama: 300 m" gibi kısa metin (yakın zamanda değiştiyse), yoksa null.</summary>
        public string ZeroingToast => Time.unscaledTime < _zeroingToastUntil && !string.IsNullOrEmpty(_weaponId)
            ? "Sıfırlama: " + Scope.ZeroingMeters(_weaponId) + " m"
            : null;

        /// <summary>
        /// Her kare çağrılır. scoped: handler.IsScoped; aiming: handler.IsAiming; breath: handler verisi.
        /// lens: viewmodel üzerindeki dürbün lens renderer'ı (yoksa null; tam ekran yedeği kullanılır).
        /// </summary>
        public void Tick(
            string weaponId,
            bool aiming,
            bool scoped,
            float aimBlend,
            bool holdingBreath,
            float breathRemaining,
            Camera camera,
            Renderer lens,
            Transform scopeAnchor,
            Vector2 sway,
            float dt)
        {
            if (weaponId != _weaponId)
            {
                _weaponId = weaponId;
                ResolveRange(weaponId);
            }

            if (!WeaponCatalog.TryGet(weaponId ?? string.Empty, out var def) || def == null)
            {
                ConsumesWheel = false;
                Drive(false, 0f, holdingBreath, breathRemaining, camera, lens, sway, dt, def: null);
                return;
            }

            var hasOptic = def.HasScope || def.AdsZoom >= 1.1f;
            var variable = ScopeMath.IsVariable(_min, _max) && def.HasScope;
            ConsumesWheel = aiming && variable;

            if (aiming && def.HasScope)
                PollKeys(dt);

            Drive(scoped || (aiming && hasOptic && !def.HasScope), aimBlend, holdingBreath, breathRemaining, camera, lens, sway, dt, def);

            if (_glint != null && camera != null)
            {
                var anchor = scopeAnchor != null ? scopeAnchor : camera.transform;
                // Bot gözlemci konumu yoksa kamera kullanılır (yalnız görsel doğrulama); bot algısı IntensityAt'i çağırır.
                _glint.Tick(ScopeMath.TierPlan(QualityLevel()).Glint, scoped, anchor.position, anchor.forward, camera.transform.position + camera.transform.forward * -50f);
            }
        }

        private void PollKeys(float dt)
        {
            try
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb[Key.PageUp].wasPressedThisFrame)
                    {
                        Scope.StepZeroing(_weaponId, +1);
                        _zeroingToastUntil = Time.unscaledTime + 1.5f;
                    }
                    else if (kb[Key.PageDown].wasPressedThisFrame)
                    {
                        Scope.StepZeroing(_weaponId, -1);
                        _zeroingToastUntil = Time.unscaledTime + 1.5f;
                    }
                }

                var mouse = Mouse.current;
                if (mouse != null && ScopeMath.IsVariable(_min, _max))
                {
                    var y = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(y) > 0.01f)
                        _magnification = ScopeMath.StepMagnification(_magnification, y, _min, _max);
                }
            }
            catch (Exception)
            {
                // Girdi sistemi yoksa yok say.
            }
        }

        private void ResolveRange(string weaponId)
        {
            if (WeaponCatalog.TryGet(weaponId ?? string.Empty, out var def) && def != null)
            {
                ScopeMath.MagnificationRange(def.AdsZoom, def.HasScope, def.Category == WeaponCategory.Sniper, out _min, out _max);
                _magnification = def.AdsZoom;
                if (_magnification < _min) _magnification = _min;
                if (_magnification > _max) _magnification = _max;
            }
            else
            {
                _min = _max = _magnification = 1f;
            }
        }

        private void Drive(bool active, float aimBlend, bool holding, float breath, Camera camera, Renderer lens, Vector2 sway, float dt, WeaponDefinitionData def)
        {
            if (camera == null)
                return;
            EnsureHost(camera);
            if (_controller == null)
                return;

            _controller.Bind(camera, lens);
            _sway = Vector2.Lerp(_sway, sway, 1f - Mathf.Exp(-10f * Mathf.Max(0f, dt)));
            var zoom = def != null ? _magnification : 1f;
            var reticle = def != null ? ScopeMath.SelectReticle(def.AdsZoom, def.HasScope) : ReticleKind.None;
            var holdover = Vector2.zero; // Retikül namlu açısıyla hizalı; holdover kaydırması gerekmiyor.

            var frame = new ScopeFrame
            {
                Scoped = active,
                AimBlend = aimBlend,
                Magnification = zoom,
                BaseFov = DefaultBaseFov,
                Sway = _sway,
                Misalign = Mathf.Clamp01(_sway.magnitude * 0.5f),
                HoldingBreath = holding,
                BreathRemaining = breath,
                BreathMax = HoldBreathState.MaxSeconds,
                Reticle = reticle,
                QualityTier = QualityLevel(),
                Dt = dt,
                Time = Time.time,
                ReticleHoldover = holdover
            };
            _controller.Apply(frame);
        }

        private static int QualityLevel()
        {
            return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, 3);
        }

        private void EnsureHost(Camera camera)
        {
            if (_controller != null)
                return;
            _host = new GameObject("ScopeController") { hideFlags = HideFlags.DontSave };
            _host.transform.SetParent(camera.transform.parent, false);
            _controller = _host.AddComponent<ScopeController>();
            _glint = _host.AddComponent<ScopeGlint>();
        }

        public void Dispose()
        {
            if (_host != null)
                UnityEngine.Object.Destroy(_host);
            _host = null;
            _controller = null;
            _glint = null;
            Scope.ClearRuntime();
        }
    }
}
