using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// CC0 silah modellerini (Assets/ThirdParty/Weapons/&lt;model&gt;/&lt;model&gt;.fbx) işler:
    /// import ayarı (okuma/collider kapalı), URP/Lit materyal (kaynak malzeme renginden), prefab
    /// (kök = Grip_R pivotu, +Z = namlu yönü) ve Muzzle/Grip_R/Grip_L/Magazine/Bolt/Sight soketleri; sonra
    /// ContentOverrides.weapons girdilerini yazar. Eşlemesi olmayan silahlara dokunmaz (prosedürel yedek kalır).
    ///
    /// Sezgiler (modelin Y yukarı olduğu varsayılır; köşe uzayı):
    ///  1. Uzunluk ekseni = X/Z içinde en büyük kenar. Uç çeyreklerden dikey (Y) boyutu küçük olan uç = namlu (dipçik/kabza arka uçta yüksek).
    ///  2. Model yaw ile namlu +Z olacak şekilde döndürülür; sınıf hedef uzunluğuna (tüfek ~0.95 m ...) ölçeklenir (alt-model ölçek = globalScale yerine child ölçeği).
    ///  3. Muzzle = en ön köşelerin merkezi (max Z). Grip_R = arka %55 içinde alt %25 köşelerin ağırlık merkezi (tabancada arka %60).
    ///  4. Grip_L = z=%68 dilimde (tabancada kabza önü) alt kenara yakın nokta. Magazine = orta bölgede alt %30 köşe merkezi.
    ///  5. Sight = üst %10 köşelerin merkezi (orta %60 uzunluk); Bolt = receiver ortası.
    ///  6. Kök pivotu Grip_R'ye taşınır (Grip_R yerel sıfır).
    /// BatchEntry.BindThirdParty yansıma ile çağırır.
    /// </summary>
    public static class ThirdPartyWeaponBinder
    {
        public const string WeaponRoot = "Assets/ThirdParty/Weapons";
        public const string MaterialRoot = "Assets/ThirdParty/Materials/Weapons";
        private const string LitShader = "Universal Render Pipeline/Lit";

        private sealed class Map
        {
            public string Model;      // Weapons/<Model>/<Model>.fbx
            public float Length;      // hedef toplam uzunluk (m)
            public bool Pistol;
            public string[] WeaponIds;
            public Map(string model, float length, bool pistol, params string[] ids)
            { Model = model; Length = length; Pistol = pistol; WeaponIds = ids; }
        }

        // Quaternius Ultimate Gun Pack (CC0). Eşleme: silüet sınıfı → weaponId. MG (lmg_*) için uygun model yok → prosedürel yedek.
        private static readonly Map[] Maps =
        {
            new Map("AssaultRifle_2",  0.95f, false, "ar_mpt76"),
            new Map("AssaultRifle_1",  0.92f, false, "ar_mpt55"),
            new Map("AssaultRifle_3",  0.92f, false, "ar_sar223"),
            new Map("AssaultRifle2_1", 1.00f, false, "ar_g3a7"),
            new Map("AssaultRifle2_2", 0.80f, false, "ar_mpt76k"),
            new Map("SniperRifle_4",   1.05f, false, "dmr_knt76"),
            new Map("SniperRifle_5",   1.05f, false, "dmr_sar762mt"),
            new Map("SniperRifle_2",   1.20f, false, "sr_jng90"),
            new Map("SubmachineGun_1", 0.60f, false, "smg_sar109t"),
            new Map("Pistol_1",        0.21f, true,  "pistol_sar9"),
            new Map("Pistol_2",        0.20f, true,  "pistol_tp9"),
            new Map("Pistol_3",        0.21f, true,  "pistol_mete"),
            new Map("Shotgun_1",       1.00f, false, "sg_escort"),
            new Map("Shotgun_2",       0.95f, false, "sg_escort_magnum"),
        };

        public static string BindAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ThirdPartyWeaponBinder] başlıyor");
            int ok = 0, fail = 0;
            try
            {
                EnsureFolder(MaterialRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var data = ContentOverridesSetup.EnsureAsset();
                var entries = new List<WeaponOverrideEntry>(data.weapons ?? new WeaponOverrideEntry[0]);

                foreach (var m in Maps)
                {
                    GameObject prefab = null;
                    try { prefab = Build(m, sb); }
                    catch (Exception e) { sb.AppendLine("  ISTISNA " + m.Model + ": " + e.Message); }
                    if (prefab == null)
                    {
                        // Görünmez silahtan prosedürel yedek iyidir: eski/bozuk override girdisini düşür.
                        foreach (var id in m.WeaponIds)
                        {
                            var id2 = id;
                            if (entries.RemoveAll(x => x != null && string.Equals(x.weaponId, id2, StringComparison.OrdinalIgnoreCase)) > 0)
                                sb.AppendLine("  override düşürüldü (prosedürel yedek): " + id);
                        }
                        fail++;
                        continue;
                    }

                    foreach (var id in m.WeaponIds)
                    {
                        entries.RemoveAll(x => x != null && string.Equals(x.weaponId, id, StringComparison.OrdinalIgnoreCase));
                        entries.Add(new WeaponOverrideEntry { weaponId = id, prefab = prefab });
                        sb.AppendLine("  " + id + " <- " + m.Model);
                    }
                    ok++;
                }

                data.weapons = entries.ToArray();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
            }
            catch (Exception e)
            {
                sb.AppendLine("  ISTISNA: " + e);
                fail++;
            }
            sb.AppendLine($"[ThirdPartyWeaponBinder] bitti: {ok} başarılı, {fail} sorun");
            return sb.ToString();
        }

        private static GameObject Build(Map m, StringBuilder sb)
        {
            var dir = WeaponRoot + "/" + m.Model;
            return BuildFromModel(dir + "/" + m.Model + ".fbx", m, dir + "/" + m.Model + ".prefab", sb);
        }

        /// <summary>Yeniden kullanılabilir yardımcı: herhangi bir model/prefab yolundan soket+ölçek sezgisiyle silah prefab'ı üretir (PurchasedPackBinder kullanır).</summary>
        public static GameObject BuildFromModel(string modelPath, string name, float length, bool pistol, string prefabPath, StringBuilder sb)
            => BuildFromModel(modelPath, new Map(name, length, pistol), prefabPath, sb);

        private static GameObject BuildFromModel(string fbxPath, Map m, string path, StringBuilder sb)
        {
            if (!File.Exists(fbxPath)) { sb.AppendLine("  yok: " + fbxPath); return null; }
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            ConfigureImport(fbxPath);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) return null;

            var root = new GameObject(m.Model);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);

                var verts = CollectVertices(model.transform);
                if (verts.Count < 8) { sb.AppendLine("  köşe yok: " + m.Model); return null; }

                // 1) uzunluk ekseni + yön
                Bounds b = Bounds(verts);
                bool alongX = b.size.x >= b.size.z;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                float lo = alongX ? b.min.x : b.min.z, len = alongX ? b.size.x : b.size.z;
                float hiEndH = EndHeight(verts, alongX, lo, len, true);
                float loEndH = EndHeight(verts, alongX, lo, len, false);
                Vector3 front = hiEndH <= loEndH ? axis : -axis;   // dikey boyutu küçük uç = namlu
                var rot = Quaternion.FromToRotation(front, Vector3.forward);

                // 2) ölçek
                float k = m.Length / Mathf.Max(0.01f, len);
                for (int i = 0; i < verts.Count; i++) verts[i] = rot * verts[i] * k;
                b = Bounds(verts);
                float L = b.size.z, H = b.size.y;
                float zMin = b.min.z, yMin = b.min.y;

                // 3) soketler
                var muzzle = Centroid(verts, v => v.z > b.max.z - 0.04f * L, new Vector3(b.center.x, b.center.y, b.max.z));
                muzzle.z = b.max.z;
                float rearZ = zMin + (m.Pistol ? 0.60f : 0.55f) * L;
                var gripR = Centroid(verts, v => v.z < rearZ && v.y < yMin + 0.25f * H, new Vector3(b.center.x, yMin + 0.15f * H, zMin + 0.25f * L));
                gripR.y += 0.02f * H;
                Vector3 gripL;
                if (m.Pistol) gripL = gripR + new Vector3(0f, 0f, 0.035f);
                else
                {
                    float zs = zMin + 0.68f * L;
                    var slab = Centroid(verts, v => Mathf.Abs(v.z - zs) < 0.05f * L, new Vector3(b.center.x, b.center.y, zs));
                    gripL = new Vector3(b.center.x, Mathf.Lerp(yMin, slab.y, 0.7f), zs);
                }
                var mag = m.Pistol ? gripR : Centroid(verts, v => v.y < yMin + 0.30f * H && v.z > zMin + 0.35f * L && v.z < zMin + 0.70f * L, gripR + new Vector3(0f, 0f, 0.06f * L));
                var sight = Centroid(verts, v => v.y > b.max.y - 0.10f * H && v.z > zMin + 0.20f * L && v.z < zMin + 0.80f * L, new Vector3(b.center.x, b.max.y, zMin + 0.5f * L));
                sight.y = b.max.y;
                var bolt = new Vector3(b.center.x, yMin + 0.65f * H, zMin + 0.45f * L);

                // 4) hizalama: root pivotu = Grip_R
                model.transform.localRotation = rot;
                model.transform.localScale = new Vector3(k, k, k);
                model.transform.localPosition = -gripR;
                AddSocket(root.transform, ContentIds.WeaponMuzzle, muzzle - gripR);
                AddSocket(root.transform, ContentIds.WeaponGripR, Vector3.zero);
                AddSocket(root.transform, ContentIds.WeaponGripL, gripL - gripR);
                AddSocket(root.transform, ContentIds.WeaponMagazine, mag - gripR);
                AddSocket(root.transform, ContentIds.WeaponBolt, bolt - gripR);
                AddSocket(root.transform, ContentIds.WeaponSight, sight - gripR);

                // 5) URP/Lit materyaller
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var src = r.sharedMaterials;
                    var dst = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++) dst[i] = BuildMaterial(m.Model, src[i], i);
                    r.sharedMaterials = dst;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                // 6) sağlık denetimi: gerçek Renderer sınırları (kök uzayı)
                if (!MeasureRenderers(root.transform, out var rb))
                { sb.AppendLine("  ATLANDI " + m.Model + ": etkin Renderer yok"); return null; }
                float maxDim = Mathf.Max(rb.size.x, Mathf.Max(rb.size.y, rb.size.z));
                string boundsInfo = $"size=({rb.size.x:0.###},{rb.size.y:0.###},{rb.size.z:0.###}) center=({rb.center.x:0.###},{rb.center.y:0.###},{rb.center.z:0.###})";
                sb.AppendLine($"  {m.Model}: sınır {boundsInfo}, k={k:0.###}");
                if (float.IsNaN(maxDim) || maxDim < 0.1f || maxDim > 2.0f || rb.center.magnitude > 1f)
                { sb.AppendLine("  ATLANDI " + m.Model + ": sınırlar makul değil → override yok"); return null; }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved != null)
                    AssetDatabase.SetLabels(saved, new[] { "bounds_" + boundsInfo.Replace(' ', '_') });
                sb.AppendLine($"  {m.Model}: uzunluk {len:0.###}→{L:0.###} m, k={k:0.###}, ön={front}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool MeasureRenderers(Transform root, out Bounds result)
        {
            result = default;
            var any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                var lb = r.localBounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return any;
        }

        private static List<Vector3> CollectVertices(Transform modelRoot)
        {
            var list = new List<Vector3>();
            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mtx = modelRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) list.Add(mtx.MultiplyPoint3x4(v));
            }
            foreach (var sm in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (sm.sharedMesh == null) continue;
                var mtx = modelRoot.worldToLocalMatrix * sm.transform.localToWorldMatrix;
                foreach (var v in sm.sharedMesh.vertices) list.Add(mtx.MultiplyPoint3x4(v));
            }
            return list;
        }

        private static float EndHeight(List<Vector3> verts, bool alongX, float lo, float len, bool high)
        {
            float yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                float t = ((alongX ? verts[i].x : verts[i].z) - lo) / len;
                if (high ? t < 0.75f : t > 0.25f) continue;
                yMin = Mathf.Min(yMin, verts[i].y); yMax = Mathf.Max(yMax, verts[i].y);
            }
            return yMax >= yMin ? yMax - yMin : 0f;
        }

        // high=true: "yüksek ucun" (t>=0.75) yüksekliği döner; ön = yüksekliği küçük uç (axis yönü).
        private static Bounds Bounds(List<Vector3> verts)
        {
            var b = new Bounds(verts[0], Vector3.zero);
            for (int i = 1; i < verts.Count; i++) b.Encapsulate(verts[i]);
            return b;
        }

        private static Vector3 Centroid(List<Vector3> verts, Func<Vector3, bool> pred, Vector3 fallback)
        {
            var sum = Vector3.zero; int n = 0;
            for (int i = 0; i < verts.Count; i++)
                if (pred(verts[i])) { sum += verts[i]; n++; }
            return n > 0 ? sum / n : fallback;
        }

        private static void AddSocket(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
        }

        private static Material BuildMaterial(string model, Material src, int index)
        {
            var name = src != null ? src.name : "mat" + index;
            var path = MaterialRoot + "/" + model + "_" + Sanitize(name) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(LitShader);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
                mat.shader = shader; // eski/bozuk (pembe) malzemeyi URP/Lit'e çek
            var color = Color.gray;
            if (src != null)
            {
                if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) color = src.GetColor("_Color");
            }
            color.a = 1f; // alfa 0 ise silah saydam/görünmez kalırdı
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            // Silah: metal ağırlıklı; koyu/nötr renkler daha metalik, parlak renkler daha mat.
            float lum = color.grayscale;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", lum < 0.35f ? 0.75f : 0.25f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", lum < 0.35f ? 0.55f : 0.35f);
            // Dokulu model gelirse (_MainTex): ana doku + metal/pürüz → maske TODO yok; doku varsa bağla.
            if (src != null && src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null && mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static void ConfigureImport(string assetPath)
        {
            var mi = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (mi == null) return;
            var dirty = false;
            if (Math.Abs(mi.globalScale - 1f) > 0.001f) { mi.globalScale = 1f; dirty = true; }
            if (mi.addCollider) { mi.addCollider = false; dirty = true; }
            if (mi.isReadable) { mi.isReadable = false; dirty = true; }
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (mi.importBlendShapes) { mi.importBlendShapes = false; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            { mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; dirty = true; }
            if (mi.materialLocation != ModelImporterMaterialLocation.InPrefab)
            { mi.materialLocation = ModelImporterMaterialLocation.InPrefab; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
