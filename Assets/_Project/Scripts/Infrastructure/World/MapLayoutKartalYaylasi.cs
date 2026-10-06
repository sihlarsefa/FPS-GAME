using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// "Kartal Yaylası" yerleşimi (1000 x 1000 m, x,z in [-500, 500], kuzey = +z): geniş otlaklı yüksek yayla —
    /// yörük obası, rüzgâr türbini tarlası, taş ağıllar, küçük pist + hangar, uçurum kenarları, çam kuşağı ve iki köy.
    /// Kar yoktur (<see cref="MapLayout.SnowLine"/> çok yüksek), göl/dere yoktur; yükseklikler yerleşim hedeflerinden gelir.
    /// </summary>
    public sealed partial class MapLayout
    {
        // Yerleşim adları (KartalYaylasiProps bunlarla eşleşir).
        public const string KartalObaName = "Yörük Obası";
        public const string KartalTurbineName = "Rüzgâr Tarlası";
        public const string KartalSheepfoldName = "Taş Ağıllar";
        public const string KartalAirstripName = "Kartal Pisti";
        public const string KartalCliffName = "Uçurum Gözetleme";

        public static MapLayout CreateKartalYaylasi(int seed)
        {
            var layout = new MapLayout
            {
                HalfSize = 500f,
                MaxHeight = 190f,
                WaterLevel = 10f,
                Seed = seed,
                Name = MapCatalog.KartalYaylasiName,
                SnowLine = 9999f
            };

            AddKartal(layout, "Pınarbaşı Köyü", LocationKind.Village, V(0, -20), 62f, 42f, LootTier.Medium, true, 98f);
            AddKartal(layout, "Çayır Köyü", LocationKind.Village, V(-290, 190), 55f, 37f, LootTier.Medium, true, 104f);
            AddKartal(layout, KartalObaName, LocationKind.Farm, V(-150, -190), 60f, 40f, LootTier.Low, true, 94f);
            AddKartal(layout, KartalSheepfoldName, LocationKind.Farm, V(-340, -110), 50f, 34f, LootTier.Low, true, 100f);
            AddKartal(layout, KartalTurbineName, LocationKind.RelayHill, V(230, 250), 80f, 52f, LootTier.High, true, 118f);
            AddKartal(layout, KartalAirstripName, LocationKind.ForwardBase, V(250, -210), 85f, 70f, LootTier.Military, true, 92f);
            AddKartal(layout, "Yayla Karakolu", LocationKind.Karakol, V(30, 330), 48f, 33f, LootTier.High, true, 110f);
            AddKartal(layout, "Çam Kuşağı", LocationKind.Forest, V(330, 20), 80f, 18f, LootTier.Low, true, 96f).ClearRadius = 22f;
            AddKartal(layout, KartalCliffName, LocationKind.Outpost, V(-440, -300), 30f, 20f, LootTier.High, false, 128f);
            AddKartal(layout, "Eski Çoban Evleri", LocationKind.Ruins, V(120, -380), 45f, 31f, LootTier.Medium, true, 90f);

            AddRoad(layout, "Yayla Yolu", RoadKind.Asphalt, 8f,
                V(0, -490), V(10, -380), V(0, -260), V(-10, -120), V(0, -20), V(10, 100), V(30, 220), V(30, 330), V(40, 490));
            AddRoad(layout, "Köy Yolu", RoadKind.Dirt, 6f, V(0, -20), V(-120, 60), V(-220, 150), V(-290, 190));
            AddRoad(layout, "Yörük Yolu", RoadKind.Dirt, 5f, V(-10, -120), V(-80, -160), V(-150, -190));
            AddRoad(layout, "Ağıl Yolu", RoadKind.Dirt, 5f, V(-150, -190), V(-250, -150), V(-340, -110));
            AddRoad(layout, "Pist Yolu", RoadKind.Dirt, 7f, V(0, -260), V(120, -240), V(250, -210));
            AddRoad(layout, "Rüzgâr Yolu", RoadKind.Dirt, 6f, V(30, 220), V(130, 240), V(230, 250));
            AddRoad(layout, "Orman Yolu", RoadKind.Dirt, 5f, V(10, 100), V(150, 60), V(330, 20));
            AddRoad(layout, "Harabe Yolu", RoadKind.Dirt, 4f, V(10, -380), V(120, -380));
            AddRoad(layout, "Gözetleme Yolu", RoadKind.Dirt, 4f, V(-150, -190), V(-300, -250), V(-440, -300));

            AddKartalIdentity(layout);
            layout.ComputeBridges();
            return layout;
        }

        private static LocationSpec AddKartal(MapLayout layout, string name, LocationKind kind, Vector2 center, float radius,
            float flatten, LootTier tier, bool major, float targetHeight)
        {
            var spec = AddLocation(layout, name, kind, center, radius, flatten, tier, major);
            spec.TargetHeight = targetHeight;
            spec.ClearRadius = radius;
            return spec;
        }
    }
}
