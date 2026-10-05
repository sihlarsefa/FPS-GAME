using System;
using System.Collections.Generic;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.DevTools
{
    /// <summary>
    /// Backquote (<c>`</c>) ile açılan geliştirici konsolu. UGUI + <see cref="UiFactory"/>.
    /// Geçmiş (↑/↓), Tab ile otomatik tamamlama, <c>help</c>.
    /// Yalnızca <see cref="DevConsoleGate.IsAvailable"/> iken oluşturulur.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class DevConsole : MonoBehaviour
    {
        public const int SortOrder = 500;
        private const int MaxHistory = 64;
        private const int VisibleLogLines = 18;

        private static DevConsole _instance;

        private Canvas _canvas;
        private RectTransform _root;
        private CanvasGroup _group;
        private Text _logText;
        private Text _suggestText;
        private Text _overlayText;
        private InputField _input;
        private bool _open;
        private bool _motorWasEnabled = true;
        private readonly List<string> _log = new(128);
        private readonly List<string> _history = new(MaxHistory);
        private int _historyIndex = -1;
        private string _historyDraft = string.Empty;

        public static DevConsole Instance => _instance;

        public bool IsOpen => _open;

        /// <summary>
        /// Kapı açıksa sahnede tek bir konsol örneği oluşturur/döner.
        /// MatchBootstrap (ve benzeri) kancası: <c>DevConsole.Ensure();</c>
        /// </summary>
        public static DevConsole Ensure()
        {
            if (!DevConsoleGate.IsAvailable)
                return null;

            if (_instance != null)
                return _instance;

            var existing = FindAnyObjectByType<DevConsole>();
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }

            var go = new GameObject("[DevConsole]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DevConsole>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            BuildUi();
            SetOpen(false, restoreCursor: false);
            Append("HAREKÂT geliştirici konsolu — help | Tab tamamla | ↑↓ geçmiş");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                DevConsoleCommands.Shutdown();
                _instance = null;
            }
        }

        private void Update()
        {
            if (!DevConsoleGate.IsAvailable)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
            {
                // InputField'a ` yazılmasını engelle: önce kapat/aç.
                if (_open && _input != null && _input.isFocused)
                {
                    var t = _input.text ?? string.Empty;
                    if (t.EndsWith("`", StringComparison.Ordinal) || t.EndsWith("´", StringComparison.Ordinal))
                        _input.text = t.Substring(0, t.Length - 1);
                }

                SetOpen(!_open);
                return;
            }

            DevConsoleCommands.Tick(Time.unscaledDeltaTime);

            var overlay = DevConsoleCommands.OverlayText();
            if (_overlayText != null)
            {
                UiFactory.SetText(_overlayText, overlay);
                _overlayText.enabled = !string.IsNullOrEmpty(overlay);
            }

            if (!_open)
                return;

            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                ApplyAutocomplete();
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                HistoryStep(1);
                return;
            }

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                HistoryStep(-1);
                return;
            }

            RefreshSuggestions();
        }

        public void ClearLog()
        {
            _log.Clear();
            RefreshLog();
        }

        public void Append(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            var parts = line.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                _log.Add(parts[i]);
                while (_log.Count > DevConsoleCommands.MaxLogLines)
                    _log.RemoveAt(0);
            }

            RefreshLog();
        }

        private void BuildUi()
        {
            _canvas = UiFactory.CreateCanvas("[DevConsoleCanvas]", SortOrder);
            _canvas.transform.SetParent(transform, false);

            _overlayText = UiFactory.Label(_canvas.transform, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft,
                UiTheme.Amber, FontStyle.Bold);
            UiFactory.SetRect(_overlayText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -36f),
                new Vector2(-16f, -8f));
            UiFactory.AddShadow(_overlayText, UiTheme.TextShadow, new Vector2(1f, -1f));
            _overlayText.raycastTarget = false;
            _overlayText.enabled = false;

            _root = UiFactory.Panel(_canvas.transform, new Color(0.05f, 0.06f, 0.04f, 0.92f));
            _root.gameObject.name = "ConsoleRoot";
            UiFactory.SetRect(_root, new Vector2(0f, 0.55f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _group = UiFactory.EnsureCanvasGroup(_root);

            var header = UiFactory.Label(_root, "GELİŞTİRİCİ KONSOLU  ·  ` kapat  ·  Tab tamamla", UiTheme.FontTiny,
                TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -28f), new Vector2(-14f, -6f));

            var stripe = UiFactory.Image(_root, null, UiTheme.Accent);
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero);

            _logText = UiFactory.Label(_root, string.Empty, UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.Text);
            UiFactory.SetRect(_logText, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 72f), new Vector2(-14f, -34f));
            _logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logText.verticalOverflow = VerticalWrapMode.Truncate;
            _logText.raycastTarget = false;

            _suggestText = UiFactory.Label(_root, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextDim);
            UiFactory.SetRect(_suggestText, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 40f), new Vector2(-14f, 64f));
            _suggestText.raycastTarget = false;

            var inputBg = UiFactory.Panel(_root, UiTheme.WithAlpha(UiTheme.PanelLight, 0.95f));
            UiFactory.SetRect(inputBg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 38f));

            var prompt = UiFactory.Label(inputBg, ">", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Accent, FontStyle.Bold);
            UiFactory.SetRect(prompt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(28f, 0f));

            var textGo = UiFactory.CreateRect("InputText", inputBg);
            UiFactory.SetRect(textGo, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 2f), new Vector2(-8f, -2f));
            var text = textGo.gameObject.AddComponent<Text>();
            text.font = UiTheme.Font;
            text.fontSize = UiTheme.FontMedium;
            text.color = UiTheme.Text;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            _input = inputBg.gameObject.AddComponent<InputField>();
            _input.textComponent = text;
            _input.lineType = InputField.LineType.SingleLine;
            _input.shouldHideMobileInput = true;
            _input.caretColor = UiTheme.Amber;
            _input.selectionColor = UiTheme.WithAlpha(UiTheme.Accent, 0.35f);
            _input.onSubmit.AddListener(OnSubmit);
        }

        private void OnSubmit(string value)
        {
            if (!_open)
                return;

            var line = (value ?? string.Empty).Trim();
            if (_input != null)
            {
                _input.text = string.Empty;
                _input.ActivateInputField();
                _input.Select();
            }

            _historyIndex = -1;
            _historyDraft = string.Empty;

            if (string.IsNullOrEmpty(line))
                return;

            // Backquote ile kapatma sırasında sızan karakter
            if (line == "`" || line == "´")
                return;

            PushHistory(line);
            Append("> " + line);
            var result = DevConsoleCommands.Execute(line);
            if (!string.IsNullOrEmpty(result))
                Append(result);

            RefreshSuggestions();
        }

        private void SetOpen(bool open, bool restoreCursor = true)
        {
            _open = open;
            if (_group != null)
            {
                _group.alpha = open ? 1f : 0f;
                _group.interactable = open;
                _group.blocksRaycasts = open;
            }

            if (_root != null)
                _root.gameObject.SetActive(open);

            var player = FindAnyObjectByType<Player.PlayerController>();
            if (open)
            {
                if (player != null && player.Motor != null)
                {
                    _motorWasEnabled = player.Motor.ControlEnabled;
                    if (!DevConsoleCommands.NoclipActive)
                        player.Motor.ControlEnabled = false;
                }

                UiFactory.SetCursorFree(true);
                UiFactory.EnsureEventSystem();
                if (_input != null)
                {
                    _input.text = string.Empty;
                    _input.ActivateInputField();
                    _input.Select();
                    EventSystem.current?.SetSelectedGameObject(_input.gameObject);
                }

                RefreshSuggestions();
            }
            else
            {
                if (restoreCursor && player != null && player.Motor != null && !DevConsoleCommands.NoclipActive)
                    player.Motor.ControlEnabled = _motorWasEnabled;

                if (_input != null)
                {
                    _input.DeactivateInputField();
                    UiFactory.ClearSelection();
                }

                // HUD/oyun imleci: yalnızca menü açık değilse kilitle.
                if (restoreCursor && Time.timeScale > 0f)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private void ApplyAutocomplete()
        {
            if (_input == null)
                return;

            var completed = DevConsoleCommands.Complete(_input.text ?? string.Empty);
            _input.text = completed ?? string.Empty;
            _input.caretPosition = _input.text.Length;
            _input.ForceLabelUpdate();
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (_suggestText == null)
                return;

            if (!_open)
            {
                UiFactory.SetText(_suggestText, string.Empty);
                return;
            }

            var list = DevConsoleCommands.Suggest(_input != null ? _input.text : string.Empty);
            if (list == null || list.Count == 0)
            {
                UiFactory.SetText(_suggestText, string.Empty);
                return;
            }

            var clip = new List<string>(8);
            var limit = list.Count > 8 ? 8 : list.Count;
            for (var i = 0; i < limit; i++)
                clip.Add(list[i]);
            var joined = string.Join("   ", clip);
            if (list.Count > 8)
                joined += " …";

            UiFactory.SetText(_suggestText, joined);
        }

        private void RefreshLog()
        {
            if (_logText == null)
                return;

            var start = Mathf.Max(0, _log.Count - VisibleLogLines);
            var sb = new System.Text.StringBuilder(512);
            for (var i = start; i < _log.Count; i++)
            {
                if (i > start)
                    sb.Append('\n');
                sb.Append(_log[i]);
            }

            UiFactory.SetText(_logText, sb.ToString());
        }

        private void PushHistory(string line)
        {
            if (_history.Count > 0 && string.Equals(_history[_history.Count - 1], line, StringComparison.Ordinal))
                return;

            _history.Add(line);
            while (_history.Count > MaxHistory)
                _history.RemoveAt(0);
        }

        private void HistoryStep(int delta)
        {
            if (_input == null || _history.Count == 0)
                return;

            if (_historyIndex < 0)
                _historyDraft = _input.text ?? string.Empty;

            _historyIndex = Mathf.Clamp(_historyIndex + delta, -1, _history.Count - 1);
            if (_historyIndex < 0)
            {
                _input.text = _historyDraft;
            }
            else
            {
                // Geçmiş en yeniden eskiye: index 0 = en eski değil, Count-1-index
                var idx = _history.Count - 1 - _historyIndex;
                if (idx < 0)
                    idx = 0;
                _input.text = _history[idx];
            }

            _input.caretPosition = _input.text.Length;
            _input.ForceLabelUpdate();
            RefreshSuggestions();
        }
    }
}
