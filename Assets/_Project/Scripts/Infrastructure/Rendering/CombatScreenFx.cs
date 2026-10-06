using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Savaş ekran efektleri: bastırma/düşük can/sersemletme için ayrı, yüksek öncelikli yerel Volume
    /// (PostProcessing'in küresel hacmine dokunmaz). Girdileri statik alanlardan okur; Presentation sürücüsü yazar.
    /// Kademe maliyeti: 0 vinyet+solma, 1 + renk sapması, 2-3 + bulanıklık (Gaussian DoF).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatScreenFx : MonoBehaviour
    {
        public const float VolumePriority = 90f;

        // Girdiler (sürücü her karede yazar)
        public static float Suppression01;
        public static float LowHealth01;
        public static float Pulse01;
        public static float Concussion01;
        public static float Tinnitus01;
        public static float Downed01;
        public static float Intensity = 1f;

        /// <summary>Kalp atışı tepe anında (ses kancası). Arg: düşük can 0..1.</summary>
        public static event System.Action<float> HeartbeatBeat;

        private static float _armorBreakAt = -10f;
        private float _prevPulse;

        /// <summary>Zırh kırıldı: beyaz parlama (halka HUD tarafında DamageIndicatorView.ArmorBreak).</summary>
        public static void NotifyArmorBreak() => _armorBreakAt = Time.unscaledTime;

        private static CombatScreenFx _instance;

        private Volume _volume;
        private VolumeProfile _profile;
        private Vignette _vignette;
        private ColorAdjustments _color;
        private ChromaticAberration _chroma;
        private DepthOfField _dof;
        private bool _wasActive;

        public static CombatScreenFx Instance => _instance;
        public static CombatScreenFxMath.FxState Last { get; private set; }

        public static CombatScreenFx EnsureInstalled()
        {
            if (_instance != null)
                return _instance;
            var go = new GameObject("CombatScreenFx") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatScreenFx>();
            return _instance;
        }

        public static void ResetInputs()
        {
            Suppression01 = LowHealth01 = Pulse01 = Concussion01 = Tinnitus01 = Downed01 = 0f;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.weight = 0f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            _volume.sharedProfile = _profile;

            _vignette = _profile.Add<Vignette>(true);
            _vignette.active = false;
            _vignette.smoothness.Override(0.55f);
            _color = _profile.Add<ColorAdjustments>(true);
            _color.active = false;
            _chroma = _profile.Add<ChromaticAberration>(true);
            _chroma.active = false;
            _dof = _profile.Add<DepthOfField>(true);
            _dof.active = false;
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(0f);
            _dof.gaussianEnd.Override(3f);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_profile != null)
                Destroy(_profile);
        }

        private void LateUpdate()
        {
            var tier = PostProcessing.QualityLevel;
            var st = CombatScreenFxMath.Compute(Suppression01, LowHealth01, Pulse01, Concussion01, Tinnitus01, Intensity, tier,
                Downed01, CombatScreenFxMath.ArmorBreakFlash(Time.unscaledTime - _armorBreakAt));
            if (CombatScreenFxMath.HeartbeatEdge(_prevPulse, Pulse01) && LowHealth01 > 0f && Intensity > 0f)
            {
                try { HeartbeatBeat?.Invoke(LowHealth01); }
                catch (System.Exception) { /* ses kancası ekranı bozmasın */ }
            }

            _prevPulse = Pulse01;
            Last = st;
            if (!st.Any)
            {
                if (_wasActive)
                {
                    _volume.weight = 0f;
                    _vignette.active = _color.active = _chroma.active = _dof.active = false;
                    _wasActive = false;
                }

                return;
            }

            _wasActive = true;
            _volume.weight = 1f;

            _vignette.active = st.Vignette > 0.001f;
            _vignette.intensity.Override(Mathf.Clamp01(st.Vignette));
            _vignette.color.Override(Color.Lerp(Color.black, new Color(0.55f, 0.02f, 0.02f), st.VignetteRed));

            _color.active = st.Saturation < -0.5f || st.Flash > 0.001f;
            _color.saturation.Override(st.Saturation * (1f - st.Flash));
            _color.postExposure.Override(st.Flash * 3.2f);
            _color.colorFilter.Override(Color.white);

            _chroma.active = st.Chromatic > 0.001f;
            _chroma.intensity.Override(Mathf.Clamp01(st.Chromatic));

            _dof.active = st.Blur > 0.001f;
            _dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, st.Blur));
            _dof.gaussianEnd.Override(Mathf.Lerp(8f, 0.5f, st.Blur));
        }
    }
}
