using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.EditorTools
{
    /// <summary>Reproducible model exports and neutral studio renders of actual runtime geometry.</summary>
    public static class ModelArtStudio
    {
        private const string Output = "Assets/_Project/Generated/ModelArt";
        private static readonly Dictionary<Object, Object> Saved = new Dictionary<Object, Object>();
        private static int _assetId;

        [MenuItem("HAREKÂT/Sanat/Asker ve Silah Model Stüdyosu")]
        public static void Generate()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Output + "/MeshAssets");
            Directory.CreateDirectory("Art/ModelRenders");
            Directory.CreateDirectory("Art/Models");
            Saved.Clear(); _assetId = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.52f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.28f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.12f);
            RenderSettings.fog = false;
            Light("Key", new Vector3(35f, -35f, 0f), new Color(1f, 0.88f, 0.74f), 2.2f);
            Light("Fill", new Vector3(25f, 145f, 0f), new Color(0.60f, 0.75f, 1f), 1.0f);
            Light("Rim", new Vector3(130f, 15f, 0f), new Color(0.85f, 0.91f, 1f), 1.8f);
            var camera = new GameObject("StudioCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.058f);
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 30f;
            camera.allowHDR = true;
            camera.orthographic = true;
            var report = new StringBuilder("# Model Art Export\n\nRuntime geometry; metres; OBJ includes normals, UVs, materials and base textures.\n\n");
            var models = new List<GameObject>();
            for (var i = 0; i < 3; i++)
            {
                var look = SoldierLook.ForTeam(i, new System.Random(41 + i));
                look.Beret = false;
                look.FaceCover = i == 0 ? FaceCoverKind.Balaclava : FaceCoverKind.None;
                var soldier = SoldierModel.Build(null, look, null, false, 0);
                soldier.SetEquipment(i == 0 ? 3 : 2, 2, 1);
                soldier.name = "Soldier_" + i;
                // Relaxed display pose; runtime bones and their local origins stay intact.
                foreach (var bone in soldier.GetComponentsInChildren<Transform>())
                {
                    if (bone.name == "LeftShoulder") bone.localRotation = Quaternion.Euler(-8f, 0f, -8f);
                    if (bone.name == "RightShoulder") bone.localRotation = Quaternion.Euler(-8f, 0f, 8f);
                    if (bone.name == "LeftElbow" || bone.name == "RightElbow") bone.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                }
                var soldierRoot = soldier.gameObject;
                Freeze(soldierRoot);
                Export(soldierRoot, report);
                if (i == 0)
                {
                    Render(camera, new Vector3(2.3f, 1.50f, 4f), new Vector3(0f, 0.91f, 0f), 1.04f, "Soldier_Full");
                    Render(camera, new Vector3(1.5f, 1.80f, 4f), new Vector3(0f, 1.56f, 0f), 0.34f, "Soldier_Detail");
                    Render(camera, new Vector3(-2.3f, 1.5f, -4f), new Vector3(0f, 0.91f, 0f), 1.04f, "Soldier_Back");
                }
                if (i == 1) Render(camera, new Vector3(1.5f, 1.80f, 4f), new Vector3(0f, 1.56f, 0f), 0.34f, "Soldier_Unmasked");
                soldierRoot.SetActive(false); models.Add(soldierRoot);
            }
            foreach (WeaponStyle style in Enum.GetValues(typeof(WeaponStyle)))
            {
                if (style == WeaponStyle.None) continue;
                var weapon = WeaponModelFactory.BuildModel(style, null, null, 0, true);
                weapon.name = style.ToString();
                foreach (var r in weapon.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
                var weaponRoot = weapon.gameObject;
                Freeze(weaponRoot); Export(weaponRoot, report);
                if (style == WeaponStyle.Mpt76 || style == WeaponStyle.Mpt55 || style == WeaponStyle.Sar9)
                    Render(camera, new Vector3(2.4f, 0.80f, 1.4f), new Vector3(0f, 0.01f, style == WeaponStyle.Sar9 ? 0.045f : 0.15f),
                        style == WeaponStyle.Sar9 ? 0.16f : 0.58f, style.ToString());
                weaponRoot.SetActive(false); models.Add(weaponRoot);
            }
            for (var i = 0; i < models.Count; i++)
            {
                models[i].SetActive(true);
                models[i].transform.position = i < 3 ? new Vector3(i * 0.85f - 0.85f, 0f, 0f)
                    : new Vector3(3f + (i - 3) % 4 * 1.5f, 0.8f, (i - 3) / 4 * 1.2f);
            }
            camera.transform.position = new Vector3(3f, 2.2f, 5f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0f)); camera.orthographicSize = 1.6f;
            EditorSceneManager.SaveScene(scene, Output + "/ModelStudio.unity");
            AssetDatabase.SaveAssets();
            File.WriteAllText("Art/Models/README.md", report.ToString());
            Debug.Log("MODEL_ART_COMPLETE: " + models.Count + " models exported to Art/Models; renders in Art/ModelRenders");
        }

        private static void Light(string name, Vector3 euler, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(euler); light.color = color; light.intensity = intensity;
            light.shadows = LightShadows.Soft;
        }

        private static void Freeze(GameObject root)
        {
            // Display assets deliberately contain no gameplay scripts; runtime uses the same builders.
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t != root.transform && !t.gameObject.activeInHierarchy) Object.DestroyImmediate(t.gameObject);
        }

        private static void Export(GameObject root, StringBuilder report)
        {
            var name = root.name;
            var obj = new StringBuilder("mtllib " + name + ".mtl\n");
            var mtl = new StringBuilder();
            var offset = 1; var triangleCount = 0; var materialIndex = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; if (mesh == null) continue;
                var render = filter.GetComponent<Renderer>(); if (render == null || !render.enabled) continue;
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                obj.AppendLine("o " + filter.name);
                foreach (var vertex in mesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(vertex); obj.AppendLine(Form("v {0} {1} {2}", -p.x, p.y, p.z));
                }
                foreach (var uv in mesh.uv) obj.AppendLine(Form("vt {0} {1}", uv.x, uv.y));
                foreach (var normal in mesh.normals)
                {
                    var n = normalMatrix.MultiplyVector(normal).normalized; obj.AppendLine(Form("vn {0} {1} {2}", -n.x, n.y, n.z));
                }
                var mats = render.sharedMaterials;
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var mat = mats[Mathf.Min(sub, mats.Length - 1)];
                    var matName = "material_" + materialIndex++;
                    obj.AppendLine("usemtl " + matName);
                    var color = mat != null && mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.gray;
                    mtl.AppendLine("newmtl " + matName); mtl.AppendLine(Form("Kd {0} {1} {2}", color.r, color.g, color.b));
                    mtl.AppendLine("Ks 0.12 0.12 0.12\nNs 40");
                    if (mat != null && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") is Texture2D tex && tex.isReadable)
                    {
                        var file = name + "_" + matName + ".png";
                        File.WriteAllBytes("Art/Models/" + file, tex.EncodeToPNG()); mtl.AppendLine("map_Kd " + file);
                    }
                    var indices = mesh.GetTriangles(sub); triangleCount += indices.Length / 3;
                    for (var i = 0; i < indices.Length; i += 3)
                    {
                        var a = indices[i] + offset; var b = indices[i + 2] + offset; var c = indices[i + 1] + offset;
                        obj.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                    }
                }
                offset += mesh.vertexCount;
                filter.sharedMesh = Save(mesh);
                for (var i = 0; i < mats.Length; i++) mats[i] = SaveMaterial(mats[i]);
                render.sharedMaterials = mats;
            }
            File.WriteAllText("Art/Models/" + name + ".obj", obj.ToString());
            File.WriteAllText("Art/Models/" + name + ".mtl", mtl.ToString());
            PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
            report.AppendLine($"- {name}: {triangleCount:N0} triangles; OBJ + Unity display prefab.");
        }

        private static string Form(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);

        private static T Save<T>(T source) where T : Object
        {
            if (source == null || AssetDatabase.Contains(source)) return source;
            if (Saved.TryGetValue(source, out var saved)) return (T)saved;
            var clone = Object.Instantiate(source); clone.name = source.name; clone.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(clone, Output + "/MeshAssets/Model_" + _assetId++ + ".asset");
            Saved[source] = clone; return clone;
        }

        private static Material SaveMaterial(Material source)
        {
            if (source == null || AssetDatabase.Contains(source)) return source;
            if (Saved.TryGetValue(source, out var saved)) return (Material)saved;
            var clone = Save(source);
            foreach (var property in source.GetTexturePropertyNames())
                if (source.GetTexture(property) is Texture2D tex) clone.SetTexture(property, Save(tex));
            EditorUtility.SetDirty(clone); return clone;
        }

        private static void Render(Camera camera, Vector3 position, Vector3 target, float size, string name)
        {
            camera.transform.position = position; camera.transform.LookAt(target); camera.orthographicSize = size;
            var rt = new RenderTexture(1600, 1600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var image = new Texture2D(1600, 1600, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1600, 1600), 0, 0); image.Apply();
                File.WriteAllBytes("Art/ModelRenders/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
