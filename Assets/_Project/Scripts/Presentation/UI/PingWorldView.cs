using Project.Infrastructure;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Yerel oyuncunun timine ait ping'i dünya-uzayı HUD işareti olarak çizer (baklava + etiket + uzaklık).
    /// Kendi ekran tuvalini kurar; <see cref="EnsureExists"/> ile tembel oluşturulur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PingWorldView : MonoBehaviour
    {
        private static PingWorldView _instance;

        private const float LabelH = 16f;

        private RectTransform _root;
        private RectTransform _marker;
        private RectTransform _beamRect;
        private RectTransform _iconRect;
        private RectTransform _distRect;
        private Image _beam;
        private Image _diamond;
        private Image _triangle;
        private Text _bang;
        private Text _distance;
        private Camera _camera;

        public static PingWorldView EnsureExists()
        {
            if (_instance != null)
                return _instance;

            var canvas = UiFactory.CreateCanvas("PingWorldCanvas", 12);
            var view = canvas.gameObject.AddComponent<PingWorldView>();
            view.Build(canvas);
            _instance = view;
            return view;
        }

        private void Build(Canvas canvas)
        {
            _root = canvas.GetComponent<RectTransform>();
            _marker = HudBuild.Rect("Ping", _root, HudBuild.Center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120f, 10f));
            HudBuild.PassiveGroup(_marker);
            var bottom = new Vector2(0.5f, 0f);
            _beam = HudBuild.Image("Beam", _marker, UiSprites.White, Color.white, bottom, bottom, Vector2.zero,
                new Vector2(PingMarkerMath.BeamWidth, PingMarkerMath.BeamMinPx));
            _beamRect = _beam.rectTransform;
            _distance = HudBuild.Text("Distance", _marker, string.Empty, 13, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold,
                bottom, bottom, Vector2.zero, new Vector2(120f, LabelH));
            _distRect = _distance.rectTransform;
            _diamond = HudBuild.Image("Diamond", _marker, UiSprites.Diamond, Color.white, bottom, bottom, Vector2.zero,
                new Vector2(PingMarkerMath.IconBase, PingMarkerMath.IconBase));
            UiFactory.AddOutline(_diamond, UiTheme.WithAlpha(Color.black, 0.6f), 1f);
            _iconRect = _diamond.rectTransform;
            _triangle = HudBuild.Image("Triangle", _marker, UiSprites.Triangle, Color.white, bottom, bottom, Vector2.zero,
                new Vector2(PingMarkerMath.IconBase, PingMarkerMath.IconBase));
            UiFactory.AddOutline(_triangle, UiTheme.WithAlpha(Color.black, 0.6f), 1f);
            _bang = HudBuild.Text("Bang", _triangle.transform, "!", 18, TextAnchor.LowerCenter, Color.black, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(32f, 24f));
            HudBuild.SetActive(_marker, false);
        }

        private Vector2 ToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var p);
            return p;
        }

        private bool IsOccluded(Vector3 from, Vector3 to, Combatant local)
        {
            var dir = to - from;
            var dist = dir.magnitude;
            if (dist < 2f)
                return false;
            var hits = Physics.RaycastAll(from, dir / dist, dist - 1.5f, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                if (local != null && hits[i].collider.transform.IsChildOf(local.transform))
                    continue;
                return true;
            }

            return false;
        }

        private static Color NormalColor()
        {
            return UiTheme.ColorBlindPalette == 1 || UiTheme.ColorBlindPalette == 2 ? UiTheme.AllyBlue : UiTheme.Amber;
        }

        private void LateUpdate()
        {
            if (_marker == null)
                return;

            var local = CombatantRegistry.LocalPlayer;
            var now = Time.time;
            if (local == null || !PingBoard.TryGet(local.Team, now, out var ping))
            {
                HudBuild.SetActive(_marker, false);
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            if (_camera == null)
            {
                HudBuild.SetActive(_marker, false);
                return;
            }

            var ground = _camera.WorldToScreenPoint(ping.Position);
            var top = _camera.WorldToScreenPoint(ping.Position + Vector3.up * PingMarkerMath.BeamWorldHeight);
            if (ground.z <= 0.1f)
            {
                HudBuild.SetActive(_marker, false);
                return;
            }

            var g = ToCanvas(ground);
            var t = ToCanvas(top);
            var beamH = PingMarkerMath.BeamHeightPx(g.y, t.y);
            var size = PingMarkerMath.IconSize(Mathf.Sin(Time.unscaledTime * 5f));
            var stack = beamH + LabelH + size;
            var anchored = PingMarkerMath.ClampAnchored(g, _root.rect.size, stack, 24f);
            beamH = Mathf.Min(beamH, Mathf.Max(PingMarkerMath.BeamMinPx * 0.5f, stack - LabelH - size));

            var enemy = ping.IsEnemy;
            var color = enemy ? UiTheme.EnemyRed : NormalColor();
            var occluded = IsOccluded(_camera.transform.position, ping.Position + Vector3.up * 1.2f, local);
            var alpha = PingMarkerMath.Alpha(ping.Remaining01(now), occluded);

            HudBuild.SetActive(_marker, true);
            _marker.anchoredPosition = anchored;
            _beamRect.sizeDelta = new Vector2(PingMarkerMath.BeamWidth, beamH);
            _beam.color = UiTheme.WithAlpha(color, alpha * PingMarkerMath.BeamAlpha);
            _distRect.anchoredPosition = new Vector2(0f, beamH);
            var iconY = beamH + LabelH;
            _iconRect.anchoredPosition = new Vector2(0f, iconY);
            _iconRect.sizeDelta = new Vector2(size, size);
            var tri = _triangle.rectTransform;
            tri.anchoredPosition = new Vector2(0f, iconY);
            tri.sizeDelta = new Vector2(size, size);
            _diamond.enabled = !enemy;
            _triangle.enabled = enemy;
            _bang.enabled = enemy;
            _diamond.color = UiTheme.WithAlpha(color, alpha);
            _triangle.color = UiTheme.WithAlpha(color, alpha);
            _bang.color = UiTheme.WithAlpha(Color.black, alpha);
            _distance.color = UiTheme.WithAlpha(UiTheme.Text, alpha);
            _distance.text = PingMarkerMath.FormatDistance(Vector3.Distance(_camera.transform.position, ping.Position));
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
