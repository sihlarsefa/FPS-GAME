using System;
using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Gece Görüş Gözlüğü ekran efekti. Oyuncu denetleyicisi her karede <see cref="Drive"/> çağırır (gözlük var mı, tuşa basıldı mı).
    /// Yeşil renk filtresi, pozlama artışı, parazit (OnGUI gren dokusu), vinyet ve bloom (el feneri/ateş parlaması) içeren
    /// ayrı yüksek öncelikli bir global Volume yumuşakça karıştırılır; sis yoğunluğu da gözlük açıkken azalır
    /// (Atmosphere gece ön ayarıyla uyumlu). URP yoksa sessizce etkisizdir.
    /// </summary>
    public sealed class NightVisionEffect : MonoBehaviour
    {
        private const float BlendSpeed = 5f;

        private static NightVisionEffect _instance;
        private static readonly NightVisionBattery BatteryModel = new NightVisionBattery();

        public static bool IsActive => BatteryModel.IsOn;
        public static float Charge => BatteryModel.Charge;

        /// <summary>Bot algı çarpanı için: oyuncunun gözlüğü o an açık mı (yalnız bilgi).</summary>
        public static NightVisionBattery Battery => BatteryModel;

        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private ColorAdjustments _ca;
        private ShadowsMidtonesHighlights _smh;
        private float _ambientLuma = 0.05f;
        private float _nextAmbient;
        private static readonly Vector3[] ProbeDir = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        private static readonly Color[] ProbeOut = new Color[6];
        private float _blend;
        private Texture2D _noise;
        private float _savedFog = -1f;
        private float _appliedFog = -1f;

        /// <summary>
        /// Karede bir: gözlük sahipliği ve tuş. Bildirim metni (pil bitti / zayıf) varsa döner, yoksa null.
        /// Gözlük yoksa (ölüm / envanter) efekt kapanır; pil kapalıyken dolar.
        /// </summary>
        public static string Drive(bool hasGoggles, bool togglePressed, float dt)
        {
            string message = null;
            if (!hasGoggles)
            {
                BatteryModel.ForceOff();
            }
            else if (togglePressed && !BatteryModel.Toggle())
            {
                message = "Gece görüş pili zayıf — şarj oluyor";
            }

            if (BatteryModel.Tick(dt))
                message = "Gece görüş pili bitti";

            if (BatteryModel.IsOn || _instance != null)
                Ensure();

            return message;
        }

        /// <summary>Yeni maç: pil dolu, efekt kapalı.</summary>
        public static void ResetState()
        {
            BatteryModel.ForceOff();
            BatteryModel.Refill();
        }

        private static void Ensure()
        {
            if (_instance != null)
                return;

            try
            {
                var go = new GameObject("[NightVision]");
                _instance = go.AddComponent<NightVisionEffect>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Awake()
        {
            try
            {
                _volume = gameObject.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 100f;
                _volume.weight = 0f;
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.DontSave;

                var ca = _profile.Add<ColorAdjustments>(true);
                _ca = ca;
                // P43 fosfor yeşili: renk filtresi + kaldırılmış gölgeler (lift), vurgular omuzla yumuşar.
                ca.colorFilter.Override(new Color(0.42f, 1f, 0.52f));
                ca.postExposure.Override(2.4f);
                ca.contrast.Override(14f);
                ca.saturation.Override(-45f);

                _smh = _profile.Add<ShadowsMidtonesHighlights>(true);
                _smh.shadows.Override(new Vector4(0.86f, 1.0f, 0.9f, 0.12f));
                _smh.midtones.Override(new Vector4(0.92f, 1.06f, 0.94f, 0.08f));
                _smh.highlights.Override(new Vector4(0.9f, 1.08f, 0.92f, -0.05f));

                var vig = _profile.Add<Vignette>(true);
                vig.color.Override(new Color(0f, 0.06f, 0f));
                vig.intensity.Override(0.5f);
                vig.smoothness.Override(0.5f);

                _bloom = _profile.Add<Bloom>(true);
                _bloom.threshold.Override(0.45f);
                _bloom.intensity.Override(1.4f);
                _bloom.scatter.Override(0.78f);
                _bloom.tint.Override(new Color(0.6f, 1f, 0.6f));

                _volume.sharedProfile = _profile;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnGUI()
        {
            if (_blend <= 0.01f || Event.current.type != EventType.Repaint)
                return;

            if (_noise == null)
            {
                _noise = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                var px = new Color32[128 * 128];
                var rng = new System.Random(7);
                for (var i = 0; i < px.Length; i++)
                {
                    var v = (byte)rng.Next(0, 256);
                    px[i] = new Color32(v, 255, v, 255);
                }
                _noise.SetPixels32(px);
                _noise.Apply(false);
            }

            var prev = GUI.color;
            GUI.color = new Color(0.4f, 1f, 0.5f, 0.12f * _blend * NightLightingMath.BatteryFlicker(BatteryModel.Charge, Time.unscaledTime));
            var u = UnityEngine.Random.value;
            var v2 = UnityEngine.Random.value;
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), _noise,
                new Rect(u, v2, Screen.width / 128f, Screen.height / 128f));
            GUI.color = prev;
        }

        private void Update()
        {
            var target = BatteryModel.IsOn ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, BlendSpeed * Time.unscaledDeltaTime);

            if (_volume != null)
                _volume.weight = _blend;

            if (_bloom != null)
            {
                // El feneri açıkken gözlük aşırı parlar: bloom güçlenir.
                var flash = Atmosphere.FlashlightOn && BatteryModel.IsOn;
                var goal = flash ? 3.2f : 1.4f;
                _bloom.intensity.value = Mathf.Lerp(_bloom.intensity.value, goal, 6f * Time.unscaledDeltaTime);
            }

            UpdateAmplification();
            UpdateFog();
        }

        /// <summary>Işık güçlendirme: ortam karanlıksa pozlama yükselir; pil &lt;%10 iken titrer.</summary>
        private void UpdateAmplification()
        {
            if (_ca == null || _blend <= 0.01f)
                return;

            if (Time.unscaledTime >= _nextAmbient)
            {
                _nextAmbient = Time.unscaledTime + 0.5f;
                _ambientLuma = SampleAmbientLuma();
            }

            var ev = NightLightingMath.AmplificationEv(_ambientLuma, 1.6f, 3.2f);
            var flicker = NightLightingMath.BatteryFlicker(BatteryModel.Charge, Time.unscaledTime);
            _ca.postExposure.value = ev * flicker;
        }

        private static float SampleAmbientLuma()
        {
            try
            {
                if (RenderSettings.ambientMode == AmbientMode.Skybox)
                {
                    RenderSettings.ambientProbe.Evaluate(ProbeDir, ProbeOut);
                    var sum = 0f;
                    for (var i = 0; i < ProbeOut.Length; i++)
                        sum += ProbeOut[i].grayscale;
                    return sum / ProbeOut.Length;
                }

                return RenderSettings.ambientMode == AmbientMode.Trilight
                    ? RenderSettings.ambientEquatorColor.grayscale
                    : RenderSettings.ambientLight.grayscale;
            }
            catch (Exception)
            {
                return 0.05f;
            }
        }

        private void UpdateFog()
        {
            if (BatteryModel.IsOn)
            {
                if (_savedFog < 0f)
                {
                    _savedFog = RenderSettings.fogDensity;
                    _appliedFog = NightLightingMath.FogDim(_savedFog);
                    RenderSettings.fogDensity = _appliedFog;
                }
            }
            else if (_savedFog >= 0f)
            {
                // Atmosphere araya girip yoğunluğu değiştirmediyse eski değeri geri yükle.
                if (Mathf.Approximately(RenderSettings.fogDensity, _appliedFog))
                    RenderSettings.fogDensity = _savedFog;
                _savedFog = -1f;
            }
        }

        private void OnDestroy()
        {
            if (_savedFog >= 0f && Mathf.Approximately(RenderSettings.fogDensity, _appliedFog))
                RenderSettings.fogDensity = _savedFog;
            _savedFog = -1f;
            if (_profile != null)
                Destroy(_profile);
            if (_noise != null)
                Destroy(_noise);
            if (_instance == this)
                _instance = null;
        }
    }
}
