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

        public const string TreeVersionFile = "TreePrototypes.version.txt";

        /// <summary>Klasördeki kalıcı ağaç prototipleri eski mesh sürümünden mi (damga yok/farklı)? Evetse yeniden üretilmeli.</summary>
        public static bool TreePrototypesStale(string folder)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(folder))
                return false;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/" + TreeVersionFile);
            return asset == null || asset.text.Trim() != TreeMeshes.MeshVersion.ToString();
#else
            return false;
#endif
        }

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

            // Sürüm damgası: eski mesh sürümüyle kalıcılaştırılmış varlıklar her durumda yeniden yazılır.
            var stale = TreePrototypesStale(folder);
            if (stale)
            {
                var stampPath = folder + "/" + TreeVersionFile;
                System.IO.File.WriteAllText(stampPath, TreeMeshes.MeshVersion.ToString());
                AssetDatabase.ImportAsset(stampPath);
            }

            for (var i = 0; i < count; i++)
            {
                var go = prototypes[i];
                if (go == null || EditorUtility.IsPersistent(go))
                    continue;

                try
                {
                    // Kök ve LOD çocukları (LOD0/1/2): her MeshFilter/MeshRenderer kalıcılaştırılır.
                    var filters = go.GetComponentsInChildren<MeshFilter>(true);
                    for (var f = 0; f < filters.Length; f++)
                    {
                        var filter = filters[f];
                        if (filter.sharedMesh == null || AssetDatabase.Contains(filter.sharedMesh))
                            continue;
                        var mesh = Object.Instantiate(filter.sharedMesh);
                        mesh.name = filter.sharedMesh.name;
                        var suffix = filter.transform == go.transform ? string.Empty : "_" + SafeName(filter.name);
                        filter.sharedMesh = SaveAsset(mesh, folder + "/" + TreePrefabPrefix + i + suffix + "_Mesh.asset");
                    }

                    var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
                    for (var r = 0; r < renderers.Length; r++)
                    {
                        var materials = renderers[r].sharedMaterials;
                        for (var m = 0; m < materials.Length; m++)
                            materials[m] = PersistMaterial(materials[m], folder);
                        renderers[r].sharedMaterials = materials;
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

        /// <summary>Ayrıntı (çimen/çiçek) prototip dokularını klasöre varlık olarak kaydeder (bellek içi doku kalıcı TerrainData'da kaybolmasın).</summary>
        public static void PersistDetailTextures(TerrainData data, string folder)
        {
#if UNITY_EDITOR
            if (data == null || string.IsNullOrEmpty(folder))
                return;
            var prototypes = data.detailPrototypes;
            if (prototypes == null || prototypes.Length == 0)
                return;
            for (var i = 0; i < prototypes.Length; i++)
            {
                var tex = prototypes[i].prototypeTexture;
                if (tex == null || AssetDatabase.Contains(tex))
                    continue;
                try
                {
                    prototypes[i].prototypeTexture = SaveAsset(tex, folder + "/Detail_" + i + "_Tex.asset");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[WorldAssetPersistence] Ayrıntı dokusu kaydedilemedi: " + e.Message);
                }
            }

            data.detailPrototypes = prototypes;
#endif
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
