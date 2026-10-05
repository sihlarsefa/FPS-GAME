using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tam harita görüntü alanının işaretçi olaylarını (tıklama, sürükleme, tekerlek, üzerine gelme) geri çağrılara
    /// iletir. Sürükleme eşiği aşılırsa bırakma tıklama sayılmaz (EventSystem kuralı). Bir ışın hedefi grafiği
    /// (ör. saydam Image) ile aynı nesnede olmalıdır.
    /// </summary>
    public sealed class MapClickSurface : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Tıklama (sürüklenmeden bırakılan).</summary>
        public Action<PointerEventData> Clicked;

        /// <summary>Sürükleme başladı.</summary>
        public Action<PointerEventData> DragStarted;

        /// <summary>Sürükleme sürüyor (delta: ekran pikseli).</summary>
        public Action<PointerEventData> Dragged;

        /// <summary>Sürükleme bitti.</summary>
        public Action<PointerEventData> DragEnded;

        /// <summary>Fare tekerleği.</summary>
        public Action<PointerEventData> Scrolled;

        /// <summary>İmleç alanın üzerinde mi?</summary>
        public bool IsHovered { get; private set; }

        /// <summary>Şu an sürükleniyor mu?</summary>
        public bool IsDragging { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || IsDragging || eventData.dragging)
                return;
            Clicked?.Invoke(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            IsDragging = true;
            DragStarted?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            IsDragging = false;
            DragEnded?.Invoke(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            Scrolled?.Invoke(eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }

        private void OnDisable()
        {
            IsHovered = false;
            IsDragging = false;
        }
    }
}
