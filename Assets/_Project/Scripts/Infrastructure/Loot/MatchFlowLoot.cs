using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Maç akışı yağması (yalnızca otorite): garantili başlangıç kiti (intikal inişi yakınında) ve ikmal sandığı açılışı.
    /// Görsel sandık/paraşüt/duman/harita işareti bu sınıfın işi değildir (PropFactory/HUD tarafı AirdropEvent'e abone olur).
    /// Docs/MAC_AKISI.md
    /// </summary>
    public static class MatchFlowLoot
    {
        private const float KitRingMin = 3f;
        private const float KitRingMax = 7f;
        private const float GroundProbeHeight = 300f;

        private static readonly List<LootItemData> Buffer = new(16);

        /// <summary>
        /// Bir timin iniş noktası çevresine üye başına asgari kiti bırakır. Her <see cref="LootSpawnService.StartPrimaryEvery"/>
        /// üyeden birine ana silah da çıkar. Bırakılan eşya sayısını döndürür.
        /// </summary>
        public static int SpawnTeamStartKits(Vector3 landing, int memberCount, ILootSpawnService service, IRandom random)
        {
            if (memberCount <= 0 || !GameContext.HasAuthority)
                return 0;

            var concrete = service as LootSpawnService;
            if (concrete == null && !GameContext.TryGet(out concrete))
                concrete = new LootSpawnService();

            if (random == null && !GameContext.TryGet(out random))
                random = new SeededRandom(System.Environment.TickCount);

            var total = 0;
            var baseAngle = random.Range(0f, 360f);
            for (var i = 0; i < memberCount; i++)
            {
                Buffer.Clear();
                concrete.RollStartKit(random, Buffer, i % LootSpawnService.StartPrimaryEvery == 0);
                var angle = baseAngle + i * (360f / memberCount);
                var distance = random.Range(KitRingMin, KitRingMax);
                var spot = landing + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                spot = SnapToGround(spot);
                total += Buffer.Count;
                LootSpawner.DropAround(spot, Buffer);
            }

            Buffer.Clear();
            return total;
        }

        /// <summary>Açılan ikmal sandığının yağmasını konumun çevresine düşürür. Üretilen eşya sayısını döndürür.</summary>
        public static int OpenSupplyCrate(Float3 position, ILootSpawnService service, IRandom random)
        {
            if (!GameContext.HasAuthority)
                return 0;

            var concrete = service as LootSpawnService;
            if (concrete == null && !GameContext.TryGet(out concrete))
                concrete = new LootSpawnService();

            if (random == null && !GameContext.TryGet(out random))
                random = new SeededRandom(System.Environment.TickCount);

            Buffer.Clear();
            concrete.RollSupplyCrate(random, Buffer);
            var count = Buffer.Count;
            LootSpawner.DropAround(SnapToGround(new Vector3(position.X, 0f, position.Z)), Buffer);
            Buffer.Clear();
            return count;
        }

        /// <summary>AirdropEvent.Opened olayında sandığı açar; dönen eylem Unsubscribe için saklanmalıdır.</summary>
        public static System.Action<AirdropEvent> BindAirdropOpen(IEventBus bus, ILootSpawnService service, IRandom random)
        {
            if (bus == null)
                return null;

            System.Action<AirdropEvent> handler = e =>
            {
                if (e.Stage == AirdropStage.Opened)
                    OpenSupplyCrate(e.Position, service, random);
            };
            bus.Subscribe(handler);
            return handler;
        }

        private static Vector3 SnapToGround(Vector3 p)
        {
            var origin = new Vector3(p.x, p.y + GroundProbeHeight, p.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, GroundProbeHeight * 2f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;

            return p;
        }
    }
}
