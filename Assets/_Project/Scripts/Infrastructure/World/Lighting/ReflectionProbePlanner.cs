using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World.Lighting
{
    public struct ProbeSpec
    {
        public Vector3 Position;
        public Vector3 Size;
        public int Importance; // 1..3 (ReflectionProbe.importance)
        public bool Interior;
    }

    /// <summary>POI merkezlerine ve iç mekanlara reflection probe yerleşimi (saf mantık).</summary>
    public static class ReflectionProbePlanner
    {
        public static int MaxProbes(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return 2; case 1: return 6; case 2: return 12; default: return 20; }
        }

        public static ProbeSpec ForPoi(Vector3 center, float radius) => new ProbeSpec
        {
            Position = center + Vector3.up * 3f,
            Size = new Vector3(radius * 2f, 14f, radius * 2f),
            Importance = 2,
            Interior = false
        };

        public static ProbeSpec ForInterior(Bounds room) => new ProbeSpec
        {
            Position = room.center,
            Size = room.size + Vector3.one * 0.5f,
            Importance = 3,
            Interior = true
        };

        /// <summary>Sınırı aşarsa önce iç mekanlar (önem), sonra izleyiciye yakınlık ile seçer.</summary>
        public static List<ProbeSpec> ApplyBudget(IList<ProbeSpec> all, Vector3 viewer, int tier)
        {
            var l = new List<ProbeSpec>(all);
            l.Sort((a, b) =>
            {
                int c = b.Importance.CompareTo(a.Importance);
                return c != 0 ? c : (a.Position - viewer).sqrMagnitude.CompareTo((b.Position - viewer).sqrMagnitude);
            });
            int max = MaxProbes(tier);
            if (l.Count > max) l.RemoveRange(max, l.Count - max);
            return l;
        }
    }
}
