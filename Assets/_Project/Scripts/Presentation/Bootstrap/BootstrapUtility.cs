using System;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Bootstrap'ların ortak kurulum yardımcıları: sahne hazırlığı, motor sistemlerinin başlatılması, güvenli çağrı,
    /// zemin örnekleme ve yedek ortam (güneş ışığı, düz zemin). Tüm adımlar hata yutar ve günlüğe yazar — tek bir modülün
    /// hatası sahnenin geri kalanını durdurmamalı.
    /// </summary>
    internal static class BootstrapUtility
    {
        /// <summary>Zaman ölçeği, ses duraklatma, fizik çarpışma matrisi ve önceki sahneden kalan statik kayıtlar.</summary>
        public static void PrepareScene()
        {
            Time.timeScale = 1f;
            if (!ServerRuntime.IsDedicatedServer)
                AudioListener.pause = false;   // sunucuda ses kalıcı olarak kapalı (ServerRuntime.ConfigureProcess)
            Try(GameLayers.ConfigureCollisionMatrix, "GameLayers.ConfigureCollisionMatrix");
            ClearStaticRegistries();
        }

        /// <summary>Sahneler arası taşınmaması gereken statik kayıtları temizler.</summary>
        public static void ClearStaticRegistries()
        {
            Try(CombatantRegistry.Clear, "CombatantRegistry.Clear");
            Try(LootRegistry.Clear, "LootRegistry.Clear");
            Try(SmokeVolume.Clear, "SmokeVolume.Clear");
            Try(Project.Infrastructure.Drone.ReconDroneSystem.ResetAll, "ReconDroneSystem.ResetAll");
        }

        /// <summary>Ses, efekt, post-processing ve atmosfer (sis/gökyüzü/ortam ışığı).</summary>
        public static void InitializeEngineSystems(PostProcessing.Look look, int qualityLevel, bool withVfx)
        {
            Try(GameAudio.Initialize, "GameAudio.Initialize");
            Try(() => Project.Infrastructure.Vfx.GpuVfx.Tier = Mathf.Clamp(qualityLevel, 0, 3), "GpuVfx.Tier");
            if (withVfx)
                Try(GameVfx.Initialize, "GameVfx.Initialize");

            Try(() => PostProcessing.EnsureGlobalVolume(look), "PostProcessing.EnsureGlobalVolume");
            Try(() => PostProcessing.ApplyQuality(qualityLevel), "PostProcessing.ApplyQuality");
            Try(() => RenderSettingsUtil.ApplyOutdoorAtmosphere(look == PostProcessing.Look.Menu), "RenderSettingsUtil.ApplyOutdoorAtmosphere");
        }

        /// <summary>
        /// Arazi + kamera hazır olduktan sonra GPU çim sistemini kurar (arazi/kamera/shader yoksa sessizce atlanır).
        /// Maç, çatışma, antrenman ve benchmark kurulumlarının sunum aşamasından çağrılır.
        /// </summary>
        public static void InstallGrass(WorldMetadata world, int qualityLevel)
        {
            Try(() =>
            {
                var terrain = world != null && world.Terrain != null ? world.Terrain : Terrain.activeTerrain;
                if (terrain == null)
                    return;
                var camera = Camera.main;
                if (camera == null && CombatantRegistry.LocalPlayer != null)
                    camera = CombatantRegistry.LocalPlayer.GetComponentInChildren<Camera>();
                var water = world != null ? world.WaterLevel : float.NegativeInfinity;
                Project.Infrastructure.World.GrassSystem.Install(camera, terrain, Mathf.Clamp(qualityLevel, 0, 3), water);
            }, "GrassSystem.Install");
        }

        /// <summary>Sahnede yönlü ışık yoksa bir "güneş" oluşturur.</summary>
        public static void EnsureSun()
        {
            if (RenderSettings.sun != null && RenderSettings.sun.isActiveAndEnabled)
                return;

            var lights = UnityEngine.Object.FindObjectsByType<Light>();
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled)
                {
                    if (RenderSettings.sun == null)
                        RenderSettings.sun = lights[i];
                    return;
                }
            }

            var go = new GameObject("Güneş");
            go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
        }

        /// <summary>Dünya yoksa düşmeyi engelleyen düz yedek zemin (çarpıştırıcılı).</summary>
        public static GameObject CreateFallbackGround(float halfSize)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Yedek Zemin";
            ground.layer = GameLayers.Default;
            var size = Mathf.Max(20f, halfSize * 2f) / 10f;   // Plane 10 x 10 m
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(size, 1f, size);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = Try(() => MaterialLibrary.Get(MaterialId.Grass), "MaterialLibrary.Get(Grass)");
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return ground;
        }

        /// <summary>
        /// XZ'deki zemin noktası (yapı çatıları hariç tutulmaz): WorldMetadata → aşağı ışın → aktif arazi → y = 0.
        /// </summary>
        public static Vector3 GroundPoint(WorldMetadata world, Vector3 position)
        {
            if (world != null)
            {
                try
                {
                    var probe = new Vector3(position.x, world.SampleGroundHeight(position), position.z);
                    if (world.TryGetGroundPoint(probe, out var point))
                        return point;

                    return probe;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            var origin = new Vector3(position.x, 2000f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 4000f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            var terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
                return new Vector3(position.x, terrain.SampleHeight(position) + terrain.transform.position.y, position.z);

            return new Vector3(position.x, 0f, position.z);
        }

        /// <summary>Sahnedeki (oyuncuya ait olmayan) etkin kameraları ve dinleyicileri kapatır.</summary>
        public static void DisableStrayCameras(Camera[] sceneCameras, Transform keepRoot)
        {
            if (sceneCameras == null)
                return;

            for (var i = 0; i < sceneCameras.Length; i++)
            {
                var cam = sceneCameras[i];
                if (cam == null)
                    continue;

                if (keepRoot != null && cam.transform.IsChildOf(keepRoot))
                    continue;

                cam.enabled = false;
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = false;
            }
        }

        /// <summary>
        /// Kurulum sırasında yeni bir kamera (oyuncu kamerası, menü dekoru) eklendiyse kurulumdan önce sahnede bulunan
        /// kameraları/dinleyicileri kapatır. Yeni kamera yoksa hiçbirine dokunmaz. Değiştirme yapıldıysa true.
        /// </summary>
        public static bool DisableSceneCamerasIfReplaced(Camera[] before)
        {
            var now = Camera.allCameras;
            var replaced = false;
            for (var i = 0; i < now.Length; i++)
            {
                var cam = now[i];
                if (cam != null && (before == null || Array.IndexOf(before, cam) < 0))
                {
                    replaced = true;
                    break;
                }
            }

            if (replaced)
                DisableStrayCameras(before, null);

            return replaced;
        }

        /// <summary>Kamera kalmadıysa yukarıdan bakan bir gözlemci kamerası oluşturur (oyuncu oluşturulamazsa).</summary>
        public static Camera CreateObserverCamera(Vector3 lookAt)
        {
            var go = new GameObject("Gözlemci Kamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            cam.nearClipPlane = 0.3f;
            go.AddComponent<AudioListener>();
            go.transform.position = lookAt + new Vector3(0f, 120f, -160f);
            go.transform.LookAt(lookAt);
            return cam;
        }

        public static bool Try(Action action, string context)
        {
            if (action == null)
                return false;

            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Bootstrap] " + context + " başarısız: " + e.Message);
                Debug.LogException(e);
                return false;
            }
        }

        public static T Try<T>(Func<T> func, string context) where T : class
        {
            if (func == null)
                return null;

            try
            {
                return func();
            }
            catch (Exception e)
            {
                Debug.LogError("[Bootstrap] " + context + " başarısız: " + e.Message);
                Debug.LogException(e);
                return null;
            }
        }

        public static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
