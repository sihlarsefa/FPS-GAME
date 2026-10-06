using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Bir karelik dürbün girdisi (Presentation hook'u doldurur).</summary>
    public struct ScopeFrame
    {
        public bool Scoped;
        public float AimBlend;
        public float Magnification;
        public float BaseFov;
        public Vector2 Sway;
        public float Misalign;
        public bool HoldingBreath;
        public float BreathRemaining;
        public float BreathMax;
        public ReticleKind Reticle;
        public int QualityTier;
        public float Dt;
        public float Time;
        public Vector2 ReticleHoldover;
    }

    /// <summary>
    /// Yüksek/Ultra: ikinci kamera -> RenderTexture -> lens materyali (PiP), viewmodel katmanı kırpılır.
    /// Düşük/Orta: tam ekran yakınlaştırma (kamera zoom) + vinyet. Retikül/göz kutusu/kir her ikisinde ekran katmanında.
    /// </summary>
    public sealed class ScopeController : MonoBehaviour
    {
        private Camera _main;
        private Camera _pip;
        private RenderTexture _rt;
        private Renderer _lens;
        private Material _lensMat;
        private Material _lensOriginal;
        private ScopeOverlay _overlay;
        private int _frame;
        private int _rtSize;

        /// <summary>PiP lens görüntüsü bu karede kullanılıyor mu.</summary>
        public bool PipActive { get; private set; }

        public ScopeOverlay Overlay => _overlay;

        public void Bind(Camera mainCamera, Renderer lensRenderer)
        {
            _main = mainCamera;
            if (_lens != lensRenderer)
            {
                RestoreLens();
                _lens = lensRenderer;
            }
        }

        private void Awake() => EnsureOverlay();

        private void EnsureOverlay()
        {
            if (_overlay == null)
            {
                _overlay = gameObject.GetComponent<ScopeOverlay>();
                if (_overlay == null)
                    _overlay = gameObject.AddComponent<ScopeOverlay>();
            }
        }

        /// <summary>Her kare (PlayerWeaponHandler Tick sonrası) çağrılır.</summary>
        public void Apply(in ScopeFrame f)
        {
            EnsureOverlay();
            var plan = ScopeMath.TierPlan(f.QualityTier);
            var active = f.Scoped && f.AimBlend > 0.05f;
            var usePip = active && plan.PictureInPicture && _lens != null && _main != null;

            Scope.CurrentMagnification = active ? Mathf.Max(1f, f.Magnification) : 1f;
            Scope.PipActive = usePip;
            PipActive = usePip;

            if (usePip)
                UpdatePip(f, plan);
            else
                DisablePip();

            UpdateOverlay(f, plan, active, usePip);
        }

        private void UpdatePip(in ScopeFrame f, ScopeTierPlan plan)
        {
            EnsurePip(plan.RenderTextureSize);
            if (_pip == null)
                return;

            var shake = ScopeMath.ShakeAmplitude(f.HoldingBreath, !f.HoldingBreath && f.BreathRemaining <= 0.01f, f.BreathRemaining, f.BreathMax);
            var tr = _main.transform;
            var pitch = (Mathf.PerlinNoise(f.Time * 2.3f, 0.5f) - 0.5f) * 2f * shake;
            var yaw = (Mathf.PerlinNoise(0.5f, f.Time * 2.7f) - 0.5f) * 2f * shake;
            _pip.transform.SetPositionAndRotation(tr.position, tr.rotation * Quaternion.Euler(pitch, yaw, 0f));
            _pip.fieldOfView = ScopeMath.FovFromMagnification(f.BaseFov > 1f ? f.BaseFov : _main.fieldOfView, f.Magnification);
            _pip.cullingMask = _main.cullingMask & ~(1 << GameLayers.Viewmodel);
            _pip.nearClipPlane = _main.nearClipPlane;
            _pip.farClipPlane = _main.farClipPlane;

            _frame++;
            _pip.enabled = plan.UpdateEveryNFrames <= 1 || (_frame % plan.UpdateEveryNFrames) == 0;
            if (_lensMat != null && _lens != null && _lens.sharedMaterial != _lensMat)
            {
                if (_lensOriginal == null)
                    _lensOriginal = _lens.sharedMaterial;
                _lens.sharedMaterial = _lensMat;
            }
        }

        private void UpdateOverlay(in ScopeFrame f, ScopeTierPlan plan, bool active, bool pip)
        {
            var target = active ? Mathf.Clamp01((f.AimBlend - 0.05f) / 0.6f) : 0f;
            _overlay.Alpha = Mathf.MoveTowards(_overlay.Alpha, target, Mathf.Max(0.0001f, f.Dt) * 8f);
            _overlay.enabled = _overlay.Alpha > 0.01f;
            _overlay.FullVignette = !pip && f.Magnification >= 1.8f;
            _overlay.Reticle = ScopeTextures.Reticle(f.Reticle);
            _overlay.ReticleScale = f.Reticle == ReticleKind.RedDot ? 1f : 1f;
            _overlay.ReticleOffset = f.ReticleHoldover;

            ScopeMath.EyeBox(f.Sway.x, f.Sway.y, f.Misalign, out var sx, out var sy, out var dark);
            _overlay.EyeShift = new Vector2(sx, sy);
            _overlay.EyeDarkness = f.Magnification >= 1.8f ? dark : dark * 0.35f;

            // Lens kiri/yansıma: güneşe doğru bakarken artar.
            var dirt = 0f;
            if (plan.LensDirt)
            {
                var sun = RenderSettings.sun;
                var align = 0f;
                if (sun != null && _main != null)
                    align = Mathf.Clamp01(Vector3.Dot(_main.transform.forward, -sun.transform.forward));
                dirt = 0.25f + align * 0.5f;
            }

            _overlay.DirtAlpha = dirt;
            _overlay.ReticleBrightness = 1f;
        }

        private void EnsurePip(int size)
        {
            if (size <= 0)
                return;
            if (_rt == null || _rtSize != size)
            {
                ReleaseRt();
                _rtSize = size;
                _rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32) { name = "ScopePiP", antiAliasing = 1 };
                _rt.Create();
            }

            if (_pip == null)
            {
                var go = new GameObject("ScopePipCamera") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                _pip = go.AddComponent<Camera>();
                _pip.depth = (_main != null ? _main.depth : 0f) - 1f;
                _pip.allowMSAA = false;
                _pip.allowHDR = false;
                _pip.enabled = false;
            }

            _pip.targetTexture = _rt;

            if (_lensMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null)
                    sh = Shader.Find("Unlit/Texture");
                if (sh != null)
                    _lensMat = new Material(sh) { name = "ScopeLens", hideFlags = HideFlags.DontSave };
            }

            if (_lensMat != null)
            {
                _lensMat.mainTexture = _rt;
                if (_lensMat.HasProperty("_BaseMap"))
                    _lensMat.SetTexture("_BaseMap", _rt);
            }
        }

        private void DisablePip()
        {
            if (_pip != null && _pip.enabled)
                _pip.enabled = false;
            RestoreLens();
        }

        private void RestoreLens()
        {
            if (_lens != null && _lensOriginal != null && _lens.sharedMaterial == _lensMat)
                _lens.sharedMaterial = _lensOriginal;
        }

        private void ReleaseRt()
        {
            if (_pip != null)
                _pip.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }

        private void OnDisable()
        {
            DisablePip();
            Scope.PipActive = false;
            Scope.CurrentMagnification = 1f;
        }

        private void OnDestroy()
        {
            RestoreLens();
            ReleaseRt();
            if (_lensMat != null)
                Destroy(_lensMat);
        }
    }
}
