using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    public enum HitChannel
    {
        HeadPitch = 0,
        HeadYaw = 1,
        HeadRoll = 2,
        SpineTwist = 3,
        SpinePitch = 4,
        SpineRoll = 5,
        LegBuckle = 6,
        StaggerX = 7,
        StaggerZ = 8
    }

    /// <summary>
    /// Kanal başına sönümlü yay: isabet bir hız darbesi verir, yay pozu geri çeker. Saf matematik (Unity Transform yok).
    /// Açılar derece, çökme 0..1, sendeleme metre.
    /// </summary>
    public sealed class HitReactionSpring
    {
        public const int Channels = 9;
        public const float Stiffness = 250f;
        public const float DampingRatio = 0.4f;

        private readonly float[] _pos = new float[Channels];
        private readonly float[] _vel = new float[Channels];
        private static readonly float[] Limit = { 28f, 30f, 18f, 30f, 22f, 18f, 0.7f, 0.2f, 0.2f };

        public bool Active { get; private set; }

        public float Get(HitChannel c) => _pos[(int)c];

        public void Impulse(HitChannel c, float velocity)
        {
            _vel[(int)c] += velocity;
            Active = true;
        }

        public void Clear()
        {
            for (var i = 0; i < Channels; i++)
            {
                _pos[i] = 0f;
                _vel[i] = 0f;
            }

            Active = false;
        }

        public void Step(float dt)
        {
            if (!Active)
                return;
            if (dt > 0.1f)
                dt = 0.1f;
            var damping = 2f * DampingRatio * Mathf.Sqrt(Stiffness);
            var rest = true;
            while (dt > 0f)
            {
                var h = dt > 1f / 60f ? 1f / 60f : dt;
                dt -= h;
                for (var i = 0; i < Channels; i++)
                {
                    var a = -Stiffness * _pos[i] - damping * _vel[i];
                    _vel[i] += a * h;
                    _pos[i] += _vel[i] * h;
                    if (_pos[i] > Limit[i])
                        _pos[i] = Limit[i];
                    else if (_pos[i] < -Limit[i])
                        _pos[i] = -Limit[i];
                }
            }

            for (var i = 0; i < Channels; i++)
            {
                var lim = Limit[i];
                if (Mathf.Abs(_pos[i]) > lim * 0.01f || Mathf.Abs(_vel[i]) > lim * 0.2f)
                {
                    rest = false;
                    break;
                }
            }

            if (rest)
                Clear();
        }
    }

    /// <summary>Bölgeye ve yöne göre isabet tepkisi darbelerini dağıtır.</summary>
    public static class HitReactionMath
    {
        /// <summary>Düşük kademede genlik %60, bacak/sendeleme kapalı; 1'de sendeleme kapalı; 2-3 tam.</summary>
        public static float TierAmplitude(int tier) => tier <= 0 ? 0.6f : 1f;

        public static bool LegsAllowed(int tier) => tier >= 1;

        public static bool StaggerAllowed(int tier) => tier >= 2;

        /// <summary>
        /// localDir: merminin gidiş yönü, model uzayında (+Z modelin önü). Önden vuruş: z negatif.
        /// strength 0..1; heavy: ağır silah/patlama (sendeleme tetikler).
        /// </summary>
        public static void Apply(HitReactionSpring s, BodyPart region, Vector3 localDir, float strength, int tier, bool heavy)
        {
            if (s == null)
                return;
            localDir.y = 0f;
            if (localDir.sqrMagnitude < 1e-6f)
                localDir = Vector3.back;
            localDir.Normalize();

            var k = Mathf.Clamp01(strength) * TierAmplitude(tier);
            var dx = localDir.x;
            var dz = localDir.z;
            var side = dx >= 0f ? 1f : -1f;

            switch (region)
            {
                case BodyPart.Head:
                    s.Impulse(HitChannel.HeadPitch, dz * 380f * k);
                    s.Impulse(HitChannel.HeadYaw, dx * 300f * k + side * 90f * k);
                    s.Impulse(HitChannel.HeadRoll, -dx * 180f * k);
                    s.Impulse(HitChannel.SpinePitch, dz * 70f * k);
                    break;
                case BodyPart.Arm:
                    s.Impulse(HitChannel.SpineTwist, side * 260f * k);
                    s.Impulse(HitChannel.SpineRoll, side * 90f * k);
                    s.Impulse(HitChannel.HeadYaw, -side * 80f * k);
                    break;
                case BodyPart.Leg:
                    if (LegsAllowed(tier))
                        s.Impulse(HitChannel.LegBuckle, 9f * k);
                    s.Impulse(HitChannel.SpinePitch, 60f * k);
                    break;
                default:
                    s.Impulse(HitChannel.SpinePitch, dz * 160f * k);
                    s.Impulse(HitChannel.SpineRoll, -dx * 100f * k);
                    s.Impulse(HitChannel.HeadPitch, dz * 120f * k);
                    break;
            }

            if ((heavy || strength >= 0.7f) && StaggerAllowed(tier))
            {
                s.Impulse(HitChannel.StaggerX, dx * 2.4f);
                s.Impulse(HitChannel.StaggerZ, dz * 2.4f);
                s.Impulse(HitChannel.LegBuckle, 4f);
                s.Impulse(HitChannel.SpinePitch, -dz * 120f);
            }
        }
    }
}
