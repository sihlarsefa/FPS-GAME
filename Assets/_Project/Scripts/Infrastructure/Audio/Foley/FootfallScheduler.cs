using System;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>Tek ayak teması olayı (topuk anı).</summary>
    public readonly struct FootfallEvent
    {
        public readonly bool LeftFoot;
        public readonly Gait Gait;
        public readonly float Speed;
        public readonly int Index;

        public FootfallEvent(bool left, Gait gait, float speed, int index)
        {
            LeftFoot = left;
            Gait = gait;
            Speed = speed;
            Index = index;
        }
    }

    /// <summary>
    /// Adım zamanlayıcı (saf): yatay mesafe/adım uzunluğu ile ayak teması üretir, sol-sağ dönüşümlüdür.
    /// Durunca sıfırlanır (ilk adım yarım adım sonra çıkar), yürüyüş biçimi değişince birikim korunur.
    /// Hava durumunda (grounded=false) adım yok; inişte iniş sesi başka sistemdir (BodyFoleyRules).
    /// </summary>
    public sealed class FootfallScheduler
    {
        public const float MinSpeed = 0.35f;
        private float _stride;
        private bool _left;
        private int _count;
        private float _idle;

        public int Count => _count;

        public bool Tick(float horizontalSpeed, bool crouching, bool prone, bool grounded, float dt, out FootfallEvent ev)
        {
            ev = default;
            if (!grounded || !(dt > 0f))
                return false;
            if (horizontalSpeed < MinSpeed)
            {
                _idle += dt;
                if (_idle > 0.25f) _stride = 0.35f * 0.5f; // durup başlayınca ilk adım hızlı gelsin
                return false;
            }
            _idle = 0f;
            var gait = FootstepRules.GaitFor(horizontalSpeed, crouching, prone);
            var len = FootstepRules.StrideLength(gait);
            _stride += horizontalSpeed * dt;
            if (_stride < len)
                return false;
            _stride -= len;
            if (_stride > len) _stride = 0f; // takılma/ışınlanmada çoklu tetiği engelle
            _left = !_left;
            ev = new FootfallEvent(_left, gait, horizontalSpeed, _count++);
            return true;
        }

        /// <summary>Bir sonraki adıma kalan tahmini süre (sn); durağansa sonsuz.</summary>
        public float SecondsToNext(float horizontalSpeed, bool crouching, bool prone)
        {
            if (horizontalSpeed < MinSpeed) return float.PositiveInfinity;
            var len = FootstepRules.StrideLength(FootstepRules.GaitFor(horizontalSpeed, crouching, prone));
            var rest = len - _stride;
            return rest <= 0f ? 0f : rest / horizontalSpeed;
        }

        public void Reset()
        {
            _stride = 0f;
            _left = false;
            _count = 0;
            _idle = 0f;
        }
    }

    /// <summary>Klip seçici (saf): son iki klibi tekrar etmez, hafif ağırlıkla çeşitlendirir (makine-silah etkisini kırar).</summary>
    public sealed class FootstepVariantPicker
    {
        private uint _state;
        private int _last1 = -1;
        private int _last2 = -1;

        public FootstepVariantPicker(uint seed = 0x9E3779B9u)
        {
            _state = seed == 0 ? 1u : seed;
        }

        private uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>0..count-1; count&lt;=2 ise yalnız son klip hariç tutulur, count&lt;=1 daima 0.</summary>
        public int Pick(int count)
        {
            if (count <= 1) return 0;
            for (var tries = 0; tries < 8; tries++)
            {
                var i = (int)(Next() % (uint)count);
                if (i == _last1) continue;
                if (count > 2 && i == _last2) continue;
                _last2 = _last1;
                _last1 = i;
                return i;
            }
            var j = (_last1 + 1) % count;
            _last2 = _last1;
            _last1 = j;
            return j;
        }

        /// <summary>Perde (çarpan) çeşitlemesi: merkez ±jitter, tekrarsız aralık.</summary>
        public float Jitter(float center, float jitter)
        {
            var u = (Next() & 0xFFFFu) / 65535f;
            return center + (u * 2f - 1f) * jitter;
        }

        public void Reset()
        {
            _last1 = _last2 = -1;
        }
    }
}
