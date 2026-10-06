using System;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Flaş bombası patlaması: yerel oyuncuya beyaz ekran + halka art-görüntüsü + kulak çınlaması (AudioMix);
    /// etki, bakış açısı ve mesafeye göre ölçeklenir, görüş hattı kapalıysa yoktur. Botlar için
    /// <see cref="BotFlashed"/> olayı yayınlanır (BotController abone olup görüşünü/nişanını bozar).
    /// </summary>
    public static class FlashbangSystem
    {
        /// <summary>Otoritede, flaşlanan bot için (savaşan, kör süresi sn, etki 0..1).</summary>
        public static event Action<Combatant, float, float> BotFlashed;

        /// <summary>Yerel oyuncunun son flaş etkisi (HUD/test için).</summary>
        public static float LastLocalIntensity { get; private set; }

        public static void Detonate(Vector3 position, PlayerId thrower)
        {
            var radius = ThrowableRules.FlashRadius;

            try
            {
                GameAudio.Play(SoundId.Explosion, position, 0.75f, UnityEngine.Random.Range(1.55f, 1.8f), 160f);
                Acoustics.OnExplosion(position, 0.6f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                FlashOverlay.PopLight(position, radius);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var all = CombatantRegistry.All;
            for (var i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;
                var c = all[i];
                if (c == null || !c.IsAlive)
                    continue;

                var isLocal = c.IsLocalPlayer;
                if (!isLocal && !(c.IsBot && GameContext.HasAuthority))
                    continue;

                var eye = c.EyePosition;
                var delta = position - eye;
                var distance = delta.magnitude;
                if (distance >= radius)
                    continue;

                var forward = isLocal ? LocalForward(c) : c.transform.forward;
                var angle = distance < 0.05f ? 0f : Vector3.Angle(forward, delta);
                var los = distance < 0.3f || !Physics.Linecast(position + Vector3.up * 0.1f, eye, GameLayers.LineOfSightMask,
                    QueryTriggerInteraction.Ignore);
                var intensity = ThrowableRules.FlashIntensity(angle, distance, radius, los);
                if (intensity <= 0f)
                    continue;

                var seconds = ThrowableRules.FlashBlindSeconds(intensity);
                if (isLocal)
                {
                    LastLocalIntensity = intensity;
                    try
                    {
                        FlashOverlay.Trigger(intensity, seconds);
                        AudioMix.State.TriggerTinnitus(intensity);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                else
                {
                    try
                    {
                        BotFlashed?.Invoke(c, seconds, intensity);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        private static Vector3 LocalForward(Combatant local)
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.forward : local.transform.forward;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            BotFlashed = null;
            LastLocalIntensity = 0f;
        }
    }

    /// <summary>Ekran beyazlığı + halka art-görüntüsü (OnGUI, ek bağımlılık yok) ve kısa patlama ışığı.</summary>
    public sealed class FlashOverlay : MonoBehaviour
    {
        private static FlashOverlay _instance;

        private Texture2D _white;
        private Texture2D _ring;
        private Light _light;
        private float _lightLife;
        private float _lightPeak;
        private float _start = -999f;
        private float _seconds;
        private float _intensity;

        public static float CurrentAlpha => _instance != null ? _instance.Alpha() : 0f;

        public static void Trigger(float intensity, float seconds)
        {
            var host = Get();
            if (host == null)
                return;
            // Daha şiddetli/uzun etki kısa olanı ezer.
            if (host.Alpha() > intensity * 0.9f && host._seconds - (Time.unscaledTime - host._start) > seconds)
                return;
            host._start = Time.unscaledTime;
            host._seconds = seconds;
            host._intensity = intensity;
        }

        public static void PopLight(Vector3 position, float radius)
        {
            var host = Get();
            if (host == null)
                return;
            if (host._light == null)
            {
                var go = new GameObject("FlashLight");
                go.transform.SetParent(host.transform, false);
                host._light = go.AddComponent<Light>();
                host._light.type = LightType.Point;
                host._light.shadows = LightShadows.None;
                host._light.color = new Color(0.95f, 0.97f, 1f);
            }

            host._light.transform.position = position + Vector3.up * 0.3f;
            host._light.range = radius * 1.4f;
            host._lightPeak = 40f;
            host._lightLife = 0.25f;
            host._light.intensity = host._lightPeak;
            host._light.enabled = true;
        }

        private static FlashOverlay Get()
        {
            if (_instance != null)
                return _instance;
            var go = new GameObject("[FlashOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FlashOverlay>();
            return _instance;
        }

        private float Alpha()
        {
            if (_seconds <= 0f)
                return 0f;
            return ThrowableRules.FlashAlpha(Time.unscaledTime - _start, _seconds, _intensity);
        }

        private void Update()
        {
            if (_light != null && _light.enabled)
            {
                _lightLife -= Time.unscaledDeltaTime;
                if (_lightLife <= 0f)
                    _light.enabled = false;
                else
                    _light.intensity = _lightPeak * (_lightLife / 0.25f);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var a = Alpha();
            if (a <= 0.003f)
                return;

            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }

            if (_ring == null)
                _ring = BuildRing(128);

            var old = GUI.color;
            GUI.depth = -10000;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);

            // Art-görüntü halkası: beyaz tam geçtikten sonra da gözde kalan koyu/mor halka.
            var t = Mathf.Clamp01((Time.unscaledTime - _start) / Mathf.Max(0.1f, _seconds));
            var ringAlpha = Mathf.Clamp01(_intensity * 1.2f) * Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI) * 0.55f;
            if (ringAlpha > 0.01f)
            {
                var size = Mathf.Min(Screen.width, Screen.height) * Mathf.Lerp(0.35f, 0.8f, t);
                GUI.color = new Color(0.55f, 0.45f, 0.95f, ringAlpha);
                GUI.DrawTexture(new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size), _ring);
            }

            GUI.color = old;
        }

        private static Texture2D BuildRing(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            var c = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    var v = Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) / 0.18f);
                    v *= v;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(v * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
