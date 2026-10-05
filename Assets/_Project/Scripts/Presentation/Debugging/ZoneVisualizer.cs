using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using UnityEngine;

namespace Project.Presentation.Debugging
{
    /// <summary>
    /// Editör gizmo'su: mevcut harekât alanını (mavi) ve bir sonraki güvenli bölgeyi (beyaz) Scene görünümünde çizer.
    /// Oyun içi görsel <see cref="World.ZoneWallView"/>'dir; bu bileşen yalnızca hata ayıklama içindir.
    /// </summary>
    public sealed class ZoneVisualizer : MonoBehaviour
    {
        [SerializeField] private float drawHeight = 2f;
        [SerializeField] private int segments = 96;

        private IZoneService _zoneService;

        private void Start()
        {
            GameContext.TryGet(out _zoneService);
        }

        private void OnDrawGizmos()
        {
            if (_zoneService == null && !GameContext.TryGet(out _zoneService))
                return;

            var current = _zoneService.CurrentZone;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            DrawCircle(current, drawHeight);

            var stage = _zoneService.Stage;
            if (stage == ZoneStage.Waiting || stage == ZoneStage.Shrinking)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
                DrawCircle(_zoneService.NextZone, drawHeight + 0.5f);
            }
        }

        private void DrawCircle(ZoneState zone, float y)
        {
            var count = Mathf.Max(12, segments);
            var center = new Vector3(zone.CenterX, y, zone.CenterZ);
            var previous = center + new Vector3(zone.Radius, 0f, 0f);
            for (var i = 1; i <= count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var point = center + new Vector3(Mathf.Cos(angle) * zone.Radius, 0f, Mathf.Sin(angle) * zone.Radius);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
