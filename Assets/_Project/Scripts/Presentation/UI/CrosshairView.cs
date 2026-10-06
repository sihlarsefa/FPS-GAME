using System;
using Project.Application.Services;
using Project.Infrastructure.Audio;
using Project.Presentation.UI.Crosshair;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Nişangâh (dört çizgi + nokta; aralık silahın anlık sekme açısından ekran pikseline çevrilir) ve isabet işareti
    /// (yerel oyuncunun isabetlerinde çapraz "X": beyaz isabet, amber kafadan, kırmızı etkisiz hâle getirme + ses).
    /// Dürbünle bakarken ya da nişan alırken (gez-arpacık/optik) nişangâh gizlenir; isabet işareti her zaman görünür.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrosshairView : MonoBehaviour
    {
        private const float LineLength = 11f;
        private const float LineThickness = 2f; // 2 px çizgi + 1 px gölge (Outline)
        private const float HitArmOffset = 5f;
        private const float HitArmLength = 8f;

        private HudContext _ctx;
        private RectTransform _crosshair;
        private CanvasGroup _crosshairGroup;
        private RectTransform _top;
        private RectTransform _bottom;
        private RectTransform _left;
        private RectTransform _right;
        private Image _dot;
        private RectTransform _hitMarker;
        private CanvasGroup _hitGroup;
        private readonly Image[] _hitLines = new Image[4];

        private float _gap = 12f;
        private float _crosshairAlpha = 1f;
        private float _hitTimer;
        private float _hitDuration;
        private float _hitScale = 1f;
        private float _lastHitSound = -1f;
        private HitKind _hitKind = HitKind.Govde;
        private HitStyle _hitStyle;
        private readonly Outline[] _lineOutlines = new Outline[5];
        private readonly Outline[] _hitOutlines = new Outline[4];
        private CrosshairSettings _settings;

        // ENTEGRASYON: AdvancedDisplay.cs / ayar paneli icinde CrosshairSettings.Apply(...) cagrisi sonrasi CrosshairView.ApplyAppearance() tetiklenmeli.

        public RectTransform Root { get; private set; }

        /// <summary>Nişangâh şu an görünür mü (son karede)?</summary>
        public bool CrosshairVisible => _crosshairAlpha > 0.01f;

        public static CrosshairView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Rect("Crosshair", parent, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(320f, 320f));
            var view = root.gameObject.AddComponent<CrosshairView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            view.ApplyAppearance();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            HudBuild.NestedCanvas(Root);

            _crosshair = HudBuild.Rect("Lines", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(4f, 4f));
            _crosshairGroup = HudBuild.PassiveGroup(_crosshair);

            _top = CreateLine("Top", new Vector2(LineThickness, LineLength), new Vector2(0.5f, 0f));
            _bottom = CreateLine("Bottom", new Vector2(LineThickness, LineLength), new Vector2(0.5f, 1f));
            _left = CreateLine("Left", new Vector2(LineLength, LineThickness), new Vector2(1f, 0.5f));
            _right = CreateLine("Right", new Vector2(LineLength, LineThickness), new Vector2(0f, 0.5f));

            _dot = HudBuild.Image("Dot", _crosshair, UiSprites.Circle, Color.white, Vector2.zero, new Vector2(2f, 2f));
            _lineOutlines[4] = UiFactory.AddOutline(_dot, UiTheme.WithAlpha(Color.black, 0.6f), 1f);

            _hitMarker = HudBuild.Rect("HitMarker", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(HudVisualRules.MaxHitMarkerPx, HudVisualRules.MaxHitMarkerPx));
            _hitGroup = HudBuild.PassiveGroup(_hitMarker, 0f);
            for (var i = 0; i < 4; i++)
            {
                var holder = HudBuild.Rect("Arm" + i, _hitMarker, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(2f, 2f));
                holder.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                var line = HudBuild.Image("Line", holder, UiSprites.White, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                    new Vector2(0f, HitArmOffset), new Vector2(2.5f, HitArmLength));
                _hitOutlines[i] = UiFactory.AddOutline(line, UiTheme.WithAlpha(Color.black, 0.55f), 1f);
                _hitLines[i] = line;
            }
        }

        /// <summary>Ayarlardaki nişangâh görünümünü (renk, uzunluk, kalınlık, nokta, kontur, opaklık) uygular.</summary>
        public void ApplyAppearance()
        {
            if (_crosshair == null)
                return;
            _settings = CrosshairSettings.Current;
            var st = _settings;
            var color = st.UseCustomColor ? st.CustomColor : AdvancedDisplay.CrosshairTint;
            color.a = 1f;
            _crosshair.localScale = Vector3.one * HudVisualRules.ClampCrosshairScale(AdvancedDisplay.CrosshairSize);

            var lines = new[] { _top, _bottom, _left, _right };
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line == null)
                    continue;
                var vertical = i < 2;
                line.sizeDelta = vertical ? new Vector2(st.LineThickness, st.LineLength) : new Vector2(st.LineLength, st.LineThickness);
                if (line.TryGetComponent<Image>(out var img))
                    img.color = color;
            }

            if (_dot != null)
            {
                _dot.color = color;
                _dot.rectTransform.sizeDelta = new Vector2(st.DotSize, st.DotSize);
                _dot.gameObject.SetActive(st.CenterDot);
            }

            var outlineAlpha = CrosshairMath.EffectiveOutlineOpacity(st);
            var outlineColor = CrosshairMath.OutlineColorFor(color, outlineAlpha);
            for (var i = 0; i < _lineOutlines.Length; i++)
            {
                var o = _lineOutlines[i];
                if (o == null)
                    continue;
                o.enabled = outlineAlpha > 0.001f && st.OutlineThickness > 0.01f;
                o.effectColor = outlineColor;
                o.effectDistance = new Vector2(st.OutlineThickness, -st.OutlineThickness);
            }

            var hitOutline = UiTheme.WithAlpha(Color.black, Mathf.Max(0.55f, outlineAlpha));
            foreach (var o in _hitOutlines)
                if (o != null)
                    o.effectColor = hitOutline;
        }

        private RectTransform CreateLine(string name, Vector2 size, Vector2 pivot)
        {
            var image = HudBuild.Image(name, _crosshair, UiSprites.White, Color.white, HudBuild.Center, pivot, Vector2.zero, size);
            UiFactory.AddOutline(image, UiTheme.WithAlpha(Color.black, 0.6f), 1f);
            return image.rectTransform;
        }

        /// <summary>Yerel oyuncunun isabetini gösterir (HitConfirmedEvent).</summary>
        public void ShowHit(bool headshot, bool kill, bool armorAbsorbed, float damage = 0f)
        {
            var st = _settings ?? CrosshairSettings.Current;
            if (st.HitMarkerEnabled)
            {
                // Saçma/çoklu isabette en önemli tür (öldürme > kafa > zırh > gövde) görünür kalır.
                var incoming = CrosshairMath.ResolveKind(headshot, kill, armorAbsorbed, st);
                _hitKind = CrosshairMath.Merge(_hitKind, _hitTimer > 0f, incoming);
                _hitStyle = CrosshairMath.StyleFor(_hitKind, st.ColorBlind);
                for (var i = 0; i < _hitLines.Length; i++)
                {
                    if (_hitLines[i] == null)
                        continue;
                    _hitLines[i].color = _hitStyle.Color;
                    var rt = _hitLines[i].rectTransform;
                    rt.sizeDelta = new Vector2(2.5f, _hitStyle.ArmLength);
                    rt.anchoredPosition = new Vector2(0f, _hitStyle.ArmOffset);
                }

                _hitDuration = _hitStyle.Duration * st.HitMarkerDuration;
                _hitTimer = _hitDuration;
                _hitScale = _hitStyle.PopScale;
            }

            // Aynı karede gelen saçma (pompalı) isabetlerinde sesi çoğaltma.
            var now = Time.unscaledTime;
            if (HitTones.ShouldPlay(kill, now, _lastHitSound))
            {
                _lastHitSound = now;
                try
                {
                    HitTones.Play(damage, headshot, kill, armorAbsorbed);
                }
                catch (Exception)
                {
                    // ses sistemi yoksa sessiz devam
                }
            }
        }

        /// <summary>HUD denetleyicisi her karede çağırır. <paramref name="hidden"/>: nişangâh tamamen gizlensin.</summary>
        public void Tick(float deltaTime, bool hidden)
        {
            if (hidden)
                _hitTimer = 0f; // ölüm/gizli: takılı kalan isabet X'i kalmasın
            UpdateCrosshair(deltaTime, hidden);
            UpdateHitMarker(deltaTime);
        }

        private void UpdateCrosshair(float deltaTime, bool hidden)
        {
            var show = !hidden;
            WeaponRuntimeService weapon = null;
            var spread = 0f;
            var aiming = false;
            var scoped = false;
            var usingItem = false;

            if (show && _ctx.PlayerValid)
            {
                try
                {
                    var player = _ctx.Player;
                    weapon = player.ActiveWeapon;
                    spread = player.SpreadAngle;
                    aiming = player.IsAiming;
                    scoped = player.IsScoped;
                    var itemUse = player.ItemUse;
                    usingItem = itemUse != null && itemUse.IsUsing;
                    if (player.IsInVehicle)
                        show = false;
                }
                catch (Exception)
                {
                    show = false;
                }
            }
            else
            {
                show = false;
            }

            var st = _settings ?? CrosshairSettings.Current;
            var adsActive = aiming && weapon != null;
            var visibility = CrosshairMath.TargetVisibility(st, !show, adsActive, scoped, usingItem);
            var targetAlpha = visibility * st.Opacity;
            show = visibility > 0f;
            _crosshairAlpha = Mathf.MoveTowards(_crosshairAlpha, targetAlpha, deltaTime * (targetAlpha > _crosshairAlpha ? 6f : 14f));
            HudBuild.SetAlpha(_crosshairGroup, _crosshairAlpha);
            if (_crosshairAlpha <= 0.001f)
                return;

            var armed = weapon != null;
            HudBuild.SetActive(_top, armed);
            HudBuild.SetActive(_bottom, armed);
            HudBuild.SetActive(_left, armed);
            HudBuild.SetActive(_right, armed);
            if (!armed)
                return;

            var targetGap = SpreadToPixels(spread, st) * CrosshairMath.AdsGapScale(st, adsActive);
            _gap = CrosshairMath.SmoothGap(_gap, targetGap, deltaTime, st);
            var g = Mathf.Round(_gap);
            HudBuild.SetPosition(_top, new Vector2(0f, g));
            HudBuild.SetPosition(_bottom, new Vector2(0f, -g));
            HudBuild.SetPosition(_left, new Vector2(-g, 0f));
            HudBuild.SetPosition(_right, new Vector2(g, 0f));
        }

        /// <summary>Sekme açısını (derece, koni yarı açısı) HUD birimine çevirir (dikey görüş alanına göre).</summary>
        private float SpreadToPixels(float spreadDegrees, CrosshairSettings st)
        {
            var cam = _ctx.Camera;
            var fov = cam != null ? cam.fieldOfView : 70f;
            var halfHeight = 540f;
            var parent = Root.parent as RectTransform;
            if (parent != null && parent.rect.height > 1f)
                halfHeight = parent.rect.height * 0.5f;
            return CrosshairMath.SpreadToPixels(spreadDegrees, fov, halfHeight, st);
        }

        private void UpdateHitMarker(float deltaTime)
        {
            if (_hitTimer <= 0f)
            {
                HudBuild.SetAlpha(_hitGroup, 0f);
                return;
            }

            _hitTimer -= deltaTime;
            var st = _settings ?? CrosshairSettings.Current;
            HudBuild.SetAlpha(_hitGroup, CrosshairMath.HitAlpha(_hitTimer, _hitDuration, st.HitMarkerOpacity));
            var scale = CrosshairMath.HitScale(_hitDuration - _hitTimer, _hitScale, st.HitMarkerSize);
            HudBuild.SetScale(_hitMarker, scale);
        }
    }
}
