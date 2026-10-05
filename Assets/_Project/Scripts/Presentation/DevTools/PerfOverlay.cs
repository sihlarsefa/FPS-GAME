using System;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Diagnostics;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Presentation.DevTools
{
    /// <summary>
    /// Performans göstergesi — F3 ile aç/kapa.
    /// Kare süresi grafiği, %1 low, GC tahsis, draw call, aktif bot ve mermi sayısı.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        private static PerfOverlay _instance;
        private static int _bootstrapped;

        private PerfSampler _sampler;
        private bool _visible;
        private readonly float[] _drawBuffer = new float[240];
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private Texture2D _pixel;

        public static PerfOverlay Instance => _instance;
        public bool IsVisible => _visible;

        public static PerfOverlay Ensure()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("[PerfOverlay]");
            DontDestroyOnLoad(go);
            return go.AddComponent<PerfOverlay>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped != 0) return;
            _bootstrapped = 1;

            // Development / -dev / -perfrun / her zaman hazır (F3 ile açılır; release'de de zararsız)
            Ensure();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _sampler = new PerfSampler(240);
            _visible = DiagnosticsCommandLine.HasArg("-perfoverlay");
        }

        private void OnDestroy()
        {
            _sampler?.Dispose();
            if (_pixel != null)
                Destroy(_pixel);
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            _sampler.SampleFrame();

            if (WasF3Pressed())
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            EnsureStyles();

            const float panelW = 360f;
            const float panelH = 210f;
            var rect = new Rect(12f, 12f, panelW, panelH);
            GUI.Box(rect, GUIContent.none, _boxStyle);

            var bots = BotController.All != null ? BotController.All.Count : 0;
            var projectiles = BallisticsSystem.Instance != null ? BallisticsSystem.Instance.ActiveProjectiles : 0;
            var avg = _sampler.AverageFrameMs();
            var oneLow = _sampler.OnePercentLowMs();
            var oneLowFps = _sampler.OnePercentLowFps();

            var y = rect.y + 8f;
            GUI.Label(new Rect(rect.x + 10f, y, panelW - 20f, 20f),
                $"PERF  FPS {_sampler.Fps:0.0}  avg {avg:0.0} ms  1%low {oneLowFps:0.0} ({oneLow:0.0} ms)", _labelStyle);
            y += 22f;
            GUI.Label(new Rect(rect.x + 10f, y, panelW - 20f, 18f),
                $"GC.Alloc {_sampler.GcAllocBytesLast} B   Draw {_sampler.DrawCallsLast}   Batches {_sampler.BatchesLast}", _labelStyle);
            y += 18f;
            GUI.Label(new Rect(rect.x + 10f, y, panelW - 20f, 18f),
                $"Bot {bots}   Mermi {projectiles}   F3 kapat", _labelStyle);
            y += 22f;

            var graph = new Rect(rect.x + 10f, y, panelW - 20f, 110f);
            DrawFrameGraph(graph);
        }

        private void DrawFrameGraph(Rect area)
        {
            GUI.Box(area, GUIContent.none);

            _sampler.CopyHistory(_drawBuffer, out var n);
            if (n < 2) return;

            float maxMs = 33.3f;
            for (var i = 0; i < n; i++)
                if (_drawBuffer[i] > maxMs) maxMs = _drawBuffer[i];
            maxMs = Mathf.Max(maxMs, 16.6f);

            // 16.6 ms ve 33.3 ms referans çizgileri
            DrawHLine(area, 16.6f / maxMs, new Color(0.3f, 0.8f, 0.3f, 0.5f));
            DrawHLine(area, 33.3f / maxMs, new Color(0.9f, 0.6f, 0.1f, 0.5f));

            var prev = Vector2.zero;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)(n - 1);
                var x = area.x + t * area.width;
                var h = Mathf.Clamp01(_drawBuffer[i] / maxMs);
                var y = area.yMax - h * area.height;
                var p = new Vector2(x, y);
                if (i > 0)
                    DrawLine(prev, p, UiTheme.Accent);
                prev = p;
            }
        }

        private void DrawHLine(Rect area, float normalizedFromBottom, Color color)
        {
            var y = area.yMax - Mathf.Clamp01(normalizedFromBottom) * area.height;
            DrawLine(new Vector2(area.x, y), new Vector2(area.xMax, y), color);
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            if (_pixel == null) return;
            var old = GUI.color;
            GUI.color = color;
            var delta = b - a;
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var len = delta.magnitude;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y, len, 2f), _pixel);
            GUIUtility.RotateAroundPivot(-angle, a);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0.07f, 0.09f, 0.06f, 0.82f));

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = false,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static bool WasF3Pressed()
        {
            try
            {
                var kb = Keyboard.current;
                if (kb != null && kb.f3Key.wasPressedThisFrame)
                    return true;
            }
            catch
            {
                // Input System yoksa eski API
            }

            return Input.GetKeyDown(KeyCode.F3);
        }

        public void SetVisible(bool visible) => _visible = visible;
        public void Toggle() => _visible = !_visible;
    }
}
