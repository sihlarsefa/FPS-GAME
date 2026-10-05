using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Modal onay penceresi: ekranı karartır, ortada başlık + ileti + "onayla / vazgeç" düğmeleri gösterir. Arkadaki
    /// tıklamaları engeller. Kapanınca kendini yok eder. Varsayılan seçim "vazgeç" düğmesidir (yanlışlıkla onaylanmasın).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuDialog : MonoBehaviour
    {
        private const float WindowWidth = 680f;
        private const float WindowHeight = 320f;

        private Action _onConfirm;
        private Action _onCancel;
        private Button _cancelButton;
        private bool _closed;
        private CanvasGroup _group;
        private float _fade;

        /// <summary>Pencere hâlâ açık mı?</summary>
        public bool IsOpen => !_closed && this != null;

        /// <summary>
        /// Onay penceresi açar. <paramref name="parent"/> genellikle tuval köküdür (pencere ebeveyni doldurur).
        /// </summary>
        /// <param name="parent">Tuval veya tam ekran kök.</param>
        /// <param name="title">Büyük harf başlık ("ANA MENÜYE DÖN").</param>
        /// <param name="message">Açıklama.</param>
        /// <param name="confirmLabel">Onay düğmesi yazısı.</param>
        /// <param name="onConfirm">Onaylanınca (pencere kapandıktan sonra) çağrılır.</param>
        /// <param name="cancelLabel">Vazgeç düğmesi yazısı.</param>
        /// <param name="onCancel">Vazgeçilince çağrılır (isteğe bağlı).</param>
        /// <param name="dangerous">true ise onay düğmesi koyu kırmızı (tehlikeli eylem) görünür.</param>
        public static MenuDialog Show(Transform parent, string title, string message, string confirmLabel, Action onConfirm,
            string cancelLabel = "VAZGEÇ", Action onCancel = null, bool dangerous = false)
        {
            var root = UiFactory.CreateRect("[Onay Penceresi]", parent);
            root.SetAsLastSibling();

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.Overlay;
            dim.raycastTarget = true;

            var dialog = root.gameObject.AddComponent<MenuDialog>();
            dialog._onConfirm = onConfirm;
            dialog._onCancel = onCancel;
            dialog._group = UiFactory.EnsureCanvasGroup(root);
            dialog._group.alpha = 0f;
            dialog.Build(root, title, message, confirmLabel, cancelLabel, dangerous);
            return dialog;
        }

        /// <summary>Onaylar: pencereyi kapatır ve onay geri çağrısını çalıştırır.</summary>
        public void Confirm()
        {
            if (_closed)
                return;

            var callback = _onConfirm;
            CloseInternal();
            Invoke(callback, "onConfirm");
        }

        /// <summary>Vazgeçer: pencereyi kapatır ve (varsa) vazgeç geri çağrısını çalıştırır.</summary>
        public void Cancel()
        {
            if (_closed)
                return;

            var callback = _onCancel;
            CloseInternal();
            Invoke(callback, "onCancel");
        }

        /// <summary>Geri çağrı çalıştırmadan pencereyi kapatır (üst menü kapanırken).</summary>
        public void Dismiss()
        {
            if (_closed)
                return;
            CloseInternal();
        }

        private void Build(RectTransform root, string title, string message, string confirmLabel, string cancelLabel, bool dangerous)
        {
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            window.gameObject.name = "Window";
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(WindowWidth, WindowHeight));

            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var stripe = UiFactory.Image(window, null, dangerous ? UiTheme.Accent : UiTheme.Amber);
            stripe.gameObject.name = "Stripe";
            UiFactory.SetRect(stripe, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), Vector2.zero);

            var titleText = UiFactory.Label(window, MenuText.ToUpperTr(title), UiTheme.FontLarge, TextAnchor.UpperLeft, UiTheme.TextHeader, FontStyle.Bold);
            UiFactory.SetRect(titleText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -84f), new Vector2(-36f, -28f));

            var body = UiFactory.Label(window, message, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.TextDim);
            UiFactory.SetRect(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(36f, 110f), new Vector2(-36f, -96f));

            var row = UiFactory.HorizontalList(window, 16f, 0, TextAnchor.MiddleRight);
            row.gameObject.name = "Buttons";
            UiFactory.SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(36f, 30f), new Vector2(-36f, 30f + UiTheme.ButtonHeight));
            var group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;

            UiFactory.FlexibleSpacer(row);
            _cancelButton = UiFactory.Button(row, string.IsNullOrEmpty(cancelLabel) ? "VAZGEÇ" : cancelLabel, Cancel, UiButtonStyle.Default);
            UiFactory.LayoutSize(_cancelButton, 240f, UiTheme.ButtonHeight);
            var confirm = UiFactory.Button(row, string.IsNullOrEmpty(confirmLabel) ? "ONAYLA" : confirmLabel, Confirm,
                dangerous ? UiButtonStyle.Danger : UiButtonStyle.Primary);
            UiFactory.LayoutSize(confirm, 260f, UiTheme.ButtonHeight);
        }

        private void Start()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _cancelButton != null)
                eventSystem.SetSelectedGameObject(_cancelButton.gameObject);
        }

        private void Update()
        {
            if (_group == null || _fade >= 1f)
                return;

            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
            _group.alpha = _fade;
        }

        private void CloseInternal()
        {
            _closed = true;
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
                eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);

            if (gameObject != null)
            {
                gameObject.SetActive(false);
                UiFactory.DestroySafe(gameObject);
            }
        }

        private static void Invoke(Action callback, string context)
        {
            if (callback == null)
                return;

            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogError("[MenuDialog] " + context + " başarısız: " + e.Message);
                Debug.LogException(e);
            }
        }
    }
}
