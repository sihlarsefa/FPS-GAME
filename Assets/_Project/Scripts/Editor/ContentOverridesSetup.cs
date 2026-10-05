using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>ContentOverrides.asset oluşturma / yükleme (Editor).</summary>
    public static class ContentOverridesSetup
    {
        public static ContentOverrides EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ContentOverrides>(ContentOverrides.AssetPath);
            if (existing != null)
            {
                ContentOverrides.InvalidateCache();
                return existing;
            }

            EnsureResourcesFolder();
            var created = ScriptableObject.CreateInstance<ContentOverrides>();
            AssetDatabase.CreateAsset(created, ContentOverrides.AssetPath);
            AssetDatabase.SaveAssets();
            ContentOverrides.InvalidateCache();
            return created;
        }

        private static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                    AssetDatabase.CreateFolder("Assets", "_Project");
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            }
        }
    }
}
