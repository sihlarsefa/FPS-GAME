using System;
using Project.Application.Services;
using Project.Infrastructure.Audio;
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
        private const float LineThickness = 2f;
        private const float MinGap = 5f;
        private const float MaxGap = 140f;
        private const float HitMarkerDuration = 0.28f;
        private const float KillMarkerDuration = 0.55f;

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

            _dot = HudBuild.Image("Dot", _crosshair, UiSprites.Circle, Color.white, Vector2.zero, new Vector2(3.5f, 3.5f));
            UiFactory.AddOutline(_dot, UiTheme.WithAlpha(Color.black, 0.6f), 1f);

            _hitMarker = HudBuild.Rect("HitMarker", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(60f, 60f));
            _hitGroup = HudBuild.PassiveGroup(_hitMarker, 0f);
            for (var i = 0; i < 4; i++)
            {
                var holder = HudBuild.Rect("Arm" + i, _hitMarker, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(2f, 2f));
                holder.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                var line = HudBuild.Image("Line", holder, UiSprites.White, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 7f), new Vector2(2.5f, 10f));
                UiFactory.AddOutline(line, UiTheme.WithAlpha(Color.black, 0.55f), 1f);
                _hitLines[i] = line;
            }
        }

        private RectTransform CreateLine(string name, Vector2 size, Vector2 pivot)
        {
            var image = HudBuild.Image(name, _crosshair, UiSprites.White, Color.white, HudBuild.Center, pivot, Vector2.zero, size);
            UiFactory.AddOutline(image, UiTheme.WithAlpha(Color.black, 0.6f), 1f);
            return image.rectTransform;
        }

        /// <summary>Yerel oyuncunun isabetini gösterir (HitConfirmedEvent).</summary>
        public void ShowHit(bool headshot, bool kill, bool armorAbsorbed)
        {
            var color = kill ? UiTheme.EnemyRed : headshot ? UiTheme.Amber : armorAbsorbed ? UiTheme.Armor : Color.white;
            for (var i = 0; i < _hitLines.Length; i++)
                _hitLines[i].color = color;

            _hitDuration = kill ? KillMarkerDuration : HitMarkerDuration;
            _hitTimer = _hitDuration;
            _hitScale = kill ? 1.45f : headshot ? 1.25f : 1.05f;

            // Aynı karede gelen saçma (pompalı) isabetlerinde sesi çoğaltma.
            var now = Time.unscaledTime;
            if (kill || now - _lastHitSound > 0.045f)
            {
                _lastHitSound = now;
                var id = kill ? SoundId.KillConfirm : headshot ? SoundId.Headshot : SoundId.HitMarker;
                var volume = kill ? 0.8f : headshot ? 0.7f : 0.5f;
                try
                {
                    GameAudio.Play2D(id, volume);
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

            if (scoped || (aiming && weapon != null) || usingItem)
                show = false;

            var targetAlpha = show ? 1f : 0f;
            _crosshairAlpha = Mathf.MoveTowards(_crosshairAlpha, targetAlpha, deltaTime * (show ? 6f : 14f));
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

            var targetGap = SpreadToPixels(spread);
            _gap = Mathf.Lerp(_gap, targetGap, 1f - Mathf.Exp(-deltaTime * 18f));
            var g = Mathf.Round(_gap);
            HudBuild.SetPosition(_top, new Vector2(0f, g));
            HudBuild.SetPosition(_bottom, new Vector2(0f, -g));
            HudBuild.SetPosition(_left, new Vector2(-g, 0f));
            HudBuild.SetPosition(_right, new Vector2(g, 0f));
        }

        /// <summary>Sekme açısını (derece, koni yarı açısı) HUD birimine çevirir (dikey görüş alanına göre).</summary>
        private float SpreadToPixels(float spreadDegrees)
        {
            if (float.IsNaN(spreadDegrees) || spreadDegrees < 0f)
                spreadDegrees = 0f;

            var cam = _ctx.Camera;
            var fov = cam != null ? cam.fieldOfView : 70f;
            fov = Mathf.Clamp(fov, 5f, 150f);

            var halfHeight = 540f;
            var parent = Root.parent as RectTransform;
            if (parent != null && parent.rect.height > 1f)
                halfHeight = parent.rect.height * 0.5f;

            var t = Mathf.Tan(Mathf.Min(spreadDegrees, 60f) * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Clamp(t * halfHeight + 3f, MinGap, MaxGap);
        }

        private void UpdateHitMarker(float deltaTime)
        {
            if (_hitTimer <= 0f)
            {
                HudBuild.SetAlpha(_hitGroup, 0f);
                return;
            }

            _hitTimer -= deltaTime;
            var t = Mathf.Clamp01(_hitTimer / Mathf.Max(0.01f, _hitDuration));
            HudBuild.SetAlpha(_hitGroup, t < 0.5f ? t * 2f : 1f);
            var scale = Mathf.Lerp(1f, _hitScale, Mathf.Sqrt(t));
            HudBuild.SetScale(_hitMarker, scale);
        }
    }
}
