using System;
using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Çarpışma yüzeyini efekt türüne çevirir. Sıra: katman (vuruş kutusu → et, araç → metal, Water katmanı → su),
    /// arazi → toprak, görüntüleyici malzemesi (MaterialId adı "HK_&lt;Id&gt;", metaliklik, anahtar sözcük), nesne/üst
    /// nesne adları, fizik malzemesi adı. Sonuç collider başına önbelleğe alınır (sıcak yolda ayırma yok). Ağaç gibi
    /// gövde + yaprak malzemeli tek ağlarda isabet yüksekliğine göre Ahşap/Yaprak seçilir. Su seviyesinin altındaki
    /// arazi isabetleri suya döner.
    /// </summary>
    internal static class SurfaceClassifier
    {
        private const int MaxCacheEntries = 4096;
        private const float LowTrunkFraction = 0.22f;
        private const float TerrainTreeClearance = 0.8f;

        private static readonly Dictionary<Collider, ColliderInfo> ColliderCache = new Dictionary<Collider, ColliderInfo>(256);
        private static readonly Dictionary<Material, SurfaceKind> MaterialCache = new Dictionary<Material, SurfaceKind>(128);
        private static readonly List<Material> SharedMaterials = new List<Material>(8);
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        private static Dictionary<string, SurfaceKind> _idTable;

        /// <summary>Ad ipuçları; sıra önemlidir (bileşik sözcükler önce: "helipad" ≠ "heli", "roofmetal" ≠ "roof").</summary>
        private static readonly KeyValuePair<string, SurfaceKind>[] Keywords =
        {
            K("helipad", SurfaceKind.Concrete),
            K("roofmetal", SurfaceKind.Metal),
            K("metalroof", SurfaceKind.Metal),
            K("rooftile", SurfaceKind.Concrete),
            K("sandbag", SurfaceKind.Dirt),
            K("hesco", SurfaceKind.Dirt),
            K("kumtorba", SurfaceKind.Dirt),

            K("water", SurfaceKind.Water),
            K("lake", SurfaceKind.Water),
            K("river", SurfaceKind.Water),
            K("pond", SurfaceKind.Water),
            K("stream", SurfaceKind.Water),

            K("foliage", SurfaceKind.Foliage),
            K("leaf", SurfaceKind.Foliage),
            K("leaves", SurfaceKind.Foliage),
            K("bush", SurfaceKind.Foliage),
            K("hedge", SurfaceKind.Foliage),
            K("shrub", SurfaceKind.Foliage),
            K("canopy", SurfaceKind.Foliage),
            K("needle", SurfaceKind.Foliage),
            K("haybale", SurfaceKind.Foliage),
            K("straw", SurfaceKind.Foliage),
            K("camonet", SurfaceKind.Foliage),
            K("camo_net", SurfaceKind.Foliage),
            K("yaprak", SurfaceKind.Foliage),
            K("çalı", SurfaceKind.Foliage),

            K("wood", SurfaceKind.Wood),
            K("bark", SurfaceKind.Wood),
            K("plank", SurfaceKind.Wood),
            K("timber", SurfaceKind.Wood),
            K("crate", SurfaceKind.Wood),
            K("pallet", SurfaceKind.Wood),
            K("door", SurfaceKind.Wood),
            K("table", SurfaceKind.Wood),
            K("bench", SurfaceKind.Wood),
            K("trunk", SurfaceKind.Wood),
            K("stump", SurfaceKind.Wood),
            K("barn", SurfaceKind.Wood),
            K("ahşap", SurfaceKind.Wood),
            K("tahta", SurfaceKind.Wood),
            K("kütük", SurfaceKind.Wood),

            K("metal", SurfaceKind.Metal),
            K("steel", SurfaceKind.Metal),
            K("rust", SurfaceKind.Metal),
            K("container", SurfaceKind.Metal),
            K("konteyner", SurfaceKind.Metal),
            K("vehicle", SurfaceKind.Metal),
            K("kirpi", SurfaceKind.Metal),
            K("helicopter", SurfaceKind.Metal),
            K("rotor", SurfaceKind.Metal),
            K("hangar", SurfaceKind.Metal),
            K("barrel", SurfaceKind.Metal),
            K("drum", SurfaceKind.Metal),
            K("tank", SurfaceKind.Metal),
            K("pipe", SurfaceKind.Metal),
            K("antenna", SurfaceKind.Metal),
            K("anten", SurfaceKind.Metal),
            K("radar", SurfaceKind.Metal),
            K("hedgehog", SurfaceKind.Metal),
            K("wreck", SurfaceKind.Metal),
            K("truck", SurfaceKind.Metal),
            K("flagpole", SurfaceKind.Metal),
            K("pole", SurfaceKind.Metal),
            K("gunmetal", SurfaceKind.Metal),
            K("demir", SurfaceKind.Metal),
            K("çelik", SurfaceKind.Metal),

            K("concrete", SurfaceKind.Concrete),
            K("cement", SurfaceKind.Concrete),
            K("plaster", SurfaceKind.Concrete),
            K("brick", SurfaceKind.Concrete),
            K("stone", SurfaceKind.Concrete),
            K("rock", SurfaceKind.Concrete),
            K("boulder", SurfaceKind.Concrete),
            K("cliff", SurfaceKind.Concrete),
            K("asphalt", SurfaceKind.Concrete),
            K("wall", SurfaceKind.Concrete),
            K("bunker", SurfaceKind.Concrete),
            K("mosque", SurfaceKind.Concrete),
            K("minaret", SurfaceKind.Concrete),
            K("dome", SurfaceKind.Concrete),
            K("tile", SurfaceKind.Concrete),
            K("stair", SurfaceKind.Concrete),
            K("floor", SurfaceKind.Concrete),
            K("building", SurfaceKind.Concrete),
            K("house", SurfaceKind.Concrete),
            K("karakol", SurfaceKind.Concrete),
            K("beton", SurfaceKind.Concrete),
            K("duvar", SurfaceKind.Concrete),
            K("kaya", SurfaceKind.Concrete),

            K("snow", SurfaceKind.Snow),
            K("kar_", SurfaceKind.Snow),
            K("dirt", SurfaceKind.Dirt),
            K("mud", SurfaceKind.Dirt),
            K("soil", SurfaceKind.Dirt),
            K("sand", SurfaceKind.Dirt),
            K("gravel", SurfaceKind.Dirt),
            K("grass", SurfaceKind.Dirt),
            K("ground", SurfaceKind.Dirt),
            K("terrain", SurfaceKind.Dirt),
            K("earth", SurfaceKind.Dirt),
            K("berm", SurfaceKind.Dirt),
            K("toprak", SurfaceKind.Dirt),
            K("çamur", SurfaceKind.Dirt),

            K("skin", SurfaceKind.Flesh),
            K("flesh", SurfaceKind.Flesh),
            K("corpse", SurfaceKind.Flesh)
        };

        /// <summary>Delme modeli için malzeme ipucu (SurfaceKind'de yok): tuğla ve kum torbası/hesco.</summary>
        internal enum PenetrationHint : byte
        {
            None = 0,
            Brick = 1,
            Sandbag = 2
        }

        private struct ColliderInfo
        {
            public SurfaceKind Kind;
            public PenetrationHint Hint;
            public bool IsTerrain;
            public Terrain Terrain;

            // Gövde + yaprak tek ağ (ağaç): eksene yakın ve alçak isabet → ahşap, aksi → yaprak.
            public bool TrunkAndCanopy;
            public Vector2 AxisXZ;
            public float TrunkRadius;
            public float LowTrunkY;
        }

        /// <summary>
        /// Delme modeli ipucu (tuğla / kum torbası-hesco): malzeme adı ya da nesne/üst nesne adı. Sonuç collider başına
        /// önbelleklenir (Classify ile aynı kayıt). SurfaceKind tuğlayı Concrete, kum torbasını Dirt gösterir; delme modeli
        /// bunları ayrı malzeme (Brick/Sandbag) olarak ele alır.
        /// </summary>
        internal static PenetrationHint HintOf(Collider collider)
        {
            return collider == null ? PenetrationHint.None : GetInfo(collider).Hint;
        }

        private static ColliderInfo GetInfo(Collider collider)
        {
            if (!ColliderCache.TryGetValue(collider, out var info))
            {
                info = Analyze(collider);
                if (ColliderCache.Count >= MaxCacheEntries)
                    ColliderCache.Clear();
                ColliderCache[collider] = info;
            }

            return info;
        }

        public static SurfaceKind Classify(Collider collider, Vector3 point)
        {
            if (collider == null)
                return BelowWater(point) ? SurfaceKind.Water : SurfaceKind.Default;

            var info = GetInfo(collider);
            var kind = info.Kind;
            if (info.TrunkAndCanopy)
            {
                var dx = point.x - info.AxisXZ.x;
                var dz = point.z - info.AxisXZ.y;
                var nearAxis = dx * dx + dz * dz <= info.TrunkRadius * info.TrunkRadius;
                kind = nearAxis || point.y < info.LowTrunkY ? SurfaceKind.Wood : SurfaceKind.Foliage;
            }
            else if (info.IsTerrain && info.Terrain != null)
            {
                // Arazi ağaçları (TreeInstance) arazi collider'ı ile vurulur: yüzeyin belirgin üstü → gövde.
                var ground = info.Terrain.SampleHeight(point) + info.Terrain.GetPosition().y;
                if (point.y > ground + TerrainTreeClearance)
                    return SurfaceKind.Wood;
            }

            if ((info.IsTerrain || kind == SurfaceKind.Default || kind == SurfaceKind.Dirt) && BelowWater(point))
                return SurfaceKind.Water;

            return kind;
        }

        public static void ClearCaches()
        {
            ColliderCache.Clear();
            MaterialCache.Clear();
            SharedMaterials.Clear();
        }

        // ------------------------------------------------------------------ analiz (collider başına bir kez)

        private static ColliderInfo Analyze(Collider collider)
        {
            var info = new ColliderInfo { Kind = SurfaceKind.Default };
            var go = collider.gameObject;
            var layer = go.layer;

            if (layer == GameLayers.Hitbox || layer == GameLayers.Player || layer == GameLayers.Bot)
            {
                info.Kind = SurfaceKind.Flesh;
                return info;
            }

            if (layer == GameLayers.Vehicle)
            {
                info.Kind = SurfaceKind.Metal;
                return info;
            }

            if (layer == GameLayers.Water)
            {
                info.Kind = SurfaceKind.Water;
                return info;
            }

            if (collider is TerrainCollider)
            {
                info.Kind = SurfaceKind.Dirt;
                info.IsTerrain = true;
                // Yalnızca ağaç örneği olan arazide yükseklik örneklemesi yapılır.
                if (collider.TryGetComponent(out Terrain terrain) && terrain.terrainData != null
                                                                && terrain.terrainData.treeInstanceCount > 0)
                    info.Terrain = terrain;
                return info;
            }

            // 1) Görüntüleyici malzemeleri.
            var renderer = FindRenderer(collider);
            info.Hint = HintFor(collider, renderer);
            if (renderer != null)
            {
                SharedMaterials.Clear();
                renderer.GetSharedMaterials(SharedMaterials);
                var hasWood = false;
                var hasFoliage = false;
                var first = SurfaceKind.Default;
                for (var i = 0; i < SharedMaterials.Count; i++)
                {
                    var kind = MaterialKind(SharedMaterials[i]);
                    if (kind == SurfaceKind.Wood)
                        hasWood = true;
                    else if (kind == SurfaceKind.Foliage)
                        hasFoliage = true;
                    if (first == SurfaceKind.Default && kind != SurfaceKind.Default)
                        first = kind;
                }

                SharedMaterials.Clear();
                if (hasWood && hasFoliage)
                {
                    var bounds = renderer.bounds;
                    var height = Mathf.Max(0.1f, bounds.size.y);
                    var center = bounds.center;
                    info.Kind = SurfaceKind.Foliage;
                    info.TrunkAndCanopy = true;
                    info.AxisXZ = new Vector2(center.x, center.z);
                    info.TrunkRadius = Mathf.Clamp(height * 0.035f, 0.15f, 0.6f) + 0.1f;
                    info.LowTrunkY = bounds.min.y + height * LowTrunkFraction;
                    return info;
                }

                if (first != SurfaceKind.Default)
                {
                    info.Kind = first;
                    return info;
                }
            }

            // 2) Nesne ve üst nesne adları (bina parçaları, dekorlar).
            var t = collider.transform;
            for (var depth = 0; depth < 4 && t != null; depth++)
            {
                var kind = FromKeywords(t.name);
                if (kind != SurfaceKind.Default)
                {
                    info.Kind = kind;
                    return info;
                }

                t = t.parent;
            }

            // 3) Fizik malzemesi adı.
            var physicsMaterial = collider.sharedMaterial;
            if (physicsMaterial != null)
                info.Kind = FromKeywords(physicsMaterial.name);

            return info;
        }

        /// <summary>Tuğla / kum torbası ipucu: önce görüntüleyici malzeme adları, sonra nesne ve üst nesne adları (collider başına bir kez).</summary>
        private static PenetrationHint HintFor(Collider collider, Renderer renderer)
        {
            if (renderer != null)
            {
                SharedMaterials.Clear();
                renderer.GetSharedMaterials(SharedMaterials);
                var found = PenetrationHint.None;
                for (var i = 0; i < SharedMaterials.Count && found == PenetrationHint.None; i++)
                {
                    if (SharedMaterials[i] != null)
                        found = HintFromName(SharedMaterials[i].name);
                }

                SharedMaterials.Clear();
                if (found != PenetrationHint.None)
                    return found;
            }

            var t = collider.transform;
            for (var depth = 0; depth < 4 && t != null; depth++)
            {
                var hint = HintFromName(t.name);
                if (hint != PenetrationHint.None)
                    return hint;

                t = t.parent;
            }

            return PenetrationHint.None;
        }

        private static PenetrationHint HintFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return PenetrationHint.None;

            if (name.IndexOf("sandbag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("hesco", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("kumtorba", StringComparison.OrdinalIgnoreCase) >= 0)
                return PenetrationHint.Sandbag;

            if (name.IndexOf("brick", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("tuğla", StringComparison.OrdinalIgnoreCase) >= 0)
                return PenetrationHint.Brick;

            return PenetrationHint.None;
        }

        private static Renderer FindRenderer(Collider collider)
        {
            if (collider.TryGetComponent(out Renderer own))
                return own;

            var parent = collider.transform.parent;
            if (parent != null && parent.TryGetComponent(out Renderer parentRenderer))
                return parentRenderer;

            // Ayrı "Collider" alt nesnesi olan dekorlar: kardeş/alt görüntüleyici.
            return collider.transform.childCount > 0 ? collider.GetComponentInChildren<Renderer>() : null;
        }

        private static SurfaceKind MaterialKind(Material material)
        {
            if (material == null)
                return SurfaceKind.Default;

            if (MaterialCache.TryGetValue(material, out var cached))
                return cached;

            var kind = FromMaterialName(material.name);
            if (kind == SurfaceKind.Default && material.HasProperty(MetallicId) && material.GetFloat(MetallicId) >= 0.5f)
                kind = SurfaceKind.Metal;

            if (MaterialCache.Count >= MaxCacheEntries)
                MaterialCache.Clear();
            MaterialCache[material] = kind;
            return kind;
        }

        private static SurfaceKind FromMaterialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return SurfaceKind.Default;

            var core = name;
            const string instanceSuffix = " (Instance)";
            while (core.EndsWith(instanceSuffix, StringComparison.Ordinal))
                core = core.Substring(0, core.Length - instanceSuffix.Length);

            core = StripPrefix(core, "HK_");
            core = StripPrefix(core, "M_");
            core = StripPrefix(core, "Mat_");

            if (IdTable.TryGetValue(core, out var exact))
                return exact;

            return FromKeywords(core);
        }

        private static string StripPrefix(string value, string prefix)
        {
            return value.Length > prefix.Length && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? value.Substring(prefix.Length)
                : value;
        }

        private static SurfaceKind FromKeywords(string name)
        {
            if (string.IsNullOrEmpty(name))
                return SurfaceKind.Default;

            for (var i = 0; i < Keywords.Length; i++)
            {
                if (name.IndexOf(Keywords[i].Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return Keywords[i].Value;
            }

            return SurfaceKind.Default;
        }

        private static bool BelowWater(Vector3 point)
        {
            return WaterLevelSource.TryGet(out var level) && point.y < level - 0.03f;
        }

        // ------------------------------------------------------------------ MaterialId eşlemesi

        private static Dictionary<string, SurfaceKind> IdTable
        {
            get
            {
                if (_idTable != null)
                    return _idTable;

                var table = new Dictionary<string, SurfaceKind>(StringComparer.OrdinalIgnoreCase);
                var values = (MaterialId[])Enum.GetValues(typeof(MaterialId));
                for (var i = 0; i < values.Length; i++)
                {
                    var kind = KindForId(values[i]);
                    table[values[i].ToString()] = kind;
                }

                _idTable = table;
                return table;
            }
        }

        /// <summary>Paylaşılan malzeme kimliğinin isabet yüzeyi (Default = genel toz/beton efekti).</summary>
        public static SurfaceKind KindForId(MaterialId id)
        {
            switch (id)
            {
                case MaterialId.Grass:
                case MaterialId.DryGrass:
                case MaterialId.Dirt:
                case MaterialId.Mud:
                case MaterialId.Sand:
                case MaterialId.Gravel:
                case MaterialId.Hesco:
                case MaterialId.Sandbag:
                    return SurfaceKind.Dirt;

                case MaterialId.Rock:
                case MaterialId.RockDark:
                case MaterialId.Asphalt:
                case MaterialId.Concrete:
                case MaterialId.ConcreteDark:
                case MaterialId.Plaster:
                case MaterialId.PlasterWarm:
                case MaterialId.Stone:
                case MaterialId.StoneDark:
                case MaterialId.Brick:
                case MaterialId.RoofTile:
                case MaterialId.MosqueDome:
                    return SurfaceKind.Concrete;

                case MaterialId.Water:
                    return SurfaceKind.Water;

                case MaterialId.Foliage:
                case MaterialId.FoliageDark:
                case MaterialId.PineNeedles:
                case MaterialId.Hay:
                case MaterialId.CamoNet:
                    return SurfaceKind.Foliage;

                case MaterialId.Bark:
                case MaterialId.DeadWood:
                case MaterialId.Wood:
                case MaterialId.WoodDark:
                case MaterialId.GunWood:
                    return SurfaceKind.Wood;

                case MaterialId.RoofMetal:
                case MaterialId.MetalPanel:
                case MaterialId.MetalDark:
                case MaterialId.Rust:
                case MaterialId.VehicleOlive:
                case MaterialId.VehicleTan:
                case MaterialId.VehicleDark:
                case MaterialId.HeliOlive:
                case MaterialId.Windshield:
                case MaterialId.RotorBlade:
                case MaterialId.GunMetal:
                case MaterialId.GunTan:
                    return SurfaceKind.Metal;

                case MaterialId.Skin:
                case MaterialId.SkinDark:
                    return SurfaceKind.Flesh;

                default:
                    return SurfaceKind.Default;
            }
        }

        private static KeyValuePair<string, SurfaceKind> K(string key, SurfaceKind kind)
        {
            return new KeyValuePair<string, SurfaceKind>(key, kind);
        }
    }
}
