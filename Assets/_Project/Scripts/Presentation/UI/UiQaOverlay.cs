using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// F9: arayüz görsel kalite denetimi katmanı. Güvenli alan (Screen.safeArea) çerçevesini, tüm görünür yazı/görsel
    /// dikdörtgenlerini (HUD kontur), 16 px altı yazıları (kırmızı) ve çakışan yazıları (macenta) çizer.
    /// Ekran görüntüsü tabanlı QA içindir; kapalıyken maliyeti yok (yalnızca tuş okuma).
    /// </summary>
    public sealed class UiQaOverlay : MonoBehaviour
    {
        private const float MinPixelFont = 16f;
        private const float RescanInterval = 0.4f;
        private const int MaxTexts = 600;

        private struct Box { public Rect Rect; public int Kind; } // 0 görsel, 1 yazı, 2 küçük yazı, 3 çakışan

        private static UiQaOverlay _instance;
        private readonly List<Box> _boxes = new List<Box>(512);
        private readonly List<Rect> _textRects = new List<Rect>(256);
        private readonly Vector3[] _corners = new Vector3[4];
        private bool _on;
        private float _nextScan;
        private Texture2D _px;
        private int _small, _overlap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null)
                return;
            var go = new GameObject("UiQaOverlay") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiQaOverlay>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                _on = !_on;
            if (_on && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + RescanInterval;
                Scan();
            }
        }

        /// <summary>İki dikdörtgenin kesişim alanı küçük olandan belirgin (>%25) büyükse çakışma sayılır.</summary>
        public static bool Overlaps(Rect a, Rect b)
        {
            var w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (w <= 0f || h <= 0f)
                return false;
            var smaller = Mathf.Min(a.width * a.height, b.width * b.height);
            return smaller > 0f && w * h > smaller * 0.25f;
        }

        private Rect ToScreen(RectTransform rt, Canvas canvas)
        {
            rt.GetWorldCorners(_corners);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        private void Scan()
        {
            _boxes.Clear();
            _textRects.Clear();
            _small = _overlap = 0;
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled || canvas.renderMode == RenderMode.WorldSpace || !canvas.isRootCanvas)
                    continue;
                foreach (var g in canvas.GetComponentsInChildren<Graphic>(false))
                {
                    if (g == null || !g.isActiveAndEnabled || g.canvasRenderer.cull || g.canvasRenderer.GetInheritedAlpha() < 0.05f)
                        continue;
                    var r = ToScreen(g.rectTransform, canvas);
                    if (r.width < 2f || r.height < 2f)
                        continue;
                    if (g is Text t)
                    {
                        if (string.IsNullOrEmpty(t.text) || _textRects.Count >= MaxTexts)
                            continue;
                        var px = t.fontSize * canvas.scaleFactor;
                        var kind = px < MinPixelFont * Screen.height / 1080f - 0.01f ? 2 : 1;
                        if (kind == 2) _small++;
                        _boxes.Add(new Box { Rect = r, Kind = kind });
                        _textRects.Add(r);
                    }
                    else if (g.raycastTarget || g is Image)
                    {
                        _boxes.Add(new Box { Rect = r, Kind = 0 });
                    }
                }
            }
            for (var i = 0; i < _textRects.Count; i++)
                for (var j = i + 1; j < _textRects.Count; j++)
                    if (Overlaps(_textRects[i], _textRects[j]))
                    {
                        _overlap++;
                        _boxes.Add(new Box { Rect = _textRects[i], Kind = 3 });
                        _boxes.Add(new Box { Rect = _textRects[j], Kind = 3 });
                    }
        }

        private void OnGUI()
        {
            if (!_on)
                return;
            if (_px == null)
            {
                _px = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }
            var old = GUI.color;
            foreach (var b in _boxes)
            {
                GUI.color = b.Kind == 0 ? new Color(0.2f, 1f, 0.4f, 0.35f)
                    : b.Kind == 1 ? new Color(0.2f, 0.9f, 1f, 0.7f)
                    : b.Kind == 2 ? new Color(1f, 0.15f, 0.15f, 0.95f)
                    : new Color(1f, 0.2f, 1f, 0.95f);
                Outline(FlipY(b.Rect), b.Kind >= 2 ? 2f : 1f);
            }
            GUI.color = new Color(1f, 0.85f, 0f, 1f);
            Outline(FlipY(Screen.safeArea), 3f);
            GUI.color = Color.black;
            GUI.Box(new Rect(8, 8, 420, 24), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 10, 416, 22),
                "F9 UI-QA  güvenli alan=sarı  yazı=camgöbeği  <16px=kırmızı(" + _small + ")  çakışma=macenta(" + _overlap + ")");
            GUI.color = old;
        }

        private static Rect FlipY(Rect r) => new Rect(r.x, Screen.height - r.yMax, r.width, r.height);

        private void Outline(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _px);
        }
    }
}
