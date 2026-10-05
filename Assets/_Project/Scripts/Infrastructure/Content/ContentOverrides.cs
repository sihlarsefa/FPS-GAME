using System;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Infrastructure.Content
{
    /// <summary>
    /// Hazır varlık (Asset Store / Mixamo / Sonniss / Poly Haven) eşlemeleri.
    /// Yoksa <c>TryGet*</c> false döner; çağıran prosedürel yedeğe düşer.
    /// Kaynak yolu: <c>Resources/ContentOverrides</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "HAREKAT/Content Overrides", fileName = "ContentOverrides")]
    public sealed class ContentOverrides : ScriptableObject
    {
        public const string ResourcePath = "ContentOverrides";
        public const string AssetPath = "Assets/_Project/Resources/ContentOverrides.asset";

        [Header("Silahlar")]
        public WeaponOverrideEntry[] weapons = Array.Empty<WeaponOverrideEntry>();

        [Header("Sesler")]
        public SoundOverrideEntry[] sounds = Array.Empty<SoundOverrideEntry>();

        [Header("Malzemeler")]
        public MaterialOverrideEntry[] materials = Array.Empty<MaterialOverrideEntry>();

        [Header("Asker")]
        public SoldierOverrideEntry soldier = new SoldierOverrideEntry();

        [Header("Araçlar (kirpi, t70)")]
        public VehicleOverrideEntry[] vehicles = Array.Empty<VehicleOverrideEntry>();

        [Header("Binalar (isteğe bağlı)")]
        public BuildingOverrideEntry[] buildings = Array.Empty<BuildingOverrideEntry>();

        private static ContentOverrides _cached;
        private static bool _loadAttempted;

        /// <summary>Resources'tan yükler; yoksa null. Sonuç oturum boyunca önbelleklenir.</summary>
        public static ContentOverrides Load()
        {
            if (_loadAttempted)
                return _cached;

            _loadAttempted = true;
            _cached = Resources.Load<ContentOverrides>(ResourcePath);
            return _cached;
        }

        /// <summary>Editör veya test için önbelleği temizler.</summary>
        public static void InvalidateCache()
        {
            _cached = null;
            _loadAttempted = false;
        }

        // ------------------------------------------------------------------ TryGet*

        public static bool TryGetWeapon(string weaponId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(weaponId))
                return false;
            if (!TryFindWeapon(weaponId, out var entry) || entry.prefab == null)
                return false;
            prefab = entry.prefab;
            return true;
        }

        public static bool TryGetWeapon(string weaponId, out WeaponOverrideEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(weaponId))
                return false;
            return TryFindWeapon(weaponId, out entry) && entry.prefab != null;
        }

        public static bool TryGetSound(SoundId id, out AudioClip clip)
        {
            return TryGetSound(id, out clip, out _, out _);
        }

        public static bool TryGetSound(SoundId id, out AudioClip clip, out float volume, out float pitch)
        {
            clip = null;
            volume = 1f;
            pitch = 1f;
            if (id == SoundId.None)
                return false;

            var data = Load();
            if (data == null || data.sounds == null)
                return false;

            for (var i = 0; i < data.sounds.Length; i++)
            {
                var entry = data.sounds[i];
                if (entry == null || entry.soundId != id || entry.clips == null || entry.clips.Length == 0)
                    continue;

                clip = PickClip(entry.clips);
                if (clip == null)
                    continue;

                volume = RandomRange(entry.volumeMin, entry.volumeMax, 1f);
                pitch = RandomRange(entry.pitchMin, entry.pitchMax, 1f);
                return true;
            }

            return false;
        }

        public static bool TryGetMaterial(MaterialId id, out Material material)
        {
            material = null;
            var data = Load();
            if (data == null || data.materials == null)
                return false;

            for (var i = 0; i < data.materials.Length; i++)
            {
                var entry = data.materials[i];
                if (entry == null || entry.materialId != id || entry.material == null)
                    continue;
                material = entry.material;
                return true;
            }

            return false;
        }

        public static bool TryGetSoldier(out GameObject humanoidPrefab, out RuntimeAnimatorController animator)
        {
            humanoidPrefab = null;
            animator = null;
            var data = Load();
            if (data == null || data.soldier == null)
                return false;

            if (data.soldier.humanoidPrefab == null)
                return false;

            humanoidPrefab = data.soldier.humanoidPrefab;
            animator = data.soldier.animatorController;
            return true;
        }

        public static bool TryGetVehicle(string vehicleId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrEmpty(vehicleId))
                return false;

            var data = Load();
            if (data == null || data.vehicles == null)
                return false;

            for (var i = 0; i < data.vehicles.Length; i++)
            {
                var entry = data.vehicles[i];
                if (entry == null || entry.prefab == null)
                    continue;
                if (!string.Equals(entry.vehicleId, vehicleId, StringComparison.OrdinalIgnoreCase))
                    continue;
                prefab = entry.prefab;
                return true;
            }

            return false;
        }

        public static bool TryGetKirpi(out GameObject prefab) => TryGetVehicle(ContentIds.Kirpi, out prefab);

        public static bool TryGetHelicopter(out GameObject prefab) => TryGetVehicle(ContentIds.Helicopter, out prefab);

        public static bool TryGetBuilding(BuildingStyle style, out GameObject prefab)
        {
            prefab = null;
            var data = Load();
            if (data == null || data.buildings == null)
                return false;

            for (var i = 0; i < data.buildings.Length; i++)
            {
                var entry = data.buildings[i];
                if (entry == null || entry.style != style || entry.prefabs == null || entry.prefabs.Length == 0)
                    continue;

                prefab = PickPrefab(entry.prefabs);
                if (prefab != null)
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ helpers

        private static bool TryFindWeapon(string weaponId, out WeaponOverrideEntry entry)
        {
            entry = null;
            var data = Load();
            if (data == null || data.weapons == null)
                return false;

            for (var i = 0; i < data.weapons.Length; i++)
            {
                var candidate = data.weapons[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.weaponId))
                    continue;
                if (!string.Equals(candidate.weaponId, weaponId, StringComparison.OrdinalIgnoreCase))
                    continue;
                entry = candidate;
                return true;
            }

            return false;
        }

        private static AudioClip PickClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
                return null;

            var start = Random.Range(0, clips.Length);
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[(start + i) % clips.Length];
                if (clip != null)
                    return clip;
            }

            return null;
        }

        private static GameObject PickPrefab(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
                return null;

            var start = Random.Range(0, prefabs.Length);
            for (var i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[(start + i) % prefabs.Length];
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static float RandomRange(float min, float max, float fallback)
        {
            if (!(min > 0.0001f) && !(max > 0.0001f))
                return fallback;
            if (max < min)
            {
                var tmp = min;
                min = max;
                max = tmp;
            }

            return min >= max - 0.0001f ? min : Random.Range(min, max);
        }
    }
}
