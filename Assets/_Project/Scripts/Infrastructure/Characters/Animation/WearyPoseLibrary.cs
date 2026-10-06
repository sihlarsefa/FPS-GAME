using UnityEngine;

namespace Project.Infrastructure.Characters.Animation
{
    /// <summary>Savaş yorgunu rölanti varyantları.</summary>
    public enum WearyIdleVariant
    {
        HeavyBreath = 0,
        SlumpedShoulders = 1,
        Alert = 2
    }

    /// <summary>Toplamsal poz ofseti (derece / metre). Unity Transform içermez.</summary>
    public struct WearyPose
    {
        public float SpinePitch, SpineRoll, HeadPitch, HeadRoll, HeadYaw;
        public float ShoulderDropL, ShoulderDropR, HipSway, Lameness;

        public static WearyPose Lerp(WearyPose a, WearyPose b, float t)
        {
            t = Mathf.Clamp01(t);
            return new WearyPose
            {
                SpinePitch = Mathf.Lerp(a.SpinePitch, b.SpinePitch, t),
                SpineRoll = Mathf.Lerp(a.SpineRoll, b.SpineRoll, t),
                HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t),
                HeadRoll = Mathf.Lerp(a.HeadRoll, b.HeadRoll, t),
                HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t),
                ShoulderDropL = Mathf.Lerp(a.ShoulderDropL, b.ShoulderDropL, t),
                ShoulderDropR = Mathf.Lerp(a.ShoulderDropR, b.ShoulderDropR, t),
                HipSway = Mathf.Lerp(a.HipSway, b.HipSway, t),
                Lameness = Mathf.Lerp(a.Lameness, b.Lameness, t)
            };
        }
    }

    /// <summary>
    /// Yorgun asker poz verileri (saf matematik). weary 0..1 ölçekler; 0 = sıfır poz.
    /// ENTEGRASYON: PipelineTiers.Tier düşükse çağıran taraf Step'i her 2 karede bir çağırmalı (bu sınıf Tier sorgulamaz).
    /// </summary>
    public static class WearyPoseLibrary
    {
        public const float BreathHz = 0.28f;

        public static WearyPose Idle(WearyIdleVariant v, float time, float weary)
        {
            weary = Mathf.Clamp01(weary);
            if (weary <= 0f) return default;
            var breath = Mathf.Sin(time * BreathHz * 2f * Mathf.PI);
            var p = new WearyPose();
            switch (v)
            {
                case WearyIdleVariant.HeavyBreath:
                    p.SpinePitch = 2.5f + 2.5f * breath;
                    p.HeadPitch = 3f + 1.5f * breath;
                    p.ShoulderDropL = p.ShoulderDropR = 0.012f * breath;
                    break;
                case WearyIdleVariant.SlumpedShoulders:
                    p.SpinePitch = 7f + 0.8f * breath;
                    p.HeadPitch = 9f;
                    p.HeadRoll = 2f * Mathf.Sin(time * 0.13f);
                    p.ShoulderDropL = p.ShoulderDropR = 0.045f;
                    break;
                default:
                    p.SpinePitch = 1f + 0.8f * breath;
                    p.HeadYaw = 6f * Mathf.Sin(time * 0.4f);
                    break;
            }

            return Scale(p, weary);
        }

        /// <summary>Hafif topallama: phase 0..1 yürüyüş döngüsü, side -1 sol / +1 sağ bacak yaralı.</summary>
        public static WearyPose Limp(float phase, float weary, int side)
        {
            weary = Mathf.Clamp01(weary);
            if (weary <= 0f) return default;
            var s = side < 0 ? -1f : 1f;
            var w = Mathf.Sin(phase * 2f * Mathf.PI);
            var p = new WearyPose
            {
                Lameness = 0.35f * weary,
                HipSway = 0.02f * s * w,
                SpineRoll = 3f * s * w,
                SpinePitch = 3f,
                HeadPitch = 2f
            };
            return Scale(p, weary, true);
        }

        private static WearyPose Scale(WearyPose p, float k, bool keepLame = false)
        {
            var lame = p.Lameness;
            var r = WearyPose.Lerp(default, p, k);
            if (keepLame) r.Lameness = lame;
            return r;
        }
    }
}
