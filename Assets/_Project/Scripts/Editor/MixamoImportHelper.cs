using System;
using System.IO;
using Project.Infrastructure.Content;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Mixamo FBX → Humanoid + lokomosyon Animator Controller üretici.
    /// Durumlar: idle, walk, run, crouch, prone, aim offset, reload, death.
    /// </summary>
    public sealed class MixamoImportHelper : EditorWindow
    {
        private const string DefaultControllerPath =
            "Assets/ThirdParty/Mixamo/Animators/SoldierLocomotion.controller";

        private DefaultAsset _fbxFolder;
        private AnimationClip _idle;
        private AnimationClip _walk;
        private AnimationClip _run;
        private AnimationClip _crouch;
        private AnimationClip _prone;
        private AnimationClip _reload;
        private AnimationClip _death;
        private AnimationClip _aimUp;
        private AnimationClip _aimDown;
        private string _controllerPath = DefaultControllerPath;
        private Vector2 _scroll;
        private string _log = string.Empty;

        [MenuItem("HAREKÂT/İçerik/Mixamo İçe Aktarma", priority = 42)]
        public static void Open()
        {
            var window = GetWindow<MixamoImportHelper>("Mixamo Helper");
            window.minSize = new Vector2(480f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "1) Mixamo FBX'leri Assets/ThirdParty/Mixamo/ altına koyun.\n" +
                "2) Klasör seçip Humanoid'e çevirin.\n" +
                "3) Klipleri atayıp Animator Controller üretin.",
                MessageType.Info);

            _fbxFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "FBX klasörü", _fbxFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("Klasördeki FBX'leri Humanoid yap"))
                ConvertFolderToHumanoid();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Animasyon klipleri", EditorStyles.boldLabel);
            _idle = ClipField("Idle", _idle);
            _walk = ClipField("Walk", _walk);
            _run = ClipField("Run", _run);
            _crouch = ClipField("Crouch / Crouch Walk", _crouch);
            _prone = ClipField("Prone / Crawl", _prone);
            _reload = ClipField("Reload", _reload);
            _death = ClipField("Death", _death);
            _aimUp = ClipField("Aim Up (opsiyonel)", _aimUp);
            _aimDown = ClipField("Aim Down (opsiyonel)", _aimDown);

            EditorGUILayout.Space(8f);
            _controllerPath = EditorGUILayout.TextField("Controller yolu", _controllerPath);
            if (GUILayout.Button("Animator Controller üret"))
                BuildController();

            if (GUILayout.Button("ContentOverrides asker Animator'ına bağla"))
                BindToContentOverrides();

            EditorGUILayout.Space(8f);
            if (!string.IsNullOrEmpty(_log))
                EditorGUILayout.HelpBox(_log, MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private static AnimationClip ClipField(string label, AnimationClip clip)
        {
            return (AnimationClip)EditorGUILayout.ObjectField(label, clip, typeof(AnimationClip), false);
        }

        private void ConvertFolderToHumanoid()
        {
            if (_fbxFolder == null)
            {
                _log = "FBX klasörü seçin.";
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(_fbxFolder);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                _log = "Geçerli bir klasör seçin.";
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ConfigureHumanoid(path))
                    count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log = count + " FBX Humanoid olarak ayarlandı (" + folderPath + ").";
        }

        /// <summary>Tek FBX'i Humanoid + animasyon içe aktarma ayarlarına getirir.</summary>
        public static bool ConfigureHumanoid(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                return false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
            return true;
        }

        private void BuildController()
        {
            if (_idle == null && _walk == null && _run == null)
            {
                _log = "En az idle/walk/run kliplerinden biri gerekli.";
                return;
            }

            EnsureParentFolders(_controllerPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_controllerPath) != null)
                AssetDatabase.DeleteAsset(_controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(_controllerPath);
            EnsureParameters(controller);

            var root = controller.layers[0].stateMachine;

            // Locomotion blend tree (Speed)
            var loco = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(loco, controller);
            if (_idle != null) loco.AddChild(_idle, 0f);
            if (_walk != null) loco.AddChild(_walk, 1.5f);
            if (_run != null) loco.AddChild(_run, 4f);
            if (_idle == null && _walk != null) loco.AddChild(_walk, 0f);

            var locoState = root.AddState("Locomotion", new Vector3(300f, 0f, 0f));
            locoState.motion = loco;
            root.defaultState = locoState;

            AddClipState(root, "Crouch", _crouch, new Vector3(300f, 80f, 0f));
            AddClipState(root, "Prone", _prone, new Vector3(300f, 160f, 0f));
            AddClipState(root, "Reload", _reload, new Vector3(560f, 40f, 0f));
            AddClipState(root, "Death", _death, new Vector3(560f, 120f, 0f));

            // Aim offset layer (optional)
            if (_aimUp != null || _aimDown != null || _idle != null)
            {
                controller.AddLayer("AimOffset");
                var layer = controller.layers[controller.layers.Length - 1];
                // layers is a copy — need SetLayer
                var layers = controller.layers;
                layers[layers.Length - 1].defaultWeight = 0.65f;
                layers[layers.Length - 1].blendingMode = AnimatorLayerBlendingMode.Override;
                controller.layers = layers;

                var aimTree = new BlendTree
                {
                    name = "AimPitch",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "AimPitch",
                    useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(aimTree, controller);
                var center = _idle != null ? _idle : (_aimUp ?? _aimDown);
                if (_aimDown != null) aimTree.AddChild(_aimDown, -1f);
                if (center != null) aimTree.AddChild(center, 0f);
                if (_aimUp != null) aimTree.AddChild(_aimUp, 1f);

                var aimSm = controller.layers[controller.layers.Length - 1].stateMachine;
                var aimState = aimSm.AddState("Aim", new Vector3(300f, 0f, 0f));
                aimState.motion = aimTree;
                aimSm.defaultState = aimState;
            }

            // Transitions
            WireBool(root, locoState, "Crouch", "Crouch", true);
            WireBool(root, locoState, "Prone", "Prone", true);
            WireTrigger(root, locoState, "Reload", "Reload");
            WireTrigger(root, locoState, "Death", "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            _log = "Controller üretildi: " + _controllerPath;
            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
        }

        private void BindToContentOverrides()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(_controllerPath);
            if (controller == null)
            {
                _log = "Önce controller üretin.";
                return;
            }

            var data = ContentOverridesSetup.EnsureAsset();
            if (data.soldier == null)
                data.soldier = new SoldierOverrideEntry();
            data.soldier.animatorController = controller;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            ContentOverrides.InvalidateCache();
            _log = "ContentOverrides.soldier.animatorController bağlandı.";
        }

        private static void EnsureParameters(AnimatorController controller)
        {
            AddParam(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParam(controller, "AimPitch", AnimatorControllerParameterType.Float);
            AddParam(controller, "Crouch", AnimatorControllerParameterType.Bool);
            AddParam(controller, "Prone", AnimatorControllerParameterType.Bool);
            AddParam(controller, "Reload", AnimatorControllerParameterType.Trigger);
            AddParam(controller, "Die", AnimatorControllerParameterType.Trigger);
        }

        private static void AddParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name)
                    return;
            }

            controller.AddParameter(name, type);
        }

        private static void AddClipState(AnimatorStateMachine root, string name, AnimationClip clip, Vector3 pos)
        {
            if (clip == null)
                return;
            var state = root.AddState(name, pos);
            state.motion = clip;
        }

        private static void WireBool(AnimatorStateMachine root, AnimatorState from, string toName, string param, bool value)
        {
            var to = FindState(root, toName);
            if (to == null)
                return;
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);

            var back = to.AddTransition(from);
            back.hasExitTime = false;
            back.duration = 0.15f;
            back.AddCondition(value ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0f, param);
        }

        private static void WireTrigger(AnimatorStateMachine root, AnimatorState from, string toName, string param)
        {
            var to = FindState(root, toName);
            if (to == null)
                return;
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.AddCondition(AnimatorConditionMode.If, 0f, param);

            if (toName != "Death")
            {
                var back = to.AddTransition(from);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine root, string name)
        {
            foreach (var s in root.states)
            {
                if (s.state != null && s.state.name == name)
                    return s.state;
            }

            return null;
        }

        private static void EnsureParentFolders(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir))
                return;

            var parts = dir.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
