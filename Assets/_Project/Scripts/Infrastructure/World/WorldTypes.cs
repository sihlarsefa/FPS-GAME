using System;
using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    public enum LocationKind
    {
        Village = 0,        // Köy (taş evler, cami)
        Karakol = 1,        // Karakol (duvar, nöbet kulesi, bayrak)
        ForwardBase = 2,    // İleri Üs / FOB (Hesco, çadır, helipad, konteyner)
        Quarry = 3,         // Taş ocağı
        Dam = 4,            // Baraj
        RelayHill = 5,      // Röle tepesi (anten, bunker)
        Forest = 6,         // Ormanlık sırt
        Farm = 7,           // Çiftlik / ağıl
        Ruins = 8,          // Harabe köy
        Outpost = 9         // Küçük mevzi / gözetleme noktası
    }

    [Serializable]
    public sealed class LocationSpec
    {
        public string Name;
        public LocationKind Kind;
        public Vector2 Center;
        public float Radius = 60f;
        public LootTier Tier = LootTier.Medium;
        public float FlattenRadius = 50f;
        public bool IsMajor = true;

        /// <summary>
        /// Düzleştirilmiş zemin yüksekliği (dünya Y). &lt; 0 ise arazi üretici otomatik hesaplar ve buraya yazar —
        /// TerrainGenerator.Create sonrasında yapı üreticileri bu değeri okuyabilir.
        /// </summary>
        public float TargetHeight = -1f;

        /// <summary>Ağaç/kaya serpiştirmesinden muaf yarıçap (≤ 0 → Radius).</summary>
        public float ClearRadius = -1f;

        public Vector3 CenterWorld(float y) => new Vector3(Center.x, y, Center.y);
    }

    public enum RoadKind
    {
        Asphalt = 0,
        Dirt = 1
    }

    [Serializable]
    public sealed class RoadSpec
    {
        public string Name;
        public RoadKind Kind;
        public float Width = 7f;
        public List<Vector2> Points = new();
    }

    [Serializable]
    public sealed class LakeSpec
    {
        public Vector2 Center;
        public float Radius = 60f;
        public float Depth = 4f;
    }

    [Serializable]
    public sealed class RiverSpec
    {
        public float Width = 10f;
        public float Depth = 2.5f;
        public List<Vector2> Points = new();
    }

    [Serializable]
    public struct LootSpawnPointData
    {
        public Vector3 Position;
        public LootTier Tier;

        public LootSpawnPointData(Vector3 position, LootTier tier)
        {
            Position = position;
            Tier = tier;
        }
    }

    [Serializable]
    public sealed class NamedLocation
    {
        public string Name;
        public Vector2 Center;
        public float Radius;
        public bool IsMajor;
    }

    [Serializable]
    public struct VehicleSpawnData
    {
        public Vector3 Position;
        public float Yaw;
        /// <summary>true: yer aracı değil, helipad üzerindeki uçurulabilir T-70 noktası.</summary>
        public bool IsAir;

        public VehicleSpawnData(Vector3 position, float yaw, bool isAir = false)
        {
            Position = position;
            Yaw = yaw;
            IsAir = isAir;
        }
    }

    public enum BuildingStyle
    {
        VillageHouse = 0, TwoStoryHouse, Mosque, Shop, Barracks, Karakol, WatchTower, Hangar, Warehouse, FactoryHall,
        Barn, Bunker, RadarStation, Shed, DamControl, ShepherdHut
    }

    public sealed class BuildingSpec
    {
        public string Name;
        public BuildingStyle Style;
        public Vector3 Position;
        public float Yaw;
        public float Width = 8f;
        public float Depth = 7f;
        public int Floors = 1;
        public float FloorHeight = 3.2f;
        public bool Ruined;
        public LootTier Tier = LootTier.Medium;
        public int Seed;
    }

    public sealed class BuildingResult
    {
        public GameObject Root;
        public Bounds Bounds;
        public List<Vector3> LootPoints = new();
    }

    /// <summary>
    /// Yolun dereyi geçtiği köprü noktası. Konum/yön yerleşimde; DeckHeight arazi üretiminde (yol profili) doldurulur.
    /// Zemin köprü başlarında yol yüksekliğinde düz bırakılır; dere yatağı altından geçer.
    /// </summary>
    [Serializable]
    public sealed class BridgeSpec
    {
        public string Name;
        public Vector2 Center;
        /// <summary>Yol yönü (birim, XZ).</summary>
        public Vector2 Direction = Vector2.up;
        /// <summary>Köprü başları arası uzunluk (m).</summary>
        public float Length = 30f;
        public float Width = 8f;
        /// <summary>Tabliye üst yüzeyi (dünya Y); &lt; 0 → henüz hesaplanmadı.</summary>
        public float DeckHeight = -1f;
        public RoadKind Kind = RoadKind.Asphalt;

        public float Yaw => Mathf.Atan2(Direction.x, Direction.y) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Harita yerleşim verisi. CreateKuzgunVadisi: dağlık harita (sırtlar, vadi, dere, köyler, karakol, FOB, taş ocağı,
    /// baraj, röle tepesi, ormanlar), yollar. Yol ve dere noktaları yoğunlaştırılmış (≈8 m) çoklu çizgilerdir.
    /// </summary>
    public sealed partial class MapLayout
    {
        public float HalfSize = 512f;
        public float MaxHeight = 160f;
        public float WaterLevel = 18f;
        public int Seed;

        /// <summary>Kar çizgisi (m). Varsayılan Kuzgun Vadisi değeri (<see cref="TerrainPainter.SnowLine"/>).</summary>
        public float SnowLine = TerrainPainter.SnowLine;

        /// <summary>Haritayı çevreleyen sonsuz deniz düzlemi (kıyı haritaları; yalnız görsel, WaterLevel'da).</summary>
        public bool SeaPlane;

        /// <summary>Göller donmuş (buz yüzeyi + kar yamaları); varsayılan false, Ayaz Geçidi true.</summary>
        public bool FrozenLakes;

        /// <summary>Su seviyesi üstünde bu yüksekliğe kadar kumsal boyanır (m; 0 → kumsal yok).</summary>
        public float BeachHeight;

        public List<LocationSpec> Locations = new();
        public List<RoadSpec> Roads = new();
        public List<LakeSpec> Lakes = new();
        public List<RiverSpec> Rivers = new();

        /// <summary>Harita adı (arayüz).</summary>
        public string Name = "Kuzgun Vadisi";

        /// <summary>Yol–dere kesişimlerindeki köprüler.</summary>
        public List<BridgeSpec> Bridges = new();
    }
}
