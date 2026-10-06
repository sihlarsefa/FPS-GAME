using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Drone
{
    /// <summary>
    /// İHA keşfi: tim başına bekleme (120 sn), 12 sn görev, 60 m içindeki düşmanları tim için işaretler.
    /// Statik erişim: HUD/harita işaret listesini <see cref="GetMarked"/> ile okur.
    /// </summary>
    public static class ReconDroneSystem
    {
        public const float MarkLifetime = 2.5f;

        private struct Mark
        {
            public Combatant Combatant;
            public int Team;
            public float Expire;
        }

        private static readonly ReconDroneService Service = new ReconDroneService();
        private static readonly List<Mark> Marks = new List<Mark>(16);
        private static readonly List<Float3> PosBuf = new List<Float3>(32);
        private static readonly List<int> TeamBuf = new List<int>(32);
        private static readonly List<bool> AliveBuf = new List<bool>(32);
        private static readonly List<int> IdxBuf = new List<int>(32);
        private static readonly List<Combatant> CombBuf = new List<Combatant>(32);

        public static float Duration => Service.Duration;

        public static float GetCooldownRemaining(int team) => Service.GetCooldownRemaining(team, Time.time);

        public static bool IsReady(int team) => Service.IsReady(team, Time.time);

        /// <summary>Hazırsa İHA'yı başlatır. Bekleme süresi başlar.</summary>
        public static bool TryLaunch(int team, Vector3 origin, Vector3 target)
        {
            if (float.IsNaN(target.x) || float.IsNaN(target.z) || !Service.TryLaunch(team, Time.time))
                return false;

            ReconDroneActor.Spawn(team, origin, target, Service.Duration);
            return true;
        }

        /// <summary>Aktif İHA'nın çağırdığı tarama: merkez çevresindeki düşmanları işaretler.</summary>
        internal static void Scan(int team, Vector3 center)
        {
            var all = CombatantRegistry.All;
            PosBuf.Clear(); TeamBuf.Clear(); AliveBuf.Clear(); CombBuf.Clear(); IdxBuf.Clear();
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null)
                    continue;
                var p = c.transform.position;
                PosBuf.Add(new Float3(p.x, p.y, p.z));
                TeamBuf.Add(c.Team);
                AliveBuf.Add(c.IsAlive);
                CombBuf.Add(c);
            }

            Service.SelectEnemies(new Float3(center.x, center.y, center.z), team, PosBuf, TeamBuf, AliveBuf, IdxBuf);
            var expire = Time.time + MarkLifetime;
            for (var k = 0; k < IdxBuf.Count; k++)
            {
                var c = CombBuf[IdxBuf[k]];
                var found = false;
                for (var m = 0; m < Marks.Count; m++)
                {
                    if (!ReferenceEquals(Marks[m].Combatant, c) || Marks[m].Team != team)
                        continue;
                    Marks[m] = new Mark { Combatant = c, Team = team, Expire = expire };
                    found = true;
                    break;
                }

                if (!found)
                    Marks.Add(new Mark { Combatant = c, Team = team, Expire = expire });
            }
        }

        /// <summary>Tim için şu an işaretli (canlı) düşmanlar.</summary>
        public static void GetMarked(int team, List<Combatant> output)
        {
            if (output == null)
                return;
            output.Clear();
            var now = Time.time;
            for (var i = Marks.Count - 1; i >= 0; i--)
            {
                var m = Marks[i];
                if (m.Expire <= now || m.Combatant == null || !m.Combatant.IsAlive)
                {
                    Marks.RemoveAt(i);
                    continue;
                }

                if (m.Team == team)
                    output.Add(m.Combatant);
            }
        }

        public static void ResetAll()
        {
            Service.Reset();
            Marks.Clear();
        }

        // Alan yeniden yüklemesi kapalıyken (Enter Play Mode) bekleme süresi/işaretler oturumlar arası taşınmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetAll();
    }
}
