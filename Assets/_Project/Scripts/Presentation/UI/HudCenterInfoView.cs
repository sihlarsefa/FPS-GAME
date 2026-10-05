using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Transport;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ekran ortasındaki bağlamsal bilgiler: etkileşim istemi ("[F] MPT-76 al"), tedavi/takviye ilerleme halkası
    /// (eşya adı + kalan süre), şarjör değiştirme halkası ve pusulanın altında intikal bilgisi
    /// ("İntikal: T-70 — [F] İn"). Metinler yalnızca değişince atanır; süreler önbellekli metinlerle yazılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudCenterInfoView : MonoBehaviour
    {
        private const float RingSize = 92f;
        private const float InsertionTop = CompassView.TopMargin + CompassView.StripHeight + 46f;

        private HudContext _ctx;

        private RectTransform _promptRoot;
        private Image _promptBackdrop;
        private Text _promptText;
        private string _shownPrompt;

        private RectTransform _ringRoot;
        private CanvasGroup _ringGroup;
        private Image _ringFill;
        private Image _ringTrack;
        private Text _ringTitle;
        private Text _ringTime;
        private float _ringAlpha;
        private string _shownRingTitle;
        private string _shownRingTime;
        private int _shownRingMode = -1;

        private RectTransform _insertionRoot;
        private CanvasGroup _insertionGroup;
        private Text _insertionTitle;
        private Text _insertionStatus;
        private Text _insertionDetail;
        private float _insertionAlpha;
        private string _shownInsertionTitle;
        private string _shownInsertionStatus;
        private string _shownInsertionDetail;

        public RectTransform Root { get; private set; }

        public static HudCenterInfoView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("CenterInfo", parent);
            var view = root.gameObject.AddComponent<HudCenterInfoView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            // Etkileşim istemi.
            _promptRoot = HudBuild.Rect("Prompt", Root, HudBuild.Center, HudBuild.Center, new Vector2(0f, -132f), new Vector2(600f, 36f));
            _promptBackdrop = HudBuild.Image("Backdrop", _promptRoot, UiSprites.ChamferRect, UiTheme.WithAlpha(UiTheme.PanelDark, 0.78f),
                Vector2.zero, new Vector2(300f, 36f));
            HudBuild.Image("Accent", _promptBackdrop.transform, UiSprites.White, UiTheme.Amber, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(UiTheme.AccentStripWidth, 28f));
            _promptText = HudBuild.Text("Text", _promptRoot, string.Empty, UiTheme.FontNormal, TextAnchor.MiddleCenter, UiTheme.Text,
                FontStyle.Bold, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(600f, 36f));
            HudBuild.SetActive(_promptRoot, false);

            // İlerleme halkası (nişangâh çevresinde).
            _ringRoot = HudBuild.Rect("Progress", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(RingSize, RingSize));
            _ringGroup = HudBuild.PassiveGroup(_ringRoot, 0f);
            _ringTrack = HudBuild.Image("Track", _ringRoot, UiSprites.Ring, UiTheme.WithAlpha(Color.black, 0.45f), Vector2.zero, new Vector2(RingSize, RingSize));
            _ringFill = HudBuild.RadialImage("Fill", _ringRoot, UiSprites.Ring, UiTheme.Success, Vector2.zero, new Vector2(RingSize, RingSize));
            _ringTitle = HudBuild.Text("Title", _ringRoot, string.Empty, UiTheme.FontSmall, TextAnchor.UpperCenter, UiTheme.Text,
                FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(420f, 22f));
            _ringTime = HudBuild.Text("Time", _ringRoot, string.Empty, UiTheme.FontTiny, TextAnchor.UpperCenter, UiTheme.TextDim,
                FontStyle.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(240f, 20f));
            HudBuild.SetActive(_ringRoot, false);

            // İntikal bilgisi (pusulanın altında).
            _insertionRoot = HudBuild.Rect("Insertion", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -InsertionTop),
                new Vector2(620f, 74f));
            _insertionGroup = HudBuild.PassiveGroup(_insertionRoot, 0f);
            HudBuild.FillImage("Backdrop", _insertionRoot, HudBuild.Banner, UiTheme.WithAlpha(Color.black, 0.55f));
            _insertionTitle = HudBuild.Text("Title", _insertionRoot, string.Empty, UiTheme.FontMedium, TextAnchor.MiddleCenter,
                UiTheme.TextHeader, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(620f, 30f));
            _insertionStatus = HudBuild.Text("Status", _insertionRoot, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleCenter,
                UiTheme.Text, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(620f, 22f));
            _insertionDetail = HudBuild.Text("Detail", _insertionRoot, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleCenter,
                UiTheme.TextDim, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(620f, 18f));
            HudBuild.SetActive(_insertionRoot, false);
        }

        /// <summary>HUD denetleyicisi her karede çağırır. <paramref name="alive"/>: oyuncu hayatta ve HUD açık.</summary>
        public void Tick(float deltaTime, bool alive)
        {
            UpdatePrompt(alive);
            UpdateRing(deltaTime, alive);
            UpdateInsertion(deltaTime, alive);
        }

        // ------------------------------------------------------------------ istem

        private void UpdatePrompt(bool alive)
        {
            string prompt = null;
            if (alive && _ctx.PlayerValid)
            {
                try
                {
                    prompt = _ctx.Player.InteractionPrompt;
                }
                catch (Exception)
                {
                    prompt = null;
                }
            }

            if (string.IsNullOrEmpty(prompt))
            {
                if (_shownPrompt != null)
                {
                    _shownPrompt = null;
                    HudBuild.SetActive(_promptRoot, false);
                }

                return;
            }

            if (ReferenceEquals(prompt, _shownPrompt) || HudFormat.Same(prompt, _shownPrompt))
                return;

            _shownPrompt = prompt;
            _promptText.text = prompt;
            var width = Mathf.Clamp(_promptText.preferredWidth + 44f, 140f, 900f);
            _promptBackdrop.rectTransform.sizeDelta = new Vector2(width, 36f);
            HudBuild.SetActive(_promptRoot, true);
        }

        // ------------------------------------------------------------------ halka

        private void UpdateRing(float deltaTime, bool alive)
        {
            var mode = 0;
            var progress = 0f;
            string title = null;
            string time = null;

            if (alive && _ctx.PlayerValid)
            {
                try
                {
                    var itemUse = _ctx.Player.ItemUse;
                    if (itemUse != null && itemUse.IsUsing)
                    {
                        mode = 1;
                        progress = itemUse.Progress;
                        title = itemUse.CurrentItemName;
                        time = HudFormat.SecondsLeft(itemUse.RemainingSeconds);
                    }
                    else
                    {
                        var weapon = _ctx.Player.ActiveWeapon;
                        if (weapon != null && weapon.IsReloading)
                        {
                            mode = 2;
                            progress = weapon.ReloadProgress;
                            title = "ŞARJÖR DEĞİŞTİRİLİYOR";
                            time = HudFormat.SecondsLeft(Mathf.Max(0f, weapon.ReloadDuration * (1f - progress)));
                        }
                    }
                }
                catch (Exception)
                {
                    mode = 0;
                }
            }

            var target = mode != 0 ? 1f : 0f;
            _ringAlpha = Mathf.MoveTowards(_ringAlpha, target, deltaTime * (mode != 0 ? 10f : 6f));
            var visible = _ringAlpha > 0.001f;
            HudBuild.SetActive(_ringRoot, visible);
            if (!visible)
            {
                _shownRingMode = -1;
                return;
            }

            HudBuild.SetAlpha(_ringGroup, _ringAlpha);
            if (mode == 0)
                return;

            if (mode != _shownRingMode)
            {
                _shownRingMode = mode;
                var color = mode == 1 ? UiTheme.Success : UiTheme.Amber;
                UiFactory.SetColor(_ringFill, color);
                var size = mode == 1 ? RingSize : RingSize * 0.62f;
                _ringFill.rectTransform.sizeDelta = new Vector2(size, size);
                _ringTrack.rectTransform.sizeDelta = new Vector2(size, size);
                _ringTitle.rectTransform.anchoredPosition = new Vector2(0f, mode == 1 ? -10f : -10f - (RingSize - size) * 0.5f);
                _ringTime.rectTransform.anchoredPosition = new Vector2(0f, mode == 1 ? -32f : -32f - (RingSize - size) * 0.5f);
                _ringTitle.fontSize = mode == 1 ? UiTheme.FontSmall : UiTheme.FontTiny;
            }

            HudBuild.SetFill(_ringFill, Mathf.Clamp01(progress));

            if (!ReferenceEquals(title, _shownRingTitle))
            {
                _shownRingTitle = title;
                UiFactory.SetText(_ringTitle, title ?? string.Empty);
            }

            if (!ReferenceEquals(time, _shownRingTime))
            {
                _shownRingTime = time;
                UiFactory.SetText(_ringTime, time ?? string.Empty);
            }
        }

        // ------------------------------------------------------------------ intikal

        private void UpdateInsertion(float deltaTime, bool alive)
        {
            string title = null;
            string status = null;
            string detail = null;

            if (alive)
                ResolveInsertion(out title, out status, out detail);

            var show = !string.IsNullOrEmpty(title);
            _insertionAlpha = Mathf.MoveTowards(_insertionAlpha, show ? 1f : 0f, deltaTime * (show ? 6f : 3f));
            var visible = _insertionAlpha > 0.001f;
            HudBuild.SetActive(_insertionRoot, visible);
            if (!visible)
                return;

            HudBuild.SetAlpha(_insertionGroup, _insertionAlpha);
            if (!show)
                return;

            if (!ReferenceEquals(title, _shownInsertionTitle))
            {
                _shownInsertionTitle = title;
                UiFactory.SetText(_insertionTitle, title);
            }

            if (!ReferenceEquals(status, _shownInsertionStatus))
            {
                _shownInsertionStatus = status;
                UiFactory.SetText(_insertionStatus, status ?? string.Empty);
            }

            if (!ReferenceEquals(detail, _shownInsertionDetail))
            {
                _shownInsertionDetail = detail;
                UiFactory.SetText(_insertionDetail, detail ?? string.Empty);
            }
        }

        private static readonly string[] TitleCache = new string[2];
        private static string _drivingTitle;
        private int _detailMeters = int.MinValue;
        private int _detailEta = int.MinValue;
        private string _enRouteDetail;
        private int _autoSeconds = int.MinValue;
        private string _autoDetail;

        private void ResolveInsertion(out string title, out string status, out string detail)
        {
            title = null;
            status = null;
            detail = null;

            var pc = _ctx.PlayerController;
            if (pc != null)
            {
                try
                {
                    if (pc.IsInTransport)
                    {
                        var transport = pc.Transport;
                        var method = transport != null ? transport.Method : InsertionMethod.Helicopter;
                        title = InsertionTitle(method);

                        if (pc.CanDisembark)
                        {
                            status = "[F] İn — araçtan in";
                            var auto = pc.AutoDisembarkRemaining;
                            if (auto >= 0f)
                            {
                                var seconds = Mathf.CeilToInt(auto);
                                if (seconds != _autoSeconds)
                                {
                                    _autoSeconds = seconds;
                                    _autoDetail = "Otomatik iniş: " + UiWidgets.Number(seconds) + " sn";
                                }

                                detail = _autoDetail;
                            }
                            else
                            {
                                detail = "İniş bölgesine varıldı";
                            }
                        }
                        else if (transport != null)
                        {
                            status = transport.StatusText;
                            var meters = Mathf.RoundToInt(transport.DistanceToLandingZone / 10f) * 10;
                            var eta = Mathf.CeilToInt(transport.EstimatedSecondsToArrival);
                            if (meters != _detailMeters || eta != _detailEta)
                            {
                                _detailMeters = meters;
                                _detailEta = eta;
                                _enRouteDetail = "İniş bölgesine " + HudFormat.Meters(meters) + "  ·  tahmini varış " + UiWidgets.Clock(eta);
                            }

                            detail = _enRouteDetail;
                        }
                        else
                        {
                            status = "İniş bölgesine intikal ediliyor";
                        }

                        return;
                    }

                    if (pc.IsDriving)
                    {
                        title = _drivingTitle ??= "ARAÇ: KİRPİ";
                        status = "[F] Araçtan in";
                        detail = "[W/S] Gaz / Fren   [A/D] Direksiyon";
                        return;
                    }
                }
                catch (Exception)
                {
                    title = null;
                    return;
                }
            }

            // Genel kaynak: yalnızca intikal durumu bilinir.
            try
            {
                if (_ctx.PlayerValid && _ctx.Player.DropState == DropState.InTransport)
                {
                    title = InsertionTitle(InsertionMethod.Helicopter);
                    status = "İniş bölgesine intikal ediliyor";
                }
            }
            catch (Exception)
            {
                title = null;
            }
        }

        private static string InsertionTitle(InsertionMethod method)
        {
            var index = method == InsertionMethod.ArmoredVehicle ? 1 : 0;
            return TitleCache[index] ??= "İNTİKAL: " + (method == InsertionMethod.ArmoredVehicle ? "KİRPİ" : "T-70");
        }
    }
}
