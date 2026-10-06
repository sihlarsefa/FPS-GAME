using Project.Application.Services;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Anahtarlı (ViewmodelPoseTimeline) şarjörlü silah doldurma: boş/taktik ayrımı, aşırı vuruş eğrileri, foley adımlarıyla hizalı.</summary>
    public sealed partial class WeaponViewModel
    {
        private bool _reloadEmpty;
        private ViewmodelPoseTimeline _timeline;
        private GameObject _spareMag;
        private WeaponModel _spareFor;
        private bool _magDropped;

        /// <summary>Boş (kurma kolu/sürgü ile) veya taktik doldurma. PlayReload(float) geriye uyumlu: taktik.</summary>
        public void PlayReload(bool emptyReload, float durationSeconds)
        {
            _reloadEmpty = emptyReload;
            PlayReload(durationSeconds);
        }

        private void BeginTimelineForReload()
        {
            ReloadKind kind;
            if (WeaponStyles.IsPistol(_shownStyle)) kind = ReloadKind.Pistol;
            else if (WeaponStyles.IsPumpAction(_shownStyle)) kind = ReloadKind.Shotgun;
            else if (WeaponStyles.IsBeltFed(_shownStyle)) kind = ReloadKind.Machinegun;
            else if (WeaponStyles.IsBoltAction(_shownStyle)) kind = ReloadKind.BoltAction;
            else kind = ReloadKind.Rifle;
            _timeline = ViewmodelPoseTimeline.Build(kind, _reloadEmpty);
            _magDropped = false;
            _reloadEmpty = false;
        }

        /// <summary>Şarjörlü silahlar (tabanca/tüfek/sürgülü): zaman çizelgesiyle sürülen doldurma.</summary>
        private void AnimateTimelineMagazineReload(float t)
        {
            var tl = _timeline;
            var m = _model;
            var pistol = tl.Kind == ReloadKind.Pistol;
            var bolt = tl.Kind == ReloadKind.BoltAction;

            var tilt = tl.Tilt.Evaluate(t);
            _actionRot = pistol
                ? Quaternion.Euler(-12f * tilt, 8f * tilt, 18f * tilt)
                : Quaternion.Euler(-7f * tilt, 10f * tilt, 26f * tilt);
            _actionPos = (pistol ? new Vector3(-0.04f, 0.035f, -0.03f) : new Vector3(-0.03f, 0.025f, -0.015f)) * tilt;

            // Şarjör: yuvadan kopar, düşer (eğilerek), yenisi cepten girer.
            const float travel = 0.32f;
            var mag = tl.MagTravel.Evaluate(t);
            var d = Mathf.Max(0f, mag) * travel;
            m.SetMagazineOffset(m.MagazineEjectDirection * (mag < 0f ? mag * 0.02f : d),
                Quaternion.Euler(-Mathf.Clamp01(mag) * 18f, 0f, 0f));
            UpdateSpareMagazine(tl, t, mag);

            var reach = Mathf.Clamp01(tl.HandReach.Evaluate(t));
            if (m.MagazineHandGrip != null)
                _left.SetAnchorGoal(m.MagazineHandGrip, reach);

            // Şarjör oturma / şamar sarsıntısı + kurma darbesi.
            var jolt = tl.Jolt.Evaluate(t);
            var mech = tl.Mechanism.Evaluate(t);
            _actionPos += new Vector3(0f, 0.004f, -0.012f) * jolt + new Vector3(0f, 0f, -0.006f) * mech;
            _actionRot = _actionRot * Quaternion.Euler(-1.5f * jolt, 0f, 0f);

            if (pistol)
            {
                m.SetSlide(Mathf.Clamp01(mech));
            }
            else if (bolt)
            {
                if (m.BoltHandGrip != null)
                    _right.SetAnchorGoal(m.BoltHandGrip, Window(t, 0.68f, 0.73f, 0.9f, 0.96f));
                var lift = SmoothRamp(mech, 0f, 0.25f) * (1f - SmoothRamp(t, 0.84f, 0.88f));
                var back = Mathf.Clamp01((mech - 0.2f) / 0.8f);
                m.SetBolt(lift, back);
                _actionRot = _actionRot * Quaternion.Euler(0f, 0f, -6f * Mathf.Clamp01(mech));
            }
            else if (mech > 0.001f)
            {
                // Kurma kolu: sağ yerine sol el elle kurulur hissi; model kızak hareketi yoksa sürgü yedeği.
                m.SetBolt(0f, Mathf.Clamp01(mech));
                _actionRot = _actionRot * Quaternion.Euler(0f, 0f, -2f * Mathf.Clamp01(mech));
            }
        }

        // ------------------------------------------------------------------ ateş modu / boş tetik

        /// <summary>Seçici (ateş modu) değişimi: başparmak hareketi — küçük yuvarlanma ve geri tık.</summary>
        public void PlayFireModeSwitch()
        {
            EnsureBuilt();
            if (_model == null || _action == ViewAction.Reload)
                return;

            AddKick(new Vector3(0.002f, 0.001f, -0.004f), new Vector3(-1.2f, 0f, 5f));
        }

        /// <summary>Boş tetik (şarjör boş): kısa metal tık sarsıntısı, geri tepme yok.</summary>
        public void PlayDryFire()
        {
            EnsureBuilt();
            if (_model == null || _action == ViewAction.Reload)
                return;

            AddKick(new Vector3(0f, 0.0008f, -0.003f), new Vector3(-0.9f, 0.4f, 0f));
        }

        // ------------------------------------------------------------------ yedek şarjör (NewMagInHand)

        private void EnsureSpareMagazine(WeaponModel m)
        {
            if (_spareMag != null && _spareFor == m)
                return;

            _spareMag = null;
            _spareFor = m;
            var src = m != null ? m.Magazine : null;
            if (src == null)
                return;

            var mf = src.GetComponent<MeshFilter>();
            var mr = src.GetComponent<Renderer>();
            if (mf == null || mf.sharedMesh == null || mr == null)
                return;

            var go = new GameObject("SpareMagazine");
            go.transform.SetParent(src.parent, false);
            go.transform.localScale = src.localScale;
            go.layer = src.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            _spareMag = go;
        }

        private void HideSpareMagazine()
        {
            _magDropped = false;
            if (_spareMag != null)
                _spareMag.SetActive(false);
        }

        /// <summary>Eski şarjör düşünce gizlenir; yedek sol elde belirir, yuvaya taşınır ve oturunca gerçek şarjör geri gelir.</summary>
        private void UpdateSpareMagazine(ViewmodelPoseTimeline tl, float t, float magTravel)
        {
            var m = _model;
            if (m == null || m.Magazine == null)
                return;

            if (magTravel > 0.9f)
                _magDropped = true;

            var seated = t >= tl.SeatTime;
            m.SetMagazineVisible(seated || !_magDropped);

            EnsureSpareMagazine(m);
            if (_spareMag == null)
                return;

            var show = !seated && tl.NewMagInHand.Evaluate(t) > 0.5f && _left != null && _left.PropSocket != null;
            if (_spareMag.activeSelf != show)
                _spareMag.SetActive(show);
            if (!show)
                return;

            var parent = m.Magazine.parent;
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (tl.SeatTime - 0.10f)) / 0.10f));
            var wellPos = parent.TransformPoint(m.MagazineHomePosition);
            var wellRot = parent.rotation * m.MagazineHomeRotation;
            _spareMag.transform.SetPositionAndRotation(
                Vector3.Lerp(_left.PropSocket.position, wellPos, k),
                wellRot * Quaternion.Euler(-25f * (1f - k), 0f, 0f));
        }
    }
}
