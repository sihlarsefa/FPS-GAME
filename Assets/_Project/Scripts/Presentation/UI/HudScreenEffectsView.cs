using System;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tam ekran efektleri (HUD'un en altında): düşük canda nabız gibi atan kırmızı kenar karartması (+ kalp atışı
    /// sesi), hasar alınca kısa kırmızı parlama, harekât alanı dışındayken mavi renk tonu ve kenar karartması
    /// (girişte uyarı sesi), ölüyken koyulaşma. Yalnızca görünür olduğunda çizilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudScreenEffectsView : MonoBehaviour
    {
        private const float LowHealthThreshold = 0.35f;
        private const float HeartbeatThreshold = 0.22f;
        private const float FlashDecay = 2.6f;

        private HudContext _ctx;
        private Image _zoneTint;
        private Image _zoneVignette;
        private Image _lowHealth;
        private Image _flash;
        private Image _deathShade;

        private float _flashAlpha;
        private float _zoneAlpha;
        private float _deathAlpha;
        private float _pulse;
        private bool _wasOutside;
        private float _nextZoneWarning;
        private AudioSource _heartbeat;
        private float _nextHeartbeatTry;

        public RectTransform Root { get; private set; }

        public static HudScreenEffectsView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("ScreenEffects", parent);
            var view = root.gameObject.AddComponent<HudScreenEffectsView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            _zoneTint = HudBuild.FillImage("ZoneTint", Root, UiSprites.White, UiTheme.WithAlpha(UiTheme.ZoneBlue, 0f));
            _zoneVignette = HudBuild.FillImage("ZoneVignette", Root, UiSprites.Vignette, UiTheme.WithAlpha(UiTheme.ZoneBlue, 0f));
            _lowHealth = HudBuild.FillImage("LowHealth", Root, UiSprites.Vignette, UiTheme.WithAlpha(UiTheme.HealthLow, 0f));
            _flash = HudBuild.FillImage("DamageFlash", Root, UiSprites.Vignette, UiTheme.WithAlpha(UiTheme.HealthLow, 0f));
            _deathShade = HudBuild.FillImage("DeathShade", Root, UiSprites.White, new Color(0.05f, 0f, 0f, 0f));

            _zoneTint.enabled = false;
            _zoneVignette.enabled = false;
            _lowHealth.enabled = false;
            _flash.enabled = false;
            _deathShade.enabled = false;
        }

        /// <summary>Hasar parlaması (miktar ile ölçeklenir).</summary>
        public void Flash(float damage)
        {
            var strength = Mathf.Clamp(0.18f + damage / 60f, 0.18f, 0.6f);
            _flashAlpha = Mathf.Max(_flashAlpha, strength);
        }

        /// <summary>HUD denetleyicisi her karede çağırır. <paramref name="outsideZone"/>: oyuncu alan dışında.</summary>
        public void Tick(float deltaTime, bool outsideZone, bool hudVisible)
        {
            var local = _ctx.Local;
            var alive = _ctx.LocalAlive && hudVisible;
            var fraction = 1f;
            if (local != null && local.IsInitialized)
            {
                var state = local.State;
                fraction = state.Max > 0f ? Mathf.Clamp01(state.Current / state.Max) : 0f;
            }

            // Düşük can.
            var low = alive && fraction < LowHealthThreshold;
            if (low)
            {
                var severity = 1f - fraction / LowHealthThreshold;
                _pulse += deltaTime * Mathf.Lerp(3.5f, 7.5f, severity);
                var wave = 0.5f + 0.5f * Mathf.Sin(_pulse);
                var alpha = Mathf.Lerp(0.25f, 0.7f, severity) * Mathf.Lerp(0.65f, 1f, wave);
                SetImage(_lowHealth, alpha);
            }
            else
            {
                SetImage(_lowHealth, 0f);
            }

            UpdateHeartbeat(alive && fraction < HeartbeatThreshold);

            // Hasar parlaması.
            if (_flashAlpha > 0f)
            {
                _flashAlpha = Mathf.Max(0f, _flashAlpha - deltaTime * FlashDecay * Mathf.Max(0.35f, _flashAlpha));
                SetImage(_flash, alive ? _flashAlpha : 0f);
            }
            else
            {
                SetImage(_flash, 0f);
            }

            // Alan dışı.
            var outside = alive && outsideZone;
            _zoneAlpha = Mathf.MoveTowards(_zoneAlpha, outside ? 1f : 0f, deltaTime * 2.5f);
            SetImage(_zoneTint, _zoneAlpha * 0.16f);
            SetImage(_zoneVignette, _zoneAlpha * 0.55f);

            if (outside && !_wasOutside && Time.unscaledTime >= _nextZoneWarning)
            {
                _nextZoneWarning = Time.unscaledTime + 6f;
                PlaySound(SoundId.ZoneWarning, 0.6f);
            }

            _wasOutside = outside;

            // Ölüm koyulaşması.
            var dead = hudVisible && !_ctx.LocalAlive && local != null && local.IsInitialized;
            _deathAlpha = Mathf.MoveTowards(_deathAlpha, dead ? 0.38f : 0f, deltaTime * 0.8f);
            SetImage(_deathShade, _deathAlpha);
        }

        /// <summary>Tüm efektleri ve döngü sesini kapatır.</summary>
        public void ResetEffects()
        {
            _flashAlpha = 0f;
            _zoneAlpha = 0f;
            _deathAlpha = 0f;
            SetImage(_lowHealth, 0f);
            SetImage(_flash, 0f);
            SetImage(_zoneTint, 0f);
            SetImage(_zoneVignette, 0f);
            SetImage(_deathShade, 0f);
            UpdateHeartbeat(false);
        }

        private static void SetImage(Image image, float alpha)
        {
            if (image == null)
                return;

            var show = alpha > 0.003f;
            if (image.enabled != show)
                image.enabled = show;
            if (show)
                HudBuild.SetAlpha(image, alpha);
        }

        private void UpdateHeartbeat(bool on)
        {
            if (on)
            {
                if (_heartbeat == null && Time.unscaledTime >= _nextHeartbeatTry)
                {
                    _nextHeartbeatTry = Time.unscaledTime + 2f;
                    try
                    {
                        _heartbeat = GameAudio.StartLoop(SoundId.Heartbeat, null, 0.55f, false);
                    }
                    catch (Exception)
                    {
                        _heartbeat = null;
                    }
                }

                return;
            }

            if (ReferenceEquals(_heartbeat, null))
                return;

            try
            {
                GameAudio.StopLoop(_heartbeat);
            }
            catch (Exception)
            {
                // ses sistemi kapanmış olabilir
            }

            _heartbeat = null;
        }

        private static void PlaySound(SoundId id, float volume)
        {
            try
            {
                GameAudio.Play2D(id, volume);
            }
            catch (Exception)
            {
                // ses sistemi yoksa sessiz devam
            }
        }

        private void OnDisable()
        {
            UpdateHeartbeat(false);
        }

        private void OnDestroy()
        {
            UpdateHeartbeat(false);
        }
    }
}
