using System;

namespace Project.Core.Domain
{
    /// <summary>Unity'den bağımsız 3B vektör (Core/Application katmanı matematiği için).</summary>
    public readonly struct Float3 : IEquatable<Float3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 Zero => new(0f, 0f, 0f);
        public static Float3 Up => new(0f, 1f, 0f);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Float3 Normalized
        {
            get
            {
                var m = Magnitude;
                return m > 1e-6f ? new Float3(X / m, Y / m, Z / m) : Zero;
            }
        }

        public Float3 Flat => new(X, 0f, Z);

        public static Float3 operator +(Float3 a, Float3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator -(Float3 a) => new(-a.X, -a.Y, -a.Z);
        public static Float3 operator *(Float3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
        public static Float3 operator *(float s, Float3 a) => new(a.X * s, a.Y * s, a.Z * s);
        public static Float3 operator /(Float3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);

        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static float Distance(Float3 a, Float3 b) => (a - b).Magnitude;

        public static float DistanceXZ(Float3 a, Float3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static Float3 Lerp(Float3 a, Float3 b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new Float3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        }

        public bool Equals(Float3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Float3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:F1}, {Y:F1}, {Z:F1})";
    }
}
