using System;
using Project.Application.Services;

namespace Project.Application.Viewmodel
{
    /// <summary>Solver girdisi (bir kare).</summary>
    public struct ViewmodelMotionInput
    {
        public ViewmodelWeaponClass WeaponClass;
        public ViewmodelStanceKind Stance;
        /// <summary>Bu karede kat edilen yatay mesafe (m).</summary>
        public float Distance;
        public bool Grounded;
        public bool Sprinting;
        public bool Aiming;
        /// <summary>-1..1 sağa strafe.</summary>
        public float Strafe;
        public float LookYawDelta;
        public float LookPitchDelta;
        /// <summary>Bu karede yere inildiyse düşme hızı (m/s), yoksa 0.</summary>
        public float LandedFallSpeed;
        /// <summary>Bu karede ateş edilen mermi sayısı (0 veya 1 tipik).</summary>
        public int Shots;
        public float RandomSide;
        public float RandomRoll;
        /// <summary>Silahın ADS süresi (veri); 0 = sınıf tablosu.</summary>
        public float AdsTimeOverride;
    }

    /// <summary>Solver çıktısı: viewmodel kök ofseti (m / derece) ve kamera tepmesi.</summary>
    public struct ViewmodelMotionOutput
    {
        public float PosX, PosY, PosZ;
        public float Pitch, Yaw, Roll;
        public float CameraPitch, CameraYaw, CameraDipM;
        public float AdsWeight;
        public float FovScale;
        public float SprintWeight;
        public int Footfalls;
    }

    /// <summary>
    /// Tüm viewmodel hareket katmanlarını birleştirir: adım bob + koşu pozu + ADS + inercia + tepme + iniş.
    /// Mevcut ViewmodelMotionMath / ViewmodelPoseTimeline / CameraRecoilSpring yeniden kullanılır.
    /// </summary>
    public sealed class ViewmodelMotionSolver
    {
        private readonly StepSyncedBob _bob = new StepSyncedBob();
        private readonly AdsCurveBlend _ads = new AdsCurveBlend();
        private readonly InertiaSway _sway = new InertiaSway();
        private readonly ProceduralWeaponRecoil _recoil = new ProceduralWeaponRecoil();
        private readonly LandingDip _land = new LandingDip();
        private readonly CameraRecoilSpring _camera = new CameraRecoilSpring();
        private float _sprint;
        private ViewmodelWeaponClass _class = (ViewmodelWeaponClass)(-1);

        public CameraRecoilSpring Camera => _camera;

        public ViewmodelMotionOutput Step(in ViewmodelMotionInput i, float dt)
        {
            var o = new ViewmodelMotionOutput();
            if (dt <= 0f)
                return o;
            if (i.WeaponClass != _class)
            {
                _class = i.WeaponClass;
                _recoil.SetTuning(ViewmodelTuning.Recoil(_class));
                _sway.SetWeight(ViewmodelTuning.Weight(_class));
            }

            var sprinting = i.Sprinting && i.Grounded && !i.Aiming;
            var adsTime = i.AdsTimeOverride > 0.01f ? i.AdsTimeOverride : ViewmodelTuning.AdsSeconds(_class);
            _ads.Step(i.Aiming, adsTime, dt);
            var ads = _ads.Weight;

            // Koşu poz ağırlığı: giriş/çıkış süreleri ayrı.
            var sprintTarget = sprinting ? 1f : 0f;
            var span = sprintTarget > _sprint ? ViewmodelTuning.SprintInSeconds(_class) : ViewmodelTuning.SprintOutSeconds(_class);
            var step = dt / (span < 0.04f ? 0.04f : span);
            _sprint = sprintTarget > _sprint ? Math.Min(sprintTarget, _sprint + step) : Math.Max(sprintTarget, _sprint - step);
            var sprintW = ViewmodelPoseTimeline.SprintWeight(_sprint, sprintTarget > 0.5f);

            // Adım bob.
            var stride = ViewmodelTuning.StrideLength((ViewmodelStanceKind)i.Stance, sprinting);
            _bob.Step(i.Distance, stride, i.Grounded, dt);
            var prof = ViewmodelMotionMath.BobProfile((ViewmodelStance)(int)i.Stance, sprinting);
            ViewmodelMotionMath.SampleBob(prof, _bob.Phase, _bob.Amplitude * (1f - 0.7f * ads),
                out var bx, out var by, out var bp, out var byaw, out var br);

            // Ataleti, tepme, iniş.
            _sway.Step(i.LookYawDelta, i.LookPitchDelta, i.Strafe, _ads.SwayScale, dt);
            if (i.LandedFallSpeed > 0f)
                _land.Land(i.LandedFallSpeed, ViewmodelTuning.Weight(_class));
            _land.Step(dt);
            _recoil.Step(dt);
            var camP = 0f;
            var camY = 0f;
            for (var s = 0; s < i.Shots && s < 4; s++)
            {
                _recoil.Fire(i.RandomSide, i.RandomRoll, ads, out var cp, out var cy);
                camP += cp; camY += cy;
            }

            if (camP != 0f || camY != 0f)
                _camera.Kick(camP, camY);
            _camera.Step(dt);

            ViewmodelTuning.SprintPose(_class, out var sx, out var sy, out var sz, out var sp, out var syw, out var sr);

            o.PosX = bx + _sway.OffsetX + sx * sprintW;
            o.PosY = by + _bob.HeelStrike + _sway.OffsetY + _land.OffsetY + sy * sprintW;
            o.PosZ = -_recoil.KickbackM + _ads.Settle + sz * sprintW;
            o.Pitch = bp + _sway.PitchDeg + _recoil.PitchDeg + _land.PitchDeg + sp * sprintW;
            o.Yaw = byaw + _sway.YawDeg + _recoil.YawDeg + syw * sprintW;
            o.Roll = br + _sway.RollDeg + _recoil.RollDeg + sr * sprintW;
            o.CameraPitch = _camera.PitchOffset;
            o.CameraYaw = _camera.YawOffset;
            o.CameraDipM = _land.CameraDipM;
            o.AdsWeight = ads;
            o.FovScale = _ads.FovScale;
            o.SprintWeight = sprintW;
            o.Footfalls = _bob.FootfallsThisStep;
            return o;
        }

        public void Reset()
        {
            _bob.Reset(); _ads.Reset(); _sway.Reset(); _recoil.Reset(); _land.Reset(); _camera.Reset();
            _sprint = 0f;
        }

        // ENTEGRASYON: Infrastructure/Weapons/WeaponViewModel.cs icinde LateUpdate'te bu solver'in Step cikisi kok ofsetine eklenmeli (bob/sway/recoil/iniş/koşu).
        // ENTEGRASYON: Infrastructure/Rendering/CameraRig.cs icinde CameraPitch/CameraYaw/CameraDipM cikislari kamera ofsetine eklenmeli; Footfalls adim sesi/toz icin kullanilabilir.
    }
}
