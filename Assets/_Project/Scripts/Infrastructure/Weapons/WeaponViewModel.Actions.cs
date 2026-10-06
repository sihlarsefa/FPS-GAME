using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>WeaponViewModel eylem animasyonları: şarjör/fişek/kutu değiştirme, sürgü/pompa/kızak, yumruk, bomba, iyileşme.</summary>
    public sealed partial class WeaponViewModel
    {
        private enum ViewAction
        {
            None,
            Reload,
            Melee,
            Throw,
            Use,
            Inspect,
            MagCheck
        }

        private enum CycleKind
        {
            None,
            Bolt,
            Pump,
            Slide
        }

        private ViewAction _action;
        private float _actionTime;
        private float _actionDuration;
        private float _actionLower;
        private Vector3 _actionPos;
        private Quaternion _actionRot = Quaternion.identity;
        private bool _useExiting;
        private float _useExitTime;
        private ViewmodelProp _useProp = ViewmodelProp.Bandage;
        private ViewmodelProp _throwProp = ViewmodelProp.FragGrenade;
        private bool _punchLeft;
        private int _shellCount = 1;

        private CycleKind _cycle;
        private float _cycleTime;
        private float _cycleDelay;
        private float _cycleDuration;
        private bool _cycleEjected;
        private Vector3 _cyclePos;
        private Quaternion _cycleRot = Quaternion.identity;

        // ------------------------------------------------------------------ Control

        private void StartAction(ViewAction action, float duration)
        {
            _action = action;
            _actionTime = 0f;
            _actionDuration = Mathf.Max(0.05f, duration);
        }

        private bool _hardEnd;

        private void EndAction()
        {
            if (!_hardEnd && TryBeginReloadCancel())
                return;
            _reloadCancelling = false;
            _reloadPlan = null;
            _reloadTracker = null;
            _action = ViewAction.None;
            _actionTime = 0f;
            _actionLower = 0f;
            _actionPos = Vector3.zero;
            _actionRot = Quaternion.identity;
            _useExiting = false;
            SetProp(_right, ViewmodelProp.None);
            SetProp(_left, ViewmodelProp.None);
            HideSpareMagazine();
            if (_model != null)
            {
                _model.ResetParts();
                _model.SetMagazineVisible(true);
            }
        }

        /// <summary>Tüm eylemleri ve atış döngülerini iptal eder; hareketli parçalar evine döner.</summary>
        private void CancelActions()
        {
            _hardEnd = true;
            EndAction();
            _hardEnd = false;
            _cycle = CycleKind.None;
            _cyclePos = Vector3.zero;
            _cycleRot = Quaternion.identity;
        }

        private static ViewmodelProp PropForItem(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.EnergyDrink: return ViewmodelProp.EnergyDrink;
                case ItemIds.Painkiller: return ViewmodelProp.Painkiller;
                case ItemIds.FirstAid:
                case ItemIds.MedKit: return ViewmodelProp.FirstAid;
                default: return ViewmodelProp.Bandage;
            }
        }

        // ------------------------------------------------------------------ Per-frame

        private void UpdateActions(float dt)
        {
            _actionLower = 0f;
            _actionPos = Vector3.zero;
            _actionRot = Quaternion.identity;

            if (_action == ViewAction.None)
                return;

            _actionTime += dt;
            switch (_action)
            {
                case ViewAction.Reload:
                    if (_reloadCancelling)
                    {
                        if (!UpdateReloadCancel(dt))
                            return;
                        break;
                    }

                    if (_model == null || _actionTime >= _actionDuration)
                    {
                        _hardEnd = true;
                        EndAction();
                        _hardEnd = false;
                        return;
                    }

                    AnimateReload(_actionTime / _actionDuration);
                    break;

                case ViewAction.Melee:
                    if (_actionTime >= _actionDuration)
                    {
                        EndAction();
                        return;
                    }

                    AnimateMelee(_actionTime / _actionDuration);
                    break;

                case ViewAction.Throw:
                    if (_actionTime >= _actionDuration)
                    {
                        EndAction();
                        return;
                    }

                    AnimateThrow(_actionTime / _actionDuration);
                    break;

                case ViewAction.Inspect:
                    if (_model == null || _actionTime >= _actionDuration || _sprintBlend > 0.3f)
                    {
                        EndAction();
                        return;
                    }

                    AnimateInspect(_actionTime / _actionDuration);
                    break;

                case ViewAction.MagCheck:
                    if (_model == null || _actionTime >= _actionDuration || _sprintBlend > 0.3f)
                    {
                        EndAction();
                        return;
                    }

                    AnimateMagCheck(_actionTime / _actionDuration);
                    break;

                case ViewAction.Use:
                    if (!_useExiting && _actionTime >= _actionDuration + 0.25f)
                    {
                        _useExiting = true;
                        _useExitTime = 0f;
                    }

                    if (_useExiting)
                    {
                        _useExitTime += dt;
                        if (_useExitTime >= 0.3f)
                        {
                            EndAction();
                            return;
                        }
                    }

                    AnimateUse();
                    break;
            }
        }

        // ------------------------------------------------------------------ Inspect

        /// <summary>Silahı incele (varsayılan K): döndürür, yana yatırır, sol el kabzayı gösterir. Ateş/nişan/koşu iptal eder.</summary>
        public void PlayInspect()
        {
            EnsureBuilt();
            if (_model == null || _hasPending || IsEquipping || _action != ViewAction.None || _wantsAim || _sprintBlend > 0.1f)
                return;

            StartAction(ViewAction.Inspect, ViewmodelPoseTimeline.InspectDuration);
        }

        private void CancelInspect()
        {
            if (_action == ViewAction.Inspect || _action == ViewAction.MagCheck)
                EndAction();
        }

        private void PollInspectKey()
        {
            try
            {
                if (Project.Infrastructure.Input.InputBindings.Pressed(Project.Application.Services.BindAction.Inspect))
                    PlayInspect();
            }
            catch { }
        }

        private void AnimateInspect(float t)
        {
            var tl = ViewmodelPoseTimeline.Inspect();
            var raise = tl.Raise.Evaluate(t);
            _actionRot = Quaternion.Euler(tl.Pitch.Evaluate(t) * raise, tl.Yaw.Evaluate(t), tl.Roll.Evaluate(t));
            _actionPos = new Vector3(-0.06f * raise, 0.04f * raise, 0.05f * raise);

            var m = _model;
            var hand = Mathf.Clamp01(tl.Hand.Evaluate(t));
            if (hand > 0.001f && m.MagazineHandGrip != null)
                _left.SetAnchorGoal(m.MagazineHandGrip, hand);

            // Kamara kontrolü: kızak/sürgü kısa çekilir.
            var check = Mathf.Clamp01(tl.Check.Evaluate(t));
            if (check > 0.001f)
            {
                if (WeaponStyles.IsPistol(_shownStyle)) m.SetSlide(check);
                else if (m.Bolt != null) m.SetBolt(0f, check * 0.8f);
            }
        }

        // ------------------------------------------------------------------ Reload

        private void AnimateReload(float t)
        {
            TickReloadPhases(t);
            if (WeaponStyles.IsPumpAction(_shownStyle))
                AnimateShellReload(t);
            else if (WeaponStyles.IsBeltFed(_shownStyle))
                AnimateBeltReload(t);
            else if (_timeline != null)
                AnimateTimelineMagazineReload(t);
            else
                AnimateMagazineReload(t);
        }

        /// <summary>Şarjörlü silahlar: eğ, sol el şarjörü çıkarır (ekran dışına), yenisini takar; JNG-90'da sonunda sürgü.</summary>
        private void AnimateMagazineReload(float t)
        {
            var m = _model;
            var pistol = WeaponStyles.IsPistol(_shownStyle);
            var e = Window(t, 0f, 0.12f, 0.86f, 1f);
            _actionRot = pistol ? Quaternion.Euler(-12f * e, 8f * e, 18f * e) : Quaternion.Euler(-7f * e, 10f * e, 26f * e);
            _actionPos = (pistol ? new Vector3(-0.04f, 0.035f, -0.03f) : new Vector3(-0.03f, 0.025f, -0.015f)) * e;

            // Şarjör yolu (m).
            const float travel = 0.32f;
            float d;
            if (t < 0.18f)
            {
                d = 0f;
            }
            else if (t < 0.34f)
            {
                var x = Ramp(t, 0.18f, 0.34f);
                d = x * x * travel;
            }
            else if (t < 0.46f)
            {
                d = travel;
            }
            else if (t < 0.64f)
            {
                var x = 1f - Ramp(t, 0.46f, 0.64f);
                d = x * x * travel;
            }
            else if (t < 0.7f)
            {
                d = -0.006f * Mathf.Sin(Ramp(t, 0.64f, 0.7f) * Mathf.PI);
            }
            else
            {
                d = 0f;
            }

            var tilt = Mathf.Clamp01(d / travel) * -18f;
            m.SetMagazineOffset(m.MagazineEjectDirection * d, Quaternion.Euler(tilt, 0f, 0f));

            var reach = Window(t, 0.06f, 0.18f, 0.7f, 0.84f);
            if (m.MagazineHandGrip != null)
                _left.SetAnchorGoal(m.MagazineHandGrip, reach);

            // Şarjör yerine oturunca silah hafifçe sarsılır (kurma/sürgü bırakma).
            var jolt = Window(t, 0.76f, 0.79f, 0.79f, 0.86f);
            _actionPos += new Vector3(0f, 0.004f, -0.012f) * jolt;

            if (pistol)
            {
                var slide = SmoothRamp(t, 0.74f, 0.78f) * (1f - Ramp(t, 0.8f, 0.82f));
                m.SetSlide(slide);
            }

            if (WeaponStyles.IsBoltAction(_shownStyle) && m.BoltHandGrip != null)
            {
                var hand = Window(t, 0.68f, 0.73f, 0.9f, 0.96f);
                _right.SetAnchorGoal(m.BoltHandGrip, hand);
                var lift = SmoothRamp(t, 0.73f, 0.76f) * (1f - SmoothRamp(t, 0.85f, 0.88f));
                var back = SmoothRamp(t, 0.76f, 0.8f) * (1f - SmoothRamp(t, 0.81f, 0.85f));
                m.SetBolt(lift, back);
                _actionRot = _actionRot * Quaternion.Euler(0f, 0f, -6f * hand);
            }
        }

        /// <summary>Kemerli (PMT-76): üst kapak açılır, boş kutu düşer, yeni kutu girer, kemer tablaya serilir, kapak kapanır.</summary>
        private void AnimateBeltReload(float t)
        {
            var tl = _timeline;
            if (tl == null || tl.Kind != ReloadKind.Machinegun)
                tl = _timeline = ViewmodelPoseTimeline.Build(ReloadKind.Machinegun, false);

            var m = _model;
            var tilt = tl.Tilt.Evaluate(t);
            _actionRot = Quaternion.Euler(-5f * tilt, 9f * tilt, 16f * tilt);
            _actionPos = new Vector3(-0.025f, 0.03f, -0.01f) * tilt;

            m.SetCoverOpen(Mathf.Clamp01(tl.Cover.Evaluate(t)));

            const float travel = 0.3f;
            var mag = tl.MagTravel.Evaluate(t);
            var d = Mathf.Max(0f, mag) * travel;
            m.SetMagazineOffset(m.MagazineEjectDirection * d, Quaternion.Euler(0f, 0f, -Mathf.Clamp01(mag) * 20f));
            UpdateSpareMagazine(tl, t, mag);

            var reach = Mathf.Clamp01(tl.HandReach.Evaluate(t));
            var lay = Mathf.Clamp01(tl.BeltLay.Evaluate(t));
            if (m.CoverHandGrip != null)
                _left.SetAnchorGoal(m.MagazineHandGrip != null ? m.MagazineHandGrip : m.CoverHandGrip, m.CoverHandGrip, lay, reach);
            else if (m.MagazineHandGrip != null)
                _left.SetAnchorGoal(m.MagazineHandGrip, reach);

            var jolt = tl.Jolt.Evaluate(t);
            var mech = tl.Mechanism.Evaluate(t);
            _actionPos += new Vector3(0f, 0.004f, -0.012f) * jolt + new Vector3(0f, 0f, -0.006f) * mech;
            if (mech > 0.001f)
                m.SetBolt(0f, Mathf.Clamp01(mech));
        }

        /// <summary>Escort: silah yan yatar, sol el fişekleri tek tek alt yükleme ağzına iter, sonunda pompa çekilir.</summary>
        private void AnimateShellReload(float t)
        {
            var m = _model;
            var e = Window(t, 0f, 0.1f, 0.88f, 1f);
            _actionRot = Quaternion.Euler(-8f * e, -6f * e, -34f * e);
            _actionPos = new Vector3(-0.025f, 0.035f, 0f) * e;

            var reach = Window(t, 0.03f, 0.1f, 0.84f, 0.9f);
            var port = m.LoadingPort != null ? m.transform.InverseTransformPoint(m.LoadingPort.position) : new Vector3(0f, 0.004f, 0.12f);
            var portRot = Quaternion.LookRotation(new Vector3(0.05f, 0.35f, 1f), new Vector3(0f, -1f, 0.35f));
            var portPos = port + new Vector3(-0.012f, -0.04f, -0.095f);
            var pickPos = port + new Vector3(-0.06f, -0.25f, -0.14f);
            var pickRot = Quaternion.LookRotation(new Vector3(0.2f, 0.2f, 1f), new Vector3(0.3f, -1f, 0f));
            var pushPos = portPos + new Vector3(0f, 0.012f, 0.03f);

            var shell = false;
            Vector3 handPos;
            Quaternion handRot;
            if (!ViewmodelPoseTimeline.ShellStage(t, _shellCount, out _, out var s))
            {
                handPos = portPos;
                handRot = portRot;
            }
            else
            {
                if (s < 0.3f)
                {
                    var x = Smooth01(s / 0.3f);
                    handPos = Vector3.Lerp(portPos, pickPos, x);
                    handRot = Quaternion.Slerp(portRot, pickRot, x);
                    shell = s > 0.22f;
                }
                else if (s < 0.7f)
                {
                    var x = Smooth01((s - 0.3f) / 0.4f);
                    handPos = Vector3.Lerp(pickPos, portPos, x);
                    handRot = Quaternion.Slerp(pickRot, portRot, x);
                    shell = true;
                }
                else if (s < 0.85f)
                {
                    var x = Smooth01((s - 0.7f) / 0.15f);
                    handPos = Vector3.Lerp(portPos, pushPos, x);
                    handRot = portRot;
                    shell = s < 0.82f;
                }
                else
                {
                    var x = Smooth01((s - 0.85f) / 0.15f);
                    handPos = Vector3.Lerp(pushPos, portPos, x);
                    handRot = portRot;
                }
            }

            _left.SetPoseGoal(GoalKind.ModelSpace, handPos, handRot, reach);
            SetProp(_left, shell && reach > 0.5f ? ViewmodelProp.ShotgunShell : ViewmodelProp.None);

            var tlp = _timeline != null && _timeline.Kind == ReloadKind.Shotgun ? _timeline : null;
            var pump = tlp != null
                ? Mathf.Clamp01(tlp.Mechanism.Evaluate(t))
                : SmoothRamp(t, 0.9f, 0.94f) * (1f - SmoothRamp(t, 0.95f, 0.99f));
            m.SetPump(pump);
            if (tlp != null)
                _actionPos += new Vector3(0f, 0.004f, -0.012f) * tlp.Jolt.Evaluate(t);
        }

        // ------------------------------------------------------------------ Melee / throw / use

        private void AnimateMelee(float t)
        {
            if (_model == null)
            {
                var extend = SmoothRamp(t, 0f, 0.3f) * (1f - SmoothRamp(t, 0.45f, 1f));
                var arm = _punchLeft ? _left : _right;
                var side = _punchLeft ? -1f : 1f;
                var pos = new Vector3(0.035f * side, -0.1f, 0.55f);
                var rot = Quaternion.LookRotation(new Vector3(-0.05f * side, 0.05f, 1f), new Vector3(0.3f * side, 1f, 0f));
                arm.SetPoseGoal(GoalKind.PoseSpace, pos, rot, extend);
                return;
            }

            // Silahla dipçik/namlu darbesi.
            var c = SmoothRamp(t, 0f, 0.25f) * (1f - SmoothRamp(t, 0.4f, 1f));
            _actionPos = new Vector3(-0.06f, 0.03f, 0.1f) * c;
            _actionRot = Quaternion.Euler(-10f * c, 28f * c, -25f * c);
        }

        private void AnimateThrow(float t)
        {
            if (_model != null)
                _actionLower = 0.65f * Window(t, 0f, 0.15f, 0.75f, 1f);

            var weight = Window(t, 0f, 0.12f, 0.75f, 0.95f);

            // Anahtar pozlar (poz uzayı).
            var p0 = new Vector3(0.08f, -0.17f, 0.28f);
            var r0 = Quaternion.LookRotation(new Vector3(0f, 0.4f, 1f), new Vector3(0.7f, 0.7f, 0f));
            var p1 = new Vector3(0.24f, 0.02f, 0.06f);
            var r1 = Quaternion.LookRotation(new Vector3(0f, 1f, -0.3f), new Vector3(1f, 0f, 0f));
            var p2 = new Vector3(0.08f, 0.02f, 0.55f);
            var r2 = Quaternion.LookRotation(new Vector3(0f, 0.2f, 1f), new Vector3(0.3f, 1f, 0f));
            var p3 = new Vector3(0.03f, -0.26f, 0.45f);
            var r3 = Quaternion.LookRotation(new Vector3(0f, -0.4f, 1f), new Vector3(0.3f, 1f, 0f));

            Vector3 pos;
            Quaternion rot;
            if (t < 0.12f)
            {
                pos = p0;
                rot = r0;
            }
            else if (t < 0.42f)
            {
                var x = SmoothRamp(t, 0.12f, 0.42f);
                pos = Vector3.Lerp(p0, p1, x);
                rot = Quaternion.Slerp(r0, r1, x);
            }
            else if (t < 0.58f)
            {
                var x = Ramp(t, 0.42f, 0.58f);
                x = x * x;
                pos = Vector3.Lerp(p1, p2, x);
                rot = Quaternion.Slerp(r1, r2, x);
            }
            else
            {
                var x = SmoothRamp(t, 0.58f, 0.78f);
                pos = Vector3.Lerp(p2, p3, x);
                rot = Quaternion.Slerp(r2, r3, x);
            }

            _right.SetPoseGoal(GoalKind.PoseSpace, pos, rot, weight);
            SetProp(_right, t < 0.56f ? _throwProp : ViewmodelProp.None);

            // Sol el: yumrukta pimi çeker, sonra öne uzanıp nişan verir.
            if (_model == null)
            {
                var pin = Window(t, 0.05f, 0.14f, 0.24f, 0.34f);
                var aimArm = Window(t, 0.3f, 0.42f, 0.58f, 0.75f);
                var pinPos = p0 + new Vector3(-0.05f, 0.02f, 0.02f);
                var pinRot = Quaternion.LookRotation(new Vector3(0.6f, 0.2f, 1f), new Vector3(-0.3f, 1f, 0f));
                var aimPos = new Vector3(-0.12f, -0.05f, 0.5f);
                var aimRot = Quaternion.LookRotation(new Vector3(0.1f, 0.15f, 1f), new Vector3(-0.2f, 1f, 0f));
                if (pin >= aimArm)
                    _left.SetPoseGoal(GoalKind.PoseSpace, pinPos, pinRot, pin);
                else
                    _left.SetPoseGoal(GoalKind.PoseSpace, aimPos, aimRot, aimArm);
            }
        }

        private void AnimateUse()
        {
            var w = Smooth01(_actionTime / 0.28f);
            if (_useExiting)
                w *= 1f - Smooth01(_useExitTime / 0.3f);

            if (_model != null)
                _actionLower = w;

            var time = _actionTime;
            switch (_useProp)
            {
                case ViewmodelProp.EnergyDrink:
                case ViewmodelProp.Painkiller:
                {
                    // Sağ el kutuyu ağza götürür; sol el aşağıda.
                    var sip = 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01((time - 0.25f) / 0.6f) * Mathf.PI);
                    if (time > 0.85f)
                        sip = 0.85f + 0.15f * Mathf.Sin((time - 0.85f) * 2.2f);
                    var low = new Vector3(0.07f, -0.17f, 0.3f);
                    var mouth = new Vector3(0.025f, -0.075f, 0.16f);
                    var pos = Vector3.Lerp(low, mouth, sip);
                    var rot = Quaternion.LookRotation(new Vector3(-0.5f, Mathf.Lerp(0.2f, 0.9f, sip), 1f), new Vector3(0.9f, 0.2f, 0.3f));
                    _right.SetPoseGoal(GoalKind.PoseSpace, pos, rot, w);
                    SetProp(_right, w > 0.05f ? _useProp : ViewmodelProp.None);

                    var leftPos = new Vector3(-0.15f, -0.34f, 0.18f);
                    var leftRot = Quaternion.LookRotation(new Vector3(0.2f, 0.3f, 1f), new Vector3(-0.8f, 0.5f, 0f));
                    _left.SetPoseGoal(GoalKind.PoseSpace, leftPos, leftRot, w);
                    SetProp(_left, ViewmodelProp.None);
                    break;
                }

                case ViewmodelProp.FirstAid:
                {
                    // Sol elde kutu, sağ el içinden malzeme alıp uygular.
                    var leftPos = new Vector3(-0.05f, -0.17f, 0.3f);
                    var leftRot = Quaternion.LookRotation(new Vector3(0.45f, 0.25f, 1f), new Vector3(0.1f, -1f, 0.2f));
                    _left.SetPoseGoal(GoalKind.PoseSpace, leftPos, leftRot, w);
                    SetProp(_left, w > 0.05f ? ViewmodelProp.FirstAid : ViewmodelProp.None);

                    var cycle = Mathf.Sin(time * 4.2f);
                    var rightPos = new Vector3(0.06f, -0.13f + 0.035f * cycle, 0.3f + 0.02f * Mathf.Cos(time * 4.2f));
                    var rightRot = Quaternion.LookRotation(new Vector3(-0.6f, -0.3f, 1f), new Vector3(0.3f, 1f, 0.2f));
                    _right.SetPoseGoal(GoalKind.PoseSpace, rightPos, rightRot, w);
                    SetProp(_right, ViewmodelProp.None);
                    break;
                }

                default:
                {
                    // Sargı: sol el ruloyu tutar, sağ el etrafında sarar.
                    var leftPos = new Vector3(-0.04f, -0.15f, 0.3f);
                    var leftRot = Quaternion.LookRotation(new Vector3(0.5f, 0.3f, 1f), new Vector3(0.2f, -1f, 0.1f));
                    _left.SetPoseGoal(GoalKind.PoseSpace, leftPos, leftRot, w);
                    SetProp(_left, w > 0.05f ? ViewmodelProp.Bandage : ViewmodelProp.None);

                    var angle = time * Mathf.PI * 2f * 1.4f;
                    var rightPos = new Vector3(0.04f + 0.055f * Mathf.Cos(angle), -0.12f + 0.04f * Mathf.Sin(angle), 0.32f);
                    var rightRot = Quaternion.LookRotation(new Vector3(-0.7f, 0.1f * Mathf.Sin(angle), 1f), new Vector3(0.3f, 1f, 0f));
                    _right.SetPoseGoal(GoalKind.PoseSpace, rightPos, rightRot, w);
                    SetProp(_right, ViewmodelProp.None);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Fire cycles

        private void HandleFireCycle(WeaponDefinitionData definition)
        {
            if (_model == null || _action == ViewAction.Reload)
            {
                EjectShell();
                return;
            }

            var interval = definition != null ? Mathf.Max(0.05f, definition.FireIntervalSeconds) : 0.1f;
            var boltAction = WeaponStyles.IsBoltAction(_shownStyle) || (definition != null && definition.IsBoltAction);
            if (boltAction && _model.Bolt != null && _model.BoltHandGrip != null)
            {
                StartCycle(CycleKind.Bolt, 0.12f, Mathf.Clamp(interval - 0.2f, 0.55f, 1.05f));
                return;
            }

            if (WeaponStyles.IsPumpAction(_shownStyle) && _model.Pump != null)
            {
                StartCycle(CycleKind.Pump, 0.08f, Mathf.Clamp(interval * 0.65f, 0.28f, 0.5f));
                return;
            }

            if (_model.Slide != null)
                StartCycle(CycleKind.Slide, 0f, Mathf.Clamp(interval * 0.6f, 0.06f, 0.1f));

            EjectShell();
        }

        private void StartCycle(CycleKind kind, float delay, float duration)
        {
            _cycle = kind;
            _cycleTime = 0f;
            _cycleDelay = delay;
            _cycleDuration = Mathf.Max(0.03f, duration);
            _cycleEjected = false;
        }

        private void UpdateCycles(float dt)
        {
            _cyclePos = Vector3.zero;
            _cycleRot = Quaternion.identity;
            if (_cycle == CycleKind.None)
                return;

            if (_model == null || _action == ViewAction.Reload || _action == ViewAction.Throw || _action == ViewAction.Use)
            {
                _cycle = CycleKind.None;
                return;
            }

            _cycleTime += dt;
            var t = (_cycleTime - _cycleDelay) / _cycleDuration;
            if (t < 0f)
                return;

            var m = _model;
            if (t >= 1f)
            {
                switch (_cycle)
                {
                    case CycleKind.Bolt: m.SetBolt(0f, 0f); break;
                    case CycleKind.Pump: m.SetPump(0f); break;
                    case CycleKind.Slide: m.SetSlide(0f); break;
                }

                _cycle = CycleKind.None;
                return;
            }

            switch (_cycle)
            {
                case CycleKind.Bolt:
                {
                    var hand = Window(t, 0f, 0.18f, 0.82f, 1f);
                    _right.SetAnchorGoal(m.BoltHandGrip, hand);
                    var lift = SmoothRamp(t, 0.18f, 0.32f) * (1f - SmoothRamp(t, 0.7f, 0.82f));
                    var back = SmoothRamp(t, 0.32f, 0.5f) * (1f - SmoothRamp(t, 0.52f, 0.68f));
                    m.SetBolt(lift, back);
                    _cycleRot = Quaternion.Euler(-2f * hand, 0f, -7f * hand);
                    _cyclePos = new Vector3(0f, 0f, -0.006f * back);
                    if (!_cycleEjected && t >= 0.48f)
                    {
                        _cycleEjected = true;
                        EjectShell();
                    }

                    break;
                }

                case CycleKind.Pump:
                {
                    var pump = SmoothRamp(t, 0f, 0.42f) * (1f - SmoothRamp(t, 0.55f, 1f));
                    m.SetPump(pump);
                    _cyclePos = new Vector3(0f, -0.004f * pump, -0.01f * pump);
                    _cycleRot = Quaternion.Euler(-1.5f * pump, 0f, 2f * pump);
                    if (!_cycleEjected && t >= 0.42f)
                    {
                        _cycleEjected = true;
                        EjectShell();
                    }

                    break;
                }

                case CycleKind.Slide:
                {
                    var slide = t < 0.3f ? t / 0.3f : 1f - Smooth01((t - 0.3f) / 0.7f);
                    m.SetSlide(slide);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Fists

        /// <summary>Silahsız yumruk duruşu (poz uzayı): koşarken aşağıda sallanır, kuşanmada aşağıdan gelir.</summary>
        private void FistPose(bool left, out Vector3 position, out Quaternion rotation)
        {
            var side = left ? -1f : 1f;
            var guard = new Vector3(left ? -0.14f : 0.15f, left ? -0.205f : -0.2f, left ? 0.26f : 0.28f);
            var guardRot = Quaternion.LookRotation(new Vector3(-0.12f * side, 0.35f, 1f), new Vector3(side, 0.4f, 0f));

            var sprint = Smooth01(_sprintBlend);
            var swing = Mathf.Sin(_bobPhase + (left ? Mathf.PI : 0f));
            var run = new Vector3(0.16f * side, -0.29f + 0.045f * swing, 0.2f + 0.06f * swing);
            var runRot = Quaternion.LookRotation(new Vector3(-0.1f * side, 0.6f + 0.3f * swing, 1f), new Vector3(side, 0.2f, 0f));

            position = Vector3.Lerp(guard, run, sprint);
            rotation = Quaternion.Slerp(guardRot, runRot, sprint);

            var lower = Smooth01(_equipLower);
            position += new Vector3(0.02f * side, -0.26f, -0.06f) * lower;
            rotation = rotation * Quaternion.Euler(35f * lower, 0f, 0f);
        }

        // ------------------------------------------------------------------ Pose profiles

        private struct PoseProfile
        {
            public Vector3 HipPosition;
            public Quaternion HipRotation;
            public Vector3 SprintPosition;
            public Quaternion SprintRotation;
            public Vector3 LowerPosition;
            public Quaternion LowerRotation;
            public float KickBack;
            public float KickPitch;
            public float KickSide;
            public float KickRoll;
            public float SwayScale;
            public float FlashScale;

            public static PoseProfile For(WeaponStyle style)
            {
                var p = Rifle(new Vector3(0.12f, -0.1f, 0.19f), 0.028f, 4f, 1f, 1f);
                switch (style)
                {
                    case WeaponStyle.None:
                        p = Rifle(new Vector3(0.12f, -0.1f, 0.19f), 0f, 0f, 1f, 0f);
                        break;

                    case WeaponStyle.Sar9:
                    case WeaponStyle.Tp9:
                    case WeaponStyle.MeteSft:
                        p = new PoseProfile
                        {
                            HipPosition = new Vector3(0.1f, -0.1f, 0.27f),
                            HipRotation = Quaternion.Euler(0f, -1.5f, 0f),
                            SprintPosition = new Vector3(0.07f, -0.2f, 0.2f),
                            SprintRotation = Quaternion.Euler(52f, -10f, 12f),
                            LowerPosition = new Vector3(0.06f, -0.34f, 0.12f),
                            LowerRotation = Quaternion.Euler(60f, -10f, 0f),
                            KickBack = 0.035f,
                            KickPitch = 10f,
                            KickSide = 0.004f,
                            KickRoll = 2.5f,
                            SwayScale = 0.8f,
                            FlashScale = 0.55f
                        };
                        break;

                    case WeaponStyle.Sar109:
                        p = Rifle(new Vector3(0.11f, -0.105f, 0.2f), 0.02f, 3.2f, 0.9f, 0.7f);
                        break;

                    case WeaponStyle.Mpt55:
                    case WeaponStyle.Sar223:
                        p = Rifle(new Vector3(0.12f, -0.1f, 0.19f), 0.025f, 3.5f, 1f, 0.85f);
                        break;

                    case WeaponStyle.Mpt76:
                    case WeaponStyle.G3a7:
                    case WeaponStyle.Mpt76K:
                        p = Rifle(new Vector3(0.12f, -0.1f, 0.19f), 0.03f, 4.5f, 1.05f, 1f);
                        break;

                    case WeaponStyle.Knt76:
                    case WeaponStyle.Sar762Mt:
                        p = Rifle(new Vector3(0.12f, -0.102f, 0.18f), 0.04f, 6f, 1.15f, 1f);
                        break;

                    case WeaponStyle.Jng90:
                        p = Rifle(new Vector3(0.12f, -0.105f, 0.17f), 0.06f, 9f, 1.3f, 1.1f);
                        break;

                    case WeaponStyle.Pmt76:
                    case WeaponStyle.Mg3:
                        p = Rifle(new Vector3(0.125f, -0.115f, 0.17f), 0.022f, 2.6f, 1.35f, 1.1f);
                        break;

                    case WeaponStyle.Escort:
                    case WeaponStyle.EscortMagnum:
                        p = Rifle(new Vector3(0.12f, -0.105f, 0.18f), 0.06f, 10f, 1.1f, 1.3f);
                        p.KickRoll = 3f;
                        break;
                }

                return p;
            }

            private static PoseProfile Rifle(Vector3 hip, float kickBack, float kickPitch, float sway, float flash)
            {
                return new PoseProfile
                {
                    HipPosition = hip,
                    HipRotation = Quaternion.Euler(0f, -1.5f, 0f),
                    SprintPosition = new Vector3(0.05f, -0.13f, 0.15f),
                    SprintRotation = Quaternion.Euler(12f, -36f, -16f),
                    LowerPosition = new Vector3(0.06f, -0.36f, 0.02f),
                    LowerRotation = Quaternion.Euler(48f, -22f, 10f),
                    KickBack = kickBack,
                    KickPitch = kickPitch,
                    KickSide = kickBack * 0.15f,
                    KickRoll = kickPitch * 0.3f,
                    SwayScale = sway,
                    FlashScale = flash
                };
            }
        }
    }
}
