using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// El bombası uyarısı: yakındaki (tehlike yarıçapının 2 katı içinde) patlamamış parçalı bombanın yönünü gösteren
    /// kırmızı simge + mesafe; yakınlaştıkça ve fitil kısaldıkça hızlı yanıp söner. Ekran merkezi çevresinde halka üzerinde döner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrenadeWarningView : MonoBehaviour
    {
        private const float Radius = 210f;
        private const float IconSize = 32f;

        private HudContext _ctx;
        private RectTransform _pivot;
        private RectTransform _icon;
        private Image _iconImage;
        private Text _distance;
        private CanvasGroup _group;

        public RectTransform Root { get; private set; }

        public static GrenadeWarningView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("GrenadeWarning", parent);
            var view = root.gameObject.AddComponent<GrenadeWarningView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            _group = HudBuild.PassiveGroup(Root, 0f);
            _pivot = HudBuild.Rect("Pivot", Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(2f, 2f));
            _icon = HudBuild.Rect("Icon", _pivot, HudBuild.Center, HudBuild.Center, new Vector2(0f, Radius), new Vector2(IconSize, IconSize));
            _iconImage = HudBuild.FillImage("Glyph", _icon, UiSprites.Circle, UiTheme.WithAlpha(UiTheme.Danger, 0.9f));
            HudBuild.Text("Mark", _icon, "!", UiTheme.FontMedium, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold,
                HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(IconSize, IconSize));
            _distance = HudBuild.Text("Distance", _icon, string.Empty, 13, TextAnchor.UpperCenter, UiTheme.Text, FontStyle.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(60f, 16f));
        }

        public void Tick(float dt, bool alive)
        {
            if (_group == null)
                return;
            if (!alive || !_ctx.PlayerValid)
            {
                _group.alpha = 0f;
                return;
            }

            var list = ThrowableProjectile.Active;
            ThrowableProjectile best = null;
            var bestScore = 0f;
            var origin = _ctx.Position;
            for (var i = 0; i < list.Count; i++)
            {
                var g = list[i];
                if (g == null || g.Kind != ThrowableKind.Frag)
                    continue;

                var distance = Vector3.Distance(origin, g.Position);
                if (!HudRules.GrenadeIsThreat(distance, g.DangerRadius, g.HasDetonated))
                    continue;

                var urgency = HudRules.GrenadeUrgency(distance, g.DangerRadius, g.FuseRemaining);
                if (urgency > bestScore)
                {
                    bestScore = urgency;
                    best = g;
                }
            }

            if (best == null)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt * 6f);
                return;
            }

            if (!HudVisualRules.IsFinite(best.Position) || !HudVisualRules.IsFinite(origin))
            {
                _group.alpha = 0f;
                return;
            }

            var rel = HudRules.RelativeBearing(origin, _ctx.Yaw, best.Position);
            _pivot.localRotation = Quaternion.Euler(0f, 0f, -rel);
            _icon.localRotation = Quaternion.Euler(0f, 0f, rel);
            var pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(6f, 22f, bestScore));
            _group.alpha = pulse;
            UiFactory.SetText(_distance, HudFormat.Meters(Vector3.Distance(origin, best.Position)));
        }
    }
}
