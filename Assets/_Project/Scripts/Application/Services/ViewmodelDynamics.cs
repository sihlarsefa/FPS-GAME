using System;

namespace Project.Application.Services
{
    /// <summary>
    /// Silah görünümü (viewmodel) hissinin saf matematiği: kareden bağımsız yay-sönümleyici, ADS süresi, duvar yakınında
    /// indirme, viewmodel FOV. Unity bağımlılığı yok; WeaponViewModel / CameraRig buradan çağırır.
    /// </summary>
    public static class ViewmodelDynamics
    {
        public const float MinViewmodelFov = 40f;
        public const float MaxViewmodelFov = 90f;
        public const float DefaultViewmodelFov = 54f;
        public const float DefaultAdsSeconds = 0.18f;
        public const float ScopeExtraSeconds = 0.08f;

        /// <summary>Üstel takip katsayısı (kareden bağımsız Lerp alfa'sı).</summary>
        public static float ExpFollow(float rate, float dt)
        {
            if (rate <= 0f || dt <= 0f)
                return 0f;
            return 1f - (float)Math.Exp(-rate * dt);
        }

        /// <summary>
        /// Tek eksenli yay-sönümleyici adımı (kapalı form çözüm; dt'den bağımsız).
        /// stiffness = k, dampingRatio = ζ (1 = kritik). Hedefe doğru çeker.
        /// </summary>
        public static void StepSpring(ref float x, ref float v, float target, float stiffness, float dampingRatio, float dt)
        {
            if (dt <= 0f || stiffness <= 0f)
                return;
            if (float.IsNaN(x) || float.IsNaN(v))
            {
                x = target;
                v = 0f;
                return;
            }

            // Analitik (kapalı form) çözüm: dt'den tamamen bağımsız, koşulsuz kararlı.
            var w = (float)Math.Sqrt(stiffness);
            var z = dampingRatio < 0f ? 0f : dampingRatio;
            var d = x - target;
            double e = Math.Exp(-z * w * dt);
            double nd, nv;
            if (z < 0.9999f)
            {
                var wd = w * Math.Sqrt(1.0 - (double)z * z);
                var cs = Math.Cos(wd * dt);
                var sn = Math.Sin(wd * dt);
                var b = (v + z * w * d) / wd;
                nd = e * (d * cs + b * sn);
                nv = e * (v * cs - (d * wd + z * w * b) * sn);
            }
            else if (z <= 1.0001f)
            {
                var b = v + w * d;
                nd = e * (d + b * dt);
                nv = e * (v - w * b * dt);
            }
            else
            {
                var s2 = w * Math.Sqrt((double)z * z - 1.0);
                var r1 = -z * w + s2;
                var r2 = -z * w - s2;
                var c2 = (v - r1 * d) / (r2 - r1);
                var c1 = d - c2;
                var e1 = Math.Exp(r1 * dt);
                var e2 = Math.Exp(r2 * dt);
                nd = c1 * e1 + c2 * e2;
                nv = c1 * r1 * e1 + c2 * r2 * e2;
            }

            x = target + (float)nd;
            v = (float)nv;
        }

        /// <summary>ADS süresi: silah verisi (AdsTime) varsa o, yoksa varsayılan; dürbünlüde ek süre.</summary>
        public static float AdsSeconds(float weaponAdsTime, bool scoped)
        {
            var t = weaponAdsTime > 0.01f && !float.IsNaN(weaponAdsTime) ? weaponAdsTime : DefaultAdsSeconds;
            if (t > 1.5f)
                t = 1.5f;
            return t + (scoped ? ScopeExtraSeconds : 0f);
        }

        /// <summary>Duvar yakınında silah indirme ağırlığı 0..1: mesafe clearStart'ta 0, fullAt'te 1.</summary>
        public static float WallLowerWeight(float hitDistance, float clearStart, float fullAt)
        {
            if (float.IsNaN(hitDistance) || hitDistance >= clearStart)
                return 0f;
            if (hitDistance <= fullAt || clearStart <= fullAt)
                return 1f;
            var t = (clearStart - hitDistance) / (clearStart - fullAt);
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Kullanıcı ayarlı viewmodel FOV'unu güvenli aralığa kısar (NaN/0 → varsayılan).</summary>
        public static float ClampViewmodelFov(float fov)
        {
            if (float.IsNaN(fov) || fov <= 0f)
                return DefaultViewmodelFov;
            return fov < MinViewmodelFov ? MinViewmodelFov : (fov > MaxViewmodelFov ? MaxViewmodelFov : fov);
        }

        /// <summary>Nişan alırken viewmodel FOV'u hafifçe daralır (model büyük görünmesin).</summary>
        public static float AdsViewmodelFov(float hipFov, float aim01)
        {
            var a = aim01 < 0f ? 0f : (aim01 > 1f ? 1f : aim01);
            return ClampViewmodelFov(hipFov) * (1f - 0.08f * a);
        }
    }

    /// <summary>Tek eksen yay durumu; kare bağımsız (ViewmodelDynamics.StepSpring).</summary>
    public struct SpringAxis
    {
        public float Position;
        public float Velocity;

        public void Kick(float impulseVelocity, float offset = 0f)
        {
            Velocity += impulseVelocity;
            Position += offset;
        }

        public void Step(float target, float stiffness, float dampingRatio, float dt)
        {
            ViewmodelDynamics.StepSpring(ref Position, ref Velocity, target, stiffness, dampingRatio, dt);
        }
    }

    /// <summary>
    /// Kamera tepmesi (pitch/yaw) — model tepmesinden AYRI yaylar: kamera daha sert/hızlı toparlanır,
    /// model yumuşak ve uzun salınır. Çıktı derece cinsinden ofsettir.
    /// </summary>
    public sealed class CameraRecoilSpring
    {
        public float Stiffness = 90f;
        public float DampingRatio = 0.85f;
        public float MaxPitch = 12f;
        public float MaxYaw = 8f;

        private SpringAxis _pitch;
        private SpringAxis _yaw;

        public float PitchOffset => _pitch.Position;
        public float YawOffset => _yaw.Position;

        /// <summary>Atış başına darbe: pitchDeg yukarı (pozitif), yawDeg yana.</summary>
        public void Kick(float pitchDeg, float yawDeg)
        {
            _pitch.Kick(pitchDeg * 14f, pitchDeg * 0.3f);
            _yaw.Kick(yawDeg * 14f, yawDeg * 0.3f);
            Clamp();
        }

        public void Step(float dt)
        {
            _pitch.Step(0f, Stiffness, DampingRatio, dt);
            _yaw.Step(0f, Stiffness, DampingRatio, dt);
            Clamp();
        }

        public void Reset()
        {
            _pitch = default;
            _yaw = default;
        }

        private void Clamp()
        {
            if (_pitch.Position > MaxPitch) _pitch.Position = MaxPitch;
            if (_pitch.Position < -MaxPitch) _pitch.Position = -MaxPitch;
            if (_yaw.Position > MaxYaw) _yaw.Position = MaxYaw;
            if (_yaw.Position < -MaxYaw) _yaw.Position = -MaxYaw;
        }
    }

    /// <summary>Viewmodel duruşu (bob eğrisi seçimi).</summary>
    public enum ViewmodelStance
    {
        Stand = 0,
        Crouch = 1,
        Prone = 2,
        Slide = 3
    }

    /// <summary>Duruşa özel bob parametreleri (konum m, açı derece, frekans rad/s).</summary>
    public struct ViewmodelBobProfile
    {
        public float PosX;
        public float PosY;
        public float RollDeg;
        public float PitchDeg;
        public float YawDeg;
        public float FreqMin;
        public float FreqMax;
        public float AmpScale;
    }

    /// <summary>
    /// CoD-modern viewmodel hareket eğrileri (saf matematik). Yürüyüş: ince 8 figürü; koşu: güçlü, silah göğüs
    /// önünde yatık; çömelme: sıkı; yatış: minimal. Tüm çıktılar sınırlı (testle doğrulanır).
    /// </summary>
    public static class ViewmodelMotionMath
    {
        public const float MaxTurnLagDeg = 3.5f;
        public const float MaxStrafeRollDeg = 1.8f;
        public const float BreathHz = 0.3f;

        public static ViewmodelBobProfile BobProfile(ViewmodelStance stance, bool sprinting)
        {
            switch (stance)
            {
                case ViewmodelStance.Prone:
                    return new ViewmodelBobProfile { PosX = 0.0015f, PosY = 0.0020f, RollDeg = 0.25f, PitchDeg = 0.15f, YawDeg = 0.15f, FreqMin = 3.5f, FreqMax = 5f, AmpScale = 0.7f };
                case ViewmodelStance.Crouch:
                    return new ViewmodelBobProfile { PosX = 0.0045f, PosY = 0.0055f, RollDeg = 0.6f, PitchDeg = 0.3f, YawDeg = 0.25f, FreqMin = 5f, FreqMax = 8f, AmpScale = 0.8f };
                case ViewmodelStance.Slide:
                    return new ViewmodelBobProfile { PosX = 0.0015f, PosY = 0.0015f, RollDeg = 0.2f, PitchDeg = 0.1f, YawDeg = 0.1f, FreqMin = 4f, FreqMax = 6f, AmpScale = 0.5f };
                default:
                    return sprinting
                        ? new ViewmodelBobProfile { PosX = 0.014f, PosY = 0.016f, RollDeg = 2.4f, PitchDeg = 1.2f, YawDeg = 1.4f, FreqMin = 8f, FreqMax = 13.5f, AmpScale = 1f }
                        : new ViewmodelBobProfile { PosX = 0.006f, PosY = 0.007f, RollDeg = 0.9f, PitchDeg = 0.45f, YawDeg = 0.35f, FreqMin = 6f, FreqMax = 9.5f, AmpScale = 1f };
            }
        }

        public static float BobFrequency(ViewmodelBobProfile p, float speed01)
        {
            var m = speed01 < 0f ? 0f : (speed01 > 1f ? 1f : speed01);
            return p.FreqMin + (p.FreqMax - p.FreqMin) * m;
        }

        /// <summary>Bob örneği: konum x/y (m), açı pitch/yaw/roll (derece). amp 0..1.5. Figür-8: x sin, y |cos| + 2x harmonik.</summary>
        public static void SampleBob(ViewmodelBobProfile p, float phase, float amp,
            out float posX, out float posY, out float pitch, out float yaw, out float roll)
        {
            var a = amp < 0f ? 0f : (amp > 1.5f ? 1.5f : amp);
            a *= p.AmpScale;
            var s = (float)Math.Sin(phase);
            var c = (float)Math.Cos(phase);
            var s2 = (float)Math.Sin(phase * 2f);
            posX = s * p.PosX * a;
            posY = (-Math.Abs(c) * p.PosY + p.PosY * 0.5f) * a;
            pitch = s2 * p.PitchDeg * a;
            yaw = s * p.YawDeg * a;
            roll = s * p.RollDeg * a;
        }

        /// <summary>ADS nefes sallantısı (derece): yavaş 0.3 Hz; nefes tutulunca (hold01 1) belirgin sıkılaşır. suppressionMul >= 0.</summary>
        public static void AdsBreath(float time, float hold01, float suppressionMul, out float pitch, out float yaw)
        {
            var h = hold01 < 0f ? 0f : (hold01 > 1f ? 1f : hold01);
            var mul = float.IsNaN(suppressionMul) || suppressionMul < 0f ? 1f : (suppressionMul > 4f ? 4f : suppressionMul);
            var amp = 0.22f * (1f - 0.85f * h) * mul;
            var w = 2f * (float)Math.PI * BreathHz;
            pitch = (float)Math.Sin(time * w) * amp;
            yaw = (float)Math.Sin(time * w * 0.63f + 1.3f) * amp * 0.6f;
        }

        /// <summary>Dönüş gecikmesi hedefi (derece): bakış hızına (deg/s) göre silah 2-4° geride kalır.</summary>
        public static float TurnLagTarget(float rateDegPerSec)
        {
            if (float.IsNaN(rateDegPerSec)) return 0f;
            var v = rateDegPerSec * 0.012f;
            return v < -MaxTurnLagDeg ? -MaxTurnLagDeg : (v > MaxTurnLagDeg ? MaxTurnLagDeg : v);
        }

        /// <summary>Strafe roll hedefi (derece): strafe -1..1 (sağ +) → 1-2° yana yatış.</summary>
        public static float StrafeRollTarget(float strafe)
        {
            if (float.IsNaN(strafe)) return 0f;
            var v = (strafe < -1f ? -1f : (strafe > 1f ? 1f : strafe)) * -MaxStrafeRollDeg;
            return v;
        }

        /// <summary>Zıplama kaldırması (m, + yukarı): havada silah hafif kalkar. air01 0..1.</summary>
        public static float JumpLift(float air01)
        {
            var a = air01 < 0f ? 0f : (air01 > 1f ? 1f : air01);
            return 0.012f * a;
        }

        /// <summary>İniş darbesi hızı (yay hızı, - aşağı): düşme hızına (m/s) göre, sınırlı.</summary>
        public static float LandImpulse(float fallSpeed)
        {
            if (float.IsNaN(fallSpeed) || fallSpeed < 0f) fallSpeed = 0f;
            var t = fallSpeed / 12f;
            t = t > 1f ? 1f : t;
            return -(0.2f + 0.3f * t);
        }
    }
}
