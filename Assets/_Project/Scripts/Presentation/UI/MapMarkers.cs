using System;
using System.Collections.Generic;
using Project.Application.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Haritalar arasında paylaşılan işaret durumu (yerel, sahne kapsamlı):
    /// <list type="bullet">
    /// <item>Oyuncunun tam haritada koyduğu işaret noktası (waypoint) — mini harita da gösterir; pres-player tim
    /// taarruz emri / topçu hedefi için okuyabilir (<see cref="FullMapView.PointMarked"/> olayı da yayınlanır).</item>
    /// <item>İsteğe bağlı intikal planları (<see cref="TeamInsertion"/>): bootstrap <see cref="SetInsertionPlans"/> ile
    /// verirse rota/LZ bu plandan çizilir; vermezse haritalar sahnedeki tim aracından (TransportVehicle) çıkarır.</item>
    /// </list>
    /// Veriler etkin sahneye damgalanır; sahne değişince otomatik geçersiz olur (eski maçın işareti taşınmaz).
    /// </summary>
    public static class MapMarkers
    {
        private static readonly List<TeamInsertion> Plans = new List<TeamInsertion>(8);
        private static Vector3 _waypoint;
        private static bool _hasWaypoint;
        private static int _waypointScene = -1;
        private static int _plansScene = -1;

        /// <summary>İşaret ya da plan her değiştiğinde artar (önbellek geçersizleştirme).</summary>
        public static int Version { get; private set; }

        /// <summary>İşaret konduğunda (dünya konumu, Y = zemin yüksekliği).</summary>
        public static event Action<Vector3> WaypointSet;

        /// <summary>İşaret kaldırıldığında.</summary>
        public static event Action WaypointCleared;

        /// <summary>Geçerli sahnede işaret noktası var mı?</summary>
        public static bool HasWaypoint => _hasWaypoint && _waypointScene == ActiveSceneHandle;

        /// <summary>İşaret noktası (yalnızca <see cref="HasWaypoint"/> true iken anlamlı).</summary>
        public static Vector3 Waypoint => _waypoint;

        /// <summary>Geçerli sahnenin intikal planları (verilmediyse boş).</summary>
        public static IReadOnlyList<TeamInsertion> InsertionPlans
        {
            get
            {
                if (_plansScene != ActiveSceneHandle && Plans.Count > 0)
                    Plans.Clear();
                return Plans;
            }
        }

        /// <summary>İşaret noktası koyar (öncekinin yerine).</summary>
        public static void SetWaypoint(Vector3 world)
        {
            if (!MapMath.IsFinite(world))
                return;

            _waypoint = world;
            _hasWaypoint = true;
            _waypointScene = ActiveSceneHandle;
            Version++;
            WaypointSet?.Invoke(world);
        }

        /// <summary>İşaret noktasını kaldırır.</summary>
        public static void ClearWaypoint()
        {
            if (!_hasWaypoint)
                return;

            _hasWaypoint = false;
            Version++;
            WaypointCleared?.Invoke();
        }

        /// <summary>
        /// İntikal planlarını kaydeder (bootstrap, InsertionPlanner.Plan sonrası). Kopyalanır; null temizler.
        /// </summary>
        public static void SetInsertionPlans(IReadOnlyList<TeamInsertion> plans)
        {
            Plans.Clear();
            if (plans != null)
            {
                for (var i = 0; i < plans.Count; i++)
                    Plans.Add(plans[i]);
            }

            _plansScene = ActiveSceneHandle;
            Version++;
        }

        /// <summary>Timin intikal planı (kayıtlıysa).</summary>
        public static bool TryGetInsertion(int team, out TeamInsertion plan)
        {
            var plans = InsertionPlans;
            for (var i = 0; i < plans.Count; i++)
            {
                if (plans[i].Team == team)
                {
                    plan = plans[i];
                    return true;
                }
            }

            plan = default;
            return false;
        }

        /// <summary>Tüm işaretleri ve planları temizler.</summary>
        public static void Reset()
        {
            Plans.Clear();
            _hasWaypoint = false;
            _waypointScene = -1;
            _plansScene = -1;
            Version++;
        }

        private static int ActiveSceneHandle
        {
            get
            {
                try
                {
                    // Scene.GetHashCode sahne tutamacından türetilir (Unity 6.6'da SceneHandle → int dönüşümü kaldırıldı).
                    return SceneManager.GetActiveScene().GetHashCode();
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Plans.Clear();
            _hasWaypoint = false;
            _waypointScene = -1;
            _plansScene = -1;
            Version = 0;
            WaypointSet = null;
            WaypointCleared = null;
        }
    }
}
