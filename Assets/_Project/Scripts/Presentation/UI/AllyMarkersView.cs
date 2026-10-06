using System.Collections.Generic;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tim arkadaşlarının başı üzerinde mavi baklava işaretleri, rütbeli ad ve uzaklık (300 m içinde). Komutanın
    /// işareti amber çerçeveli. Kameranın arkasındakiler gizlenir; uzaklıkla küçülür ve solar.
    /// Dünya → ekran dönüşümü her karede, havuzlu işaretlerle (iç içe tuval; tahsis yok).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyMarkersView : MonoBehaviour
    {
        public const float MaxDistance = 300f;
        private const float HeadOffset = 2.15f;
        private const float NameDistance = MaxDistance;

        private sealed class Marker
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Diamond;
            public Image Frame;
            public Text Name;
            public Text Distance;
            public Combatant Combatant;
            public string ShownName;
            public int ShownDistance = int.MinValue;
            public bool ShownCommander;
        }

        private HudContext _ctx;
        private readonly List<Marker> _markers = new List<Marker>(12);

        public RectTransform Root { get; private set; }

        public static AllyMarkersView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("AllyMarkers", parent);
            var view = root.gameObject.AddComponent<AllyMarkersView>();
            view._ctx = context;
            view.Root = root;
            HudBuild.PassiveGroup(root);
            HudBuild.NestedCanvas(root);
            return view;
        }

        private Marker GetMarker(int index)
        {
            while (_markers.Count <= index)
            {
                var marker = new Marker();
                marker.Rect = HudBuild.Rect("Ally" + _markers.Count, Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(16f, 16f));
                marker.Group = HudBuild.PassiveGroup(marker.Rect);
                marker.Frame = HudBuild.Image("Frame", marker.Rect, UiSprites.Diamond, UiTheme.Amber, Vector2.zero, new Vector2(20f, 20f));
                marker.Frame.enabled = false;
                marker.Diamond = HudBuild.Image("Diamond", marker.Rect, UiSprites.Diamond, UiTheme.AllyBlue, Vector2.zero, new Vector2(14f, 14f));
                UiFactory.AddOutline(marker.Diamond, UiTheme.WithAlpha(Color.black, 0.5f), 1f);
                marker.Name = HudBuild.Text("Name", marker.Rect, string.Empty, 14, TextAnchor.LowerCenter, UiTheme.AllyBlue, FontStyle.Bold,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(260f, 18f));
                marker.Distance = HudBuild.Text("Distance", marker.Rect, string.Empty, 14, TextAnchor.UpperCenter,
                    UiTheme.WithAlpha(UiTheme.Text, 0.85f), FontStyle.Normal, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -1f), new Vector2(120f, 16f));
                HudBuild.SetActive(marker.Rect, false);
                _markers.Add(marker);
            }

            return _markers[index];
        }

        /// <summary>Tüm işaretleri gizler.</summary>
        public void HideAll()
        {
            for (var i = 0; i < _markers.Count; i++)
                HudBuild.SetActive(_markers[i].Rect, false);
        }

        /// <summary>HUD denetleyicisi her karede çağırır.</summary>
        public void Tick(float deltaTime)
        {
            var cam = _ctx.Camera;
            if (cam == null)
            {
                HideAll();
                return;
            }

            var squad = _ctx.Squad;
            var local = _ctx.Local;
            var commander = _ctx.CommanderId;
            var camPos = cam.transform.position;
            var used = 0;

            for (var i = 0; i < squad.Count; i++)
            {
                var member = squad[i];
                var c = member.Combatant;
                if (c == null || c == local || !c.IsAlive)
                    continue;

                var world = c.transform.position + Vector3.up * HeadOffset;
                var distance = Vector3.Distance(camPos, world);
                if (distance > MaxDistance)
                    continue;

                var screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0.1f)
                    continue;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, new Vector2(screen.x, screen.y), null, out var localPoint))
                    continue;

                var marker = GetMarker(used++);
                HudBuild.SetActive(marker.Rect, true);
                HudBuild.SetPosition(marker.Rect, localPoint);

                if (!ReferenceEquals(marker.Combatant, c) || !ReferenceEquals(marker.ShownName, member.Name))
                {
                    marker.Combatant = c;
                    marker.ShownName = member.Name;
                    marker.ShownDistance = int.MinValue;
                    UiFactory.SetText(marker.Name, member.Name);
                }

                var isCommander = commander.IsValid && c.Id == commander;
                if (isCommander != marker.ShownCommander)
                {
                    marker.ShownCommander = isCommander;
                    marker.Frame.enabled = isCommander;
                }

                var meters = Mathf.RoundToInt(distance);
                if (meters != marker.ShownDistance)
                {
                    marker.ShownDistance = meters;
                    UiFactory.SetText(marker.Distance, HudFormat.Meters(meters));
                }

                var near = distance <= NameDistance;
                if (marker.Name.enabled != near)
                    marker.Name.enabled = near;

                var t = Mathf.Clamp01(distance / MaxDistance);
                HudBuild.SetScale(marker.Rect, Mathf.Lerp(1f, 0.7f, t));
                HudBuild.SetAlpha(marker.Group, Mathf.Lerp(1f, 0.5f, t));
            }

            for (var i = used; i < _markers.Count; i++)
                HudBuild.SetActive(_markers[i].Rect, false);
        }
    }
}
