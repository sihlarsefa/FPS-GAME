using System;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Content
{
    [Serializable]
    public sealed class WeaponOverrideEntry
    {
        [Tooltip("WeaponIds sabiti, örn. ar_mpt76")]
        public string weaponId;

        public GameObject prefab;

        [Tooltip("Namlu ucu Transform adı (varsayılan Muzzle).")]
        public string muzzleName = ContentIds.WeaponMuzzle;

        [Tooltip("Sağ el tutamağı Transform adı.")]
        public string gripRName = ContentIds.WeaponGripR;

        [Tooltip("Sol el tutamağı Transform adı.")]
        public string gripLName = ContentIds.WeaponGripL;
    }

    [Serializable]
    public sealed class SoundOverrideEntry
    {
        public SoundId soundId;
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 2f)] public float volumeMin = 0.95f;
        [Range(0f, 2f)] public float volumeMax = 1.05f;
        [Range(0.5f, 2f)] public float pitchMin = 0.97f;
        [Range(0.5f, 2f)] public float pitchMax = 1.03f;
    }

    [Serializable]
    public sealed class MaterialOverrideEntry
    {
        public MaterialId materialId;
        public Material material;
    }

    [Serializable]
    public sealed class SoldierOverrideEntry
    {
        [Tooltip("Humanoid rig'li asker prefab'ı (Mixamo vb.).")]
        public GameObject humanoidPrefab;

        [Tooltip("Lokomosyon + nişan + reload + death Animator Controller.")]
        public RuntimeAnimatorController animatorController;
    }

    [Serializable]
    public sealed class VehicleOverrideEntry
    {
        [Tooltip("ContentIds.Kirpi / ContentIds.Helicopter")]
        public string vehicleId;

        public GameObject prefab;
    }

    [Serializable]
    public sealed class BuildingOverrideEntry
    {
        public BuildingStyle style;
        public GameObject[] prefabs = Array.Empty<GameObject>();
    }

    /// <summary>Arazi katmanı (Grass/Dirt/… → TerrainLayer).</summary>
    [Serializable]
    public sealed class TerrainLayerOverrideEntry
    {
        public MaterialId materialId;
        public TerrainLayer layer;
    }

    /// <summary>Ağaç/çalı türü → LOD’lu prefab listesi.</summary>
    [Serializable]
    public sealed class VegetationOverrideEntry
    {
        [Tooltip("pine / oak / bush / dead vb. ContentIds.Veg*")]
        public string speciesId;
        public GameObject[] prefabs = Array.Empty<GameObject>();
    }

    [Serializable]
    public sealed class RockOverrideEntry
    {
        [Tooltip("small / medium / large")]
        public string sizeClass = "medium";
        public GameObject[] prefabs = Array.Empty<GameObject>();
    }

    [Serializable]
    public sealed class PropOverrideEntry
    {
        public string propId;
        public GameObject prefab;
    }

    [Serializable]
    public sealed class ViewmodelArmsOverrideEntry
    {
        public GameObject armsPrefab;
        public Material gloveMaterial;
    }

    [Serializable]
    public sealed class WeaponAnimationOverrideEntry
    {
        public string weaponId;
        public AnimatorOverrideController overrideController;
    }

    [Serializable]
    public sealed class SkyOverrideEntry
    {
        [Tooltip("day_clear / cloudy / sunset")]
        public string skyId = "day_clear";
        public Cubemap hdri;
        [Range(0.1f, 8f)] public float exposure = 1f;
    }

    [Serializable]
    public sealed class DecalSetOverrideEntry
    {
        public Project.Infrastructure.Vfx.SurfaceKind surface;
        public Material decalMaterial;
    }
}
