using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// PostProcessing.EnsureGlobalVolume tarafından eklenen işaretçi: çalışma zamanında üretilen VolumeProfile'ın sahibidir
    /// ve nesne yok edilince profili de yok eder (sızıntı olmaz). Sahneye kaydedilmiş bir hacimde profil kaybolmuşsa
    /// PostProcessing yeniden üretir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class RuntimeGlobalVolume : MonoBehaviour
    {
        [SerializeField] private PostProcessing.Look look = PostProcessing.Look.Gameplay;

        private VolumeProfile _ownedProfile;

        public PostProcessing.Look Look
        {
            get => look;
            internal set => look = value;
        }

        public Volume Volume => GetComponent<Volume>();

        /// <summary>Bu bileşenin ürettiği (ve yok edeceği) profil.</summary>
        public VolumeProfile OwnedProfile => _ownedProfile;

        internal void SetOwnedProfile(VolumeProfile profile)
        {
            if (_ownedProfile == profile)
                return;

            DestroyOwnedProfile();
            _ownedProfile = profile;
        }

        private void Awake()
        {
            // Sahneden yüklendiyse ve profil eksikse (çalışma zamanı profili sahneye kaydedilemez) yeniden kur.
            var volume = GetComponent<Volume>();
            if (volume != null && volume.sharedProfile == null)
                PostProcessing.Rebuild(this);
        }

        private void OnDestroy()
        {
            if (PostProcessing.Current == this)
                PostProcessing.ClearCurrent();
            DestroyOwnedProfile();
        }

        private void DestroyOwnedProfile()
        {
            if (_ownedProfile == null)
                return;

            var profile = _ownedProfile;
            _ownedProfile = null;

            // Bileşenler profilin alt nesneleri değil, ayrı ScriptableObject'ler.
            if (profile.components != null)
            {
                for (var i = 0; i < profile.components.Count; i++)
                {
                    var component = profile.components[i];
                    if (component != null)
                        DestroySafe(component);
                }
            }

            DestroySafe(profile);
        }

        private static void DestroySafe(Object obj)
        {
            if (obj == null)
                return;

            if (UnityEngine.Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
    }
}
