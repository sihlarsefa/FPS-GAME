using System.Collections.Generic;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Drone;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>İHA keşfiyle işaretlenen düşmanların dünya üzerindeki kırmızı baklava işaretleri (havuzlu).</summary>
    [DisallowMultipleComponent]
    public sealed class ReconMarkersView : MonoBehaviour
    {
        private const float HeadOffset = 2.3f;

        private HudContext _ctx;
        private readonly List<RectTransform> _markers = new List<RectTransform>(8);
        private readonly List<Combatant> _buffer = new List<Combatant>(16);

        public RectTransform Root { get; private set; }

        public static ReconMarkersView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Fill("ReconMarkers", parent);
            var view = root.gameObject.AddComponent<ReconMarkersView>();
            view._ctx = context;
            view.Root = root;
            HudBuild.PassiveGroup(root);
            HudBuild.NestedCanvas(root);
            return view;
        }

        public void HideAll()
        {
            for (var i = 0; i < _markers.Count; i++)
                HudBuild.SetActive(_markers[i], false);
        }

        private RectTransform Get(int index)
        {
            while (_markers.Count <= index)
            {
                var rect = HudBuild.Rect("Recon" + _markers.Count, Root, HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(18f, 18f));
                var img = HudBuild.Image("Diamond", rect, UiSprites.Diamond, UiTheme.EnemyRed, Vector2.zero, new Vector2(16f, 16f));
                UiFactory.AddOutline(img, UiTheme.WithAlpha(Color.black, 0.6f), 1f);
                HudBuild.SetActive(rect, false);
                _markers.Add(rect);
            }

            return _markers[index];
        }

        public void Tick(float deltaTime)
        {
            var cam = _ctx != null ? _ctx.Camera : null;
            var local = _ctx != null ? _ctx.Local : null;
            if (cam == null || local == null)
            {
                HideAll();
                return;
            }

            ReconDroneSystem.GetMarked(local.Team, _buffer);
            var used = 0;
            for (var i = 0; i < _buffer.Count; i++)
            {
                var screen = cam.WorldToScreenPoint(_buffer[i].transform.position + Vector3.up * HeadOffset);
                if (screen.z <= 0.1f)
                    continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, new Vector2(screen.x, screen.y), null, out var lp))
                    continue;
                var rect = Get(used++);
                HudBuild.SetActive(rect, true);
                HudBuild.SetPosition(rect, lp);
                HudBuild.SetScale(rect, Mathf.Lerp(1.2f, 0.7f, Mathf.Clamp01(screen.z / 300f)));
            }

            for (var i = used; i < _markers.Count; i++)
                HudBuild.SetActive(_markers[i], false);
        }
    }
}
