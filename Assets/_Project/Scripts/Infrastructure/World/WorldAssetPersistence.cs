using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Editörde (oynatma dışı) KALICI bir TerrainData varlığına üretim yapılırken, varlığın başvurduğu bellek içi nesneleri
    /// aynı klasöre varlık olarak kaydeder: arazi katmanları (+ dokuları) ve ağaç prototipleri (prefab + mesh + malzeme/doku).
    /// Aksi halde TerrainData (varlık) sahne-içi/bellek-içi nesnelere başvuramayacağı için kaydedince dokular ve ağaçlar kaybolur.
    /// Oyuncu derlemesinde ve oynatmada hiçbir şey yapmaz. Mesh/malzeme gibi SAHNE nesnelerinin başvurdukları (kaya, su, köprü)
    /// sahneyle birlikte kaydedilir; NavMesh ve mini harita editör kurulumunca kaydedilir.
    /// </summary>
    public static class WorldAssetPersistence
    {
        public const string TreePrefabPrefix = "Tree_";

        /// <summary>Bu TerrainData için varlık kalıcılaştırma yapılmalı mı (editör, oynatma dışı, data bir varlık)?</summary>
        public static bool ShouldPersist(Object terrainDataAsset)
        {
#if UNITY_EDITOR
            return terrainDataAsset != null && !UnityEngine.Application.isPlaying && AssetDatabase.Contains(terrainDataAsset);
#else
            return false;
#endif
        }

        /// <summary>Varlığın bulunduğu klasör ("Assets/..."); bulunamazsa null.</summary>
        public static string FolderOf(Object asset)
        {
#if UNITY_EDITOR
            if (asset == null)
                return null;
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return null;
            var slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : null;
#else
            return null;
#endif
        }

        /// <summary>Katmanları (ve dokularını) klasöre kaydeder; kaydedilen (varlık) katmanları döner. Hata olursa girişi döner.</summary>
        public static TerrainLayer[] PersistLayers(TerrainLayer[] layers, string folder)
        {
#if UNITY_EDITOR
            if (layers == null || string.IsNullOrEmpty(folder))
                return layers;

            var result = new TerrainLayer[layers.Length];
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                result[i] = layer;
                if (layer == null || AssetDatabase.Contains(layer))
                    continue;

                try
                {
                    var baseName = folder + "/" + SafeName(layer.name);
                    if (layer.diffuseTexture != null && !AssetDatabase.Contains(layer.diffuseTexture))
                        layer.diffuseTexture = SaveAsset(layer.diffuseTexture, baseName + "_Diffuse.asset");
                    if (layer.normalMapTexture != null && !AssetDatabase.Contains(layer.normalMapTexture))
                        layer.normalMapTexture = SaveAsset(layer.normalMapTexture, baseName + "_Normal.asset");
                    result[i] = SaveAsset(layer, baseName + ".terrainlayer");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[WorldAssetPersistence] Katman kaydedilemedi (" + layer.name + "): " + e.Message);
                }
            }

            return result;
#else
            return layers;
#endif
        }

        /// <summary>
        /// Ağaç prototip nesnelerini prefab olarak kaydeder (önce mesh, malzeme ve dokuları varlık yapılır). Kaydedilen prefab
        /// varlıklarını döner (indeks korunur); bir prototip kaydedilemezse o indekste sahne nesnesi kalır.
        /// </summary>
        public static GameObject[] PersistTreePrototypes(IReadOnlyList<GameObject> prototypes, string folder)
        {
            var count = prototypes != null ? prototypes.Count : 0;
            var result = new GameObject[count];
            for (var i = 0; i < count; i++)
                result[i] = prototypes[i];
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(folder))
                return result;

            for (var i = 0; i < count; i++)
            {
                var go = prototypes[i];
                if (go == null || EditorUtility.IsPersistent(go))
                    continue;

                try
                {
                    var filter = go.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh))
                    {
                        var mesh = Object.Instantiate(filter.sharedMesh);
                        mesh.name = filter.sharedMesh.name;
                        filter.sharedMesh = SaveAsset(mesh, folder + "/" + TreePrefabPrefix + i + "_Mesh.asset");
                    }

                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        var materials = renderer.sharedMaterials;
                        for (var m = 0; m < materials.Length; m++)
                            materials[m] = PersistMaterial(materials[m], folder);
                        renderer.sharedMaterials = materials;
                    }

                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + TreePrefabPrefix + i + ".prefab", out var success);
                    if (success && prefab != null)
                        result[i] = prefab;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[WorldAssetPersistence] Ağaç prefabı kaydedilemedi (" + go.name + "): " + e.Message);
                }
            }
#endif
            return result;
        }

        /// <summary>Editörde varlığı kirli işaretler (kaydedilsin). Oyuncuda/oynatmada etkisiz.</summary>
        public static void MarkDirty(Object asset)
        {
#if UNITY_EDITOR
            if (asset != null && !UnityEngine.Application.isPlaying && AssetDatabase.Contains(asset))
                EditorUtility.SetDirty(asset);
#endif
        }

#if UNITY_EDITOR
        private static readonly List<int> TexturePropertyIds = new List<int>(8);

        /// <summary>Malzemeyi (ve bellek içi dokularını) varlık yapar; zaten varlıksa aynen döner.</summary>
        private static Material PersistMaterial(Material material, string folder)
        {
            if (material == null || AssetDatabase.Contains(material))
                return material;

            var baseName = folder + "/Mat_" + SafeName(material.name);
            TexturePropertyIds.Clear();
            material.GetTexturePropertyNameIDs(TexturePropertyIds);
            for (var i = 0; i < TexturePropertyIds.Count; i++)
            {
                var id = TexturePropertyIds[i];
                if (!(material.GetTexture(id) is Texture2D texture) || AssetDatabase.Contains(texture))
                    continue;
                material.SetTexture(id, SaveAsset(texture, baseName + "_Tex" + i + ".asset"));
            }

            return SaveAsset(material, baseName + ".mat");
        }

        /// <summary>Nesneyi yola varlık olarak kaydeder (varsa üzerine yazar) ve kaydedilen nesneyi döner.</summary>
        private static T SaveAsset<T>(T obj, string path) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }
#endif

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
