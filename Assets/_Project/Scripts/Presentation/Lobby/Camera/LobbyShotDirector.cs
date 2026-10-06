using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Çekim geçişini yöneten saf durum makinesi (Unity nesnesi kullanmaz). Geçiş sırasında yeni çekim istenirse mevcut ara pozdan
    /// başlar (sıçrama yok). Süre mesafeden türetilir. <see cref="Tick"/> her karede güncel çekimi ve geçiş ilerlemesini verir.
    /// </summary>
    public sealed class LobbyShotDirector
    {
        private readonly LobbyShot[] _shots;
        private LobbyShot _from;
        private LobbyShot _to;
        private LobbyShot _current;
        private float _t = 1f;
        private float _duration = 1f;

        public LobbyShotDirector(LobbyShot[] shots, int startIndex)
        {
            _shots = shots != null && shots.Length > 0 ? shots : LobbyShotLibrary.Build(LobbyShotLibrary.DefaultAspect);
            Index = Mathf.Clamp(startIndex, 0, _shots.Length - 1);
            _from = _to = _current = _shots[Index];
        }

        public int Index { get; private set; }

        public LobbyShot Current => _current;

        public bool IsMoving => _t < 1f;

        /// <summary>Geçiş ilerlemesi 0..1 (hareketsizken 1).</summary>
        public float Progress => _t;

        /// <summary>Geçişin "hareket yoğunluğu": ortada 1, uçlarda 0 (el kamerası güçlenmesi için).</summary>
        public float Transit01 => IsMoving ? Mathf.Sin(Mathf.PI * _t) : 0f;

        public float Duration => _duration;

        public void SetShot(int index)
        {
            index = Mathf.Clamp(index, 0, _shots.Length - 1);
            if (index == Index && _t >= 1f) return;
            if (index == Index) return; // zaten oraya gidiyoruz

            _from = _current;
            _to = _shots[index];
            Index = index;
            _t = 0f;
            _duration = LobbyShotBlend.DurationFor(_from, _to);
        }

        /// <summary>Çekim tablosunu (örn. ekran oranı değişince) yeniler; hareketsizse doğrudan yeni poza oturur.</summary>
        public void Replace(LobbyShot[] shots)
        {
            if (shots == null || shots.Length != _shots.Length) return;
            for (var i = 0; i < shots.Length; i++) _shots[i] = shots[i];
            if (_t >= 1f) _from = _to = _current = _shots[Index];
            else _to = _shots[Index];
        }

        public LobbyShot Tick(float dt)
        {
            if (dt < 0f || float.IsNaN(dt)) dt = 0f;
            if (_t < 1f)
                _t = Mathf.Min(1f, _t + dt / Mathf.Max(0.05f, _duration));

            _current = _t >= 1f ? _to : LobbyShotBlend.Evaluate(_from, _to, _t);
            return _current;
        }
    }
}
