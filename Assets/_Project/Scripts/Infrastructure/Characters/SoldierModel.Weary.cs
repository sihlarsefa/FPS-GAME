using System;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Characters.Animation;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Savaş yıpranmış (yorgun ama tetikte) rölanti katmanı: SetWeary(0..1). Ana poz LateUpdate'te yazıldıktan sonra
    /// çalışan toplamsal (additive) bir uygulayıcı; hareket, nişan, jest, tepme, çömelme/yatma/oturma ve ölümde etkisi sıfırdır.
    /// ENTEGRASYON: WearyBreathIn olayı sesli nefes (Audio) için kancadır; SetWearyAiming ADS durumunu bildirmek içindir.
    /// </summary>
    public sealed partial class SoldierModel
    {
        private SoldierWearyApplier _wearyApplier;
        private float _wearyTarget;
        private bool _wearyAiming;
        private readonly WearyReactionState _wearyReactions = new WearyReactionState();

        internal WearyReactionState WearyReactions => _wearyReactions;
        internal float WearyCrouchRaw => _crouch;

        /// <summary>Yakın mermi geçişi: dir -1 sol / +1 sağ, closeness 0..1.</summary>
        public void Flinch(float dir, float closeness)
        {
            _wearyReactions.Flinch(dir, closeness);
            EnsureWearyApplier();
        }

        /// <summary>Hasar sendelemesi: damage 0..100.</summary>
        public void Stagger(float dir, float damage)
        {
            _wearyReactions.Stagger(dir, damage);
            EnsureWearyApplier();
        }

        private void EnsureWearyApplier()
        {
            if (_wearyApplier != null || _spine == null)
                return;
            _wearyApplier = gameObject.GetComponent<SoldierWearyApplier>();
            if (_wearyApplier == null)
                _wearyApplier = gameObject.AddComponent<SoldierWearyApplier>();
            _wearyApplier.Bind(this);
        }

        /// <summary>Hedef yorgunluk 0..1.</summary>
        public float Weary => _wearyTarget;

        /// <summary>Yeni derin nefes alışında tetiklenir (arg: etkin yorgunluk 0..1). Sesli nefes kancası.</summary>
        public event Action<float> WearyBreathIn;

        /// <summary>Yorgunluk düzeyi. 0 verilince uygulayıcı kendini sıfırlar.</summary>
        public void SetWeary(float weary)
        {
            if (float.IsNaN(weary))
                return;
            _wearyTarget = Mathf.Clamp01(weary);
            if (_wearyApplier == null)
            {
                if (_wearyTarget < 0.001f || _spine == null)
                    return;
                _wearyApplier = gameObject.GetComponent<SoldierWearyApplier>();
                if (_wearyApplier == null)
                    _wearyApplier = gameObject.AddComponent<SoldierWearyApplier>();
            }

            _wearyApplier.Bind(this);
        }

        /// <summary>Oyuncu/bot nişan alıyor mu (yorgunluk katmanı bu sürede kapanır).</summary>
        public void SetWearyAiming(bool aiming)
        {
            _wearyAiming = aiming;
        }

        internal float WearyEffectiveNow()
        {
            var aiming = _wearyAiming || Mathf.Abs(_aimPitchTarget) > 12f;
            return SoldierDetailRules.WearyEffective(_wearyTarget, _speed, aiming, _prone, _seat, _gesture != 0, _recoil, _dead || _ragdollPosed);
        }

        internal int WearyFreeHandNow(int seed)
        {
            return SoldierDetailRules.WearyFreeHand(_hold == HoldKind.Rifle, _hold == HoldKind.Pistol, seed);
        }

        internal void WearyRaiseBreath(float effective)
        {
            var h = WearyBreathIn;
            if (h != null)
                h(effective);
        }

        /// <summary>Topallama için can oranı (mevcut hasar akışındaki Combatant sağlığı); sahip yoksa 1.</summary>
        internal float WearyHealthNormalized => _owner != null && _owner.Health != null ? _owner.State.Normalized : 1f;
        internal float WearySpeed => _speed;
        internal bool WearyDead => _dead;

        internal Transform WearySpine => _spine;
        internal Transform WearyNeck => _neck;
        internal Transform WearyBody => _body;
        internal Transform WearyArmsPivot => _armsPivot;
        internal Transform WearyShoulder(int side) => side == 0 ? _leftShoulder : _rightShoulder;
        internal Transform WearyElbow(int side) => side == 0 ? _leftElbow : _rightElbow;
    }

    /// <summary>Ana pozdan sonra toplamsal yorgunluk katmanını yazar; LOD atlanan karede çift uygulamayı geri alır.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(510)]
    public sealed class SoldierWearyApplier : MonoBehaviour
    {
        private const float Smooth = 3f;

        private sealed class Layer
        {
            public Transform T;
            public Quaternion LastRot;
            public Vector3 LastPos;
            public Quaternion Applied = Quaternion.identity;
            public Vector3 AppliedPos;
            public bool Has;

            public void Begin(Transform t)
            {
                T = t;
                if (T == null || !Has)
                    return;
                if (T.localRotation == LastRot && T.localPosition == LastPos)
                {
                    T.localRotation = T.localRotation * Quaternion.Inverse(Applied);
                    T.localPosition -= AppliedPos;
                }
            }

            public void Write(Quaternion rotDelta, Vector3 posDelta)
            {
                if (T == null)
                    return;
                T.localRotation = T.localRotation * rotDelta;
                T.localPosition += posDelta;
                Applied = rotDelta;
                AppliedPos = posDelta;
                LastRot = T.localRotation;
                LastPos = T.localPosition;
                Has = true;
            }

            public void Forget()
            {
                Has = false;
            }
        }

        private SoldierModel _model;
        private int _seed;
        private float _gate;
        private float _time;
        private int _headIndex;
        private float _headStart;
        private int _browIndex;
        private float _browStart;
        private float _wipe;
        private float _limp;
        private float _limpPhase;
        private int _handSide = 1;
        private readonly Layer _body = new Layer();
        private readonly Layer _spine = new Layer();
        private readonly Layer _neck = new Layer();
        private readonly Layer _arms = new Layer();
        private readonly Layer _shL = new Layer();
        private readonly Layer _shR = new Layer();
        private readonly Layer _elL = new Layer();
        private readonly Layer _elR = new Layer();

        public void Bind(SoldierModel model)
        {
            if (_model == model)
                return;
            _model = model;
            _seed = model != null ? (model.name.GetHashCode() ^ (int)(model.transform.position.x * 131f) ^ (int)(model.transform.position.z * 313f)) & 0x7fffffff : 1;
            _headIndex = 0;
            _headStart = 4f + 4f * SoldierDetailRules.WearyHash01(_seed, 5);
            _browIndex = 0;
            _browStart = 8f + SoldierDetailRules.WearyBrowInterval(_seed, 0) * 0.5f;
        }

        /// <summary>Saf: yumuşatma adımı (test edilebilir).</summary>
        public static float Step(float current, float target, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Smooth * Mathf.Max(0f, dt)));
        }

        private void LateUpdate()
        {
            if (_model == null)
                return;

            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            // önceki yazımları (ana poz yazmadıysa) geri al
            _body.Begin(_model.WearyBody);
            _spine.Begin(_model.WearySpine);
            _neck.Begin(_model.WearyNeck);
            _arms.Begin(_model.WearyArmsPivot);
            _shL.Begin(_model.WearyShoulder(0));
            _shR.Begin(_model.WearyShoulder(1));
            _elL.Begin(_model.WearyElbow(0));
            _elR.Begin(_model.WearyElbow(1));

            var react = _model.WearyReactions;
            react.SetCrouchTarget(_model.WearyCrouchRaw);
            react.Step(dt);
            _gate = Step(_gate, _model.WearyEffectiveNow(), dt);
            var limpTarget = _model.WearyDead ? 0f : WearyReactionRules.LimpWeight(_model.WearyHealthNormalized, _model.WearySpeed);
            _limp = Step(_limp, limpTarget, dt * 2f);
            if (_gate < 0.003f && _limp < 0.003f && react.FlinchAmount + react.StaggerAmount < 0.003f)
            {
                _wipe = 0f;
                ForgetAll();
                return;
            }

            var prevTime = _time;
            _time += dt;
            // Lobi varsayılanı: ağır nefes + omuz çökük, her askere farklı faz; çömelmede azalır
            var idleT = _time + 17f * SoldierDetailRules.WearyHash01(_seed, 11);
            var idle = WearyPose.Lerp(WearyPoseLibrary.Idle(WearyIdleVariant.HeavyBreath, idleT, _model.Weary),
                WearyPoseLibrary.Idle(WearyIdleVariant.SlumpedShoulders, idleT, _model.Weary), 0.5f);
            idle = WearyPose.Lerp(default(WearyPose), idle, _gate * (1f - 0.5f * react.Crouch));
            var rp = react.Evaluate(_time);
            // Düşük can: topallama (WearyPoseLibrary.Limp); yürüyüş hızıyla faz ilerler, yaralı bacak tohumdan
            _limpPhase = Mathf.Repeat(_limpPhase + dt * (0.8f + 0.7f * Mathf.Clamp01(_model.WearySpeed / 4f)), 1f);
            var limp = WearyPoseLibrary.Limp(_limpPhase, _limp, (_seed & 1) == 0 ? -1 : 1);
            rp.HipSway += limp.HipSway;
            rp.SpineRoll += limp.SpineRoll;
            rp.SpinePitch += limp.SpinePitch;
            rp.HeadPitch += limp.HeadPitch;
            var g = _gate;
            var weary = _model.Weary;

            // Nefes
            var breath = SoldierDetailRules.WearyBreathWave(_time) * SoldierDetailRules.WearyBreathAmplitude(weary) * g;
            if (SoldierDetailRules.WearyInhaleCrossed(prevTime, _time))
                _model.WearyRaiseBreath(g);

            // Baş olayı (tohumlu 9-14 s)
            var headT = _time - _headStart;
            if (headT > SoldierDetailRules.WearyHeadEventDuration)
            {
                _headStart += SoldierDetailRules.WearyHeadInterval(_seed, _headIndex);
                _headIndex++;
                headT = _time - _headStart;
            }

            var droop = SoldierDetailRules.WearyHeadDroopPitch(headT, weary) * g;
            var shake = SoldierDetailRules.WearyHeadShakeYaw(headT, _seed, _headIndex) * g;

            // Alın silme (yalnız serbest el)
            var browT = _time - _browStart;
            var free = _model.WearyFreeHandNow(_seed);
            if (browT > SoldierDetailRules.WearyBrowWipeDuration)
            {
                _browStart += SoldierDetailRules.WearyBrowInterval(_seed, _browIndex);
                _browIndex++;
                browT = _time - _browStart;
                _handSide = free;
            }

            var wipeTarget = free >= 0 ? SoldierDetailRules.WearyBrowWipeWeight(browT) : 0f;
            _wipe = Step(_wipe, wipeTarget, dt * 4f);
            if (free >= 0)
                _handSide = free;

            // Titreme
            var tr = SoldierDetailRules.WearyTremorAmplitude(weary) * g;
            var trN = tr * SoldierDetailRules.WearyTremorWave(_time, 0.3f);
            var trA = tr * SoldierDetailRules.WearyTremorWave(_time, 2.1f);

            var drop = SoldierDetailRules.WearySlumpDrop(weary) * g;
            var roll = SoldierDetailRules.WearySlumpRoll(weary) * g;

            _body.Write(Quaternion.identity, new Vector3(SoldierDetailRules.WearyHipSway(_time, weary) * g + idle.HipSway + rp.HipSway, 0f, 0f));
            _spine.Write(Quaternion.Euler(roll + breath * 0.9f + idle.SpinePitch + rp.SpinePitch, 0f, idle.SpineRoll + rp.SpineRoll), Vector3.zero);
            _arms.Write(Quaternion.Euler(0f, 0f, trA * 0.5f), new Vector3(0f, -drop + breath * 0.008f - 0.5f * (idle.ShoulderDropL + idle.ShoulderDropR + rp.ShoulderDropL + rp.ShoulderDropR), 0f));
            _neck.Write(Quaternion.Euler(droop + trN + idle.HeadPitch + rp.HeadPitch, shake + idle.HeadYaw, idle.HeadRoll), Vector3.zero);

            // Serbest kol: alına götür (silah taşımayan elde)
            var wL = _handSide == 0 ? _wipe : 0f;
            var wR = _handSide == 1 ? _wipe : 0f;
            WriteArm(_shL, _elL, wL, -1f);
            WriteArm(_shR, _elR, wR, 1f);
            if (_wipe > 0.001f && free < 0)
                _wipe = 0f;
        }

        private static void WriteArm(Layer sh, Layer el, float w, float side)
        {
            if (w < 0.001f)
            {
                sh.Write(Quaternion.identity, Vector3.zero);
                el.Write(Quaternion.identity, Vector3.zero);
                return;
            }

            // Omuz öne-yukarı kalkar, dirsek kıvrılır: el alına gelir (delta = ağırlıklı hedef pozisyon).
            sh.Write(Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-75f, 0f, side * 12f), w), Vector3.zero);
            el.Write(Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-125f, 0f, 0f), w), Vector3.zero);
        }

        private void ForgetAll()
        {
            _body.Forget(); _spine.Forget(); _neck.Forget(); _arms.Forget();
            _shL.Forget(); _shR.Forget(); _elL.Forget(); _elR.Forget();
        }
    }
}
