using System.Collections.Generic;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// CC0 gerçek ses kaynaklarını (Resources/Audio/SFX) ContentOverrides SoundId yuvalarına bağlar.
    /// Silah/ortam klipleri Resources klasör keşfiyle çalışır; burada yalnız SoundId tabanlı olanlar (patlama) bağlanır.
    /// Batch: -executeMethod Project.EditorTools.AudioSourceBinder.BindBatch
    /// </summary>
    public static class AudioSourceBinder
    {
        private const string ExplosionFolder = "Assets/_Project/Resources/Audio/SFX/Explosion";

        [MenuItem("HAREKÂT/İçerik/Gerçek Ses Kaynaklarını Bağla")]
        public static void Bind() => BindInternal();

        public static void BindBatch()
        {
            var n = BindInternal();
            Debug.Log("[AudioSourceBinder] bağlanan SoundId yuvası: " + n);
        }

        private static int BindInternal()
        {
            var asset = ContentOverridesSetup.EnsureAsset();
            if (asset == null) return 0;
            var bound = 0;
            if (BindFolder(asset, SoundId.Explosion, ExplosionFolder, 0.95f, 1.05f, 0.94f, 1.06f)) bound++;
            if (bound > 0)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
            }
            return bound;
        }

        private static bool BindFolder(ContentOverrides asset, SoundId id, string folder, float vMin, float vMax, float pMin, float pMax)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return false;
            var clips = new List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) clips.Add(c);
            }
            if (clips.Count == 0) return false;
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var list = new List<SoundOverrideEntry>(asset.sounds ?? new SoundOverrideEntry[0]);
            SoundOverrideEntry entry = null;
            foreach (var e in list)
                if (e != null && e.soundId == id) { entry = e; break; }
            if (entry == null)
            {
                entry = new SoundOverrideEntry { soundId = id };
                list.Add(entry);
            }
            entry.clips = clips.ToArray();
            entry.volumeMin = vMin; entry.volumeMax = vMax; entry.pitchMin = pMin; entry.pitchMax = pMax;
            asset.sounds = list.ToArray();
            return true;
        }
    }
}
