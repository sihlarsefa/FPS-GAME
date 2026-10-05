using Project.Infrastructure;
using Project.Infrastructure.Rendering;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Ana menü sahnesinin giriş noktası: oturumu (ayarlar/kariyer) hazırlar, önceki maçtan kalan durumu temizler, sesi ve
    /// menü görünümünü (sıcak post-processing, atmosfer) başlatır; 3B dekoru (<see cref="MenuBackdrop"/>) ve menü
    /// arayüzünü (<see cref="MainMenuController"/>) kurar. İmleç serbesttir.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        private MenuBackdrop _backdrop;
        private MainMenuController _menu;

        public MenuBackdrop Backdrop => _backdrop;
        public MainMenuController Menu => _menu;

        private void Awake()
        {
            GameSession.EnsureInitialized();
            BootstrapUtility.PrepareScene();

            // Menüde maç kapsayıcısı olmamalı (önceki sahne temizlemediyse).
            if (GameContext.IsReady)
                GameContext.Clear();

            var quality = GameSession.Settings != null ? GameSession.Settings.Current.QualityLevel : 2;
            BootstrapUtility.InitializeEngineSystems(PostProcessing.Look.Menu, quality, false);
            BootstrapUtility.ReleaseCursor();
        }

        private void Start()
        {
            var sceneCameras = Camera.allCameras;

            _backdrop = FindAnyObjectByType<MenuBackdrop>();
            if (_backdrop == null)
                _backdrop = BootstrapUtility.Try(() => MenuBackdrop.Create(null), "MenuBackdrop.Create");

            // Dekor kendi kamerasını kurduysa sahnedeki varsayılan kameraları kapat (çift dinleyici/çizim olmasın);
            // hiç kamera yoksa sabit bir menü kamerası kur.
            var replaced = BootstrapUtility.DisableSceneCamerasIfReplaced(sceneCameras);
            if (!replaced && Camera.allCamerasCount == 0)
                CreateMenuCamera();

            _menu = FindAnyObjectByType<MainMenuController>();
            if (_menu == null)
            {
                var go = new GameObject("[Ana Menü]");
                _menu = BootstrapUtility.Try(() => go.AddComponent<MainMenuController>(), "MainMenuController");
            }

            BootstrapUtility.ReleaseCursor();
            GameSession.HideLoading();
        }

        private void Update()
        {
            // Menüde imleç her zaman serbest (başka bir sistem kilitlemiş olabilir).
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                BootstrapUtility.ReleaseCursor();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private static void CreateMenuCamera()
        {
            var go = new GameObject("Menü Kamerası");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 1500f;
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(new Vector3(0f, 2f, -8f), Quaternion.Euler(6f, 0f, 0f));
        }
    }
}
