using System;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Ragdoll ölüm + isabet tepkisi. Yaralı (downed) durum ragdoll olmaz; yalnız gerçek ölüm (Died) olur.
    /// Humanoid override varsa GetHumanoidBone/EnterRagdollMode kullanılır; yoksa prosedürel kemikler. Başarısızlıkta
    /// eski animasyonlu ölüm (PlayDeath) çalışmaya devam eder.
    /// </summary>
    public sealed partial class SoldierModel
    {
        private RagdollRig _rig;
        private bool _ragdollPosed;
        private readonly HitReactionSpring _hit = new HitReactionSpring();

        /// <summary>Ragdoll ölümü açık mı (kapalıysa yalnız prosedürel düşüş).</summary>
        public bool RagdollEnabled { get; set; } = true;

        /// <summary>Ragdoll pozunda (fizik aktif ya da donmuş) mu.</summary>
        public bool IsRagdoll => _ragdollPosed;

        private static int CurrentTier => QualityTierApplier.LastTier < 0 ? 2 : QualityTierApplier.LastTier;

        /// <summary>
        /// Ölüm: önce PlayDeath durumu (vuruş kutuları kapanır), sonra ragdoll'a geçiş. force: ham itki (N·s,
        /// <see cref="RagdollMath.ImpulseNs"/>); hitDir: merminin gidiş yönü (atıcıdan kurbana).
        /// </summary>
        public void Die(Vector3 hitDir, float force)
        {
            Die(hitDir, force, BodyPart.Torso, false);
        }

        public void Die(Vector3 hitDir, float force, BodyPart region, bool explosive)
        {
            if (!_built || _dead)
                return;

            PlayDeath(hitDir);
            if (!RagdollEnabled || (_owner != null && _owner.IsDowned))
                return;

            try
            {
                StartRagdoll(hitDir, force, region, explosive);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierModel] Ragdoll kurulamadı, animasyonlu ölüm kullanılıyor: " + e.Message);
                ExitRagdoll();
            }
        }

        /// <summary>Katkı niteliğinde isabet tepkisi: bölgeye göre irkilme, ağır vuruşta sendeleme. dir: mermi gidiş yönü (dünya).</summary>
        public void PlayHit(BodyPart region, Vector3 dir, float strength)
        {
            PlayHit(region, dir, strength, strength >= 0.7f);
        }

        public void PlayHit(BodyPart region, Vector3 dir, float strength, bool heavy)
        {
            if (!_built || _dead || _ragdollPosed)
                return;
            var local = transform.InverseTransformDirection(dir);
            HitReactionMath.Apply(_hit, region, local, strength, CurrentTier, heavy);
        }

        private void StartRagdoll(Vector3 hitDir, float force, BodyPart region, bool explosive)
        {
            var humanoid = _humanoidAnimator != null;
            var bones = new RagdollRig.Bones { Frame = transform };
            if (humanoid)
            {
                if (!_humanoidAnimator.isHuman)
                    return;
                var m = bones.Bone;
                m[(int)RagdollPart.Hips] = GetHumanoidBone(HumanBodyBones.Hips);
                m[(int)RagdollPart.Spine] = GetHumanoidBone(HumanBodyBones.Spine);
                m[(int)RagdollPart.Chest] = GetHumanoidBone(HumanBodyBones.Chest) ?? GetHumanoidBone(HumanBodyBones.UpperChest);
                m[(int)RagdollPart.Head] = GetHumanoidBone(HumanBodyBones.Head);
                m[(int)RagdollPart.UpperArmL] = GetHumanoidBone(HumanBodyBones.LeftUpperArm);
                m[(int)RagdollPart.UpperArmR] = GetHumanoidBone(HumanBodyBones.RightUpperArm);
                m[(int)RagdollPart.ForearmL] = GetHumanoidBone(HumanBodyBones.LeftLowerArm);
                m[(int)RagdollPart.ForearmR] = GetHumanoidBone(HumanBodyBones.RightLowerArm);
                m[(int)RagdollPart.ThighL] = GetHumanoidBone(HumanBodyBones.LeftUpperLeg);
                m[(int)RagdollPart.ThighR] = GetHumanoidBone(HumanBodyBones.RightUpperLeg);
                m[(int)RagdollPart.ShinL] = GetHumanoidBone(HumanBodyBones.LeftLowerLeg);
                m[(int)RagdollPart.ShinR] = GetHumanoidBone(HumanBodyBones.RightLowerLeg);
                var neck = GetHumanoidBone(HumanBodyBones.Neck);
                var e = bones.End;
                e[(int)RagdollPart.Hips] = m[(int)RagdollPart.Spine] ?? m[(int)RagdollPart.Chest];
                e[(int)RagdollPart.Spine] = m[(int)RagdollPart.Chest] ?? neck ?? m[(int)RagdollPart.Head];
                e[(int)RagdollPart.Chest] = neck ?? m[(int)RagdollPart.Head];
                e[(int)RagdollPart.UpperArmL] = m[(int)RagdollPart.ForearmL];
                e[(int)RagdollPart.UpperArmR] = m[(int)RagdollPart.ForearmR];
                e[(int)RagdollPart.ForearmL] = GetHumanoidBone(HumanBodyBones.LeftHand);
                e[(int)RagdollPart.ForearmR] = GetHumanoidBone(HumanBodyBones.RightHand);
                e[(int)RagdollPart.ThighL] = m[(int)RagdollPart.ShinL];
                e[(int)RagdollPart.ThighR] = m[(int)RagdollPart.ShinR];
                e[(int)RagdollPart.ShinL] = GetHumanoidBone(HumanBodyBones.LeftFoot);
                e[(int)RagdollPart.ShinR] = GetHumanoidBone(HumanBodyBones.RightFoot);
                // Sınır koşulları: uç yoksa o parça kapsül uzunluğunu varsayılandan alır.
            }
            else
            {
                var m = bones.Bone;
                m[(int)RagdollPart.Hips] = _body;
                m[(int)RagdollPart.Spine] = _spine;
                m[(int)RagdollPart.Chest] = Chest;
                m[(int)RagdollPart.Head] = Head;
                m[(int)RagdollPart.UpperArmL] = _leftShoulder;
                m[(int)RagdollPart.UpperArmR] = _rightShoulder;
                m[(int)RagdollPart.ForearmL] = _leftElbow;
                m[(int)RagdollPart.ForearmR] = _rightElbow;
                m[(int)RagdollPart.ThighL] = _leftHip;
                m[(int)RagdollPart.ThighR] = _rightHip;
                m[(int)RagdollPart.ShinL] = _leftKnee;
                m[(int)RagdollPart.ShinR] = _rightKnee;
                var e = bones.End;
                e[(int)RagdollPart.Hips] = _spine;
                e[(int)RagdollPart.Spine] = Chest;
                e[(int)RagdollPart.Chest] = _neck;
                e[(int)RagdollPart.UpperArmL] = _leftElbow;
                e[(int)RagdollPart.UpperArmR] = _rightElbow;
                e[(int)RagdollPart.ForearmL] = _leftHand;
                e[(int)RagdollPart.ForearmR] = _rightHand;
                e[(int)RagdollPart.ThighL] = _leftKnee;
                e[(int)RagdollPart.ThighR] = _rightKnee;
                e[(int)RagdollPart.ShinL] = _leftAnkle;
                e[(int)RagdollPart.ShinR] = _rightAnkle;
            }

            if (bones.Bone[(int)RagdollPart.Hips] == null || bones.Bone[(int)RagdollPart.Head] == null)
                return;

            // Humanoid: animatör durdurulmadan önce kemik pozları korunur (EnterRagdollMode yalnız enabled=false yapar).
            if (humanoid && !EnterRagdollMode())
                return;

            if (_rig == null)
                _rig = gameObject.GetComponent<RagdollRig>() ?? gameObject.AddComponent<RagdollRig>();
            if (!_rig.Build(bones, CurrentTier))
            {
                if (humanoid)
                    ExitRagdoll();
                return;
            }

            _ragdollPosed = true;
            _hit.Clear();

            var leftSide = UnityEngine.Random.value < 0.5f;
            var part = RagdollMath.PartForRegion((int)region, leftSide);
            var partMass = RagdollMath.MassOf(part);
            var headshot = region == BodyPart.Head && !explosive;
            var kick = RagdollMath.ComputeKick(hitDir, force, explosive, partMass, headshot);
            var inherit = _owner != null ? _owner.Velocity : Vector3.zero;
            inherit.y = Mathf.Min(inherit.y, 0f);

            if (_current != null && _current.Root != null && !humanoid)
                _rig.DropWeapon(_current.Root.transform, transform);
            _rig.Launch(kick, part, inherit * 0.6f);
            if (headshot)
                KnockOffHelmetOnHeadshot(bones.Bone[(int)RagdollPart.Head], hitDir, inherit * 0.6f);
        }

        /// <summary>Kafadan vuruşta etkin kask (seviye 1-3) parçalarını fırlatır; kep/bere ya da kask yoksa atlanır.</summary>
        private void KnockOffHelmetOnHeadshot(Transform head, Vector3 hitDir, Vector3 inherit)
        {
            if (_rig == null || head == null || _helmetLevel < 1 || _helmetLevel >= _helmets.Length)
                return;
            var v = _helmets[_helmetLevel];
            if (v == null || (_showBeretOverHelmet && _beret != null && _beret.Objects.Count > 0 && _beret.Objects[0] != null &&
                              _beret.Objects[0].activeInHierarchy))
                return;
            var parts = new System.Collections.Generic.List<Transform>(v.Objects.Count);
            for (var i = 0; i < v.Objects.Count; i++)
                if (v.Objects[i] != null && v.Objects[i].activeInHierarchy)
                    parts.Add(v.Objects[i].transform);
            _rig.KnockOffHelmet(parts, head, hitDir, inherit);
        }

        private void ExitRagdoll()
        {
            if (_rig != null)
                _rig.Restore();
            if (_ragdoll && _humanoidAnimator != null)
                _humanoidAnimator.enabled = true;
            _ragdoll = false;
            _ragdollPosed = false;
        }

        // ------------------------------------------------------------------ İsabet tepkisi uygulaması

        private void ApplyHitReaction(float dt)
        {
            if (!_hit.Active)
                return;
            _hit.Step(dt);
            if (!_hit.Active)
                return;

            var humanoid = _humanoidAnimator != null && _humanoidAnimator.isHuman;
            var hips = humanoid ? GetHumanoidBone(HumanBodyBones.Hips) : _body;
            var spine = humanoid ? GetHumanoidBone(HumanBodyBones.Spine) : _spine;
            var head = humanoid ? GetHumanoidBone(HumanBodyBones.Head) : Head;

            var spineQ = Quaternion.Euler(_hit.Get(HitChannel.SpinePitch), _hit.Get(HitChannel.SpineTwist), _hit.Get(HitChannel.SpineRoll));
            var headQ = Quaternion.Euler(_hit.Get(HitChannel.HeadPitch), _hit.Get(HitChannel.HeadYaw), _hit.Get(HitChannel.HeadRoll));
            var buckle = Mathf.Clamp01(_hit.Get(HitChannel.LegBuckle));
            var sx = _hit.Get(HitChannel.StaggerX);
            var sz = _hit.Get(HitChannel.StaggerZ);

            if (buckle > 0.001f)
            {
                var thighL = humanoid ? GetHumanoidBone(HumanBodyBones.LeftUpperLeg) : _leftHip;
                var thighR = humanoid ? GetHumanoidBone(HumanBodyBones.RightUpperLeg) : _rightHip;
                var kneeL = humanoid ? GetHumanoidBone(HumanBodyBones.LeftLowerLeg) : _leftKnee;
                var kneeR = humanoid ? GetHumanoidBone(HumanBodyBones.RightLowerLeg) : _rightKnee;
                var thighQ = Quaternion.Euler(-50f * buckle, 0f, 0f);
                var kneeQ = Quaternion.Euler(90f * buckle, 0f, 0f);
                AddRigRotation(thighL, thighQ);
                AddRigRotation(thighR, thighQ);
                AddRigRotation(kneeL, kneeQ);
                AddRigRotation(kneeR, kneeQ);
                if (hips != null)
                    hips.position -= transform.up * (0.13f * buckle);
            }

            // Sendeleme: kalça isabet yönünde kayar, gövde geride kalır.
            if (hips != null && (Mathf.Abs(sx) > 0.001f || Mathf.Abs(sz) > 0.001f))
            {
                hips.position += transform.TransformDirection(new Vector3(sx, 0f, sz));
                spineQ = Quaternion.Euler(-sz * 35f, 0f, sx * 35f) * spineQ;
            }

            AddRigRotation(spine, spineQ);
            AddRigRotation(head, headQ);
        }

        /// <summary>Model uzayında verilen dönüşü kemiğin dünya dönüşüne katar (kemik eksenlerinden bağımsız).</summary>
        private void AddRigRotation(Transform t, Quaternion rigSpace)
        {
            if (t == null)
                return;
            var r = transform.rotation;
            t.rotation = r * rigSpace * Quaternion.Inverse(r) * t.rotation;
        }
    }
}
