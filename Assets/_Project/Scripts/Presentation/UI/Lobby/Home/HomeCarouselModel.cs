using System;

namespace Project.Presentation.UI.Lobby.Home
{
    /// <summary>
    /// Haber karuselinin saf durum makinesi (UGUI'siz, test edilebilir). Otomatik donme, fare/odak ile duraklatma,
    /// elle secimden sonra ek bekleme ve capraz geçis karisimi. AAA lobilerde yaygin: ~7 sn bekleme, ~0,4 sn gecis,
    /// kullanici dokununca sayac sifirlanir (kullanici kontrolu ele gecirilmez).
    /// </summary>
    public sealed class HomeCarouselModel
    {
        public const float DefaultInterval = 7f;
        public const float DefaultFade = 0.4f;
        /// <summary>Elle secimden sonra sayac bu kadar geriden baslar (okuma payi).</summary>
        public const float ManualGrace = 3f;

        private readonly int _count;
        private readonly float _interval;
        private readonly float _fade;
        private float _elapsed;
        private float _fadeTimer;

        public HomeCarouselModel(int count, float interval = DefaultInterval, float fade = DefaultFade)
        {
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval));
            _count = Math.Max(0, count);
            _interval = interval;
            _fade = Math.Max(0.01f, fade);
            _fadeTimer = _fade;
            Previous = -1;
        }

        public int Count => _count;
        public int Index { get; private set; }
        /// <summary>Gecis sirasinda solan slayt (-1: yok).</summary>
        public int Previous { get; private set; }
        public bool Paused { get; set; }

        /// <summary>Bekleme ilerlemesi 0..1 (ilerleme cubugu).</summary>
        public float Progress => Clamp01(_elapsed / _interval);

        /// <summary>Gelen slaydin gorunurlugu 0..1 (smoothstep).</summary>
        public float Blend
        {
            get
            {
                var t = Clamp01(_fadeTimer / _fade);
                return t * t * (3f - 2f * t);
            }
        }

        public bool InTransition => Previous >= 0 && _fadeTimer < _fade;

        /// <summary>Zamani ilerletir; slayt degistiyse true.</summary>
        public bool Tick(float dt)
        {
            if (_count <= 0 || dt <= 0f) return false;
            if (_fadeTimer < _fade)
            {
                _fadeTimer += dt;
                if (_fadeTimer >= _fade) { _fadeTimer = _fade; Previous = -1; }
            }

            if (Paused || _count < 2) return false;
            _elapsed += dt;
            if (_elapsed < _interval) return false;
            Go(Wrap(Index + 1), 0f);
            return true;
        }

        public void Next() => Select(Index + 1);
        public void PreviousSlide() => Select(Index - 1);

        /// <summary>Elle secim: sayac -ManualGrace'ten baslar. Ayni slayt secilirse hicbir sey olmaz.</summary>
        public void Select(int index)
        {
            if (_count <= 0) return;
            var target = Wrap(index);
            if (target == Index) { _elapsed = -ManualGrace; return; }
            Go(target, -ManualGrace);
        }

        /// <summary>Slayt <paramref name="i"/> su an gorunur agirliga sahip mi (alfa hesabi): 0..1.</summary>
        public float AlphaOf(int i)
        {
            if (i == Index) return InTransition ? Blend : 1f;
            if (i == Previous && InTransition) return 1f - Blend;
            return 0f;
        }

        private void Go(int target, float startElapsed)
        {
            Previous = Index;
            Index = target;
            _elapsed = startElapsed;
            _fadeTimer = 0f;
        }

        private int Wrap(int i) => _count <= 0 ? 0 : ((i % _count) + _count) % _count;
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
