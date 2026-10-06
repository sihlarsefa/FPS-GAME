using Project.Application.Dialogue;
using Project.Application.Services;
using Project.Infrastructure.Audio.Dialogue;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Yaralı (DBNO) ekranı: kenarlarda nabız gibi atan kırmızı vinyet, kanama halkası (süre azaldıkça kızarır ve hızlanır),
    /// iç halkada kaldırma ilerlemesi (kaldıran adı + yüzde), kaldırma kesilirse "KALDIRMA KESİLDİ" uyarısı, sürünme ipucu ve
    /// "Yardım iste" çağrısı (sol tık: tim telsizine "yaralıyım" der, 5 sn bekleme). Kendini kurar; yerel oyuncu yaralı
    /// değilken tuvali gizli tutar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DownedOverlayView : MonoBehaviour
    {
        private const int SortOrder = 48;
        private const string CallKey = "[SOL TIK]";

        private static DownedOverlayView _instance;

        private Combatant _local;
        private Canvas _canvas;
        private Image _vignette;
        private Image _bleedRing;
        private Image _reviveRing;
        private Text _seconds;
        private Text _title;
        private Text _status;
        private Text _hint;
        private Text _call;
        private Text _allies;
        private float _lastCall = -999f;
        private float _prevProgress;
        private float _interruptUntil;
        private static Sprite _ringSprite;
        private static Sprite _vignetteSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[YaralıEkranı]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DownedOverlayView>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void Update()
        {
            if (_local == null || !_local.IsInitialized)
                _local = FindLocal();

            var downed = _local != null && _local.IsDowned;
            if (!downed)
            {
                if (_canvas != null && _canvas.enabled)
                    _canvas.enabled = false;
                _prevProgress = 0f;
                return;
            }

            if (_canvas == null)
                Build();
            if (!_canvas.enabled)
            {
                _canvas.enabled = true;
                _prevProgress = 0f;
            }

            Refresh();
        }

        private static Combatant FindLocal()
        {
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsInitialized && all[i].IsLocalPlayer)
                    return all[i];
            return null;
        }

        private void Build()
        {
            EnsureSprites();
            _canvas = UiFactory.CreateCanvas("[Yaralı UI]", SortOrder);
            DontDestroyOnLoad(_canvas.gameObject);
            var root = (RectTransform)_canvas.transform;

            _vignette = UiFactory.Image(root, _vignetteSprite, new Color(0.75f, 0f, 0f, 0f));
            _vignette.raycastTarget = false;
            UiFactory.Stretch(_vignette);

            _title = UiFactory.Label(root, "YARALISIN", 40, TextAnchor.MiddleCenter, UiTheme.EnemyRed, FontStyle.Bold);
            UiFactory.Anchor(_title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(700f, 54f));
            UiFactory.AddShadow(_title, UiTheme.TextShadow, new Vector2(2f, -2f));

            RingImage(root, new Color(0f, 0f, 0f, 0.5f), 190f, 1f, -190f); // iz
            _bleedRing = RingImage(root, UiTheme.HealthLow, 190f, 1f, -190f);
            _reviveRing = RingImage(root, UiTheme.Success, 150f, 0f, -190f);

            _seconds = UiFactory.Label(root, "45", 44, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Anchor(_seconds, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(140f, 60f));

            _status = UiFactory.Label(root, "", 22, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(_status, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(760f, 34f));
            UiFactory.AddShadow(_status, UiTheme.TextShadow, new Vector2(2f, -2f));

            _call = UiFactory.Label(root, "", 20, TextAnchor.MiddleCenter, UiTheme.Amber, FontStyle.Bold);
            UiFactory.Anchor(_call, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -356f), new Vector2(520f, 30f));

            _hint = UiFactory.Label(root, "W A S D: yavaşça sürün   ·   Müttefik yanına gelip F'ye basılı tutarak seni kaldırır", 18,
                TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.Anchor(_hint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -388f), new Vector2(900f, 24f));

            _allies = UiFactory.Label(root, "", 15, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.Anchor(_allies, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -412f), new Vector2(900f, 24f));
        }

        private static Image RingImage(RectTransform root, Color color, float size, float fill, float yOffset)
        {
            var img = UiFactory.Image(root, _ringSprite, color);
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = false;
            img.fillAmount = fill;
            UiFactory.Anchor(img, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yOffset), new Vector2(size, size));
            return img;
        }

        private void Refresh()
        {
            var svc = ReviveRuntime.Service;
            var frac = DownedHudMath.BleedFraction(_local.BleedRemaining, svc != null ? svc.BleedOutSeconds : ReviveService.DefaultBleedOutSeconds);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * DownedHudMath.PulseHz(frac) * Mathf.PI * 2f);

            _bleedRing.fillAmount = frac;
            UiFactory.SetColor(_bleedRing, Color.Lerp(UiTheme.HealthLow, UiTheme.Amber, frac));
            UiFactory.SetText(_seconds, Mathf.Max(0, Mathf.CeilToInt(_local.BleedRemaining)).ToString());
            UiFactory.SetColor(_seconds, Color.Lerp(Color.Lerp(UiTheme.HealthLow, Color.white, pulse), Color.white, frac));
            UiFactory.SetColor(_vignette, new Color(0.75f, 0f, 0f, (0.18f + (1f - frac) * 0.22f) * (0.55f + 0.45f * pulse)));

            var progress = _local.ReviveProgress;
            _reviveRing.fillAmount = progress;
            if (DownedHudMath.ReviveInterrupted(_prevProgress, progress, true))
                _interruptUntil = Time.unscaledTime + 1.6f;
            _prevProgress = progress;

            if (progress > 0.001f)
            {
                var name = string.Empty;
                if (svc != null && CombatantRegistry.TryGet(svc.CurrentReviver(_local.Id), out var reviver) && reviver != null)
                    name = reviver.RankedName;
                UiFactory.SetText(_status, (string.IsNullOrEmpty(name) ? "Müttefik" : name) + " seni kaldırıyor %" + Mathf.RoundToInt(progress * 100f));
                UiFactory.SetColor(_status, UiTheme.Success);
            }
            else if (Time.unscaledTime < _interruptUntil)
            {
                UiFactory.SetText(_status, "KALDIRMA KESİLDİ");
                UiFactory.SetColor(_status, Color.Lerp(UiTheme.Amber, UiTheme.EnemyRed, pulse));
            }
            else
            {
                UiFactory.SetText(_status, "Kanıyorsun: " + Mathf.Max(0, Mathf.CeilToInt(_local.BleedRemaining)) + " sn");
                UiFactory.SetColor(_status, UiTheme.Text);
            }

            HandleCall();
            UiFactory.SetText(_call, DownedHudMath.CallButtonText(Time.unscaledTime, _lastCall, CallKey));

            var healthy = ReviveRuntime.CountHealthyAllies(_local);
            UiFactory.SetText(_allies, healthy > 0
                ? "Yaralı olmayan müttefik: " + healthy
                : "Yaralı olmayan müttefik kalmadı - tim elenirse ölürsün");
            UiFactory.SetColor(_allies, healthy > 0 ? UiTheme.TextMuted : UiTheme.Warning);
        }

        private void HandleCall()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.timeScale <= 0f)
                return;
            if (!DownedHudMath.CanCall(Time.unscaledTime, _lastCall))
                return;

            _lastCall = Time.unscaledTime;
            try
            {
                DialogueDirector.Say(_local, DialogueCats.ManDown);
                DialogueDirector.Say(_local, DialogueCats.Medic);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void EnsureSprites()
        {
            if (_ringSprite == null)
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[n * n];
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = (x + 0.5f) / n * 2f - 1f;
                    var dy = (y + 0.5f) / n * 2f - 1f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var outer = Mathf.Clamp01((1f - d) * n * 0.5f);
                    var inner = Mathf.Clamp01((d - 0.84f) * n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Min(outer, inner)));
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }

            if (_vignetteSprite == null)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[n * n];
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = (x + 0.5f) / n * 2f - 1f;
                    var dy = (y + 0.5f) / n * 2f - 1f;
                    var d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    var a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.45f) / 0.95f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a));
                }

                tex.SetPixels32(px);
                tex.Apply(false, true);
                _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
