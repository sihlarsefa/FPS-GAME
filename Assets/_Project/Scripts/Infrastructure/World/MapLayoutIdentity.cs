using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Harita kimliği POI'leri: Ayaz Geçidi (kar), Mavi Liman (liman/konteyner) ve Kartal Yaylası (obsidyen/telesiyej/şenlik)
    /// için ek lokasyonlar + yol çıkmaları. Yerleşim tohumdan bağımsız ve deterministiktir. Yapılar
    /// <see cref="MapIdentityProps"/> tarafından adlarına göre üretilir; <see cref="LocationBuilder"/> bu adları genel kalıpla üretmez.
    /// Kuzgun Vadisi'ne dokunulmaz.
    /// </summary>
    public sealed partial class MapLayout
    {
        // --- Ayaz Geçidi
        public const string AyazKayakKalintiName = "Kayak İstasyonu Kalıntısı";
        public const string AyazBarinakName = "Dağ Barınakları";
        public const string AyazTipiName = "Tipi Gözcüsü";
        public const string AyazSiperName = "Kar Siper Hattı";

        // --- Mavi Liman
        public const string LimanKonteynerName = "Konteyner Sahası";
        public const string LimanBalikHaliName = "Balık Hali";
        public const string LimanDalgakiranName = "Dalgakıran";

        // --- Kartal Yaylası
        public const string KartalObsidyenName = "Obsidyen Kayalıkları";
        public const string KartalYukariMeraName = "Yukarı Mera";
        public const string KartalKuzeyMeraName = "Kuzey Mera";
        public const string KartalTeleAltName = "Telesiyej Alt İstasyon";
        public const string KartalTeleUstName = "Telesiyej Üst İstasyon";
        public const string KartalSenlikName = "Yayla Şenlik Alanı";

        /// <summary>Kimlik POI'si olarak özel üretilen lokasyon adları (LocationBuilder genel kalıbı atlar).</summary>
        public static bool IsIdentityPoi(string name)
        {
            switch (name)
            {
                case AyazKayakKalintiName:
                case AyazBarinakName:
                case AyazTipiName:
                case AyazSiperName:
                case LimanKonteynerName:
                case LimanBalikHaliName:
                case LimanDalgakiranName:
                case KartalObsidyenName:
                case KartalYukariMeraName:
                case KartalKuzeyMeraName:
                case KartalTeleAltName:
                case KartalTeleUstName:
                case KartalSenlikName:
                    return true;
                default:
                    return false;
            }
        }

        private static void AddAyazIdentity(MapLayout layout)
        {
            AddPoi(layout, AyazKayakKalintiName, LocationKind.Ruins, V(-415, 110), 40f, 28f, LootTier.Medium, true);
            AddPoi(layout, AyazBarinakName, LocationKind.Farm, V(-120, -195), 34f, 24f, LootTier.Low, false);
            AddPoi(layout, AyazTipiName, LocationKind.Outpost, V(150, 130), 36f, 24f, LootTier.High, false);
            AddPoi(layout, AyazSiperName, LocationKind.Outpost, V(200, 235), 30f, 20f, LootTier.Medium, false);
            AddSpurs(layout, 4, 6);
        }

        private static void AddMaviIdentity(MapLayout layout)
        {
            AddPoi(layout, LimanKonteynerName, LocationKind.ForwardBase, V(150, -420), 50f, 34f, LootTier.High, true);
            AddPoi(layout, LimanBalikHaliName, LocationKind.Farm, V(185, -35), 40f, 26f, LootTier.Medium, true);
            AddPoi(layout, LimanDalgakiranName, LocationKind.Farm, V(262, 380), 30f, 18f, LootTier.High, false);
            AddSpurs(layout, 3, 6);
        }

        private static void AddKartalIdentity(MapLayout layout)
        {
            AddPoi(layout, KartalObsidyenName, LocationKind.Outpost, V(-230, -5), 44f, 30f, LootTier.High, false);
            AddPoi(layout, KartalYukariMeraName, LocationKind.Farm, V(-230, -340), 40f, 28f, LootTier.Low, true);
            AddPoi(layout, KartalKuzeyMeraName, LocationKind.Farm, V(-60, 270), 40f, 28f, LootTier.Low, true);
            AddPoi(layout, KartalTeleAltName, LocationKind.Outpost, V(-40, 140), 26f, 18f, LootTier.Medium, false);
            AddPoi(layout, KartalTeleUstName, LocationKind.Outpost, V(-210, 300), 26f, 18f, LootTier.Medium, false);
            AddPoi(layout, KartalSenlikName, LocationKind.Farm, V(390, -110), 45f, 30f, LootTier.Medium, true);
            AddSpurs(layout, 6, 4);
        }

        private static LocationSpec AddPoi(MapLayout layout, string name, LocationKind kind, Vector2 center, float radius,
            float flatten, LootTier tier, bool major)
        {
            var spec = AddLocation(layout, name, kind, center, radius, flatten, tier, major);
            spec.ClearRadius = radius;
            return spec;
        }

        /// <summary>Son eklenen <paramref name="count"/> lokasyon için en yakın mevcut yoldan çıkma yolu ekler.</summary>
        private static void AddSpurs(MapLayout layout, int count, float width)
        {
            var first = layout.Locations.Count - count;
            var baseRoads = layout.Roads.Count;
            for (var i = first; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                var spur = PlanSpur(layout.Roads, baseRoads, loc.Center, 260f);
                if (spur == null)
                    continue;
                layout.Roads.Add(new RoadSpec
                {
                    Name = loc.Name + " Yolu",
                    Kind = RoadKind.Dirt,
                    Width = width,
                    Points = Densify(spur, DensifySpacing)
                });
            }
        }

        /// <summary>
        /// Hedefe en yakın yol noktasından hedefe giden kontrol noktaları (orta nokta hafif yanal sapmalı); en yakın nokta
        /// <paramref name="maxLength"/>'ten uzaksa ya da hedefe zaten &lt; 12 m ise null. Saf mantık (test edilebilir).
        /// </summary>
        public static Vector2[] PlanSpur(IReadOnlyList<RoadSpec> roads, int roadCount, Vector2 target, float maxLength)
        {
            var best = float.MaxValue;
            var from = Vector2.zero;
            for (var r = 0; r < roadCount && r < roads.Count; r++)
            {
                var pts = roads[r]?.Points;
                if (pts == null)
                    continue;
                for (var i = 0; i < pts.Count; i++)
                {
                    var d = (pts[i] - target).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        from = pts[i];
                    }
                }
            }

            if (best == float.MaxValue)
                return null;
            var dist = Mathf.Sqrt(best);
            if (dist > maxLength || dist < 12f)
                return null;
            var dir = (target - from) / dist;
            var side = new Vector2(-dir.y, dir.x) * (dist * 0.06f);
            var mid = (from + target) * 0.5f + side;
            return new[] { from, mid, target };
        }
    }
}
