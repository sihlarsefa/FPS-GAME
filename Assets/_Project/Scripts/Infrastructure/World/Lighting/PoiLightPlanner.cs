using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World.Lighting
{
    public enum PoiSiteType { Village, MilitaryBase, Checkpoint, Camp }
    public enum PoiLightKind { StreetLamp, Floodlight, BarrelFire }

    /// <summary>Planlanan tek bir lokal ışık (saf veri).</summary>
    public struct PoiLightSpec
    {
        public PoiLightKind Kind;
        public Vector3 Position;
        public Vector3 Direction; // Floodlight için bakış yönü
        public Color Color;
        public float Range;
        public float Intensity;
        public float Flicker; // 0..1 titreşim genliği (ateş)
        public bool Shadow;
    }

    /// <summary>POI tipine göre lokal ışık yerleşimi ve gölge bütçesi. Saf mantık; sahne nesnesi üretmez.</summary>
    public static class PoiLightPlanner
    {
        public const int MaxShadowed = 4;

        /// <summary>Kademe başına (0=Low..3=Ultra) toplam aktif ışık sınırı.</summary>
        public static int MaxLights(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return 6; case 1: return 12; case 2: return 20; default: return 32; }
        }

        /// <summary>Kademe başına gölgeli ışık sınırı (en fazla 4).</summary>
        public static int MaxShadowCasters(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return 0; case 1: return 2; default: return MaxShadowed; }
        }

        public static List<PoiLightSpec> Plan(PoiSiteType type, Vector3 center, float radius, int seed)
        {
            var list = new List<PoiLightSpec>();
            var rng = new System.Random(seed);
            radius = Mathf.Max(4f, radius);
            switch (type)
            {
                case PoiSiteType.Village:
                {
                    int n = Mathf.Clamp(Mathf.RoundToInt(radius / 10f), 2, 6);
                    for (int i = 0; i < n; i++)
                        list.Add(Lamp(Ring(center, radius * 0.7f, i, n, rng, 0.15f)));
                    list.Add(Fire(Ring(center, radius * 0.25f, 0, 1, rng, 0f)));
                    break;
                }
                case PoiSiteType.MilitaryBase:
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var p = Ring(center, radius * 0.85f, i, 4, rng, 0.05f);
                        var dir = (center - p); dir.y = -0.6f; dir.Normalize();
                        list.Add(new PoiLightSpec { Kind = PoiLightKind.Floodlight, Position = p + Vector3.up * 7f, Direction = dir,
                            Color = new Color(0.85f, 0.92f, 1f), Range = 26f, Intensity = 3.2f });
                    }
                    list.Add(Lamp(Ring(center, radius * 0.3f, 0, 1, rng, 0f)));
                    break;
                }
                case PoiSiteType.Checkpoint:
                    list.Add(Lamp(center + new Vector3(2.5f, 0f, 0f)));
                    list.Add(Fire(center + new Vector3(-3f, 0f, 1.5f)));
                    break;
                default: // Camp
                    list.Add(Fire(center));
                    if (rng.NextDouble() < 0.5) list.Add(Fire(Ring(center, radius * 0.6f, 0, 1, rng, 1f)));
                    break;
            }
            return list;
        }

        private static Vector3 Ring(Vector3 c, float r, int i, int n, System.Random rng, float jitter)
        {
            float a = (i + (float)rng.NextDouble() * jitter) / Mathf.Max(1, n) * Mathf.PI * 2f + (n == 1 ? (float)rng.NextDouble() * 6.28f : 0f);
            return c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        private static PoiLightSpec Lamp(Vector3 p) => new PoiLightSpec
        {
            Kind = PoiLightKind.StreetLamp, Position = p + Vector3.up * 5f, Direction = Vector3.down,
            Color = new Color(1f, 0.8f, 0.5f), Range = 14f, Intensity = 2.2f
        };

        private static PoiLightSpec Fire(Vector3 p) => new PoiLightSpec
        {
            Kind = PoiLightKind.BarrelFire, Position = p + Vector3.up * 1.1f, Direction = Vector3.up,
            Color = new Color(1f, 0.5f, 0.15f), Range = 9f, Intensity = 2.6f, Flicker = 0.35f
        };

        /// <summary>İzleyiciye en yakın ışıklara gölge verir (kademe sınırı kadar), kalanları gölgesiz bırakır; toplam sınırı aşanı en uzaktan keser.</summary>
        public static List<PoiLightSpec> ApplyBudget(IList<PoiLightSpec> all, Vector3 viewer, int tier)
        {
            var sorted = new List<PoiLightSpec>(all);
            sorted.Sort((a, b) => (a.Position - viewer).sqrMagnitude.CompareTo((b.Position - viewer).sqrMagnitude));
            int max = MaxLights(tier);
            if (sorted.Count > max) sorted.RemoveRange(max, sorted.Count - max);
            int shadows = MaxShadowCasters(tier);
            for (int i = 0; i < sorted.Count; i++)
            {
                var s = sorted[i];
                s.Shadow = i < shadows;
                sorted[i] = s;
            }
            return sorted;
        }

        /// <summary>Ateş titreşimi: [1-amp, 1+amp] aralığında çarpan (t zaman, phase ışık başına sabit).</summary>
        public static float FlickerFactor(float t, float phase, float amp)
        {
            float n = Mathf.Sin(t * 13.1f + phase) * 0.5f + Mathf.Sin(t * 7.3f + phase * 2.1f) * 0.3f + Mathf.Sin(t * 29f + phase * 0.7f) * 0.2f;
            return 1f + Mathf.Clamp(n, -1f, 1f) * Mathf.Clamp01(amp);
        }
    }
}
