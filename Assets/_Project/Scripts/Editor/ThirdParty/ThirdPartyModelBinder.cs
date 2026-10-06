using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Poly Haven / CC0 FBX modellerini işler ve ContentOverrides vegetation / rocks / props slotlarına bağlar:
    /// ModelImporter ayarı (1 birim = 1 m, collider/okuma kapalı), URP/Lit materyal üretimi (alfa kırpmalı yaprak dahil),
    /// LODGroup (modelde _LODn varsa onlar, yoksa köşe-kümeleme ile üretilmiş ucuz LOD1), Assets/ThirdParty/Prefabs/&lt;id&gt;.prefab.
    /// BatchEntry.BindThirdParty yansıma ile çağırır. Her varlık ayrı try/catch'te; bozuk model diğerlerini durdurmaz
    /// (slot boş kalırsa prosedürel yedek geçerli).
    /// </summary>
    public static class ThirdPartyModelBinder
    {
        public const string ModelRoot = "Assets/ThirdParty/Models";
        public const string PrefabRoot = "Assets/ThirdParty/Prefabs";
        public const string MeshRoot = "Assets/ThirdParty/Prefabs/Meshes";
        public const string MaterialRoot = "Assets/ThirdParty/Materials/PH";
        private const string LitShader = "Universal Render Pipeline/Lit";

        private enum Kind { Veg, Rock, Prop }

        private sealed class Map
        {
            public Kind Kind;
            public string Id;       // veg: speciesId, rock: sizeClass, prop: propId
            public string Folder;   // Models/<Folder>
            public float Target;    // >0: kaya için en büyük boyut (m), bitki için yükseklik (m). 0 = doğal ölçek.
            public bool Collider = true;
            public Map(Kind kind, string id, string folder, float target = 0f, bool collider = true)
            { Kind = kind; Id = id; Folder = folder; Target = target; Collider = collider; }
        }

        // Klasör adı (Models/<folder>/…) → ContentIds slot. Sıra = slot içi öncelik (props: ilk bulunan kazanır).
        private static readonly Map[] Maps =
        {
            // Çam: fidanlar 7 m'ye ölçeklenir (arazi ağaç prototipi). Meşe: jacaranda ~3.8M üçgen → oyun dışı, prosedürel kalır.
            new Map(Kind.Veg, ContentIds.VegPine, "fir_sapling", 7.5f),
            new Map(Kind.Veg, ContentIds.VegPine, "pine_sapling_small", 6.5f),
            new Map(Kind.Veg, ContentIds.VegBush, "shrub_01"),
            new Map(Kind.Veg, ContentIds.VegBush, "shrub_02"),
            new Map(Kind.Veg, ContentIds.VegBush, "shrub_03"),
            new Map(Kind.Veg, ContentIds.VegBush, "shrub_04"),
            new Map(Kind.Veg, ContentIds.VegBush, "fern_02"),
            new Map(Kind.Veg, "grass", "grass_medium_01"),
            new Map(Kind.Veg, "grass", "grass_medium_02"),

            // Kayalar: sınıf başına en büyük boyut (RockScatter prefab'ı doğal ölçekle koyar, ±%40 ölçek çarpanı ile).
            new Map(Kind.Rock, ContentIds.RockSmall, "stone_01", 0.9f),
            new Map(Kind.Rock, ContentIds.RockSmall, "rock_07", 1.0f),
            new Map(Kind.Rock, ContentIds.RockMedium, "boulder_01", 1.6f),
            new Map(Kind.Rock, ContentIds.RockMedium, "namaqualand_boulder_02", 1.6f),
            new Map(Kind.Rock, ContentIds.RockMedium, "namaqualand_boulder_03", 1.7f),
            new Map(Kind.Rock, ContentIds.RockMedium, "namaqualand_boulder_05", 1.5f),
            new Map(Kind.Rock, ContentIds.RockLarge, "namaqualand_cliff_01", 5.5f),
            new Map(Kind.Rock, ContentIds.RockLarge, "namaqualand_boulders_01", 3.5f),
            new Map(Kind.Rock, ContentIds.RockLarge, "rock_face_01", 3.5f),

            // Proplar: doğal (Poly Haven metre) ölçek; PropFactory çağrı noktaları: ammo_crate, barrel, barrier, stump.
            new Map(Kind.Prop, "ammo_crate", "wooden_military_crate"),
            new Map(Kind.Prop, "crate", "wooden_crate_01"),
            new Map(Kind.Prop, "crate_02", "wooden_crate_02"),
            new Map(Kind.Prop, "ammo_box", "ammo_box"),
            new Map(Kind.Prop, "barrel", "barrel_03"),
            new Map(Kind.Prop, "barrel_explosive", "Barrel_01"),
            new Map(Kind.Prop, "barrel_wood", "wooden_barrels_01"),
            new Map(Kind.Prop, "barrel_wine", "wine_barrel_01"),
            new Map(Kind.Prop, "barrier", "concrete_road_barrier"),
            new Map(Kind.Prop, "fence", "modular_chainlink_fence"),
            new Map(Kind.Prop, "jerrycan", "metal_jerrycan_green"),
            new Map(Kind.Prop, "bucket", "wooden_bucket_01"),
            new Map(Kind.Prop, "bucket_02", "wooden_bucket_02"),
            new Map(Kind.Prop, "ladder", "wooden_ladder"),
            new Map(Kind.Prop, "ladder_02", "wooden_ladder_02"),
            new Map(Kind.Prop, "spade", "rusted_spade_01"),
            new Map(Kind.Prop, "basket", "wicker_basket_01"),
            new Map(Kind.Prop, "bags", "compost_bags"),
            new Map(Kind.Prop, "cement_bag", "cement_bag"),
            new Map(Kind.Prop, "handtruck", "hand_truck"),
            new Map(Kind.Prop, "stump", "tree_stump_01"),
            new Map(Kind.Prop, "stump_02", "tree_stump_02"),
            new Map(Kind.Prop, "log", "dead_tree_trunk"),
            new Map(Kind.Prop, "log_02", "dead_tree_trunk_02"),
            new Map(Kind.Prop, "branches", "dry_branches_medium_01"),
        };

        public static string BindAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ThirdPartyModelBinder] başlıyor");
            int ok = 0, fail = 0;
            try
            {
                EnsureFolder(PrefabRoot);
                EnsureFolder(MeshRoot);
                EnsureFolder(MaterialRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var data = ContentOverridesSetup.EnsureAsset();

                var veg = new Dictionary<string, List<GameObject>>(StringComparer.OrdinalIgnoreCase);
                var rocks = new Dictionary<string, List<GameObject>>(StringComparer.OrdinalIgnoreCase);
                var props = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

                foreach (var m in Maps)
                {
                    GameObject prefab = null;
                    try { prefab = BuildPrefab(m, sb); }
                    catch (Exception e) { sb.AppendLine("  ISTISNA " + m.Folder + ": " + e.Message); }
                    if (prefab == null) { fail++; continue; }

                    switch (m.Kind)
                    {
                        case Kind.Veg:
                            if (!veg.TryGetValue(m.Id, out var vl)) veg[m.Id] = vl = new List<GameObject>();
                            vl.Add(prefab);
                            break;
                        case Kind.Rock:
                            if (!rocks.TryGetValue(m.Id, out var rl)) rocks[m.Id] = rl = new List<GameObject>();
                            rl.Add(prefab);
                            break;
                        default:
                            props[m.Id] = prefab;
                            break;
                    }
                    sb.AppendLine("  " + m.Kind + " " + m.Id + " <- " + m.Folder);
                    ok++;
                }

                data.vegetation = ToVeg(veg);
                data.rocks = ToRock(rocks);
                data.props = ToProp(props);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
            }
            catch (Exception e)
            {
                sb.AppendLine("  ISTISNA: " + e);
                fail++;
            }
            sb.AppendLine($"[ThirdPartyModelBinder] bitti: {ok} başarılı, {fail} sorun");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Prefab üretimi

        private static GameObject BuildPrefab(Map m, StringBuilder sb)
        {
            var fbx = FindFbx(m.Folder);
            if (fbx == null) { sb.AppendLine("  FBX yok: " + m.Folder); return null; }
            ConfigureModelImport(fbx);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { sb.AppendLine("  Model yüklenemedi: " + fbx); return null; }

            var root = new GameObject(m.Folder);
            GameObject lod0 = null, lod1 = null;
            try
            {
                lod0 = (GameObject)PrefabUtility.InstantiatePrefab(model);
                lod0.name = "LOD0";
                lod0.transform.SetParent(root.transform, false);
                ApplyMaterials(lod0, m.Folder);

                // Ölçek + taban oturtma.
                var b = RendererBounds(lod0);
                var maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                var k = 1f;
                if (m.Target > 0f)
                    k = m.Kind == Kind.Veg ? m.Target / Mathf.Max(0.01f, b.size.y) : m.Target / Mathf.Max(0.01f, maxDim);
                else if (maxDim > 40f) k = 0.01f;       // cm → m kaçağı
                else if (maxDim < 0.02f) k = 100f;      // m → mm kaçağı
                var height = b.size.y * k;
                var sink = m.Kind == Kind.Rock ? -0.07f * height : (m.Id.StartsWith("stump", StringComparison.Ordinal) ? -0.02f : 0f);
                var pos = new Vector3(-b.center.x * k, -b.min.y * k + sink, -b.center.z * k);
                lod0.transform.localScale = Vector3.one * k;
                lod0.transform.localPosition = pos;
                sb.AppendLine($"    {m.Folder}: sınır {b.size.x:0.##}x{b.size.y:0.##}x{b.size.z:0.##} m, ölçek x{k:0.###}");

                // LOD: modelde _LODn varsa kullan; yoksa üçgen çoksa ucuz LOD1 üret.
                var lods = BuildLods(root, lod0, model, m, k, pos, sb, out lod1);
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods);
                group.RecalculateBounds();

                // Çarpıştırıcılar (siper/NavMesh): prop & kaya kutu, çam gövde kapsülü.
                if (m.Collider)
                {
                    var size = b.size * k;
                    if (m.Kind == Kind.Veg)
                    {
                        if (m.Id == ContentIds.VegPine)
                        {
                            var cap = root.AddComponent<CapsuleCollider>();
                            cap.direction = 1;
                            cap.radius = 0.22f;
                            cap.height = height;
                            cap.center = new Vector3(0f, height * 0.5f, 0f);
                        }
                    }
                    else
                    {
                        var box = root.AddComponent<BoxCollider>();
                        box.center = new Vector3(0f, pos.y + (b.center.y - 0f) * k, 0f);
                        box.size = size;
                    }
                }

                Directory.CreateDirectory(PrefabRoot);
                var path = PrefabRoot + "/" + m.Folder + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static LOD[] BuildLods(GameObject root, GameObject lod0, GameObject model, Map m, float k, Vector3 pos,
            StringBuilder sb, out GameObject lod1Go)
        {
            lod1Go = null;
            var cull = m.Kind == Kind.Prop ? 0.015f : (m.Kind == Kind.Rock ? 0.012f : 0.02f);
            var all = lod0.GetComponentsInChildren<Renderer>(true);

            // 1) Modelin kendi LOD düğümleri.
            var byLevel = new SortedDictionary<int, List<Renderer>>();
            var rx = new Regex(@"LOD\s*_?(\d)$", RegexOptions.IgnoreCase);
            foreach (var r in all)
            {
                var mt = rx.Match(r.gameObject.name);
                if (!mt.Success) continue;
                var lvl = int.Parse(mt.Groups[1].Value);
                if (!byLevel.TryGetValue(lvl, out var l)) byLevel[lvl] = l = new List<Renderer>();
                l.Add(r);
            }
            if (byLevel.Count >= 2)
            {
                var res = new List<LOD>();
                var n = byLevel.Count;
                var i = 0;
                foreach (var kv in byLevel)
                {
                    var h = i == 0 ? 0.35f : (i == n - 1 ? 0.05f : 0.35f / (1 + i * 1.8f));
                    res.Add(new LOD(i == n - 1 ? Mathf.Max(h, cull) : h, kv.Value.ToArray()));
                    i++;
                }
                sb.AppendLine("    LOD: modelin " + n + " kademesi");
                return res.ToArray();
            }

            // 2) Tek kademe. Opak ve yoğunsa köşe-kümeleme ile ucuz LOD1.
            var tris = TriangleCount(all);
            var foliage = HasAlphaClip(all);
            if (!foliage && tris > 3000 && m.Kind != Kind.Veg)
            {
                try
                {
                    var lod1 = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    lod1.name = "LOD1";
                    lod1.transform.SetParent(root.transform, false);
                    lod1.transform.localScale = lod0.transform.localScale;
                    lod1.transform.localPosition = pos;
                    var r0 = lod0.GetComponentsInChildren<MeshFilter>(true);
                    var r1 = lod1.GetComponentsInChildren<MeshFilter>(true);
                    ApplyMaterials(lod1, m.Folder);
                    var any = false;
                    for (var i = 0; i < r1.Length && i < r0.Length; i++)
                    {
                        var src = r0[i].sharedMesh;
                        if (src == null) continue;
                        var simp = SimplifyToBudget(src, tris);
                        if (simp == null) continue;
                        var mp = MeshRoot + "/" + m.Folder + "_" + i + "_LOD1.asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                        if (existing != null)
                        {
                            EditorUtility.CopySerialized(simp, existing); // yeniden üretimde yerinde güncelle
                            UnityEngine.Object.DestroyImmediate(simp);
                        }
                        else AssetDatabase.CreateAsset(simp, mp);
                        r1[i].sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                        any = true;
                    }
                    if (any)
                    {
                        lod1Go = lod1;
                        var lod1Tris = TriangleCount(lod1.GetComponentsInChildren<Renderer>(true));
                        sb.AppendLine($"    LOD: LOD0 {tris} → LOD1 {lod1Tris} üçgen (köşe kümeleme)");
                        var t0 = m.Kind == Kind.Rock ? 0.22f : 0.18f;
                        var t1 = m.Kind == Kind.Rock ? 0.05f : 0.04f;
                        return new[]
                        {
                            new LOD(t0, lod0.GetComponentsInChildren<Renderer>(true)),
                            new LOD(Mathf.Max(t1, cull), lod1.GetComponentsInChildren<Renderer>(true)),
                        };
                    }
                    UnityEngine.Object.DestroyImmediate(lod1);
                }
                catch (Exception e) { sb.AppendLine("    LOD1 üretilemedi: " + e.Message); }
            }

            sb.AppendLine("    LOD: tek kademe (" + tris + " üçgen" + (foliage ? ", yaprak" : "") + "), kesme " + cull);
            return new[] { new LOD(cull, all) };
        }

        // ---------------------------------------------------------------- Köşe kümeleme sadeleştirme

        private static Mesh SimplifyToBudget(Mesh src, int originalTris)
        {
            var target = Mathf.Max(400, originalTris / 4);
            Mesh best = null;
            var div = 64;
            while (div >= 6)
            {
                var m = Simplify(src, div);
                if (m != null)
                {
                    best = m;
                    var t = 0;
                    for (var s = 0; s < m.subMeshCount; s++) t += (int)(m.GetIndexCount(s) / 3);
                    if (t <= target) break;
                }
                div = div * 3 / 4;
            }
            return best;
        }

        private static Mesh Simplify(Mesh src, int div)
        {
            var v = src.vertices;
            if (v.Length == 0) return null;
            var n = src.normals;
            var uv = src.uv;
            var hasN = n != null && n.Length == v.Length;
            var hasUv = uv != null && uv.Length == v.Length;
            var b = src.bounds;
            var cell = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) / div;
            if (cell <= 1e-6f) return null;

            var map = new int[v.Length];
            var dict = new Dictionary<(int, int, int, int, int), int>(v.Length / 2);
            var accP = new List<Vector3>();
            var accN = new List<Vector3>();
            var accUv = new List<Vector2>();
            var cnt = new List<int>();
            for (var i = 0; i < v.Length; i++)
            {
                var p = v[i];
                var key = (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell),
                    hasUv ? Mathf.FloorToInt(uv[i].x * 24f) : 0, hasUv ? Mathf.FloorToInt(uv[i].y * 24f) : 0);
                if (!dict.TryGetValue(key, out var id))
                {
                    id = accP.Count;
                    dict[key] = id;
                    accP.Add(p);
                    accN.Add(hasN ? n[i] : Vector3.up);
                    accUv.Add(hasUv ? uv[i] : Vector2.zero);
                    cnt.Add(1);
                }
                else
                {
                    accP[id] += p;
                    if (hasN) accN[id] += n[i];
                    if (hasUv) accUv[id] += uv[i];
                    cnt[id]++;
                }
                map[i] = id;
            }

            var count = accP.Count;
            var verts = new Vector3[count];
            var norms = new Vector3[count];
            var uvs = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                verts[i] = accP[i] / cnt[i];
                norms[i] = accN[i].sqrMagnitude > 1e-8f ? accN[i].normalized : Vector3.up;
                uvs[i] = accUv[i] / cnt[i];
            }

            var mesh = new Mesh { name = src.name + "_LOD1" };
            mesh.indexFormat = count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = norms;
            if (hasUv) mesh.uv = uvs;
            mesh.subMeshCount = src.subMeshCount;
            var total = 0;
            for (var s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                var list = new List<int>(tris.Length / 2);
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = map[tris[t]], c = map[tris[t + 1]], d = map[tris[t + 2]];
                    if (a == c || c == d || a == d) continue;
                    list.Add(a); list.Add(c); list.Add(d);
                }
                mesh.SetTriangles(list, s);
                total += list.Count / 3;
            }
            if (total == 0) { UnityEngine.Object.DestroyImmediate(mesh); return null; }
            mesh.RecalculateBounds();
            if (hasUv) mesh.RecalculateTangents();
            return mesh;
        }

        // ---------------------------------------------------------------- Materyaller

        private static void ApplyMaterials(GameObject instance, string assetId)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                var dst = new Material[src.Length];
                for (var i = 0; i < src.Length; i++)
                {
                    var name = src[i] != null ? src[i].name : assetId;
                    dst[i] = GetOrBuildMaterial(assetId, name);
                }
                r.sharedMaterials = dst;
            }
        }

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        private static Material GetOrBuildMaterial(string assetId, string matName)
        {
            var key = assetId + "|" + matName;
            if (MatCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texDir = ModelRoot + "/" + assetId + "/textures";
            var baseName = PickBase(texDir, assetId, matName);
            var safe = Regex.Replace(matName, @"[^A-Za-z0-9_\-]", "_");
            var matPath = MaterialRoot + "/PH_" + assetId + "_" + safe + ".mat";

            var shader = Shader.Find(LitShader);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;
            mat.name = "PH_" + assetId + "_" + safe;

            if (baseName != null)
            {
                var diff = FindTexture(texDir, baseName + "_diff_");
                var nor = FindTexture(texDir, baseName + "_nor_gl_");
                var metal = FindTexture(texDir, baseName + "_metal_");
                var alpha = FindTexture(texDir, baseName + "_alpha_");

                var foliage = alpha != null;
                Texture2D albedo = null;
                if (diff != null)
                {
                    if (foliage)
                    {
                        var rgba = ComposeRgba(diff, alpha, MaterialRoot + "/PH_" + baseName + "_rgba.png");
                        albedo = rgba != null ? rgba : ConfigureTexture(diff, false, false);
                        if (rgba == null) foliage = false;
                    }
                    else albedo = ConfigureTexture(diff, false, false);
                }

                SetTex(mat, "_BaseMap", albedo);
                SetTex(mat, "_MainTex", albedo);
                mat.SetColor("_BaseColor", Color.white);
                var normalTex = nor != null ? ConfigureTexture(nor, true, false) : null;
                SetTex(mat, "_BumpMap", normalTex);
                if (normalTex != null) { mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
                else mat.DisableKeyword("_NORMALMAP");

                var metalTex = metal != null ? ConfigureTexture(metal, false, true) : null;
                SetTex(mat, "_MetallicGlossMap", metalTex);
                if (metalTex != null)
                {
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", 1f);
                    mat.SetFloat("_Smoothness", 0.5f);
                }
                else
                {
                    mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", foliage ? 0.12f : 0.2f);
                }

                if (foliage)
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", 0.5f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", 0f);
                    mat.renderQueue = 2450;
                }
                else
                {
                    mat.SetFloat("_AlphaClip", 0f);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", 2f);
                    mat.renderQueue = -1;
                }
                mat.SetFloat("_Surface", 0f);
            }
            else
            {
                mat.SetColor("_BaseColor", new Color(0.45f, 0.45f, 0.42f));
                mat.SetFloat("_Smoothness", 0.15f);
            }

            if (isNew) AssetDatabase.CreateAsset(mat, matPath);
            else EditorUtility.SetDirty(mat);
            MatCache[key] = mat;
            return mat;
        }

        /// <summary>Materyal adına en uygun doku öneki (…_diff_NK.ext dosyasından). Yoksa null.</summary>
        private static string PickBase(string texDir, string assetId, string matName)
        {
            if (!Directory.Exists(texDir)) return null;
            var diffs = new List<string>();
            foreach (var f in Directory.GetFiles(texDir))
            {
                var fn = Path.GetFileName(f);
                if (fn.EndsWith(".meta", StringComparison.Ordinal)) continue;
                var idx = fn.IndexOf("_diff_", StringComparison.OrdinalIgnoreCase);
                if (idx > 0) diffs.Add(fn.Substring(0, idx));
            }
            if (diffs.Count == 0) return null;
            if (diffs.Count == 1) return diffs[0];

            var mn = matName.ToLowerInvariant();
            foreach (var d in diffs) if (string.Equals(d, matName, StringComparison.OrdinalIgnoreCase)) return d;
            foreach (var d in diffs) if (string.Equals(d, assetId + "_" + matName, StringComparison.OrdinalIgnoreCase)) return d;
            foreach (var d in diffs) { var dl = d.ToLowerInvariant(); if (dl.EndsWith("_" + mn, StringComparison.Ordinal) || mn.EndsWith(dl, StringComparison.Ordinal)) return d; }
            foreach (var d in diffs) if (d.ToLowerInvariant().Contains(mn)) return d;
            foreach (var d in diffs) if (string.Equals(d, assetId, StringComparison.OrdinalIgnoreCase)) return d;
            return diffs[0];
        }

        private static string FindTexture(string texDir, string prefix)
        {
            if (!Directory.Exists(texDir)) return null;
            foreach (var f in Directory.GetFiles(texDir))
            {
                var fn = Path.GetFileName(f);
                if (fn.EndsWith(".meta", StringComparison.Ordinal)) continue;
                if (fn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return texDir + "/" + fn;
            }
            return null;
        }

        private static void SetTex(Material m, string prop, Texture tex)
        {
            if (m.HasProperty(prop)) m.SetTexture(prop, tex);
        }

        private static Texture2D ConfigureTexture(string path, bool normal, bool linear)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                var dirty = false;
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != type) { ti.textureType = type; dirty = true; }
                if (!normal && ti.sRGBTexture == linear) { ti.sRGBTexture = !linear; dirty = true; }
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
                if (ti.isReadable) { ti.isReadable = false; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>diff RGB + alpha(R) → RGBA PNG (URP/Lit alfa kırpma albedo alfasından okur).</summary>
        private static Texture2D ComposeRgba(string diffPath, string alphaPath, string outPath)
        {
            try
            {
                if (!File.Exists(outPath))
                {
                    var d = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    var a = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(d, File.ReadAllBytes(diffPath)) || !ImageConversion.LoadImage(a, File.ReadAllBytes(alphaPath)))
                    {
                        UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(a);
                        return null;
                    }
                    var dp = d.GetPixels32();
                    var o = new Color32[dp.Length];
                    var same = a.width == d.width && a.height == d.height;
                    var ap = same ? a.GetPixels32() : null;
                    for (var y = 0; y < d.height; y++)
                        for (var x = 0; x < d.width; x++)
                        {
                            var i = y * d.width + x;
                            byte av;
                            if (same) av = ap[i].r;
                            else av = (byte)Mathf.RoundToInt(a.GetPixelBilinear((x + 0.5f) / d.width, (y + 0.5f) / d.height).r * 255f);
                            o[i] = new Color32(dp[i].r, dp[i].g, dp[i].b, av);
                        }
                    var res = new Texture2D(d.width, d.height, TextureFormat.RGBA32, false);
                    res.SetPixels32(o);
                    File.WriteAllBytes(outPath, ImageConversion.EncodeToPNG(res));
                    UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(res);
                    AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
                }

                var ti = AssetImporter.GetAtPath(outPath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true;
                    ti.alphaTestReferenceValue = 0.5f;
                    ti.isReadable = false;
                    ti.maxTextureSize = 2048;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ThirdPartyModelBinder] RGBA dokusu üretilemedi (" + outPath + "): " + e.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- Yardımcılar

        private static bool HasAlphaClip(Renderer[] renderers)
        {
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) return true;
            return false;
        }

        private static int TriangleCount(Renderer[] renderers)
        {
            var t = 0;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                for (var s = 0; s < mf.sharedMesh.subMeshCount; s++) t += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
            }
            return t;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = rs[0].bounds;
            for (var i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static VegetationOverrideEntry[] ToVeg(Dictionary<string, List<GameObject>> d)
        {
            var list = new List<VegetationOverrideEntry>();
            foreach (var kv in d)
                list.Add(new VegetationOverrideEntry { speciesId = kv.Key, prefabs = kv.Value.ToArray() });
            return list.ToArray();
        }

        private static RockOverrideEntry[] ToRock(Dictionary<string, List<GameObject>> d)
        {
            var list = new List<RockOverrideEntry>();
            foreach (var kv in d)
                list.Add(new RockOverrideEntry { sizeClass = kv.Key, prefabs = kv.Value.ToArray() });
            return list.ToArray();
        }

        private static PropOverrideEntry[] ToProp(Dictionary<string, GameObject> d)
        {
            var list = new List<PropOverrideEntry>();
            foreach (var kv in d)
                list.Add(new PropOverrideEntry { propId = kv.Key, prefab = kv.Value });
            return list.ToArray();
        }

        private static string FindFbx(string folder)
        {
            var dir = ModelRoot + "/" + folder;
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "*.fbx", SearchOption.TopDirectoryOnly);
            if (files.Length == 0) return null;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[0].Replace('\\', '/');
        }

        private static void ConfigureModelImport(string assetPath)
        {
            var mi = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (mi == null) return;
            var dirty = false;
            // Poly Haven FBX metre cinsinden; useFileScale + globalScale 1 → 1 birim = 1 m. Bounds BuildPrefab'ta ayrıca denetlenir.
            if (Math.Abs(mi.globalScale - 1f) > 0.001f) { mi.globalScale = 1f; dirty = true; }
            if (mi.addCollider) { mi.addCollider = false; dirty = true; }
            if (mi.isReadable) { mi.isReadable = false; dirty = true; }
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (mi.importBlendShapes) { mi.importBlendShapes = false; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (mi.generateSecondaryUV) { mi.generateSecondaryUV = false; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            { mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; dirty = true; }
            if (mi.materialLocation != ModelImporterMaterialLocation.InPrefab)
            { mi.materialLocation = ModelImporterMaterialLocation.InPrefab; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }
    }
}
