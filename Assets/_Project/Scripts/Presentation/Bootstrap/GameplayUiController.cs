using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Presentation.Player;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Oyun içi arayüz düzeni ve imleç yönetimi (harekât ve poligon ortak): HUD + mini harita, tam harita (M), envanter (Tab),
    /// duraklatma menüsü (Esc) ve maç sonu ekranı. Herhangi bir katman açıkken imleç serbest kalır ve oyuncu girdisi kapanır;
    /// hepsi kapanınca imleç kilitlenir. Esc önce açık haritayı/envanteri kapatır, sonra duraklatma menüsünü açar/kapatır.
    /// Görünümlerden biri oluşturulamazsa diğerleri çalışmaya devam eder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUiController : MonoBehaviour
    {
        private const int OverlayCanvasSortOrder = 30;

        private PlayerController _player;
        private HudController _hud;
        private MinimapView _minimap;
        private FullMapView _map;
        private InventoryView _inventory;
        private PauseMenu _pause;
        private Canvas _overlayCanvas;
        private Action _onMainMenu;
        private bool _endScreenActive;
        private bool _cursorFreeApplied;
        private bool _cursorStateKnown;
        private bool _inputEnabledApplied = true;
        private bool _hasFocus = true;

        public HudController Hud => _hud;
        public MinimapView Minimap => _minimap;
        public FullMapView Map => _map;
        public InventoryView Inventory => _inventory;
        public PauseMenu Pause => _pause;

        /// <summary>Duraklatma, harita, envanter ya da maç sonu ekranı açık mı?</summary>
        public bool AnyOverlayOpen =>
            _endScreenActive || IsOpen(_pause) || (_map != null && _map.IsOpen) || (_inventory != null && _inventory.IsOpen);

        /// <summary>Tam haritada işaretlenen nokta (tim emri/topçu hedefi için ek bilgi).</summary>
        public Vector3? LastMapMarker { get; private set; }

        public event Action<Vector3> MapPointMarked;

        /// <summary>
        /// Arayüzü kurar. player null olabilir (yalnızca duraklatma menüsü kurulur).
        /// </summary>
        public static GameplayUiController Create(Transform parent, PlayerController player, SettingsService settings, Action onMainMenu)
        {
            var go = new GameObject("[Oyun Arayüzü]");
            if (parent != null)
                go.transform.SetParent(parent, false);

            var controller = go.AddComponent<GameplayUiController>();
            controller.Build(player, settings, onMainMenu);
            return controller;
        }

        /// <summary>Maç sonu ekranı açıldı/kapandı: HUD ve diğer katmanlar kapanır, imleç serbest kalır.</summary>
        public void SetEndScreenActive(bool active)
        {
            _endScreenActive = active;
            if (!active)
                return;

            CloseMapAndInventory();
            if (IsOpen(_pause))
                BootstrapUtility.Try(_pause.Close, "PauseMenu.Close");

            SetHudVisible(false);
            ApplyCursorAndInput(true);
        }

        public void SetHudVisible(bool visible)
        {
            if (_hud == null)
                return;

            BootstrapUtility.Try(() => _hud.SetVisible(visible), "HudController.SetVisible");
        }

        public void ShowMessage(string text, float seconds)
        {
            if (_hud == null || string.IsNullOrEmpty(text))
                return;

            BootstrapUtility.Try(() => _hud.ShowCenterMessage(text, seconds), "HudController.ShowCenterMessage");
        }

        /// <summary>Oyuncu ayarları değişince (duraklatma menüsündeki ayarlar) çağrılır.</summary>
        public void ApplySettings(GameSettings settings)
        {
            if (_player == null || settings == null)
                return;

            BootstrapUtility.Try(() => _player.ApplySettings(settings), "PlayerController.ApplySettings");
        }

        /// <summary>Duraklatma menüsünden "Devam Et".</summary>
        public void OnResumeRequested()
        {
            if (IsOpen(_pause))
                BootstrapUtility.Try(_pause.Close, "PauseMenu.Close");

            Time.timeScale = 1f;
            ApplyCursorAndInput(AnyOverlayOpen);
        }

        private void Build(PlayerController player, SettingsService settings, Action onMainMenu)
        {
            _player = player;
            _onMainMenu = onMainMenu;

            BootstrapUtility.Try(() => UiFactory.EnsureEventSystem(), "UiFactory.EnsureEventSystem");

            if (player != null)
            {
                _hud = BootstrapUtility.Try(() => HudController.Create(player), "HudController.Create");
                if (_hud != null && _hud.Root != null)
                    _minimap = BootstrapUtility.Try(() => MinimapView.Create(_hud.Root, player), "MinimapView.Create");

                _overlayCanvas = BootstrapUtility.Try(() => UiFactory.CreateCanvas("[Harita ve Envanter]", OverlayCanvasSortOrder), "UiFactory.CreateCanvas");
                var overlayRoot = _overlayCanvas != null ? _overlayCanvas.transform : (_hud != null ? (Transform)_hud.Root : null);
                if (overlayRoot != null)
                {
                    _map = BootstrapUtility.Try(() => FullMapView.Create(overlayRoot, player), "FullMapView.Create");
                    _inventory = BootstrapUtility.Try(() => InventoryView.Create(overlayRoot, player), "InventoryView.Create");
                }

                if (_map != null)
                    _map.PointMarked += OnMapPointMarked;
            }

            _pause = BootstrapUtility.Try(() => PauseMenu.Create(OnResumeRequested, HandleMainMenu, settings), "PauseMenu.Create");
            ApplyCursorAndInput(false);
        }

        private void Update()
        {
            ReadUiInput(out var pausePressed, out var mapPressed, out var inventoryPressed);

            if (!_endScreenActive)
            {
                if (pausePressed)
                    HandleEscape();
                else if (!IsOpen(_pause))
                {
                    if (mapPressed && _map != null)
                    {
                        if (_inventory != null && _inventory.IsOpen)
                            BootstrapUtility.Try(_inventory.Toggle, "InventoryView.Toggle");
                        BootstrapUtility.Try(_map.Toggle, "FullMapView.Toggle");
                    }
                    else if (inventoryPressed && _inventory != null && !IsPlayerDead())
                    {
                        if (_map != null && _map.IsOpen)
                            BootstrapUtility.Try(_map.Toggle, "FullMapView.Toggle");
                        BootstrapUtility.Try(_inventory.Toggle, "InventoryView.Toggle");
                    }
                }

                // Ölünce envanter kapanır (haritaya bakılabilir).
                if (_inventory != null && _inventory.IsOpen && IsPlayerDead())
                    BootstrapUtility.Try(_inventory.Toggle, "InventoryView.Toggle");
            }

            ApplyCursorAndInput(AnyOverlayOpen);
        }

        private void HandleEscape()
        {
            if (_map != null && _map.IsOpen)
            {
                BootstrapUtility.Try(_map.Toggle, "FullMapView.Toggle");
                return;
            }

            if (_inventory != null && _inventory.IsOpen)
            {
                BootstrapUtility.Try(_inventory.Toggle, "InventoryView.Toggle");
                return;
            }

            if (_pause == null)
                return;

            if (_pause.IsOpen)
                OnResumeRequested();
            else
                BootstrapUtility.Try(_pause.Open, "PauseMenu.Open");
        }

        private void ReadUiInput(out bool pause, out bool map, out bool inventory)
        {
            if (_player != null && _player.Input != null)
            {
                var ui = _player.Input.ReadUi();
                pause = ui.Pause;
                map = ui.ToggleMap;
                inventory = ui.ToggleInventory;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                pause = map = inventory = false;
                return;
            }

            pause = keyboard.escapeKey.wasPressedThisFrame;
            map = keyboard.mKey.wasPressedThisFrame;
            inventory = keyboard.tabKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame;
        }

        private void ApplyCursorAndInput(bool overlayOpen)
        {
            var cursorFree = overlayOpen || !_hasFocus;
            if (!_cursorStateKnown || cursorFree != _cursorFreeApplied || (!cursorFree && Cursor.lockState != CursorLockMode.Locked))
            {
                _cursorStateKnown = true;
                _cursorFreeApplied = cursorFree;
                Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = cursorFree;
            }

            var inputEnabled = !overlayOpen;
            if (_player != null && inputEnabled != _inputEnabledApplied)
            {
                _inputEnabledApplied = inputEnabled;
                _player.InputEnabled = inputEnabled;
            }
        }

        private void CloseMapAndInventory()
        {
            if (_map != null && _map.IsOpen)
                BootstrapUtility.Try(_map.Toggle, "FullMapView.Toggle");

            if (_inventory != null && _inventory.IsOpen)
                BootstrapUtility.Try(_inventory.Toggle, "InventoryView.Toggle");
        }

        private void HandleMainMenu()
        {
            Time.timeScale = 1f;
            if (_onMainMenu != null)
                _onMainMenu();
            else
                GameSession.ReturnToMainMenu();
        }

        private void OnMapPointMarked(Vector3 point)
        {
            LastMapMarker = point;

            // Harita işareti oyuncunun topçu/tim emri hedefi olur.
            if (_player != null)
                BootstrapUtility.Try(() => _player.SetMapMarker(point), "PlayerController.SetMapMarker");

            MapPointMarked?.Invoke(point);
        }

        private bool IsPlayerDead() => _player != null && _player.Combatant != null && _player.Combatant.IsInitialized && !_player.Combatant.IsAlive;

        private static bool IsOpen(PauseMenu pause) => pause != null && pause.IsOpen;

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            _cursorStateKnown = false;
        }

        private void OnDestroy()
        {
            if (_map != null)
                _map.PointMarked -= OnMapPointMarked;

            MapPointMarked = null;
            BootstrapUtility.ReleaseCursor();
        }
    }
}
