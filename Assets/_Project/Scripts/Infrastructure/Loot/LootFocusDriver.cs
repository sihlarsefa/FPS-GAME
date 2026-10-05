using System;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yerel oyuncunun baktığı eşyanın altındaki parlak odak halkasını otomatik yönetir (10 Hz). Oyuncu etkileşim kodu
    /// <see cref="LootRegistry.SetFocus"/> çağırıyorsa (son 0.35 s) devre dışı kalır, böylece halka her zaman F ile
    /// alınacak eşyayı gösterir. Bakış: Camera.main (CameraRig dünya kamerası "MainCamera" etiketlidir); hedef seçimi
    /// oyuncu etkileşimiyle aynıdır: önce bakılan eşya (3 m), yoksa ayak dibindeki en yakın eşya (1.4 m).
    /// Dedicated sunucuda (grafik aygıtı yok) oluşturulmaz. Sahne kapsamlıdır; ilk eşya üretiminde kendini kurar.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LootFocusDriver : MonoBehaviour
    {
        public const float LookDistance = 3f;
        public const float NearRadius = 1.4f;
        private const float Interval = 0.1f;

        private static readonly Func<LootPickupComponent, bool> AvailableFilter = IsAvailable;
        private static LootFocusDriver _instance;

        private float _nextScan;

        /// <summary>Sürücünün sahnede olmasını sağlar (oyun modu, grafik aygıtı varken). Tekrarlı çağrı ucuzdur.</summary>
        public static void Ensure()
        {
            if (_instance != null || !UnityEngine.Application.isPlaying || WorldItemVisuals.IsHeadless)
                return;

            var go = new GameObject("[Eşya Odak Sürücüsü]") { hideFlags = HideFlags.HideInHierarchy };
            _instance = go.AddComponent<LootFocusDriver>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                if (!LootRegistry.HasExternalFocusOwner)
                    LootRegistry.ApplyFocus(null);
            }
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now < _nextScan)
                return;

            _nextScan = now + Interval;
            if (LootRegistry.HasExternalFocusOwner)
                return;

            LootPickupComponent target = null;
            try
            {
                target = FindTarget();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (!ReferenceEquals(target, LootRegistry.Focused))
                LootRegistry.ApplyFocus(target);
        }

        private static LootPickupComponent FindTarget()
        {
            if (LootRegistry.Count == 0)
                return null;

            var player = CombatantRegistry.LocalPlayer;
            if (player == null || !player.IsAlive || player.DropState != DropState.Landed)
                return null;

            var camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled)
                return null;

            var view = camera.transform;
            var target = LootRegistry.FindLookTarget(view.position, view.forward, LookDistance);
            if (target == null)
                target = LootRegistry.FindNearest(player.transform.position, NearRadius, AvailableFilter);

            return target;
        }

        private static bool IsAvailable(LootPickupComponent pickup) => pickup != null && pickup.IsAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }
    }
}
