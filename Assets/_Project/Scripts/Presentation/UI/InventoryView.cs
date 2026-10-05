using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Envanter ekranı (Tab). Uygulama yazılıyor.</summary>
    public sealed class InventoryView : MonoBehaviour
    {
        public static InventoryView Create(Transform canvasRoot, IPlayerHudSource player)
        {
            var go = new GameObject("Inventory", typeof(RectTransform));
            if (canvasRoot != null)
                go.transform.SetParent(canvasRoot, false);
            return go.AddComponent<InventoryView>();
        }

        public bool IsOpen { get; private set; }

        public void Toggle()
        {
            IsOpen = !IsOpen;
        }
    }
}
