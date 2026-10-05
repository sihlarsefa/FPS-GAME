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
}
