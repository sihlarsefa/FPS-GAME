using UnityEngine;

namespace Project.Infrastructure.Rendering.Grading
{
    /// <summary>GradeSpec geçişini smoothstep ile yumuşatan durum makinesi (Unity nesnesi gerektirmez; test edilebilir).</summary>
    public sealed class GradeSpecBlender
    {
        private GradeSpec _from, _to, _cur;
        private float _t = 1f;
        private bool _has;

        public float Duration = 2.5f;
        public bool HasTarget => _has;
        public bool Blending => _has && _t < 1f;
        public GradeSpec Current => _cur;

        public void Reset() { _has = false; _t = 1f; }

        public void SetTarget(GradeSpec target, bool snap = false)
        {
            target = GradingPresets.Clamp(target);
            if (!_has || snap) { _from = _to = _cur = target; _t = 1f; _has = true; return; }
            if (Same(target, _to)) return;
            _from = _cur; _to = target; _t = 0f;
        }

        /// <summary>Zamanı ilerletir; değer değiştiyse true.</summary>
        public bool Advance(float dt)
        {
            if (!Blending) return false;
            _t = Mathf.Min(1f, _t + Mathf.Max(0f, dt) / Mathf.Max(0.05f, Duration));
            _cur = GradingPresets.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _t));
            return true;
        }

        private static bool Same(GradeSpec a, GradeSpec b) =>
            a.Temperature == b.Temperature && a.Tint == b.Tint && a.Saturation == b.Saturation && a.Contrast == b.Contrast &&
            a.PostExposure == b.PostExposure && a.Gain == b.Gain && a.Lift == b.Lift && a.Gamma == b.Gamma &&
            a.SplitHighlights == b.SplitHighlights && a.SplitShadows == b.SplitShadows && a.Highlights == b.Highlights;
    }
}
