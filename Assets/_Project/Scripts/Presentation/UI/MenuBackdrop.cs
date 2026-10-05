using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Ana menü 3B dekoru (geçici iskelet).</summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public static MenuBackdrop Create(Transform parent) => new GameObject("MenuBackdrop").AddComponent<MenuBackdrop>();
    }
}
