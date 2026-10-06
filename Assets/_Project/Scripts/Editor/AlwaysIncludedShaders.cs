using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Assets/_Project/Shaders altındaki "HAREKAT/..." gölgelendiricilerini Graphics > Always Included Shaders listesine ekler
    /// (Shader.Find build'de bulabilsin). Tekrar çalıştırmak güvenlidir (idempotent).
    /// </summary>
    public static class AlwaysIncludedShaders
    {
        private const string ShaderFolder = "Assets/_Project/Shaders";

        [MenuItem("HAREKÂT/Kurulum/Gölgelendiricileri Build'e Dahil Et", priority = 20)]
        public static void EnsureAll()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogWarning("[HAREKÂT] GraphicsSettings okunamadı; Always Included Shaders atlandı.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[HAREKÂT] m_AlwaysIncludedShaders bulunamadı; elle ekleyin.");
                return;
            }

            var present = new HashSet<Object>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null) present.Add(o);
            }

            var added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { ShaderFolder }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader == null || !shader.name.StartsWith("HAREKAT/") || present.Contains(shader))
                    continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                present.Add(shader);
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[HAREKÂT] Always Included Shaders: " + added + " yeni HAREKAT gölgelendirici eklendi.");
        }
    }
}
