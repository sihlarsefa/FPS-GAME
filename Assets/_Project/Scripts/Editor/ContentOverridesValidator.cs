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
    /// ContentOverrides doğrulayıcı + isim kuralıyla otomatik doldurma.
    /// Menü: HAREKÂT/İçerik/Doğrula ve Rapor, HAREKÂT/İçerik/İsim Kuralıyla Doldur.
    /// </summary>
    public static class ContentOverridesValidator
    {
        private const string ReadmePath = "Assets/ThirdParty/README.md";
        private const string ReportPath = "Library/ContentValidationReport.txt";

        [MenuItem("HAREKÂT/İçerik/İsim Kuralıyla Doldur (HK_W_*, HK_V_*)", priority = 50)]
        public static void AutoFillMenu()
        {
            var n = AutoFill(ContentOverridesSetup.EnsureAsset());
            Debug.Log($"[İçerik] İsim kuralıyla {n} slot dolduruldu.");
        }

        [MenuItem("HAREKÂT/İçerik/Doğrula ve Rapor", priority = 51)]
        public static void ValidateMenu()
        {
            var report = Validate(ContentOverridesSetup.EnsureAsset());
            try { File.WriteAllText(ReportPath, report); } catch (Exception) { /* yoksay */ }
            Debug.Log("[İçerik] Doğrulama raporu (" + ReportPath + "):\n" + report);
        }

        /// <summary>Proje genelinde HK_* adlı prefabları tarar, boş slotları doldurur. Dönen: eklenen/ayarlanan sayı.</summary>
        public static int AutoFill(ContentOverrides data)
        {
            if (data == null) return 0;
            var count = 0;
            var guids = AssetDatabase.FindAssets("t:Prefab HK_");
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !ContentNameConvention.TryParse(prefab.name, out var kind, out var id))
                    continue;
                switch (kind)
                {
                    case ContentNameKind.Weapon:
                        if (FindWeapon(data, id) == null)
                        {
                            Append(ref data.weapons, new WeaponOverrideEntry { weaponId = id, prefab = prefab });
                            count++;
                        }
                        break;
                    case ContentNameKind.Vehicle:
                        if (!HasVehicle(data, id))
                        {
                            Append(ref data.vehicles, new VehicleOverrideEntry { vehicleId = id, prefab = prefab });
                            count++;
                        }
                        break;
                    case ContentNameKind.Vegetation:
                        count += AddToList(ref data.vegetation, ContentNameConvention.StripVariantSuffix(id), prefab,
                            e => e.speciesId, e => e.prefabs, (e, p) => e.prefabs = p,
                            s => new VegetationOverrideEntry { speciesId = s });
                        break;
                    case ContentNameKind.Rock:
                        count += AddToList(ref data.rocks, ContentNameConvention.StripVariantSuffix(id), prefab,
                            e => e.sizeClass, e => e.prefabs, (e, p) => e.prefabs = p,
                            s => new RockOverrideEntry { sizeClass = s });
                        break;
                    case ContentNameKind.Prop:
                        if (!HasProp(data, id))
                        {
                            Append(ref data.props, new PropOverrideEntry { propId = id, prefab = prefab });
                            count++;
                        }
                        break;
                }
            }

            if (count > 0)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                ContentOverrides.InvalidateCache();
            }
            return count;
        }

        public static string Validate(ContentOverrides data)
        {
            var sb = new StringBuilder();
            var counts = new int[3];
            var readme = File.Exists(ReadmePath) ? File.ReadAllText(ReadmePath) : "";
            sb.AppendLine("HAREKÂT İçerik Doğrulama Raporu — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            if (data == null) { sb.AppendLine("HATA ContentOverrides yok."); return sb.ToString(); }

            foreach (var w in data.weapons ?? new WeaponOverrideEntry[0])
                if (w != null) Slot(sb, counts, "Silah/" + w.weaponId, w.prefab, ContentNameKind.Weapon, readme, 0.3f, 1.8f);
            foreach (var v in data.vehicles ?? new VehicleOverrideEntry[0])
                if (v != null) Slot(sb, counts, "Araç/" + v.vehicleId, v.prefab, ContentNameKind.Vehicle, readme, 3f, 25f);
            foreach (var b in data.buildings ?? new BuildingOverrideEntry[0])
                if (b?.prefabs != null)
                    foreach (var p in b.prefabs) Slot(sb, counts, "Bina/" + b.style, p, ContentNameKind.Building, readme, 3f, 60f);
            foreach (var e in data.vegetation ?? new VegetationOverrideEntry[0])
                if (e?.prefabs != null)
                    foreach (var p in e.prefabs) Slot(sb, counts, "Bitki/" + e.speciesId, p, ContentNameKind.Vegetation, readme, 0.3f, 40f);
            foreach (var e in data.rocks ?? new RockOverrideEntry[0])
                if (e?.prefabs != null)
                    foreach (var p in e.prefabs) Slot(sb, counts, "Kaya/" + e.sizeClass, p, ContentNameKind.Rock, readme, 0.2f, 15f);
            foreach (var e in data.props ?? new PropOverrideEntry[0])
                if (e != null) Slot(sb, counts, "Prop/" + e.propId, e.prefab, ContentNameKind.Prop, readme, 0.1f, 10f);
            if (data.viewmodelArms != null && data.viewmodelArms.armsPrefab != null)
                Slot(sb, counts, "Kollar", data.viewmodelArms.armsPrefab, ContentNameKind.None, readme, 0.3f, 1.5f);

            foreach (var t in data.terrainLayers ?? new TerrainLayerOverrideEntry[0])
                if (t != null) Line(sb, counts, "ArazıKatmanı/" + t.materialId, t.layer == null ? ContentSeverity.Hata : ContentSeverity.Ok, t.layer == null ? "TerrainLayer boş" : "");
            foreach (var s in data.skies ?? new SkyOverrideEntry[0])
                if (s != null) Line(sb, counts, "Gökyüzü/" + s.skyId, s.hdri == null ? ContentSeverity.Hata : ContentSeverity.Ok, s.hdri == null ? "HDRI boş" : "");
            foreach (var d in data.decalSets ?? new DecalSetOverrideEntry[0])
                if (d != null) Line(sb, counts, "Decal/" + d.surface, d.decalMaterial == null ? ContentSeverity.Hata : ContentSeverity.Ok, d.decalMaterial == null ? "Materyal boş" : "");
            foreach (var a in data.weaponAnimations ?? new WeaponAnimationOverrideEntry[0])
                if (a != null) Line(sb, counts, "SilahAnim/" + a.weaponId, a.overrideController == null ? ContentSeverity.Hata : ContentSeverity.Ok, a.overrideController == null ? "Controller boş" : "");

            sb.AppendLine();
            try { counts[2] += CreditsAudit.AppendTo(sb); }
            catch (Exception ex) { sb.AppendLine("UYARI Lisans denetimi çalışmadı: " + ex.Message); counts[1]++; }

            sb.AppendLine($"Özet: OK={counts[0]} UYARI={counts[1]} HATA={counts[2]}");
            return sb.ToString();
        }

        private static void Slot(StringBuilder sb, int[] counts, string slot, GameObject prefab,
            ContentNameKind kind, string readme, float minM, float maxM)
        {
            if (prefab == null) { Line(sb, counts, slot, ContentSeverity.Hata, "prefab boş"); return; }
            var notes = new List<string>();
            var worst = ContentSeverity.Ok;
            void Add(ContentSeverity s, string msg)
            {
                if (s == ContentSeverity.Ok) return;
                if (s > worst) worst = s;
                notes.Add(ContentValidationRules.Label(s) + ": " + msg);
            }

            if (kind == ContentNameKind.Weapon)
                foreach (var sock in ContentValidationRules.RequiredWeaponSockets)
                    Add(ContentValidationRules.CheckSocket(sock, FindDeep(prefab.transform, sock) != null), "soket yok: " + sock);

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var tris = 0;
            var hasBounds = false;
            var bounds = new Bounds();
            var shaderBad = new HashSet<string>();
            foreach (var r in renderers)
            {
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; } else bounds.Encapsulate(r.bounds);
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && ContentValidationRules.CheckShader(m.shader.name) != ContentSeverity.Ok)
                        shaderBad.Add(m.shader.name);
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                if (mesh != null && IsLod0(r, prefab)) tris += (int)(mesh.triangles.Length / 3);
            }

            if (hasBounds)
            {
                var ext = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (kind != ContentNameKind.None || minM > 0f)
                    Add(ContentValidationRules.CheckScale(ext, minM, maxM), $"ölçek {ext:0.##} m (beklenen {minM}-{maxM} m, 1 birim = 1 m)");
                var local = prefab.transform.InverseTransformPoint(bounds.center);
                var bottom = new Vector3(local.x, bounds.min.y - prefab.transform.position.y, local.z);
                Add(ContentValidationRules.CheckPivot(new Vector2(bottom.x, bottom.z).magnitude, ext), "pivot taban/merkezden uzak");
            }
            else Add(ContentSeverity.Hata, "Renderer yok");

            Add(ContentValidationRules.CheckTriangles(tris, ContentValidationRules.TriangleBudget(kind)),
                $"üçgen {tris} > bütçe {ContentValidationRules.TriangleBudget(kind)}");
            Add(ContentValidationRules.CheckLodGroup(kind, prefab.GetComponentInChildren<LODGroup>(true) != null), "LODGroup yok");
            foreach (var s in shaderBad) Add(ContentSeverity.Uyari, "shader URP/Lit değil: " + s);

            var assetPath = AssetDatabase.GetAssetPath(prefab);
            if (!ContentValidationRules.HasLicenseRecord(readme, prefab.name, assetPath))
                Add(ContentSeverity.Hata, "lisans kaydı yok (Assets/ThirdParty/README.md)");

            Line(sb, counts, slot, worst, string.Join("; ", notes));
        }

        private static bool IsLod0(Renderer r, GameObject root)
        {
            var lod = root.GetComponentInChildren<LODGroup>(true);
            if (lod == null) return true;
            var lods = lod.GetLODs();
            if (lods.Length == 0) return true;
            foreach (var rr in lods[0].renderers) if (rr == r) return true;
            return false;
        }

        private static void Line(StringBuilder sb, int[] counts, string slot, ContentSeverity s, string note)
        {
            counts[(int)s]++;
            sb.Append('[').Append(ContentValidationRules.Label(s)).Append("] ").Append(slot);
            if (!string.IsNullOrEmpty(note)) sb.Append(" — ").Append(note);
            sb.AppendLine();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        private static WeaponOverrideEntry FindWeapon(ContentOverrides d, string id)
        {
            if (d.weapons == null) return null;
            foreach (var w in d.weapons)
                if (w != null && string.Equals(w.weaponId, id, StringComparison.OrdinalIgnoreCase)) return w;
            return null;
        }

        private static bool HasVehicle(ContentOverrides d, string id)
        {
            if (d.vehicles == null) return false;
            foreach (var v in d.vehicles)
                if (v != null && string.Equals(v.vehicleId, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasProp(ContentOverrides d, string id)
        {
            if (d.props == null) return false;
            foreach (var v in d.props)
                if (v != null && string.Equals(v.propId, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Append<T>(ref T[] arr, T item)
        {
            var old = arr ?? new T[0];
            var n = new T[old.Length + 1];
            Array.Copy(old, n, old.Length);
            n[old.Length] = item;
            arr = n;
        }

        private static int AddToList<T>(ref T[] arr, string key, GameObject prefab,
            Func<T, string> getKey, Func<T, GameObject[]> getList, Action<T, GameObject[]> setList, Func<string, T> make) where T : class
        {
            T entry = null;
            if (arr != null)
                foreach (var e in arr)
                    if (e != null && string.Equals(getKey(e), key, StringComparison.OrdinalIgnoreCase)) { entry = e; break; }
            if (entry == null) { entry = make(key); Append(ref arr, entry); }
            var list = getList(entry) ?? new GameObject[0];
            foreach (var p in list) if (p == prefab) return 0;
            var n = new GameObject[list.Length + 1];
            Array.Copy(list, n, list.Length);
            n[list.Length] = prefab;
            setList(entry, n);
            return 1;
        }
    }
}
