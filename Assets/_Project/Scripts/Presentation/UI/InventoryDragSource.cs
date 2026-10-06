using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Envanter öğesi girdisi: tıklama + sürükleme olayları (kısa dokunuş tıklama, hareket sürükleme sayılır).
    /// Sürükleme sırasında tıklama gönderilmez. İş mantığı <see cref="InventoryView"/> içindedir.
    /// </summary>
    public sealed class InventoryDragSource : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action Clicked;
        public Action DoubleClicked;
        public Func<bool> CanDrag;
        public Action<PointerEventData> DragBegan;
        public Action<PointerEventData> Dragged;
        public Action<PointerEventData> DragEnded;

        private bool _active;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || eventData.button != PointerEventData.InputButton.Left)
                return;

            Clicked?.Invoke();
            if (eventData.clickCount >= 2)
                DoubleClicked?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _active = eventData.button == PointerEventData.InputButton.Left && (CanDrag == null || CanDrag());
            if (_active)
                DragBegan?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_active)
                Dragged?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_active)
                return;
            _active = false;
            DragEnded?.Invoke(eventData);
        }

        private void OnDisable()
        {
            _active = false;
        }
    }
}
