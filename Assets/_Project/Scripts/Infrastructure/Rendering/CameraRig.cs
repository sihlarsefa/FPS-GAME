using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Birinci şahıs kamera düzeneği: dünya kamerası (taban, uzak kırpma 1500 m, post-processing, FXAA/SMAA, AudioListener)
    /// + silah kamerası (Viewmodel katmanı, yakın kırpma 0,01, sabit 60° FOV). URP'de silah kamerası Overlay olarak
    /// dünya kamerasının yığınına eklenir; URP yoksa daha yüksek derinlikte yalnız derinliği temizleyen ikinci kamera olur.
    /// Dünya kamerası düzeneğin kendi nesnesindedir (rig.transform == WorldCamera.transform).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRig : MonoBehaviour
    {
        public const float WorldNearClip = 0.05f;
        public const float WorldFarClip = 1500f;
        public const float ViewmodelNearClip = 0.01f;
        public const float ViewmodelFarClip = 20f;
        public const float ViewmodelFieldOfView = 60f;

        private static readonly List<CameraRig> ActiveRigs = new List<CameraRig>();
        private static int _antialiasingLevel = 2;

        private readonly List<AudioListener> _suppressedListeners = new List<AudioListener>();
        private bool _viewmodelVisible = true;
        private bool _usingUrpStack;

        public Camera WorldCamera { get; private set; }
        public Camera ViewmodelCamera { get; private set; }
        public AudioListener Listener { get; private set; }

        /// <summary>Dünya kamerasının dikey görüş açısı (derece).</summary>
        public float FieldOfView => WorldCamera != null ? WorldCamera.fieldOfView : 60f;

        /// <summary>Silah kamerası görünür mü (SetViewmodelVisible).</summary>
        public bool IsViewmodelVisible => _viewmodelVisible && ViewmodelCamera != null;

        /// <summary>Silah modellerinin bağlanacağı dönüşüm (silah kamerası; yoksa düzeneğin kendisi).</summary>
        public Transform ViewmodelRoot => ViewmodelCamera != null ? ViewmodelCamera.transform : transform;

        /// <summary>URP kamera yığını (taban + overlay) kullanılıyor mu? false ise yerleşik hat yedeği.</summary>
        public bool UsesUrpStack => _usingUrpStack;

        /// <summary>Şu an etkin olan düzenekler (kalite değişiminde AA güncellemesi için).</summary>
        public static IReadOnlyList<CameraRig> Active => ActiveRigs;

        /// <summary>
        /// Düzeneği oluşturur. parent null olabilir (sahne kökü). Dünya kamerası "MainCamera" etiketlidir.
        /// Sahnedeki diğer etkin AudioListener'lar bu düzenek yaşadığı sürece kapatılır ve yok edilince geri açılır.
        /// </summary>
        public static CameraRig Create(Transform parent, float fieldOfView, bool withViewmodel = true)
        {
            var go = new GameObject("CameraRig");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Kamera bileşenleri önce eklenir, CameraRig en son (Awake'i hazır kameraları bulsun diye).
            var world = go.AddComponent<Camera>();
            ConfigureWorldCamera(world, fieldOfView);
            go.tag = "MainCamera";

            Camera viewmodel = null;
            if (withViewmodel)
            {
                var vmGo = new GameObject("ViewmodelCamera");
                vmGo.layer = GameLayers.Viewmodel;
                vmGo.transform.SetParent(go.transform, false);
                viewmodel = vmGo.AddComponent<Camera>();
                ConfigureViewmodelCamera(viewmodel, world);
            }

            var rig = go.AddComponent<CameraRig>();
            rig.Bind(world, viewmodel);
            return rig;
        }

        /// <summary>Dünya kamerasının FOV'u (1..170). Silah kamerası sabit 60°'de kalır.</summary>
        public void SetFieldOfView(float fov)
        {
            if (WorldCamera == null)
                return;

            fov = Mathf.Clamp(fov, 1f, 170f);
            if (!Mathf.Approximately(WorldCamera.fieldOfView, fov))
                WorldCamera.fieldOfView = fov;
        }

        /// <summary>Silah kamerasını açar/kapatır (dürbün, araç, ölüm). Post-processing yönlendirmesi güncellenir.</summary>
        public void SetViewmodelVisible(bool visible)
        {
            _viewmodelVisible = visible;
            if (ViewmodelCamera != null && ViewmodelCamera.enabled != visible)
                ViewmodelCamera.enabled = visible;
            RoutePostProcessing();
        }

        /// <summary>Kenar yumuşatma seviyesi: 0 kapalı, 1 FXAA, 2-3 SMAA. Tüm etkin düzeneklere uygulanır.</summary>
        public static void SetAntialiasingLevel(int qualityLevel)
        {
            _antialiasingLevel = Mathf.Clamp(qualityLevel, 0, 3);
            for (var i = ActiveRigs.Count - 1; i >= 0; i--)
            {
                var rig = ActiveRigs[i];
                if (rig == null)
                {
                    ActiveRigs.RemoveAt(i);
                    continue;
                }

                rig.ApplyAntialiasing();
            }
        }

        /// <summary>
        /// Render hattı değiştiyse (kalite seviyesi farklı bir URP varlığı seçtiyse) tüm düzeneklerin kamera yığınını,
        /// kenar yumuşatmasını ve post-processing yönlendirmesini yeniden kurar.
        /// </summary>
        public static void RefreshAll()
        {
            for (var i = ActiveRigs.Count - 1; i >= 0; i--)
            {
                var rig = ActiveRigs[i];
                if (rig == null)
                {
                    ActiveRigs.RemoveAt(i);
                    continue;
                }

                rig.Refresh();
            }
        }

        /// <summary>Gökyüzü malzemesi yoksa dünya kameralarını sis rengine düz temizlemeye alır (RenderSettingsUtil çağırır).</summary>
        public static void RefreshSkyClear()
        {
            for (var i = ActiveRigs.Count - 1; i >= 0; i--)
            {
                var rig = ActiveRigs[i];
                if (rig == null)
                {
                    ActiveRigs.RemoveAt(i);
                    continue;
                }

                if (rig.WorldCamera != null)
                    ApplySkyClear(rig.WorldCamera);
            }
        }

        /// <summary>Bu düzeneğin hat kurulumunu yeniler (yığın, AA, post-processing).</summary>
        public void Refresh()
        {
            if (WorldCamera == null)
                return;

            // SetupPipeline idempotenttir: URP ↔ yerleşik hat geçişinde yığını/derinlik kamerasını yeniden kurar.
            SetupPipeline();
            ApplyAntialiasing();
            RoutePostProcessing();
            ApplySkyClear(WorldCamera);
        }

        // ---------------------------------------------------------------- Lifecycle

        private void Awake()
        {
            // Sahneye elle eklenmişse (Create kullanılmadan) mevcut kameraları bağla.
            if (WorldCamera == null)
            {
                var world = GetComponent<Camera>();
                if (world != null)
                {
                    Camera viewmodel = null;
                    for (var i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i).GetComponent<Camera>();
                        if (child != null && child.gameObject.layer == GameLayers.Viewmodel)
                        {
                            viewmodel = child;
                            break;
                        }
                    }

                    Bind(world, viewmodel);
                }
            }
        }

        private void OnEnable()
        {
            if (!ActiveRigs.Contains(this))
                ActiveRigs.Add(this);

            if (WorldCamera != null)
            {
                // Pasifken değişmiş olabilecek küresel ayarları (AA, gökyüzü) yakala.
                ApplyAntialiasing();
                RoutePostProcessing();
                ApplySkyClear(WorldCamera);
            }
        }

        private void OnDisable()
        {
            ActiveRigs.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveRigs.Remove(this);

            if (_usingUrpStack && WorldCamera != null && ViewmodelCamera != null)
            {
                var data = WorldCamera.GetUniversalAdditionalCameraData();
                if (data != null && data.cameraStack != null)
                    data.cameraStack.Remove(ViewmodelCamera);
            }

            // Kapattığımız dinleyicileri geri aç.
            for (var i = 0; i < _suppressedListeners.Count; i++)
            {
                var other = _suppressedListeners[i];
                if (other != null)
                    other.enabled = true;
            }

            _suppressedListeners.Clear();
        }

        // ---------------------------------------------------------------- Setup

        private void Bind(Camera world, Camera viewmodel)
        {
            if (WorldCamera != null)
                return;

            WorldCamera = world;
            ViewmodelCamera = viewmodel;

            Listener = GetComponent<AudioListener>();
            if (Listener == null)
                Listener = gameObject.AddComponent<AudioListener>();
            SuppressOtherListeners();

            SetupPipeline();
            ApplyAntialiasing();
            RoutePostProcessing();

            if (isActiveAndEnabled && !ActiveRigs.Contains(this))
                ActiveRigs.Add(this);
        }

        private static void ConfigureWorldCamera(Camera camera, float fieldOfView)
        {
            camera.fieldOfView = Mathf.Clamp(fieldOfView <= 0f ? 75f : fieldOfView, 1f, 170f);
            camera.nearClipPlane = WorldNearClip;
            camera.farClipPlane = WorldFarClip;
            camera.cullingMask = ~(1 << GameLayers.Viewmodel);
            ApplySkyClear(camera);
            camera.depth = 0f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = true;
        }

        private static void ApplySkyClear(Camera camera)
        {
            if (camera == null)
                return;

            camera.backgroundColor = RenderSettingsUtil.BackgroundColor;
            var clear = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (camera.clearFlags != clear)
                camera.clearFlags = clear;
        }

        private static void ConfigureViewmodelCamera(Camera camera, Camera world)
        {
            camera.fieldOfView = ViewmodelFieldOfView;
            camera.nearClipPlane = ViewmodelNearClip;
            camera.farClipPlane = ViewmodelFarClip;
            camera.cullingMask = 1 << GameLayers.Viewmodel;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.depth = world.depth + 1f;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
        }

        /// <summary>URP etkinse taban/overlay yığınını kurar; değilse yerleşik hat yedeği (derinlik temizleyen ikinci kamera).</summary>
        private void SetupPipeline()
        {
            _usingUrpStack = false;
            if (!RenderPipelineInfo.IsUrpActive)
            {
                if (ViewmodelCamera != null)
                {
                    ViewmodelCamera.clearFlags = CameraClearFlags.Depth;
                    ViewmodelCamera.depth = WorldCamera.depth + 1f;
                }

                return;
            }

            var worldData = WorldCamera.GetUniversalAdditionalCameraData();
            if (worldData != null)
            {
                worldData.renderType = CameraRenderType.Base;
                worldData.renderShadows = true;
                worldData.requiresDepthTexture = false;
                worldData.renderPostProcessing = true;
            }

            if (ViewmodelCamera == null || worldData == null)
                return;

            var vmData = ViewmodelCamera.GetUniversalAdditionalCameraData();
            if (vmData == null)
                return;

            vmData.renderType = CameraRenderType.Overlay;
            vmData.renderShadows = false;
            vmData.requiresDepthTexture = false;
            vmData.renderPostProcessing = false;

            var stack = worldData.cameraStack;
            if (stack != null)
            {
                if (!stack.Contains(ViewmodelCamera))
                    stack.Add(ViewmodelCamera);
                _usingUrpStack = true;
            }
        }

        /// <summary>
        /// URP yığınında post-processing'i iki kez uygulamamak için yalnız SON kamerada açar: silah kamerası görünürken
        /// overlay'de (tüm yığına uygulanır, silah da tonlanır), gizliyken dünya kamerasında.
        /// </summary>
        private void RoutePostProcessing()
        {
            if (!RenderPipelineInfo.IsUrpActive || WorldCamera == null)
                return;

            var worldData = WorldCamera.GetUniversalAdditionalCameraData();
            if (worldData == null)
                return;

            // Düzenek pasifken de doğru yönlendirilsin diye isActiveAndEnabled değil kameranın kendi durumu kullanılır.
            var overlayActive = _usingUrpStack && _viewmodelVisible && ViewmodelCamera != null && ViewmodelCamera.enabled
                && ViewmodelCamera.gameObject.activeSelf;
            worldData.renderPostProcessing = !overlayActive;

            if (ViewmodelCamera != null)
            {
                var vmData = ViewmodelCamera.GetUniversalAdditionalCameraData();
                if (vmData != null)
                    vmData.renderPostProcessing = overlayActive;
            }
        }

        private void ApplyAntialiasing()
        {
            if (!RenderPipelineInfo.IsUrpActive || WorldCamera == null)
                return;

            var mode = _antialiasingLevel <= 0
                ? AntialiasingMode.None
                : _antialiasingLevel == 1
                    ? AntialiasingMode.FastApproximateAntialiasing
                    : AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // Overlay kameralar tabanın AA ayarını devralır; yine de tutarlı olsun diye ikisine de yazılır.
            var worldData = WorldCamera.GetUniversalAdditionalCameraData();
            if (worldData != null)
                worldData.antialiasing = mode;

            if (ViewmodelCamera != null)
            {
                var vmData = ViewmodelCamera.GetUniversalAdditionalCameraData();
                if (vmData != null)
                    vmData.antialiasing = mode;
            }
        }

        private void SuppressOtherListeners()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            for (var i = 0; i < listeners.Length; i++)
            {
                var other = listeners[i];
                if (other == null || other == Listener || !other.enabled)
                    continue;

                other.enabled = false;
                _suppressedListeners.Add(other);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveRigs.Clear();
            _antialiasingLevel = 2;
        }
    }
}
