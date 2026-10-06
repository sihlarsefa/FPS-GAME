using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Asset Store'dan satın alınan paketler için OTOMATİK KARŞILAMA. Paket Assets/&lt;PaketAdı&gt;/ altına gelir;
    /// _Project ve ThirdParty dışındaki tüm Assets taranır. Adaylar puanlanır (saf sezgiler: PurchasedPackRules),
    /// en iyi asker ContentOverrides.soldier'a, silahlar ThirdPartyWeaponBinder.BuildFromModel ile soketlenip weapons'a yazılır.
    /// Öncelik: satın alınan paket &gt; Quaternius. BatchEntry.BindPurchasedPack() raporu basar ve doğrulayıcıyı çalıştırır.
    /// </summary>
    public static class PurchasedPackBinder
    {
        public const string CharacterOut = "Assets/ThirdParty/Characters/Purchased";
        public const string WeaponOut = "Assets/ThirdParty/Weapons/Purchased";
        public const string MaterialOut = "Assets/ThirdParty/Materials/Purchased";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const int MaxPerClass = 4;
        private const int MinCharacterScore = 40;

        public sealed class Candidate
        {
            public string Path, Name;
            public int Triangles, Score;
            public bool Humanoid, Lod;
            public Vector3 Size;
            public PackWeaponClass Class;
            public string MappedTo = "-";
            public string Sockets = "-";
        }

        [MenuItem("HAREKÂT/Satın Alınan Paketi Bağla")]
        public static void BindMenu() { Debug.Log(BindAll()); }

        public static string BindAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PurchasedPackBinder] başlıyor (tarama: Assets/ — _Project ve ThirdParty hariç)");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var data = ContentOverridesSetup.EnsureAsset();
                var rows = new List<Candidate>();

                var chars = ScanCharacters();
                BindCharacter(data, chars, rows, sb);
                var weapons = ScanWeapons();
                BindWeapons(data, weapons, rows, sb);

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
                AppendTable(sb, rows, chars.Count, weapons.Count);
            }
            catch (Exception e) { sb.AppendLine("  ISTISNA: " + e); }
            sb.AppendLine("[PurchasedPackBinder] bitti");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- tarama

        private static IEnumerable<string> PackAssetPaths(string filter)
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (PurchasedPackRules.IsExcludedPath(p)) continue;
                var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".prefab" || ext == ".fbx" || ext == ".obj" || ext == ".blend" || ext == ".dae" || ext == ".gltf" || ext == ".glb")
                    yield return p;
            }
        }

        private static int CountTriangles(GameObject go)
        {
            long n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                n += Tris(mf.sharedMesh);
            foreach (var sm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                n += Tris(sm.sharedMesh);
            return (int)Math.Min(n, int.MaxValue);
        }

        private static long Tris(Mesh m)
        {
            if (m == null) return 0;
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
            return n;
        }

        private static Avatar FindAvatar(GameObject asset, string path)
        {
            var anim = asset.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.avatar != null) return anim.avatar;
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (o is Avatar a) return a;
            return null;
        }

        private static bool TryMeasure(GameObject asset, out Vector3 size)
        {
            size = Vector3.zero;
            var inst = UnityEngine.Object.Instantiate(asset);
            try
            {
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one;
                Bounds b = default; bool any = false;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any) size = b.size;
                return any;
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }

        public static List<Candidate> ScanCharacters()
        {
            var list = new List<Candidate>();
            foreach (var p in PackAssetPaths("t:Prefab t:Model"))
            {
                try
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null || go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
                    var avatar = FindAvatar(go, p);
                    bool human = avatar != null && avatar.isHuman;
                    int tris = CountTriangles(go);
                    if (tris < 500) continue;
                    TryMeasure(go, out var size);
                    bool lod = go.GetComponentInChildren<LODGroup>(true) != null;
                    var c = new Candidate { Path = p, Name = go.name, Triangles = tris, Humanoid = human, Lod = lod, Size = size };
                    c.Score = PurchasedPackRules.ScoreCharacter(go.name, human, tris, lod, size.y);
                    list.Add(c);
                }
                catch (Exception e) { Debug.LogWarning("[PurchasedPackBinder] karakter taraması atlandı " + p + ": " + e.Message); }
            }
            return list.OrderByDescending(x => x.Score).ToList();
        }

        public static List<Candidate> ScanWeapons()
        {
            var best = new Dictionary<string, Candidate>();
            foreach (var p in PackAssetPaths("t:Prefab t:Model"))
            {
                try
                {
                    var cls = PurchasedPackRules.ClassifyWeapon(System.IO.Path.GetFileNameWithoutExtension(p));
                    if (cls == PackWeaponClass.None) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null || go.GetComponentInChildren<MeshRenderer>(true) == null) continue;
                    if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null && go.GetComponentInChildren<Animator>(true) != null) continue; // karakter/kol
                    if (!TryMeasure(go, out var size)) continue;
                    int tris = CountTriangles(go);
                    int score = PurchasedPackRules.ScoreWeapon(go.name, size.x, size.y, size.z, tris);
                    if (score <= 0) continue;
                    var c = new Candidate { Path = p, Name = go.name, Triangles = tris, Score = score, Size = size, Class = cls };
                    // .prefab, ham modele tercih edilir (+1); aynı ad tekilleştirilir
                    if (p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) c.Score++;
                    if (!best.TryGetValue(go.name, out var old) || old.Score < c.Score) best[go.name] = c;
                }
                catch (Exception e) { Debug.LogWarning("[PurchasedPackBinder] silah taraması atlandı " + p + ": " + e.Message); }
            }
            return best.Values.OrderByDescending(x => x.Score).ToList();
        }

        // ---------------------------------------------------------------- karakter

        private static void BindCharacter(ContentOverrides data, List<Candidate> chars, List<Candidate> rows, StringBuilder sb)
        {
            var best = chars.FirstOrDefault(c => c.Score >= MinCharacterScore);
            if (best == null) { sb.AppendLine("  karakter: uygun aday yok (en az " + MinCharacterScore + " puan)."); return; }
            rows.Add(best);

            if (data.soldier == null) data.soldier = new SoldierOverrideEntry();
            var existing = data.soldier.humanoidPrefab;
            if (existing != null)
            {
                var ep = AssetDatabase.GetAssetPath(existing);
                int es = ScoreFromLabels(existing);
                if (ep.StartsWith(CharacterOut, StringComparison.Ordinal) && !PurchasedPackRules.ShouldReplace(true, es, best.Score, false))
                { best.MappedTo = "soldier (mevcut paket karakteri korundu, puan " + es + ")"; return; }
            }

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(best.Path);
            EnsureHumanoidImport(src, best.Path, sb);
            src = AssetDatabase.LoadAssetAtPath<GameObject>(best.Path);
            ThirdPartyWeaponBinder.EnsureFolder(CharacterOut);
            ThirdPartyWeaponBinder.EnsureFolder(MaterialOut);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            GameObject saved;
            try
            {
                var cache = new Dictionary<Material, Material>();
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    var dst = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++) dst[i] = UpgradeMaterial(best.Name, mats[i], cache);
                    r.sharedMaterials = dst;
                }
                var anim = inst.GetComponentInChildren<Animator>(true);
                if (anim == null) anim = inst.AddComponent<Animator>();
                if (anim.avatar == null || !anim.avatar.isHuman) anim.avatar = FindAvatar(src, best.Path);
                var path = CharacterOut + "/" + Safe(best.Name) + "_HAREKAT.prefab";
                saved = PrefabUtility.SaveAsPrefabAsset(inst, path);
                if (saved != null) AssetDatabase.SetLabels(saved, new[] { "pk_char", "pk_score_" + best.Score });
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
            if (saved == null) { sb.AppendLine("  karakter prefab kaydedilemedi: " + best.Name); return; }

            data.soldier.humanoidPrefab = saved;
            var ctrl = PickController(best.Path, data.soldier.animatorController, sb);
            if (ctrl != null) data.soldier.animatorController = ctrl;
            best.MappedTo = "soldier (humanoidPrefab" + (ctrl != null ? " + " + ctrl.name : "") + ")";
            sb.AppendLine("  asker <- " + best.Path + " puan " + best.Score);
        }

        private static void EnsureHumanoidImport(GameObject src, string path, StringBuilder sb)
        {
            var paths = new HashSet<string>();
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var sm in src.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (sm.sharedMesh != null) paths.Add(AssetDatabase.GetAssetPath(sm.sharedMesh));
            }
            else paths.Add(path);
            foreach (var mp in paths)
            {
                var mi = AssetImporter.GetAtPath(mp) as ModelImporter;
                if (mi == null) continue;
                if (mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.SaveAndReimport();
                    sb.AppendLine("  Humanoid rig ayarlandı: " + mp);
                }
            }
        }

        private static RuntimeAnimatorController PickController(string packModelPath, RuntimeAnimatorController current, StringBuilder sb)
        {
            var root = packModelPath;
            var parts = packModelPath.Split('/');
            if (parts.Length >= 2) root = parts[0] + "/" + parts[1];
            foreach (var g in AssetDatabase.FindAssets("t:AnimatorController", new[] { root }))
            {
                var c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null) { sb.AppendLine("  controller (paket): " + c.name); return c; }
            }
            if (current != null) return current; // paket controller'ı yok → mevcut (Mixamo vb.) kalır
            foreach (var folder in new[] { "Assets/ThirdParty", "Assets/_Project" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var g in AssetDatabase.FindAssets("t:AnimatorController", new[] { folder }))
                {
                    var c = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(g));
                    if (c != null) { sb.AppendLine("  controller (yeniden kullanıldı): " + c.name); return c; }
                }
            }
            return null;
        }

        /// <summary>Standard/özel shader → URP/Lit kopyası: _MainTex→_BaseMap, _Color→_BaseColor, normal, metal/pürüz, AO, emisyon, alfa kesme.</summary>
        public static Material UpgradeMaterial(string owner, Material src, Dictionary<Material, Material> cache)
        {
            if (src == null) return null;
            if (src.shader != null && src.shader.name.StartsWith("Universal Render Pipeline", StringComparison.Ordinal)) return src;
            if (cache != null && cache.TryGetValue(src, out var done)) return done;
            var shader = Shader.Find(LitShader);
            if (shader == null) return src;
            var path = MaterialOut + "/" + Safe(owner) + "_" + Safe(src.name) + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else if (mat.shader != shader) mat.shader = shader;

            var color = Color.white;
            if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
            else if (src.HasProperty("_Color")) color = src.GetColor("_Color");
            mat.SetColor("_BaseColor", color);
            Texture main = null;
            if (src.HasProperty("_BaseMap")) main = src.GetTexture("_BaseMap");
            if (main == null && src.HasProperty("_MainTex")) main = src.GetTexture("_MainTex");
            if (main != null) { mat.SetTexture("_BaseMap", main); }
            if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
            {
                mat.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                mat.SetFloat("_BumpScale", src.HasProperty("_BumpScale") ? src.GetFloat("_BumpScale") : 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            bool hasMetalMap = src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap") != null;
            if (hasMetalMap)
            {
                mat.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f);
            float smooth = src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : (src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness") : 0.4f);
            mat.SetFloat("_Smoothness", smooth);
            if (src.HasProperty("_OcclusionMap") && src.GetTexture("_OcclusionMap") != null)
            {
                mat.SetTexture("_OcclusionMap", src.GetTexture("_OcclusionMap"));
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
            {
                mat.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                mat.SetColor("_EmissionColor", src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.white);
                mat.EnableKeyword("_EMISSION");
            }
            if (src.HasProperty("_Mode") && Mathf.RoundToInt(src.GetFloat("_Mode")) == 1)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
            }
            EditorUtility.SetDirty(mat);
            if (cache != null) cache[src] = mat;
            return mat;
        }

        // ---------------------------------------------------------------- silahlar

        private static void BindWeapons(ContentOverrides data, List<Candidate> weapons, List<Candidate> rows, StringBuilder sb)
        {
            var entries = new List<WeaponOverrideEntry>(data.weapons ?? new WeaponOverrideEntry[0]);
            foreach (PackWeaponClass cls in Enum.GetValues(typeof(PackWeaponClass)))
            {
                if (cls == PackWeaponClass.None) continue;
                var pool = weapons.Where(w => w.Class == cls).Take(MaxPerClass).ToList();
                if (pool.Count == 0) continue;
                var built = new List<KeyValuePair<Candidate, GameObject>>();
                foreach (var cand in pool)
                {
                    rows.Add(cand);
                    var prefabPath = WeaponOut + "/" + Safe(cand.Name) + "/" + Safe(cand.Name) + ".prefab";
                    GameObject prefab = null;
                    try
                    {
                        prefab = ThirdPartyWeaponBinder.BuildFromModel(cand.Path, Safe(cand.Name), PurchasedPackRules.TargetLength(cls), cls == PackWeaponClass.Pistol, prefabPath, sb);
                        if (prefab != null) AssetDatabase.SetLabels(prefab, new[] { "pk_weapon", "pk_score_" + cand.Score });
                    }
                    catch (Exception e) { sb.AppendLine("  ISTISNA " + cand.Name + ": " + e.Message); }
                    if (prefab == null) { cand.MappedTo = "(bağlanamadı)"; continue; }
                    cand.Sockets = CountSockets(prefab) + "/6";
                    built.Add(new KeyValuePair<Candidate, GameObject>(cand, prefab));
                }
                if (built.Count == 0) continue;

                foreach (var a in PurchasedPackRules.AssignIds(cls, built.Count))
                {
                    var cand = built[a.Value].Key; var prefab = built[a.Value].Value;
                    var ex = entries.FirstOrDefault(x => x != null && string.Equals(x.weaponId, a.Key, StringComparison.OrdinalIgnoreCase));
                    bool missing = ex == null || ex.prefab == null;
                    bool exPurchased = !missing && AssetDatabase.GetAssetPath(ex.prefab).StartsWith(WeaponOut, StringComparison.Ordinal);
                    int exScore = missing ? -1 : ScoreFromLabels(ex.prefab);
                    if (!PurchasedPackRules.ShouldReplace(exPurchased, exScore, cand.Score, missing))
                    { sb.AppendLine("  " + a.Key + ": mevcut paket silahı korundu (puan " + exScore + " ≥ " + cand.Score + ")"); continue; }
                    entries.RemoveAll(x => x != null && string.Equals(x.weaponId, a.Key, StringComparison.OrdinalIgnoreCase));
                    entries.Add(new WeaponOverrideEntry { weaponId = a.Key, prefab = prefab });
                    cand.MappedTo = cand.MappedTo == "-" ? a.Key : cand.MappedTo + "," + a.Key;
                    sb.AppendLine("  " + a.Key + " <- " + cand.Name);
                }
            }
            data.weapons = entries.ToArray();
        }

        private static int CountSockets(GameObject prefab)
        {
            string[] names = { ContentIds.WeaponMuzzle, ContentIds.WeaponGripR, ContentIds.WeaponGripL, ContentIds.WeaponMagazine, ContentIds.WeaponBolt, ContentIds.WeaponSight };
            int n = 0;
            foreach (var nm in names) if (prefab.transform.Find(nm) != null) n++;
            return n;
        }

        private static int ScoreFromLabels(UnityEngine.Object o)
        {
            foreach (var l in AssetDatabase.GetLabels(o))
            {
                var s = PurchasedPackRules.ParseScoreLabel(l);
                if (s >= 0) return s;
            }
            return -1;
        }

        private static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s ?? "x") sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        private static void AppendTable(StringBuilder sb, List<Candidate> rows, int charCount, int weaponCount)
        {
            sb.AppendLine($"  Bulunan aday: {charCount} karakter, {weaponCount} silah.");
            sb.AppendLine("  prefab | tri | eşlenen | soket");
            foreach (var r in rows)
                sb.AppendLine($"  {r.Path} | {r.Triangles} | {r.MappedTo} | {r.Sockets}");
            if (rows.Count == 0) sb.AppendLine("  (paket bulunamadı — Package Manager > My Assets > Import sonrası tekrar çalıştırın)");
        }
    }
}
