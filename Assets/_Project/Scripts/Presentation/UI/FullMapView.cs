using System;
using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Tam ekran harita (M). Uygulama yazılıyor.</summary>
    public sealed class FullMapView : MonoBehaviour
    {
        public static FullMapView Create(Transform canvasRoot, IPlayerHudSource player)
        {
            var go = new GameObject("FullMap", typeof(RectTransform));
            if (canvasRoot != null)
                go.transform.SetParent(canvasRoot, false);
            return go.AddComponent<FullMapView>();
        }

        public bool IsOpen { get; private set; }

        public event Action<Vector3> PointMarked;

        public void Toggle()
        {
            IsOpen = !IsOpen;
        }

        private void RaisePointMarked(Vector3 point) => PointMarked?.Invoke(point);
    }
}
