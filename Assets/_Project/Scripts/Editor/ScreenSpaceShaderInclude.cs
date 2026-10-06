using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.EditorTools
{
    /// <summary>
    /// Shaders/ScreenSpace altındaki HAREKAT gölgelendiricilerini Graphics Settings "Always Included Shaders" listesine ekler.
    /// Çalışma zamanı Shader.Find("HAREKAT/ScreenSpace/...") derlenmiş oyunda yalnız bu liste (veya Resources) ile bulur.
    /// Menü: HAREKÂT/Render/Ekran-Uzayı Gölgelendiricilerini Dahil Et (idempotent).
    /// </summary>
    public static class ScreenSpaceShaderInclude
    {
        private const string ShaderFolder = "Assets/_Project/Shaders/ScreenSpace";

        [MenuItem("HAREKÂT/Render/Ekran-Uzayı Gölgelendiricilerini Dahil Et", priority = 200)]
        public static void IncludeMenu()
        {
            var added = Include();
            Debug.Log($"[Render] Ekran-uzayı gölgelendiricileri: {added} yeni kayıt eklendi (Always Included Shaders).");
        }

        public static int Include()
        {
            var shaders = new List<Shader>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { ShaderFolder }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader != null && shader.name.StartsWith("HAREKAT/"))
                    shaders.Add(shader);
            }

            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                if (all != null && all.Length > 0)
                    gs = all[0] as GraphicsSettings;
            }
            if (gs == null)
                return 0;

            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null)
                return 0;

            var added = 0;
            foreach (var shader in shaders)
            {
                var exists = false;
                for (var i = 0; i < arr.arraySize; i++)
                {
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                    continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            return added;
        }
    }
}
