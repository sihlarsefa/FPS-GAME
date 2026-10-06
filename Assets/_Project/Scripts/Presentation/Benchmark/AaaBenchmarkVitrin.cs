using System;
using System.Collections.Generic;
using System.IO;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Benchmark
{
    /// <summary>Benchmark sahnesi çalışma modu.</summary>
    public enum BenchmarkMode
    {
        /// <summary>Oynanabilir: oyuncu serbest.</summary>
        Oyna = 0,

        /// <summary>Vitrin: oyuncu girdisi kapalı, 6 planlık betikli kamera yolu döngüde.</summary>
        Vitrin = 1
    }

    /// <summary>
    /// Vitrin yönetmeni. Kamera: oyuncunun dünya kamerası (CameraRig) betikli planlarda LateUpdate sonunda taşınır
    /// (böylece URP renderer özellikleri/post-process aynen çalışır); silah planlarında (yakın plan, ateş) oyuncu kamerası
    /// dokunulmadan kalır ve silah <see cref="PlayerWeaponHandler.Tick"/> (dt=0) ile betikli olarak tetiklenir/şarjör değiştirir.
    /// F9: Oyna/Vitrin geçişi, F12: ekran görüntüsü → Logs/benchmark/*.png.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class AaaBenchmarkVitrin : MonoBehaviour
    {
        public const string ScreenshotFolder = "Logs/benchmark";

        private PlayerController _player;
        private GameplayUiController _ui;
        private Vector3 _spawnPoint;
        private float _spawnYaw;
        private List<BenchmarkShot> _shots;
        private Func<BenchmarkShotPath.Anchors> _anchorProvider;
        private BenchmarkShotPath.Anchors _anchors;
        private float _nextRefresh;
        private float _holdUntil;
        private float _aimYaw;

        /// <summary>Her plan başında kamera donuk bekler (TAA/özne oturması).</summary>
        public const float SettleSeconds = 0.5f;
        private BenchmarkMode _mode = BenchmarkMode.Oyna;
        private float _time;
        private int _lastShot = -1;
        private bool _reloadSent;
        private bool _wasFiring;
        private int _shotCounter;
        private float _toastUntil;
        private string _toast = string.Empty;
        private Camera _camera;
        private float _restoreFov = 80f;
        private bool _viewmodelHidden;
        private string _shotName = string.Empty;
        private AaaBenchmarkShooter _shooter;

        public BenchmarkMode Mode => _mode;
        public string CurrentShotName => _shotName;
        public int ShotCount => _shots != null ? _shots.Count : 0;

        /// <summary>Plan adı (otomatik ekran görüntüsü için); geçersiz sıra boş döner.</summary>
        public string ShotNameAt(int index) => _shots != null && index >= 0 && index < _shots.Count ? _shots[index].Name : string.Empty;

        /// <summary>
        /// Vitrin zamanını verilen planın başına (+ saniye) atlatır; Vitrin modunda değilse geçirir.
        /// Otomatik ekran görüntüsü (-otoekran) planları tek tek yakalamak için kullanır.
        /// </summary>
        public void SeekToShot(int index, float intoShotSeconds)
        {
            if (_shots == null || _shots.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, _shots.Count - 1);
            if (_mode != BenchmarkMode.Vitrin)
                SetMode(BenchmarkMode.Vitrin);
            var start = 0f;
            for (var i = 0; i < index; i++)
                start += _shots[i].Duration;
            _time = start + Mathf.Clamp(intoShotSeconds, 0f, _shots[index].Duration - 0.01f);
            RefreshShots();
        }

        /// <summary>Ana 6 plan Ultra kalite kademesinde çekilir; değilse yükseltir.</summary>
        public static void EnsureUltraTier()
        {
            if (QualityTierApplier.LastTier == AaaBenchmarkFeatureInstaller.UltraTier)
                return;
            BootstrapUtility.Try(() => PostProcessing.ApplyQuality(AaaBenchmarkFeatureInstaller.UltraTier), "Vitrin: Ultra kademe");
        }

        /// <summary>Çapaları canlı öznelerden yeniler (eksik özne yeniden doğar) ve plan listesini yeniden kurar.</summary>
        private void RefreshShots()
        {
            if (_anchorProvider == null)
                return;
            BenchmarkShotPath.Anchors anchors;
            try { anchors = _anchorProvider(); }
            catch (Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Vitrin çapaları alınamadı: " + e.Message);
                return;
            }

            _anchors = anchors;
            _aimYaw = BenchmarkShotPath.YawToward(_spawnPoint, anchors.Targets, _spawnYaw);
            var rebuilt = BenchmarkShotPath.Build(anchors);
            if (rebuilt.Count == (_shots != null ? _shots.Count : -1))
                _shots = rebuilt;
            _nextRefresh = Time.unscaledTime + 0.2f;
        }

        internal void Initialize(PlayerController player, GameplayUiController ui, Vector3 spawnPoint, float spawnYaw,
            Func<BenchmarkShotPath.Anchors> anchorProvider, BenchmarkMode startMode, AaaBenchmarkShooter shooter = null)
        {
            _shooter = shooter;
            _player = player;
            _ui = ui;
            _spawnPoint = spawnPoint;
            _spawnYaw = spawnYaw;
            _anchorProvider = anchorProvider;
            _anchors = anchorProvider != null ? anchorProvider() : default;
            _aimYaw = BenchmarkShotPath.YawToward(spawnPoint, _anchors.Targets, spawnYaw);
            _shots = BenchmarkShotPath.Build(_anchors);
            SetMode(startMode);
        }

        public void SetMode(BenchmarkMode mode)
        {
            if (mode == _mode && _lastShot != -1)
                return;

            var leaving = _mode == BenchmarkMode.Vitrin && mode != BenchmarkMode.Vitrin;
            _mode = mode;
            _time = 0f;
            _lastShot = -2;
            _reloadSent = false;
            _wasFiring = false;
            ResolveCamera();

            if (_player != null)
                _player.InputEnabled = mode == BenchmarkMode.Oyna;
            if (_ui != null)
                _ui.SetHudVisible(mode == BenchmarkMode.Oyna);

            if (mode == BenchmarkMode.Vitrin)
            {
                if (_camera != null)
                    _restoreFov = _camera.fieldOfView;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                Toast("VİTRİN — F9: oynanabilir moda dön, F12: ekran görüntüsü");
            }
            else
            {
                if (leaving)
                    RestoreViewmodel();
                Toast("OYNA — F9: vitrine geç, F12: ekran görüntüsü");
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f9Key.wasPressedThisFrame)
                    SetMode(_mode == BenchmarkMode.Oyna ? BenchmarkMode.Vitrin : BenchmarkMode.Oyna);
                if (kb.f12Key.wasPressedThisFrame)
                    TakeScreenshot();
            }
        }

        private void LateUpdate()
        {
            if (_mode != BenchmarkMode.Vitrin || _shots == null || _shots.Count == 0)
                return;

            ResolveCamera();
            if (Time.unscaledTime >= _holdUntil)
                _time += Time.deltaTime;
            if (Time.unscaledTime >= _nextRefresh && _lastShot != -2)
                RefreshShots();
            var pose = BenchmarkShotPath.Evaluate(_shots, _time);
            _shotName = _shots[pose.ShotIndex].Name;
            var shotSeconds = pose.ShotTime01 * _shots[pose.ShotIndex].Duration;

            if (pose.ShotIndex != _lastShot)
            {
                _lastShot = pose.ShotIndex;
                _reloadSent = false;
                _wasFiring = false;
                _holdUntil = Time.unscaledTime + SettleSeconds;
                RefreshShots();
                pose = BenchmarkShotPath.Evaluate(_shots, _time);
                if (pose.PlayerCamera && _player != null)
                {
                    // Oyuncu kamerası planları: silah hedef grubuna dönük (arka plan boş arazi değil, atış alanı).
                    BootstrapUtility.Try(() => _player.Teleport(_spawnPoint, _aimYaw), "Vitrin: oyuncuyu yerleştir");
                    if (_camera != null)
                        _camera.fieldOfView = _restoreFov;
                }
                else if (_shooter != null && _anchors.HasShooter)
                {
                    _shooter.Aim();
                }
            }

            if (pose.PlayerCamera)
            {
                RestoreViewmodel();
                DriveWeapon(pose, shotSeconds);
                return;
            }

            HideViewmodel();
            DriveShooter(pose, shotSeconds);
            if (_camera == null)
                return;

            var t = _camera.transform;
            t.position = pose.Position;
            var dir = pose.LookAt - pose.Position;
            if (dir.sqrMagnitude > 1e-4f)
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            _camera.fieldOfView = pose.Fov;
        }

        /// <summary>Vitrin askeri: ateş planında seri ateş, silah yakın planında kısa seri (namlu alevi + mermi izi).</summary>
        private void DriveShooter(BenchmarkPose pose, float shotSeconds)
        {
            if (_shooter == null || !_anchors.HasShooter)
                return;
            var fire = false;
            if (pose.Fire)
                fire = BenchmarkShotPath.BurstOn(shotSeconds);
            else if (pose.Reload)
                fire = shotSeconds > 0.8f && shotSeconds < 1.5f;
            if (pose.Reload && !_reloadSent && shotSeconds > 2.4f)
            {
                _reloadSent = true;
                _shooter.Reload(2.5f);
            }

            if (fire)
                _shooter.Fire();
        }

        private void DriveWeapon(BenchmarkPose pose, float shotSeconds)
        {
            if (_player == null || _player.Weapons == null)
                return;

            var fire = false;
            var reload = false;
            if (pose.Fire)
                fire = BenchmarkShotPath.BurstOn(shotSeconds);
            else if (pose.Reload)
            {
                // Silah yakın planı: kısa seri, sonra şarjör değiştirme.
                fire = shotSeconds > 0.8f && shotSeconds < 1.3f;
                if (!_reloadSent && shotSeconds > 2.4f)
                {
                    reload = true;
                    _reloadSent = true;
                }
            }

            var pressed = fire && !_wasFiring;
            _wasFiring = fire;
            var aim = pose.Reload && shotSeconds < 2.4f;
            var input = new CombatInputState(fire, pressed, reload, aim, false, -1, 0, false, false, false, false, false, false);
            BootstrapUtility.Try(() => _player.Weapons.Tick(input, LookInputState.Zero, 0f), "Vitrin: silah tetik");
        }

        private void ResolveCamera()
        {
            if (_camera != null)
                return;
            if (_player != null && _player.CameraRig != null)
                _camera = _player.CameraRig.WorldCamera;
            if (_camera == null)
                _camera = Camera.main;
        }

        private void HideViewmodel()
        {
            if (_viewmodelHidden || _player == null || _player.CameraRig == null)
                return;
            _player.CameraRig.SetViewmodelVisible(false);
            _viewmodelHidden = true;
        }

        private void RestoreViewmodel()
        {
            if (!_viewmodelHidden || _player == null || _player.CameraRig == null)
                return;
            _player.CameraRig.SetViewmodelVisible(true);
            _viewmodelHidden = false;
        }

        // ------------------------------------------------------------------ Ekran görüntüsü

        public static string ScreenshotDirectory()
        {
            var root = Directory.GetParent(UnityEngine.Application.dataPath);
            var baseDir = root != null ? root.FullName : UnityEngine.Application.persistentDataPath;
            return Path.Combine(baseDir, ScreenshotFolder);
        }

        public void TakeScreenshot()
        {
            try
            {
                var dir = ScreenshotDirectory();
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, BenchmarkShotPath.ScreenshotName(DateTime.UtcNow, ++_shotCounter));
                ScreenCapture.CaptureScreenshot(path);
                Toast("Ekran görüntüsü: " + path);
                Debug.Log("[AAA Benchmark] Ekran görüntüsü → " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAA Benchmark] Ekran görüntüsü alınamadı: " + e.Message);
                Toast("Ekran görüntüsü alınamadı");
            }
        }

        private void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 4f;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _toastUntil && _mode == BenchmarkMode.Oyna)
                return;

            var text = Time.unscaledTime <= _toastUntil ? _toast
                : "VİTRİN — " + _shotName;
            if (_mode == BenchmarkMode.Vitrin && Time.unscaledTime > _toastUntil)
                text = string.Empty;
            if (string.IsNullOrEmpty(text))
                return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(0f, 10f, Screen.width, 30f), text, style);
        }

        private void OnDisable()
        {
            RestoreViewmodel();
        }
    }
}
