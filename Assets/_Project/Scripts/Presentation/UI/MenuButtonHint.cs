using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Menü düğmesi üzerine gelindiğinde ya da klavye/gamepad ile seçildiğinde açıklama metnini bildirir
    /// (ana menüdeki düğme açıklama satırı için).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuButtonHint : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private string _hint;
        private Action<string> _onHint;

        /// <summary>Açıklama metni.</summary>
        public string Hint => _hint;

        /// <summary>Bileşeni ekler ve bağlar.</summary>
        public static MenuButtonHint Attach(Component target, string hint, Action<string> onHint)
        {
            if (target == null)
                return null;

            var component = target.GetComponent<MenuButtonHint>();
            if (component == null)
                component = target.gameObject.AddComponent<MenuButtonHint>();
            component._hint = hint ?? string.Empty;
            component._onHint = onHint;
            return component;
        }

        public void OnPointerEnter(PointerEventData eventData) => Notify();

        public void OnSelect(BaseEventData eventData) => Notify();

        private void Notify()
        {
            if (_onHint == null)
                return;
            try
            {
                _onHint(_hint);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnDestroy()
        {
            _onHint = null;
        }
    }
}
