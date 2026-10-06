using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Player;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Bağımsız geri bildirim yöneticisi (sahne kurulumu gerektirmez, oyun yüklenince kendini kurar):
    /// yakın ıska -> bastırma ölçeri (vinyet, renk solması, nişan sallantısı); hasar alınca yönlü kamera yumruğu;
    /// düşük canda ses boğuklaştırma (AudioListener üstünde alçak geçiren filtre). İsabet işareti sesleri
    /// (kafa/gövde/öldürme) CrosshairView'da, kalp atışı + kırmızı kenar HudScreenEffectsView'dadır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFeedbackDirector : MonoBehaviour
    {
        private static HitFeedbackDirector _instance;

        private SuppressionView _suppression;
        private FirstPersonCameraController _camera;
        private IEventBus _bus;
        private Action<PlayerDamagedEvent> _onDamaged;
        private float _nextLookup;
        private AudioLowPassFilter _lowPass;
        private AudioListener _lowPassListener;
        private bool _nearMissHooked;
        private DamageNumbersView _numbers;
        private Action<HitConfirmedEvent> _onHit;
        private Action<PlayerDiedEvent> _onDied;
        private KillBanner _killBanner;
        private readonly System.Collections.Generic.List<PendingFarConfirm> _pendingFar = new System.Collections.Generic.List<PendingFarConfirm>(4);

        private struct PendingFarConfirm
        {
            public float DueAt; public float Distance; public bool Head, Kill, Armor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;
            var go = new GameObject("HitFeedbackDirector");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HitFeedbackDirector>();
        }

        private void Awake()
        {
            _suppression = SuppressionView.Create(gameObject);
            _onDamaged = OnDamaged;
            _onHit = OnHitConfirmed;
            _onDied = OnDied;
            _numbers = DamageNumbersView.Create(gameObject);
            _killBanner = KillBanner.Create(gameObject);
        }

        private void OnEnable()
        {
            if (!_nearMissHooked)
            {
                BallisticsSystem.NearMiss += OnNearMiss;
                _nearMissHooked = true;
            }
        }

        private void OnDisable()
        {
            if (_nearMissHooked)
            {
                BallisticsSystem.NearMiss -= OnNearMiss;
                _nearMissHooked = false;
            }
            Unbind();
            if (_camera != null)
                _camera.SetSuppressionSway(0f);
            if (_lowPass != null)
                _lowPass.enabled = false;
        }

        private void OnNearMiss(float distance)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (_suppression == null || local == null || !local.IsAlive)
                return;
            _suppression.OnNearMiss(distance);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            EnsureBinding();
            for (var i = _pendingFar.Count - 1; i >= 0; i--)
            {
                var p = _pendingFar[i];
                if (Time.unscaledTime < p.DueAt) continue;
                _pendingFar.RemoveAt(i);
                HitTones.PlayFar(p.Distance, p.Head, p.Kill, p.Armor);
            }

            var local = CombatantRegistry.LocalPlayer;
            var alive = local != null && local.IsAlive;
            if (_suppression == null)
                return;
            if (!alive)
                _suppression.ResetMeter();
            else
                _suppression.Tick(dt);

            if (_camera == null && Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + 1f;
                _camera = FindFirstObjectByType<FirstPersonCameraController>();
            }
            if (_camera != null)
                _camera.SetSuppressionSway(ScreenEffectsMath.SwayAmount(_suppression.Meter));

            UpdateMuffle(alive ? local.State.Normalized : 1f);
        }

        private void UpdateMuffle(float healthFraction)
        {
            var cutoff = ScreenEffectsMath.MuffleCutoff(healthFraction);
            var needed = cutoff < 21000f;
            if (!needed && (_lowPass == null || !_lowPass.enabled))
                return;

            var listener = _lowPassListener != null && _lowPassListener.isActiveAndEnabled
                ? _lowPassListener
                : FindFirstObjectByType<AudioListener>();
            if (listener == null)
                return;
            if (_lowPass == null || _lowPassListener != listener)
            {
                _lowPassListener = listener;
                _lowPass = listener.GetComponent<AudioLowPassFilter>();
                if (_lowPass == null)
                    _lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }

            _lowPass.cutoffFrequency = cutoff;
            _lowPass.enabled = needed;
        }

        private void EnsureBinding()
        {
            var services = GameContext.Services;
            IEventBus bus = null;
            if (services != null && !services.TryResolve(out bus))
                bus = null;

            if (ReferenceEquals(bus, _bus))
                return;
            Unbind();
            if (bus == null)
                return;
            _bus = bus;
            _bus.Subscribe(_onDamaged);
            _bus.Subscribe(_onHit);
            _bus.Subscribe(_onDied);
        }

        private void Unbind()
        {
            if (_bus != null)
            {
                try { _bus.Unsubscribe(_onDamaged); _bus.Unsubscribe(_onHit); _bus.Unsubscribe(_onDied); }
                catch (Exception) { }
            }
            _bus = null;
        }

        private void OnHitConfirmed(HitConfirmedEvent e)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (local == null || local.Id != e.AttackerId || e.VictimId == e.AttackerId || _numbers == null)
                return;
            var found = CombatantRegistry.TryGet(e.VictimId, out var victim);
            var pos = found
                ? victim.transform.position + Vector3.up * 1.9f
                : Vector3.zero;
            _numbers.Hit(e.VictimId, pos, e.Damage, e.IsHeadshot, e.IsKill, e.ArmorAbsorbed, Time.unscaledTime);
            if (victim != null)
            {
                var dist = Vector3.Distance(local.transform.position, victim.transform.position);
                var delay = HitTones.ConfirmDelay(dist, HitTones.DefaultMuzzleSpeed);
                if (delay > 0f && _pendingFar.Count < 8)
                    _pendingFar.Add(new PendingFarConfirm { DueAt = Time.unscaledTime + delay, Distance = dist, Head = e.IsHeadshot, Kill = e.IsKill, Armor = e.ArmorAbsorbed });
            }
        }

        private void OnDied(PlayerDiedEvent e)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (local == null || _numbers == null || e.VictimId == local.Id)
                return;
            var now = Time.unscaledTime;
            if (e.KillerId == local.Id && _killBanner != null)
            {
                var name = CombatantRegistry.TryGet(e.VictimId, out var vc) ? vc.DisplayName : null;
                var farDist = vc != null ? Vector3.Distance(local.transform.position, vc.transform.position) : 0f;
                var sub = HitTones.IsFarKill(farDist, true) ? "UZAK MENZİL" : null;
                var chain = _killBanner.OnLocalKill(name, e.WeaponId, e.IsHeadshot, now, sub);
                if (_camera != null)
                    _camera.AddPunch(-KillChainRules.PunchPitch(chain, e.IsHeadshot), 0f, 0f);
            }
            if (_numbers.Stacker.IsAssist(e.VictimId, e.KillerId == local.Id, now))
            {
                _numbers.Assist(now);
                if (_killBanner != null)
                    _killBanner.OnLocalAssist(now);
            }
        }

        private void OnDamaged(PlayerDamagedEvent e)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (local == null || local.Id != e.VictimId || e.DamageAmount <= 0f || _camera == null)
                return;
            if (string.Equals(e.WeaponId, DamageSourceIds.Zone, StringComparison.Ordinal))
                return;

            var angle = 0f;
            Vector3 source;
            if (e.HasSourcePosition)
                source = DamageIndicatorView.ToVector(e.SourcePosition);
            else if (e.AttackerId.IsValid && e.AttackerId != e.VictimId && CombatantRegistry.TryGet(e.AttackerId, out var attacker))
                source = attacker.transform.position;
            else
                source = Vector3.zero;

            if (source.sqrMagnitude > 0.0001f)
            {
                var to = source - _camera.transform.position;
                to.y = 0f;
                var fwd = _camera.transform.forward;
                fwd.y = 0f;
                if (to.sqrMagnitude > 0.0001f && fwd.sqrMagnitude > 0.0001f)
                    angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            }

            ScreenEffectsMath.PunchFor(e.DamageAmount, angle, out var pitch, out var yaw, out var roll);
            _camera.AddPunch(pitch, yaw, roll);
        }
    }
}
