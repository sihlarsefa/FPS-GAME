using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Lobi kamerasının alan derinliği (odak mesafesi) ve pozlama kaymasını süren yerel hacim (öncelik 110; menü keskin hacmi 100).
    /// Kalite kademesi 0-1'de DoF kapalıdır (yalnız pozlama). <see cref="Dispose"/> hacmi ve profili temizler.
    /// </summary>
    public sealed class LobbyCameraGrade
    {
        private VolumeProfile _profile;
        private GameObject _object;
        private Volume _volume;
        private DepthOfField _dof;
        private ColorAdjustments _color;
        private readonly bool _dofAllowed;

        private LobbyCameraGrade(bool dofAllowed) => _dofAllowed = dofAllowed;

                /// <summary>Bulanıklık başlangıcı: odak düzleminin hemen arkası (+0,6 m kahraman derinliği).</summary>
        public static float GaussianStartFor(float focusDistance) => Mathf.Max(0.1f, focusDistance) + 0.6f;

        /// <summary>Tam bulanıklığa uzaklık: güçlü blur'da 2 m, zayıfta 14 m sonra.</summary>
        public static float GaussianEndFor(float focusDistance, float blur01) =>
            GaussianStartFor(focusDistance) + Mathf.Lerp(14f, 2f, LobbyEasing.Clamp01(blur01));

        public static float GaussianRadiusFor(float blur01) => Mathf.Lerp(0.5f, 1.5f, LobbyEasing.Clamp01(blur01));

        public static bool DofAllowedForTier(int tier) => tier >= 2;

        public static LobbyCameraGrade Create(Transform parent, int qualityTier)
        {
            var grade = new LobbyCameraGrade(DofAllowedForTier(qualityTier));
            try
            {
                grade._profile = ScriptableObject.CreateInstance<VolumeProfile>();
                grade._profile.name = "HK_LobiKameraProfili";
                grade._dof = grade._profile.Add<DepthOfField>(true);
                grade._dof.mode.Override(DepthOfFieldMode.Off);
                grade._color = grade._profile.Add<ColorAdjustments>(true);
                grade._color.postExposure.Override(0f);

                grade._object = new GameObject("LobiKameraHacmi");
                if (parent != null) grade._object.transform.SetParent(parent, false);
                grade._volume = grade._object.AddComponent<Volume>();
                grade._volume.isGlobal = true;
                grade._volume.priority = 110f;
                grade._volume.weight = 1f;
                grade._volume.sharedProfile = grade._profile;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LobbyCameraGrade] Hacim kurulamadı: " + e.Message);
                grade.Dispose();
                return null;
            }

            return grade;
        }

        public void Apply(in LobbyShot shot)
        {
            if (_profile == null) return;

            _color.postExposure.value = shot.ExposureEv;

            if (!_dofAllowed || shot.Blur < 0.02f)
            {
                _dof.mode.value = DepthOfFieldMode.Off;
                return;
            }

            // Gaussian: odak mesafesinden sonrası bulanır. Bu derleme hedefinde Bokeh alanları açık olmadığından yalnız uzak bulanıklık sürülür.
            _dof.mode.value = DepthOfFieldMode.Gaussian;
            _dof.gaussianStart.value = GaussianStartFor(shot.FocusDistance);
            _dof.gaussianEnd.value = GaussianEndFor(shot.FocusDistance, shot.Blur);
            _dof.gaussianMaxRadius.value = GaussianRadiusFor(shot.Blur);
        }

        public void Dispose()
        {
            if (_object != null) Object.Destroy(_object);
            if (_profile != null) Object.Destroy(_profile);
            _object = null;
            _profile = null;
        }
    }
}
