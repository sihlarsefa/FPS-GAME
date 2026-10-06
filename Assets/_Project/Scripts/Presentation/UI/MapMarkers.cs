using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
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

        // ------------------------------------------------------------------ Düşen tim arkadaşı işaretleri

        /// <summary>Düşen (ölen) tim arkadaşının harita kaydı.</summary>
        public readonly struct FallenMarker
        {
            public readonly Vector3 Position;
            public readonly string Name;
            public readonly MilitaryRank Rank;
            public readonly float Time;

            public FallenMarker(Vector3 position, string name, MilitaryRank rank, float time)
            {
                Position = position;
                Name = name ?? string.Empty;
                Rank = rank;
                Time = time;
            }
        }

        /// <summary>Saklanan en fazla düşen işareti sayısı.</summary>
        public const int MaxFallen = 12;

        private static readonly List<FallenMarker> FallenList = new List<FallenMarker>(MaxFallen);
        private static int _fallenScene = -1;
        private static bool _tracking;

        /// <summary>Geçerli sahnede düşen tim arkadaşları (en eskiden yeniye).</summary>
        public static IReadOnlyList<FallenMarker> Fallen
        {
            get
            {
                if (_fallenScene != ActiveSceneHandle && FallenList.Count > 0)
                    FallenList.Clear();
                return FallenList;
            }
        }

        /// <summary>Düşen tim arkadaşı kaydeder (en eski, sınır aşılırsa atılır).</summary>
        public static void RecordFallen(Vector3 position, string name, MilitaryRank rank, float time)
        {
            if (!MapMath.IsFinite(position))
                return;

            if (_fallenScene != ActiveSceneHandle)
            {
                FallenList.Clear();
                _fallenScene = ActiveSceneHandle;
            }

            if (FallenList.Count >= MaxFallen)
                FallenList.RemoveAt(0);
            FallenList.Add(new FallenMarker(position, name, rank, time));
            Version++;
        }

        /// <summary>
        /// Ölüm olaylarını dinlemeye başlar (idempotent): yerel oyuncunun timindeki ölümler (kendisi hariç) kaydedilir.
        /// Haritalar kurulurken çağrılır.
        /// </summary>
        public static void StartTrackingFallen()
        {
            if (_tracking)
                return;

            _tracking = true;
            CombatantRegistry.Registered -= HookCombatant;
            CombatantRegistry.Registered += HookCombatant;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
                HookCombatant(all[i]);
        }

        private static void HookCombatant(Combatant combatant)
        {
            if (combatant == null)
                return;
            combatant.Died -= OnCombatantDied;
            combatant.Died += OnCombatantDied;
        }

        private static void OnCombatantDied(Combatant combatant, DamageInfo info)
        {
            if (combatant == null)
                return;

            var local = CombatantRegistry.LocalPlayer;
            if (local == null || combatant == local || combatant.Team != local.Team)
                return;

            RecordFallen(combatant.transform.position, combatant.DisplayName, combatant.Rank, Time.time);
        }

        /// <summary>Tüm işaretleri ve planları temizler.</summary>
        public static void Reset()
        {
            Plans.Clear();
            FallenList.Clear();
            _hasWaypoint = false;
            _waypointScene = -1;
            _plansScene = -1;
            _fallenScene = -1;
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
            FallenList.Clear();
            _fallenScene = -1;
            _tracking = false;
            _hasWaypoint = false;
            _waypointScene = -1;
            _plansScene = -1;
            Version = 0;
            WaypointSet = null;
            WaypointCleared = null;
        }
    }
}
