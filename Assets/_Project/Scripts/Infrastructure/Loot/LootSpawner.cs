using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yağma üretimi (yalnızca otorite):
    ///  - SpawnWorldLoot: her yağma noktası SpawnChance ile dolar; RollSpawnGroup grubu noktanın etrafına kümelenir
    ///    (ilk eşya noktada, diğerleri 0.45–0.8 m çevresinde; duvarların içine girmez).
    ///  - DropAround: ölen savaşanın eşyaları iç içe halkalar halinde dizilir.
    ///  - SpawnDropped: tek eşyayı bir noktanın yakınına, diğer eşyalarla çakışmadan bırakır.
    /// "Mühimmat Sandığı" gibi statik sandık modelleri yapı modülüne aittir; burada yalnızca eşya üretilir.
    /// </summary>
    public static class LootSpawner
    {
        private const float ClusterMin = 0.45f;
        private const float ClusterMax = 0.8f;
        private const float WallMargin = 0.22f;
        private const float ProbeHeight = 0.3f;
        private const float RingSpacing = 0.62f;
        private const float FirstRingRadius = 0.7f;
        private const float RingStep = 0.6f;
        private const float GoldenAngle = 137.50776f;

        private static readonly List<LootItemData> GroupBuffer = new(8);
        private static readonly List<LootItemData> ValidBuffer = new(32);
        private static int _sequence;
        private static bool _loggedRollError;

        /// <summary>
        /// Dünya yağmasını üretir. service/random null ise GameContext'ten (yoksa varsayılan) çözülür.
        /// Üretilen eşya sayısını döndürür.
        /// </summary>
        public static int SpawnWorldLoot(IReadOnlyList<LootSpawnPointData> points, ILootSpawnService service, IRandom random)
        {
            if (points == null || points.Count == 0 || !GameContext.HasAuthority)
                return 0;

            service ??= ResolveService();
            random ??= ResolveRandom();

            // Aynı karede oluşturulan dünya çarpıştırıcıları zemin ışınları için senkronlansın.
            Physics.SyncTransforms();

            var spawned = 0;
            for (var p = 0; p < points.Count; p++)
            {
                var point = points[p];
                var chance = Mathf.Clamp01(SafeChance(service, point.Tier));
                if (random.NextFloat() >= chance)
                    continue;

                GroupBuffer.Clear();
                try
                {
                    service.RollSpawnGroup(point.Tier, random, GroupBuffer);
                }
                catch (Exception e)
                {
                    if (!_loggedRollError)
                    {
                        _loggedRollError = true;
                        Debug.LogException(e);
                    }

                    GroupBuffer.Clear();
                    continue;
                }

                spawned += SpawnCluster(point.Position, GroupBuffer, random);
            }

            GroupBuffer.Clear();
            return spawned;
        }

        /// <summary>Eşyaları merkezin etrafında iç içe halkalara dizer (ölüm düşürmesi). Geçersiz eşyalar atlanır.</summary>
        public static void DropAround(Vector3 center, IReadOnlyList<LootItemData> items)
        {
            if (items == null || items.Count == 0 || !GameContext.HasAuthority)
                return;

            ValidBuffer.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsValid)
                    ValidBuffer.Add(items[i]);
            }

            var remaining = ValidBuffer.Count;
            if (remaining == 0)
                return;

            var start = NextSequence() * GoldenAngle;
            var index = 0;
            var ring = 0;
            while (remaining > 0)
            {
                var radius = FirstRingRadius + ring * RingStep;
                var capacity = Mathf.Max(3, Mathf.FloorToInt(2f * Mathf.PI * radius / RingSpacing));
                var inRing = Mathf.Min(capacity, remaining);
                var step = 360f / inRing;
                var ringOffset = start + ring * (step * 0.5f);
                for (var k = 0; k < inRing; k++)
                {
                    var angle = ringOffset + k * step;
                    var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    var position = ClampToFreeSpace(center, direction, radius);
                    // Eşya merkeze dik dursun (silahlar halka boyunca uzanır).
                    LootPickupComponent.Spawn(ValidBuffer[index], position, angle + 90f);
                    index++;
                }

                remaining -= inRing;
                ring++;
            }

            ValidBuffer.Clear();
        }

        /// <summary>Tek eşyayı 'near' yakınına (diğer eşyalarla çakışmadan, duvar içine girmeden) bırakır.</summary>
        public static LootPickupComponent SpawnDropped(LootItemData item, Vector3 near)
        {
            if (!item.IsValid || !GameContext.HasAuthority)
                return null;

            var seq = NextSequence();
            var baseAngle = seq * GoldenAngle;
            var best = near;
            var bestYaw = baseAngle;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var angle = baseAngle + attempt * 45f;
                var distance = 0.55f + (attempt % 3) * 0.2f;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var candidate = ClampToFreeSpace(near, direction, distance);
                if (attempt == 0)
                {
                    best = candidate;
                    bestYaw = angle;
                }

                if (LootRegistry.FindNearest(candidate, 0.38f) == null)
                {
                    best = candidate;
                    bestYaw = angle;
                    break;
                }
            }

            return LootPickupComponent.Spawn(item, best, bestYaw + 90f);
        }

        // ------------------------------------------------------------------ Internals

        private static int SpawnCluster(Vector3 point, List<LootItemData> group, IRandom random)
        {
            var count = group.Count;
            if (count == 0)
                return 0;

            var spawned = 0;
            var others = Mathf.Max(1, count - 1);
            var baseAngle = random.Range(0f, 360f);
            for (var i = 0; i < count; i++)
            {
                var item = group[i];
                if (!item.IsValid)
                    continue;

                var position = point;
                var yaw = random.Range(0f, 360f);
                if (i > 0)
                {
                    var angle = baseAngle + (i - 1) * (360f / others) + random.Range(-25f, 25f);
                    var distance = random.Range(ClusterMin, ClusterMax);
                    var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    position = ClampToFreeSpace(point, direction, distance);
                }

                if (LootPickupComponent.Spawn(item, position, yaw) != null)
                    spawned++;
            }

            return spawned;
        }

        /// <summary>Merkezden yöne doğru en fazla 'distance' kadar ilerler; duvar varsa önünde durur.</summary>
        private static Vector3 ClampToFreeSpace(Vector3 center, Vector3 direction, float distance)
        {
            var origin = center + Vector3.up * ProbeHeight;
            if (Physics.Raycast(origin, direction, out var hit, distance + WallMargin, GameLayers.LineOfSightMask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0f, hit.distance - WallMargin);
            }

            return center + direction * distance;
        }

        private static float SafeChance(ILootSpawnService service, LootTier tier)
        {
            try
            {
                return service.SpawnChance(tier);
            }
            catch (Exception e)
            {
                if (!_loggedRollError)
                {
                    _loggedRollError = true;
                    Debug.LogException(e);
                }

                return 0.5f;
            }
        }

        private static ILootSpawnService ResolveService()
        {
            if (GameContext.TryGet<ILootSpawnService>(out var service))
                return service;
            if (GameContext.TryGet<LootSpawnService>(out var concrete))
                return concrete;
            return new LootSpawnService();
        }

        private static IRandom ResolveRandom()
        {
            if (GameContext.TryGet<IRandom>(out var random))
                return random;
            return new SeededRandom(Environment.TickCount);
        }

        private static int NextSequence()
        {
            unchecked
            {
                _sequence++;
            }

            return _sequence;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GroupBuffer.Clear();
            ValidBuffer.Clear();
            _sequence = 0;
            _loggedRollError = false;
        }
    }
}
