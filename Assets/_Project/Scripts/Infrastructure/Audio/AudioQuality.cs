using Project.Core.Domain;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Ses kalitesi yardımcıları: yüzey → ayak sesi, silah sınıfı → şarjör sesi, mesafe kademesi (saf mantık).</summary>
    public static class AudioQuality
    {
        public const float MidShotDistance = 70f;
        public const float FarShotDistance = 250f;

        public enum ShotTier { Close, Mid, Far }

        public static ShotTier TierForDistance(float distance)
        {
            if (distance >= FarShotDistance) return ShotTier.Far;
            return distance >= MidShotDistance ? ShotTier.Mid : ShotTier.Close;
        }

        public static SoundId FootstepFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Concrete: return SoundId.FootstepConcrete;
                case SurfaceKind.Metal: return SoundId.FootstepMetal;
                case SurfaceKind.Wood: return SoundId.FootstepWood;
                case SurfaceKind.Foliage: return SoundId.FootstepGrass;
                case SurfaceKind.Snow: return SoundId.FootstepSnow;
                default: return SoundId.Footstep; // toprak / varsayılan
            }
        }

        // ---- Adım çeşitliliği (saf kurallar) ----

        /// <summary>Tek adım varyantı: ana ses, perde/ses çarpanı ve isteğe bağlı ek katman (ahşap esneme vb.).</summary>
        public struct StepRecipe
        {
            public SoundId Id;
            public float Pitch;
            public float Volume;
            public SoundId Extra;
            public float ExtraVolume;
        }

        public const int StepVariantsPerSurface = 5;

        /// <summary>Bot adımlarının oyuncununkine göre çarpanı (%15 kısık).</summary>
        public const float BotStepVolumeScale = 0.85f;

        /// <summary>Yerel oyuncunun kendi adımı (hafif kısık).</summary>
        public const float LocalStepVolumeScale = 0.8f;

        /// <summary>Bot adımı ne kadar kısılırsa kısılsın (çömelme dahil) bu düzeyin altına inmez: okunabilirlik.</summary>
        public const float MinAudibleStepVolume = 0.12f;

        public static int StepVariantCount(SurfaceKind kind) => StepVariantsPerSurface;

        public static StepRecipe StepVariant(SurfaceKind kind, int variant)
        {
            var v = ((variant % StepVariantsPerSurface) + StepVariantsPerSurface) % StepVariantsPerSurface;
            var baseId = FootstepFor(kind);
            var r = new StepRecipe { Id = baseId, Pitch = 1f, Volume = 1f, Extra = SoundId.None, ExtraVolume = 0f };
            switch (kind)
            {
                case SurfaceKind.Foliage: // kuru ot hışırtısı
                    if (v == 1) { r.Pitch = 0.92f; r.Volume = 0.9f; }
                    else if (v == 2) { r.Pitch = 1.08f; r.Volume = 0.85f; }
                    else if (v == 3) { r.Pitch = 1.0f; r.Extra = SoundId.ClothRustle; r.ExtraVolume = 0.3f; }
                    else if (v == 4) { r.Pitch = 0.85f; r.Volume = 1.05f; }
                    break;
                case SurfaceKind.Snow: // gıcırt
                    if (v == 1) { r.Pitch = 0.9f; r.Volume = 1.05f; }
                    else if (v == 2) { r.Pitch = 1.12f; r.Volume = 0.8f; }
                    else if (v == 3) { r.Pitch = 1.0f; r.Extra = SoundId.FootstepGravel; r.ExtraVolume = 0.15f; }
                    else if (v == 4) { r.Pitch = 0.82f; }
                    break;
                case SurfaceKind.Concrete: // tok
                    if (v == 1) { r.Pitch = 0.9f; r.Volume = 1.1f; }
                    else if (v == 2) { r.Pitch = 1.1f; r.Volume = 0.9f; }
                    else if (v == 3) { r.Id = SoundId.FootstepGravel; r.Pitch = 0.95f; r.Volume = 0.8f; r.Extra = baseId; r.ExtraVolume = 0.5f; }
                    else if (v == 4) { r.Pitch = 0.8f; r.Volume = 1.15f; }
                    break;
                case SurfaceKind.Wood: // gıcırtı + esneme
                    if (v == 1) { r.Pitch = 0.9f; r.Extra = SoundId.WoodCreak; r.ExtraVolume = 0.45f; }
                    else if (v == 2) { r.Pitch = 1.1f; r.Volume = 0.9f; }
                    else if (v == 3) { r.Pitch = 0.95f; r.Extra = SoundId.WoodCreak; r.ExtraVolume = 0.7f; }
                    else if (v == 4) { r.Pitch = 1.05f; r.Volume = 1.05f; }
                    break;
                case SurfaceKind.Metal: // tınlama
                    if (v == 1) { r.Pitch = 0.88f; r.Volume = 1.05f; }
                    else if (v == 2) { r.Pitch = 1.15f; r.Volume = 0.9f; }
                    else if (v == 3) { r.Pitch = 1.0f; r.Extra = SoundId.GearJingle; r.ExtraVolume = 0.25f; }
                    else if (v == 4) { r.Pitch = 0.78f; r.Volume = 1.1f; }
                    break;
                case SurfaceKind.Water:
                    r.Id = SoundId.FootstepMud; r.Pitch = 1.1f + 0.04f * v; r.Volume = 0.9f;
                    break;
                default: // toprak: normal / çamur vakumu / çakıl
                    if (v == 1) { r.Id = SoundId.FootstepMud; r.Pitch = 1f; r.Volume = 0.95f; }
                    else if (v == 2) { r.Id = SoundId.FootstepGravel; r.Pitch = 1.05f; r.Volume = 0.9f; }
                    else if (v == 3) { r.Pitch = 0.9f; r.Volume = 1.05f; }
                    else if (v == 4) { r.Id = SoundId.FootstepMud; r.Pitch = 0.9f; r.Volume = 1.05f; r.Extra = SoundId.Footstep; r.ExtraVolume = 0.3f; }
                    break;
            }

            return r;
        }

        /// <summary>Bir önceki varyanttan farklı varyant seç (r01: [0,1) rastgele).</summary>
        public static int NextStepVariant(SurfaceKind kind, int last, float r01)
        {
            var n = StepVariantCount(kind);
            if (last < 0 || last >= n)
                return Mathf.Clamp((int)(r01 * n), 0, n - 1);
            var pick = Mathf.Clamp((int)(r01 * (n - 1)), 0, n - 2);
            return pick >= last ? pick + 1 : pick;
        }

        /// <summary>Yürüme yumuşak, koşu sert; çömelme/sürünme sessiz.</summary>
        public static float GaitVolume(Stance stance, bool sprinting)
        {
            switch (stance)
            {
                case Stance.Crouching: return 0.4f;
                case Stance.Prone: return 0.25f;
                default: return sprinting ? 1.3f : 0.85f;
            }
        }

        /// <summary>Koşuda teçhizat şıngırtısı her adımda, yürümede yok, çömelmede yok.</summary>
        public static bool GearJingleOnStep(Stance stance, bool sprinting, int stepCount)
            => stance == Stance.Standing && sprinting && stepCount % 2 == 0;

        /// <summary>Kimin adımı: yerel oyuncu hafif kısık, bot %15 kısık, diğerleri tam.</summary>
        public static float ActorStepScale(bool isLocalPlayer, bool isBot)
            => isLocalPlayer ? LocalStepVolumeScale : isBot ? BotStepVolumeScale : 1f;

        /// <summary>Botun adımı çömelmede bile duyulabilir kalır (zemin), oyuncununki serbest.</summary>
        public static float FinalStepVolume(float volume, bool isLocalPlayer, bool isBot)
            => isBot ? Mathf.Max(MinAudibleStepVolume, volume) : volume;

        /// <summary>İniş: yüzeye göre iniş sesinin (ses, perde) çarpanı ve ek yüzey katmanı.</summary>
        public static void LandingTweak(SurfaceKind kind, out float volume, out float pitch, out SoundId layer)
        {
            layer = FootstepFor(kind);
            switch (kind)
            {
                case SurfaceKind.Metal: volume = 1.15f; pitch = 1.1f; break;
                case SurfaceKind.Concrete: volume = 1.1f; pitch = 1f; break;
                case SurfaceKind.Wood: volume = 1f; pitch = 0.9f; break;
                case SurfaceKind.Snow: volume = 0.7f; pitch = 0.85f; break;
                case SurfaceKind.Foliage: volume = 0.75f; pitch = 0.9f; break;
                case SurfaceKind.Water: volume = 0.8f; pitch = 0.8f; layer = SoundId.FootstepMud; break;
                default: volume = 0.85f; pitch = 0.88f; break;
            }
        }

        public static SoundId ReloadFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return SoundId.ReloadPistol;
                case WeaponCategory.Shotgun: return SoundId.ReloadShotgun;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr: return SoundId.ReloadSniper;
                case WeaponCategory.Lmg: return SoundId.ReloadLmg;
                case WeaponCategory.Melee:
                case WeaponCategory.None: return SoundId.None;
                default: return SoundId.ReloadRifle;
            }
        }

        /// <summary>Uzak atış perdesi: ağır silahlar daha derin.</summary>
        public static float DistantPitch(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return 1.15f;
                case WeaponCategory.Smg: return 1.1f;
                case WeaponCategory.Sniper: return 0.85f;
                case WeaponCategory.Lmg:
                case WeaponCategory.Dmr: return 0.92f;
                case WeaponCategory.Shotgun: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>Kovanın düştüğü yüzeye göre (ses düzeyi, perde) çarpanları.</summary>
        public static void CasingTweak(SurfaceKind kind, out float volume, out float pitch)
        {
            switch (kind)
            {
                case SurfaceKind.Metal: volume = 1f; pitch = 1.08f; break;
                case SurfaceKind.Concrete: volume = 0.9f; pitch = 1f; break;
                case SurfaceKind.Wood: volume = 0.7f; pitch = 0.88f; break;
                case SurfaceKind.Foliage:
                case SurfaceKind.Dirt: volume = 0.35f; pitch = 0.8f; break;
                case SurfaceKind.Water: volume = 0.3f; pitch = 0.7f; break;
                default: volume = 0.6f; pitch = 0.95f; break;
            }
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[4];

        /// <summary>Noktanın altındaki yüzeyi (aşağı ışın + GameVfx.Classify) bulur; bulunamazsa Default.</summary>
        public static SurfaceKind SampleSurface(Vector3 position, float rayHeight = 0.4f)
        {
            try
            {
                var count = Physics.RaycastNonAlloc(position + Vector3.up * rayHeight, Vector3.down, Hits, rayHeight + 1.2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                var bestDist = float.MaxValue;
                var best = -1;
                for (var i = 0; i < count; i++)
                {
                    var c = Hits[i].collider;
                    if (c == null || Hits[i].distance <= 0f || c.attachedRigidbody != null && c.GetComponent<CharacterController>() != null)
                        continue;
                    if (Hits[i].distance < bestDist) { bestDist = Hits[i].distance; best = i; }
                }

                if (best < 0)
                    return SurfaceKind.Default;

                var kind = GameVfx.Classify(Hits[best].collider, Hits[best].point);
                if (kind == SurfaceKind.Default && Hits[best].collider is TerrainCollider)
                    kind = TerrainKind(Hits[best].collider as TerrainCollider, Hits[best].point);
                return kind == SurfaceKind.Flesh ? SurfaceKind.Default : kind;
            }
            catch (System.Exception)
            {
                return SurfaceKind.Default;
            }
        }

        /// <summary>Arazide baskın boya katmanının adı çim/yaprak ise Foliage (çim ayak sesi), kaya/yol ise Concrete.</summary>
        private static SurfaceKind TerrainKind(TerrainCollider collider, Vector3 point)
        {
            var terrain = collider.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return SurfaceKind.Dirt;

            var data = terrain.terrainData;
            var layers = data.terrainLayers;
            if (layers == null || layers.Length == 0)
                return SurfaceKind.Dirt;

            var local = point - terrain.GetPosition();
            var size = data.size;
            var u = Mathf.Clamp01(local.x / Mathf.Max(0.01f, size.x));
            var v = Mathf.Clamp01(local.z / Mathf.Max(0.01f, size.z));
            var ax = Mathf.Clamp((int)(u * data.alphamapWidth), 0, data.alphamapWidth - 1);
            var az = Mathf.Clamp((int)(v * data.alphamapHeight), 0, data.alphamapHeight - 1);
            var map = data.GetAlphamaps(ax, az, 1, 1);
            var bestLayer = 0;
            var bestWeight = -1f;
            for (var i = 0; i < layers.Length && i < map.GetLength(2); i++)
            {
                if (map[0, 0, i] > bestWeight) { bestWeight = map[0, 0, i]; bestLayer = i; }
            }

            return KindFromLayerName(layers[bestLayer] != null ? layers[bestLayer].name : null);
        }

        public static SurfaceKind KindFromLayerName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return SurfaceKind.Dirt;

            var n = name.ToLowerInvariant();
            if (n.Contains("grass") || n.Contains("çim") || n.Contains("cim") || n.Contains("meadow") || n.Contains("leaf") || n.Contains("moss"))
                return SurfaceKind.Foliage;
            if (n.Contains("rock") || n.Contains("stone") || n.Contains("road") || n.Contains("asphalt") || n.Contains("cliff") || n.Contains("kaya"))
                return SurfaceKind.Concrete;
            return SurfaceKind.Dirt;
        }
    }
}
