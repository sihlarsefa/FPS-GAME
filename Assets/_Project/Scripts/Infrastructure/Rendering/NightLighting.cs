using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Gece operasyonu ışık/gözlük matematiği (Unity nesnesi yok, test edilebilir): el feneri parametreleri, 4300K rengi,
    /// el sallanması, hacimsel koni opaklığı, gece görüş fosfor eğrisi, ışık güçlendirme (pozlama) ve pil titremesi.
    /// </summary>
    public static class NightLightingMath
    {
        public const float FlashInnerDeg = 8f;
        public const float FlashOuterDeg = 22f;
        public const float FlashRange = 25f;
        public const float FlashKelvin = 4300f;
        public const float FlashIntensity = 9f;
        public const float FlickerBelow = 0.10f;
        public const float MoonFloorIntensity = 0.14f;

        /// <summary>Tanner Helland yaklaşımı: Kelvin (1000-12000) -> lineer olmayan sRGB 0..1.</summary>
        public static void KelvinToRgb(float kelvin, out float r, out float g, out float b)
        {
            var t = Math.Max(1000f, Math.Min(12000f, kelvin)) / 100f;
            double rr, gg, bb;
            if (t <= 66f) rr = 255.0;
            else rr = 329.698727446 * Math.Pow(t - 60f, -0.1332047592);
            if (t <= 66f) gg = 99.4708025861 * Math.Log(t) - 161.1195681661;
            else gg = 288.1221695283 * Math.Pow(t - 60f, -0.0755148492);
            if (t >= 66f) bb = 255.0;
            else if (t <= 19f) bb = 0.0;
            else bb = 138.5177312231 * Math.Log(t - 10f) - 305.0447927307;
            r = Clamp01((float)(rr / 255.0));
            g = Clamp01((float)(gg / 255.0));
            b = Clamp01((float)(bb / 255.0));
        }

        /// <summary>El sallanması (derece): düşük frekanslı iki sinüs; hareket ederken genlik artar. x = pitch, y = yaw.</summary>
        public static void Sway(float time, float moveSpeed01, out float pitchDeg, out float yawDeg)
        {
            var amp = 0.22f + 0.35f * Clamp01(moveSpeed01);
            pitchDeg = amp * (0.65f * (float)Math.Sin(time * 1.7f) + 0.35f * (float)Math.Sin(time * 3.1f + 1.3f));
            yawDeg = amp * (0.65f * (float)Math.Sin(time * 1.3f + 0.7f) + 0.35f * (float)Math.Sin(time * 2.6f + 2.1f));
        }

        /// <summary>Koninin uç yarıçapı (m).</summary>
        public static float ConeRadius(float length, float outerDeg) =>
            length * (float)Math.Tan(Math.Max(1f, Math.Min(80f, outerDeg)) * 0.5 * Math.PI / 180.0);

        /// <summary>Hacimsel koni taban opaklığı: yoğunluk 0 ise 0; sis arttıkça artar, üst sınırlı (pastel, boğmaz).</summary>
        public static float ConeAlpha(float volumetricDensity, bool volumetricActive)
        {
            if (!volumetricActive || volumetricDensity <= 0f)
                return 0f;
            return 0.05f + 0.22f * (1f - (float)Math.Exp(-volumetricDensity * 90f));
        }

        /// <summary>Koni boyunca opaklık düşüşü (0 = kaynak yakını, 1 = uç): yakında kısık, ortada tepe, uçta sönük.</summary>
        public static float ConeFalloff(float t01)
        {
            var t = Clamp01(t01);
            var fadeIn = Clamp01(t / 0.08f);
            var fadeOut = (1f - t) * (1f - t);
            return fadeIn * fadeOut;
        }

        /// <summary>Gece görüş fosfor eğrisi: gölgeler kalkar (lift), vurgular omuzla yumuşar. Tek yönlü artan, f(1)=1.</summary>
        public static float Phosphor(float x, float lift, float gain)
        {
            x = Clamp01(x);
            lift = Math.Max(0f, Math.Min(0.4f, lift));
            gain = Math.Max(0.1f, gain);
            var v = lift + (1f - lift) * x;
            var shoulder = 1f - (float)Math.Exp(-gain * v);
            var norm = 1f - (float)Math.Exp(-gain);
            return Clamp01(shoulder / norm);
        }

        /// <summary>Işık güçlendirme: ortam parlaklığı düştükçe pozlama (EV) artar. luma >= 0.25 minEv, 0 maxEv.</summary>
        public static float AmplificationEv(float ambientLuma, float minEv, float maxEv)
        {
            var k = Clamp01(ambientLuma / 0.25f);
            k = k * k * (3f - 2f * k);
            return maxEv + (minEv - maxEv) * k;
        }

        /// <summary>Pil &lt;%10: parlaklık çarpanı 0.35..1; yavaş soluma + kısa kesintiler. Üstünde tam 1.</summary>
        public static float BatteryFlicker(float charge, float time)
        {
            if (charge >= FlickerBelow)
                return 1f;
            var depth = Clamp01((FlickerBelow - Math.Max(0f, charge)) / FlickerBelow); // 0..1
            var slow = 0.5f + 0.5f * (float)Math.Sin(time * (9f + 14f * depth));
            var m = 1f - depth * 0.35f * slow;
            // 8 Hz dilimlerde sahte rastgele kısa kesinti; derinlikle olasılık artar.
            var slot = (int)Math.Floor(time * 8f);
            var h = Hash01(slot);
            if (h < 0.10f + 0.30f * depth)
                m *= 0.35f + 0.25f * (1f - depth);
            return Math.Max(0.2f, Math.Min(1f, m));
        }

        /// <summary>Gece görüş sisi çarpanı: sis yoğunluğu ne kadar azalır (0.45 = eski değer).</summary>
        public static float FogDim(float baseDensity) => Math.Max(0f, baseDensity) * 0.45f;

        private static float Hash01(int n)
        {
            unchecked
            {
                var x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Gece ışıklandırma uygulayıcısı (otomatik kurulur): Atmosphere'in el fenerini sıkı spot (8/22°, 4300K, 25 m) olarak yeniden
    /// ayarlar, hafif el sallanması ekler, hacimsel sis aktifken görünür ışık konisi çizer; Gece ön ayarında ay ışığını
    /// (soğuk renk, yumuşak gölge) güvenceye alır. Atmosphere'e dokunmaz; yalnız Light özelliklerini sonradan (LateUpdate) ayarlar.
    /// </summary>
    public sealed class NightLighting : MonoBehaviour
    {
        private static NightLighting _instance;

        private Light _flash;
        private Atmosphere _atmosphere;
        private GameObject _cone;
        private MeshRenderer _coneRenderer;
        private Material _coneMat;
        private float _nextScan;
        private float _nextMoon;
        private Vector3 _lastCamPos;
        private float _speed01;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;
            try
            {
                var go = new GameObject("[NightLighting]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<NightLighting>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void LateUpdate()
        {
            try
            {
                var now = Time.unscaledTime;
                if (now >= _nextMoon)
                {
                    _nextMoon = now + 0.5f;
                    TuneMoon();
                }

                if (_flash == null && now >= _nextScan)
                {
                    _nextScan = now + 1f;
                    FindFlash();
                }

                if (_flash == null)
                    return;

                if (!Atmosphere.FlashlightOn)
                {
                    if (_cone != null && _cone.activeSelf) _cone.SetActive(false);
                    return;
                }

                TuneFlash();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                enabled = false;
            }
        }

        private void FindFlash()
        {
            if (_atmosphere == null)
                _atmosphere = FindAnyObjectByType<Atmosphere>();
            if (_atmosphere == null)
                return;
            var t = _atmosphere.transform.Find("Flashlight");
            if (t != null)
                _flash = t.GetComponent<Light>();
        }

        private void TuneFlash()
        {
            var tr = _flash.transform;
            _flash.type = LightType.Spot;
            _flash.innerSpotAngle = NightLightingMath.FlashInnerDeg;
            _flash.spotAngle = NightLightingMath.FlashOuterDeg;
            _flash.range = NightLightingMath.FlashRange;
            _flash.intensity = NightLightingMath.FlashIntensity;
            NightLightingMath.KelvinToRgb(NightLightingMath.FlashKelvin, out var r, out var g, out var b);
            _flash.color = new Color(r, g, b);

            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var p = tr.position;
            var spd = (p - _lastCamPos).magnitude / dt;
            _lastCamPos = p;
            _speed01 = Mathf.Lerp(_speed01, Mathf.Clamp01(spd / 6f), 6f * dt);
            NightLightingMath.Sway(Time.unscaledTime, _speed01, out var pitch, out var yaw);
            tr.rotation = tr.rotation * Quaternion.Euler(pitch, yaw, 0f);

            UpdateCone(tr);
        }

        private void UpdateCone(Transform lightTr)
        {
            var density = VolumetricFog.Settings.Density;
            var alpha = NightLightingMath.ConeAlpha(density, VolumetricFog.IsActive);
            if (alpha <= 0.001f)
            {
                if (_cone != null && _cone.activeSelf) _cone.SetActive(false);
                return;
            }

            if (_cone == null && !BuildCone(lightTr))
                return;
            if (_cone.transform.parent != lightTr)
                _cone.transform.SetParent(lightTr, false);
            _cone.SetActive(true);
            if (_coneMat != null)
                _coneMat.color = new Color(1f, 0.93f, 0.8f, alpha);
        }

        private bool BuildCone(Transform parent)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return false;
            const int seg = 20;
            const int rings = 6;
            var len = NightLightingMath.FlashRange;
            var rad = NightLightingMath.ConeRadius(len, NightLightingMath.FlashOuterDeg);
            var verts = new Vector3[1 + rings * seg];
            var cols = new Color[verts.Length];
            verts[0] = Vector3.zero;
            cols[0] = new Color(1f, 1f, 1f, 0f);
            for (var ri = 0; ri < rings; ri++)
            {
                var t = (ri + 1f) / rings;
                for (var s = 0; s < seg; s++)
                {
                    var a = s * Mathf.PI * 2f / seg;
                    verts[1 + ri * seg + s] = new Vector3(Mathf.Cos(a) * rad * t, Mathf.Sin(a) * rad * t, len * t);
                    cols[1 + ri * seg + s] = new Color(1f, 1f, 1f, NightLightingMath.ConeFalloff(t));
                }
            }
            var tris = new System.Collections.Generic.List<int>();
            for (var s = 0; s < seg; s++)
            {
                var n = (s + 1) % seg;
                tris.Add(0); tris.Add(1 + s); tris.Add(1 + n);
            }
            for (var ri = 0; ri < rings - 1; ri++)
            {
                for (var s = 0; s < seg; s++)
                {
                    var n = (s + 1) % seg;
                    var a0 = 1 + ri * seg + s; var a1 = 1 + ri * seg + n;
                    var b0 = 1 + (ri + 1) * seg + s; var b1 = 1 + (ri + 1) * seg + n;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            }
            var mesh = new Mesh { name = "FlashCone", hideFlags = HideFlags.DontSave };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();

            _cone = new GameObject("FlashCone");
            _cone.transform.SetParent(parent, false);
            _cone.AddComponent<MeshFilter>().sharedMesh = mesh;
            _coneRenderer = _cone.AddComponent<MeshRenderer>();
            _coneMat = new Material(shader) { hideFlags = HideFlags.DontSave, renderQueue = 3100 };
            _coneRenderer.sharedMaterial = _coneMat;
            _coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coneRenderer.receiveShadows = false;
            return true;
        }

        private static void TuneMoon()
        {
            if (Atmosphere.CurrentTime != TimeOfDay.Gece)
                return;
            var sun = RenderSettings.sun;
            if (sun == null)
                return;
            // Ay gökte olmalı: ışık yönü aşağı bakmalı (forward.y < 0).
            if (sun.transform.forward.y > -0.2f)
                sun.transform.rotation = Quaternion.Euler(42f, 30f, 0f);
            sun.color = new Color(0.55f, 0.66f, 1f);
            if (sun.intensity < NightLightingMath.MoonFloorIntensity)
                sun.intensity = NightLightingMath.MoonFloorIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
        }

        private void OnDestroy()
        {
            if (_coneMat != null) Destroy(_coneMat);
            if (_instance == this) _instance = null;
        }
    }
}
