using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>
    /// Sunucu bomba (throwable) doğrulaması: bilinen kod aralığı, tür başına + genel bekleme, vektör temizleme.
    /// Bilinmeyen kod reddedilir (eskiden sessizce Frag'e düşüyordu).
    /// </summary>
    public sealed class ThrowGate
    {
        /// <summary>Geçerli kodlar 0..MaxKnownCode (Frag, Smoke, Flash, Molotov, Decoy).</summary>
        public const byte MaxKnownCode = 4;
        public const float GlobalCooldown = 1.0f;
        public const float MaxSpeed = 24f;
        public const float MaxOriginOffset = 3f;

        private static readonly float[] KindCooldowns = { 1.0f, 1.0f, 0.8f, 1.5f, 1.0f };

        private readonly float[] _nextByKind = new float[MaxKnownCode + 1];
        private float _nextAny;

        public int RejectedUnknown { get; private set; }
        public int RejectedCooldown { get; private set; }

        public static bool IsKnownCode(byte code) => code <= MaxKnownCode;
        public static float CooldownFor(byte code) => IsKnownCode(code) ? KindCooldowns[code] : GlobalCooldown;

        public bool TryAccept(byte code, float now)
        {
            if (!IsKnownCode(code))
            {
                RejectedUnknown++;
                return false;
            }

            if (now < _nextAny || now < _nextByKind[code])
            {
                RejectedCooldown++;
                return false;
            }

            _nextAny = now + GlobalCooldown * 0.5f; // art arda tür değiştirerek seli engelle
            _nextByKind[code] = now + KindCooldowns[code];
            return true;
        }

        public void Reset()
        {
            _nextAny = 0f;
            for (var i = 0; i < _nextByKind.Length; i++) _nextByKind[i] = 0f;
        }

        /// <summary>NaN/∞ reddedilir; origin göz noktasından çok uzaksa göze çekilir; hız MaxSpeed'e kırpılır.</summary>
        public static bool Sanitize(Vector3 origin, Vector3 velocity, Vector3 eye, out Vector3 safeOrigin, out Vector3 safeVelocity)
        {
            safeOrigin = eye;
            safeVelocity = Vector3.zero;
            if (!Finite(origin) || !Finite(velocity) || !Finite(eye))
                return false;

            safeOrigin = (origin - eye).sqrMagnitude > MaxOriginOffset * MaxOriginOffset ? eye : origin;
            safeVelocity = velocity.sqrMagnitude > MaxSpeed * MaxSpeed ? velocity.normalized * MaxSpeed : velocity;
            return true;
        }

        private static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
              || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
