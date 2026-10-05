using System;
using System.Collections.Generic;
using System.Text;
using Project.Application.Catalogs;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Content;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>HAREKÂT/İçerik/Varlık Eşleyici — hazır varlıkları ContentOverrides kimliklerine bağlar.</summary>
    public sealed class ContentMapperWindow : EditorWindow
    {
        private enum Tab
        {
            Silahlar = 0,
            Sesler,
            Malzemeler,
            Asker,
            Araçlar,
            Binalar,
            Csv
        }

        private ContentOverrides _data;
        private Tab _tab;
        private Vector2 _scroll;
        private string _status = string.Empty;
        private readonly StringBuilder _validation = new StringBuilder();

        [MenuItem("HAREKÂT/İçerik/Varlık Eşleyici", priority = 40)]
        public static void Open()
        {
            var window = GetWindow<ContentMapperWindow>("Varlık Eşleyici");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        [MenuItem("HAREKÂT/İçerik/ContentOverrides Oluştur", priority = 41)]
        public static void EnsureOverridesMenu()
        {
            var asset = ContentOverridesSetup.EnsureAsset();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("[HAREKÂT] ContentOverrides hazır: " + ContentOverrides.AssetPath);
        }

        private void OnEnable()
        {
            Reload();
        }

        private void Reload()
        {
            _data = ContentOverridesSetup.EnsureAsset();
            ContentOverrides.InvalidateCache();
            ContentOverrides.Load();
        }

        private void OnGUI()
        {
            if (_data == null)
                Reload();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Reload();
            if (GUILayout.Button("Kaydet", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                Save();
            if (GUILayout.Button("Doğrula", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                ValidateAll();
            if (GUILayout.Button("CSV Ön Doldur", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                PrefillFromCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[]
            {
                "Silahlar", "Sesler", "Malzemeler", "Asker", "Araçlar", "Binalar", "CSV"
            });

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Silahlar: DrawWeapons(); break;
                case Tab.Sesler: DrawSounds(); break;
                case Tab.Malzemeler: DrawMaterials(); break;
                case Tab.Asker: DrawSoldier(); break;
                case Tab.Araçlar: DrawVehicles(); break;
                case Tab.Binalar: DrawBuildings(); break;
                case Tab.Csv: DrawCsvPanel(); break;
            }

            EditorGUILayout.EndScrollView();

            if (_validation.Length > 0)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(_validation.ToString(), MessageType.Warning);
            }
        }

        private void Save()
        {
            if (_data == null)
                return;
            EditorUtility.SetDirty(_data);
            AssetDatabase.SaveAssets();
            ContentOverrides.InvalidateCache();
            _status = "Kaydedildi " + DateTime.Now.ToString("HH:mm:ss");
        }

        // ------------------------------------------------------------------ Drawers

        private void DrawWeapons()
        {
            EnsureWeaponSlots();
            EditorGUILayout.LabelField("Silah prefab (Muzzle / Grip_R / Grip_L)", EditorStyles.boldLabel);
            for (var i = 0; i < _data.weapons.Length; i++)
            {
                var e = _data.weapons[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUI.BeginChangeCheck();
                e.weaponId = EditorGUILayout.TextField("WeaponId", e.weaponId);
                e.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", e.prefab, typeof(GameObject), false);
                e.muzzleName = EditorGUILayout.TextField("Muzzle adı", e.muzzleName);
                e.gripRName = EditorGUILayout.TextField("Grip_R adı", e.gripRName);
                e.gripLName = EditorGUILayout.TextField("Grip_L adı", e.gripLName);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_data);

                if (e.prefab != null)
                {
                    var msg = ValidateWeaponPrefab(e);
                    if (!string.IsNullOrEmpty(msg))
                        EditorGUILayout.HelpBox(msg, MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox("Doğrulama OK", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawSounds()
        {
            EnsureSoundSlots();
            EditorGUILayout.LabelField("SoundId → AudioClip listesi (rastgele + volume/pitch)", EditorStyles.boldLabel);
            var so = new SerializedObject(_data);
            var soundsProp = so.FindProperty("sounds");
            EditorGUILayout.PropertyField(soundsProp, true);
            so.ApplyModifiedProperties();

            if (GUILayout.Button("Eksik SoundId satırlarını ekle"))
            {
                EnsureSoundSlots(forceAll: true);
                EditorUtility.SetDirty(_data);
            }
        }

        private void DrawMaterials()
        {
            EnsureMaterialSlots();
            EditorGUILayout.LabelField("MaterialId → Material", EditorStyles.boldLabel);
            for (var i = 0; i < _data.materials.Length; i++)
            {
                var e = _data.materials[i];
                EditorGUILayout.BeginHorizontal("box");
                EditorGUI.BeginChangeCheck();
                e.materialId = (MaterialId)EditorGUILayout.EnumPopup(e.materialId, GUILayout.Width(160f));
                e.material = (Material)EditorGUILayout.ObjectField(e.material, typeof(Material), false);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_data);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Eksik MaterialId satırlarını ekle"))
                EnsureMaterialSlots(forceAll: true);
        }

        private void DrawSoldier()
        {
            if (_data.soldier == null)
                _data.soldier = new SoldierOverrideEntry();

            EditorGUILayout.LabelField("Asker (humanoid + Animator)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _data.soldier.humanoidPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Humanoid prefab", _data.soldier.humanoidPrefab, typeof(GameObject), false);
            _data.soldier.animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Animator Controller", _data.soldier.animatorController, typeof(RuntimeAnimatorController), false);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_data);

            if (_data.soldier.humanoidPrefab != null)
            {
                var msg = ValidateHumanoid(_data.soldier.humanoidPrefab);
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(msg) ? "Humanoid OK" : msg,
                    string.IsNullOrEmpty(msg) ? MessageType.Info : MessageType.Warning);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Mixamo yardımcısını aç"))
                MixamoImportHelper.Open();
        }

        private void DrawVehicles()
        {
            EnsureVehicleSlots();
            EditorGUILayout.LabelField("Araçlar (kirpi, t70)", EditorStyles.boldLabel);
            for (var i = 0; i < _data.vehicles.Length; i++)
            {
                var e = _data.vehicles[i];
                EditorGUILayout.BeginHorizontal("box");
                EditorGUI.BeginChangeCheck();
                e.vehicleId = EditorGUILayout.TextField(e.vehicleId, GUILayout.Width(120f));
                e.prefab = (GameObject)EditorGUILayout.ObjectField(e.prefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_data);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawBuildings()
        {
            EnsureBuildingSlots();
            EditorGUILayout.LabelField("Bina stili → prefab listesi (isteğe bağlı)", EditorStyles.boldLabel);
            var so = new SerializedObject(_data);
            var buildings = so.FindProperty("buildings");
            EditorGUILayout.PropertyField(buildings, true);
            so.ApplyModifiedProperties();
        }

        private void DrawCsvPanel()
        {
            EditorGUILayout.LabelField("Design/Assets CSV (C3-1)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Kolon adları sabit. C3-1 doldurur; buradan kimlik satırları ContentOverrides'a ön doldurulur.\n" +
                ContentCsvSchema.DesignAssetsFolder + "/{" +
                ContentCsvSchema.MaterialsFile + ", " + ContentCsvSchema.SoundsFile + ", " +
                ContentCsvSchema.AnimationsFile + ", " + ContentCsvSchema.ModelsFile + "}",
                MessageType.Info);

            EditorGUILayout.LabelField("Klasör", ContentCsvImport.DesignAssetsAbsolutePath);
            if (GUILayout.Button("CSV'den ön doldur"))
                PrefillFromCsv();
            if (GUILayout.Button("CSV başlık şablonlarını yaz (boş satır yok)"))
            {
                WriteCsvHeaderStubs();
                _status = "CSV şablonları yazıldı";
            }
        }

        // ------------------------------------------------------------------ Prefill / slots

        private void PrefillFromCsv()
        {
            var added = 0;
            EnsureWeaponSlots();
            EnsureVehicleSlots();
            EnsureSoundSlots();
            EnsureMaterialSlots();
            EnsureBuildingSlots();

            if (ContentCsvImport.TryLoad(ContentCsvSchema.ModelsFile, out var models, out _))
            {
                var idCol = ContentCsvImport.FindColumn(models, ContentCsvSchema.ModelsColumns[0]);
                foreach (var row in models.Rows)
                {
                    var id = ContentCsvImport.Cell(row, idCol);
                    if (string.IsNullOrEmpty(id))
                        continue;
                    added += PrefillModelId(id) ? 1 : 0;
                }
            }

            if (ContentCsvImport.TryLoad(ContentCsvSchema.SoundsFile, out var sounds, out _))
            {
                var idCol = ContentCsvImport.FindColumn(sounds, ContentCsvSchema.SoundsColumns[0]);
                foreach (var row in sounds.Rows)
                {
                    var name = ContentCsvImport.Cell(row, idCol);
                    if (!Enum.TryParse(name, true, out SoundId soundId) || soundId == SoundId.None)
                        continue;
                    if (FindSoundIndex(soundId) < 0)
                    {
                        AppendSound(soundId);
                        added++;
                    }
                }
            }

            if (ContentCsvImport.TryLoad(ContentCsvSchema.MaterialsFile, out var mats, out _))
            {
                var idCol = ContentCsvImport.FindColumn(mats, ContentCsvSchema.MaterialsColumns[0]);
                foreach (var row in mats.Rows)
                {
                    var name = ContentCsvImport.Cell(row, idCol);
                    if (!Enum.TryParse(name, true, out MaterialId materialId))
                        continue;
                    if (FindMaterialIndex(materialId) < 0)
                    {
                        AppendMaterial(materialId);
                        added++;
                    }
                }
            }

            Save();
            _status = "CSV ön doldurma: +" + added + " satır";
            _validation.Clear();
            _validation.AppendLine("CSV okundu. Prefab/clip atamaları hâlâ sürükle-bırak ile yapılır.");
            if (ContentCsvImport.TryLoad(ContentCsvSchema.ModelsFile, out var m2, out var errM))
                _validation.AppendLine("models.csv: " + m2.Rows.Count + " satır");
            else
                _validation.AppendLine(errM ?? "models.csv yok");
        }

        private bool PrefillModelId(string id)
        {
            if (string.Equals(id, ContentIds.Soldier, StringComparison.OrdinalIgnoreCase))
                return false; // asker tek slot

            if (string.Equals(id, ContentIds.Kirpi, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, ContentIds.Helicopter, StringComparison.OrdinalIgnoreCase))
            {
                if (FindVehicleIndex(id) < 0)
                {
                    AppendVehicle(id);
                    return true;
                }

                return false;
            }

            if (Enum.TryParse(id, true, out BuildingStyle style))
            {
                if (FindBuildingIndex(style) < 0)
                {
                    AppendBuilding(style);
                    return true;
                }

                return false;
            }

            // WeaponId (bilinen önekler)
            if (LooksLikeWeaponId(id) && FindWeaponIndex(id) < 0)
            {
                AppendWeapon(id);
                return true;
            }

            return false;
        }

        private static bool LooksLikeWeaponId(string id)
        {
            return id.StartsWith("pistol_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("smg_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("ar_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("dmr_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("sr_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("lmg_", StringComparison.OrdinalIgnoreCase)
                   || id.StartsWith("sg_", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureWeaponSlots()
        {
            var known = new[]
            {
                WeaponIds.Sar9, WeaponIds.Tp9, WeaponIds.Sar109, WeaponIds.Mpt55, WeaponIds.Mpt76,
                WeaponIds.G3, WeaponIds.Knt76, WeaponIds.Jng90, WeaponIds.Pmt76, WeaponIds.Escort
            };
            var list = new List<WeaponOverrideEntry>(_data.weapons ?? Array.Empty<WeaponOverrideEntry>());
            foreach (var id in known)
            {
                if (FindWeaponIndex(id) < 0)
                    list.Add(new WeaponOverrideEntry { weaponId = id });
            }

            _data.weapons = list.ToArray();
        }

        private void EnsureSoundSlots(bool forceAll = false)
        {
            var list = new List<SoundOverrideEntry>(_data.sounds ?? Array.Empty<SoundOverrideEntry>());
            if (forceAll || list.Count == 0)
            {
                foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                {
                    if (id == SoundId.None)
                        continue;
                    if (FindSoundIndex(id) < 0)
                        list.Add(new SoundOverrideEntry { soundId = id });
                }
            }

            _data.sounds = list.ToArray();
        }

        private void EnsureMaterialSlots(bool forceAll = false)
        {
            var list = new List<MaterialOverrideEntry>(_data.materials ?? Array.Empty<MaterialOverrideEntry>());
            if (forceAll || list.Count == 0)
            {
                foreach (MaterialId id in Enum.GetValues(typeof(MaterialId)))
                {
                    if (FindMaterialIndex(id) < 0)
                        list.Add(new MaterialOverrideEntry { materialId = id });
                }
            }

            _data.materials = list.ToArray();
        }

        private void EnsureVehicleSlots()
        {
            var list = new List<VehicleOverrideEntry>(_data.vehicles ?? Array.Empty<VehicleOverrideEntry>());
            if (FindVehicleIndex(ContentIds.Kirpi) < 0)
                list.Add(new VehicleOverrideEntry { vehicleId = ContentIds.Kirpi });
            if (FindVehicleIndex(ContentIds.Helicopter) < 0)
                list.Add(new VehicleOverrideEntry { vehicleId = ContentIds.Helicopter });
            _data.vehicles = list.ToArray();
        }

        private void EnsureBuildingSlots()
        {
            var list = new List<BuildingOverrideEntry>(_data.buildings ?? Array.Empty<BuildingOverrideEntry>());
            if (list.Count == 0)
            {
                foreach (BuildingStyle style in Enum.GetValues(typeof(BuildingStyle)))
                    list.Add(new BuildingOverrideEntry { style = style });
            }

            _data.buildings = list.ToArray();
        }

        private int FindWeaponIndex(string id)
        {
            if (_data.weapons == null)
                return -1;
            for (var i = 0; i < _data.weapons.Length; i++)
            {
                if (_data.weapons[i] != null &&
                    string.Equals(_data.weapons[i].weaponId, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private int FindSoundIndex(SoundId id)
        {
            if (_data.sounds == null)
                return -1;
            for (var i = 0; i < _data.sounds.Length; i++)
            {
                if (_data.sounds[i] != null && _data.sounds[i].soundId == id)
                    return i;
            }

            return -1;
        }

        private int FindMaterialIndex(MaterialId id)
        {
            if (_data.materials == null)
                return -1;
            for (var i = 0; i < _data.materials.Length; i++)
            {
                if (_data.materials[i] != null && _data.materials[i].materialId == id)
                    return i;
            }

            return -1;
        }

        private int FindVehicleIndex(string id)
        {
            if (_data.vehicles == null)
                return -1;
            for (var i = 0; i < _data.vehicles.Length; i++)
            {
                if (_data.vehicles[i] != null &&
                    string.Equals(_data.vehicles[i].vehicleId, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private int FindBuildingIndex(BuildingStyle style)
        {
            if (_data.buildings == null)
                return -1;
            for (var i = 0; i < _data.buildings.Length; i++)
            {
                if (_data.buildings[i] != null && _data.buildings[i].style == style)
                    return i;
            }

            return -1;
        }

        private void AppendWeapon(string id)
        {
            var list = new List<WeaponOverrideEntry>(_data.weapons ?? Array.Empty<WeaponOverrideEntry>())
            {
                new WeaponOverrideEntry { weaponId = id }
            };
            _data.weapons = list.ToArray();
        }

        private void AppendSound(SoundId id)
        {
            var list = new List<SoundOverrideEntry>(_data.sounds ?? Array.Empty<SoundOverrideEntry>())
            {
                new SoundOverrideEntry { soundId = id }
            };
            _data.sounds = list.ToArray();
        }

        private void AppendMaterial(MaterialId id)
        {
            var list = new List<MaterialOverrideEntry>(_data.materials ?? Array.Empty<MaterialOverrideEntry>())
            {
                new MaterialOverrideEntry { materialId = id }
            };
            _data.materials = list.ToArray();
        }

        private void AppendVehicle(string id)
        {
            var list = new List<VehicleOverrideEntry>(_data.vehicles ?? Array.Empty<VehicleOverrideEntry>())
            {
                new VehicleOverrideEntry { vehicleId = id }
            };
            _data.vehicles = list.ToArray();
        }

        private void AppendBuilding(BuildingStyle style)
        {
            var list = new List<BuildingOverrideEntry>(_data.buildings ?? Array.Empty<BuildingOverrideEntry>())
            {
                new BuildingOverrideEntry { style = style }
            };
            _data.buildings = list.ToArray();
        }

        // ------------------------------------------------------------------ Validation

        private void ValidateAll()
        {
            _validation.Clear();
            if (_data.weapons != null)
            {
                foreach (var e in _data.weapons)
                {
                    if (e?.prefab == null)
                        continue;
                    var msg = ValidateWeaponPrefab(e);
                    if (!string.IsNullOrEmpty(msg))
                        _validation.AppendLine(e.weaponId + ": " + msg);
                }
            }

            if (_data.soldier?.humanoidPrefab != null)
            {
                var msg = ValidateHumanoid(_data.soldier.humanoidPrefab);
                if (!string.IsNullOrEmpty(msg))
                    _validation.AppendLine("asker: " + msg);
            }

            if (_validation.Length == 0)
                _validation.AppendLine("Tüm atanan varlıklar doğrulamadan geçti.");
            _status = "Doğrulama bitti";
        }

        public static string ValidateWeaponPrefab(WeaponOverrideEntry entry)
        {
            if (entry?.prefab == null)
                return "Prefab yok";

            var root = entry.prefab.transform;
            var scale = root.lossyScale;
            if (scale.x < 0.01f || scale.y < 0.01f || scale.z < 0.01f ||
                scale.x > 50f || scale.y > 50f || scale.z > 50f)
                return "Ölçek şüpheli (" + scale + ") — metre cinsinden olmalı";

            var muzzle = FindDeep(root, string.IsNullOrEmpty(entry.muzzleName) ? ContentIds.WeaponMuzzle : entry.muzzleName);
            if (muzzle == null)
                return "Muzzle Transform bulunamadı (beklenen: " + entry.muzzleName + ")";

            return null;
        }

        public static string ValidateHumanoid(GameObject prefab)
        {
            if (prefab == null)
                return "Prefab yok";

            var animator = prefab.GetComponentInChildren<Animator>();
            if (animator == null)
                return "Animator yok";

            var avatar = animator.avatar;
            if (avatar == null || !avatar.isHuman)
                return "Humanoid Avatar yok — Mixamo FBX'i Humanoid olarak içe aktarın";

            var scale = prefab.transform.lossyScale;
            if (Mathf.Abs(scale.y - 1f) > 0.35f)
                return "Ölçek ~1 olmalı (şimdi " + scale.y.ToString("0.00") + ")";

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void WriteCsvHeaderStubs()
        {
            var folder = ContentCsvImport.DesignAssetsAbsolutePath;
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            WriteHeaderIfMissing(folder, ContentCsvSchema.MaterialsFile, ContentCsvSchema.MaterialsColumns);
            WriteHeaderIfMissing(folder, ContentCsvSchema.SoundsFile, ContentCsvSchema.SoundsColumns);
            WriteHeaderIfMissing(folder, ContentCsvSchema.AnimationsFile, ContentCsvSchema.AnimationsColumns);
            WriteHeaderIfMissing(folder, ContentCsvSchema.ModelsFile, ContentCsvSchema.ModelsColumns);
            Debug.Log("[HAREKÂT] CSV başlıkları: " + folder);
        }

        private static void WriteHeaderIfMissing(string folder, string fileName, string[] columns)
        {
            var path = System.IO.Path.Combine(folder, fileName);
            if (System.IO.File.Exists(path))
                return;
            System.IO.File.WriteAllText(path, string.Join(",", columns) + "\n", Encoding.UTF8);
        }
    }
}
