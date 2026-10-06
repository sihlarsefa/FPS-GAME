using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Infrastructure.Localization;
using Project.Infrastructure.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tuş atamaları penceresi: eylem listesi, tıkla -> "tuşa basın" yakalama (Esc iptal), varsayılana dön,
    /// gamepad sağ çubuk hassasiyeti. Değişiklikler anında <see cref="InputBindings"/> ile kaydedilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyBindingsPanel : MonoBehaviour
    {
        private readonly Dictionary<BindAction, Button> _buttons = new();
        private Action _onClose;
        private Text _status;
        private BindAction? _capturing;
        private int _captureStartFrame;

        /// <summary>Tuş yakalama sürerken true (Esc'in menüyü kapatmaması için dış kodlar kontrol edebilir).</summary>
        public static bool IsCapturing { get; private set; }

        public static KeyBindingsPanel Create(Transform parent, Action onClose)
        {
            var root = UiFactory.CreateRect("[TuşAtamaları]", parent);
            root.SetAsLastSibling();
            UiFactory.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = UiTheme.Overlay;
            dim.raycastTarget = true;

            var panel = root.gameObject.AddComponent<KeyBindingsPanel>();
            panel._onClose = onClose;
            panel.Build(root);
            return panel;
        }

        private void Build(RectTransform root)
        {
            var window = UiFactory.Panel(root, UiTheme.Panel, UiSprites.ChamferRect);
            UiFactory.Anchor(window, UiAnchor.Center, Vector2.zero, new Vector2(900f, 860f));
            var border = UiFactory.Image(window, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.PanelBorder);
            UiFactory.Stretch(border);

            var title = UiFactory.Label(window, Loc.Get("bindings.title", "TUŞ ATAMALARI"), UiTheme.FontTitle, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -96f), new Vector2(-40f, -22f));

            var scroll = UiWidgets.ScrollList(window, out var content, 8f, 4);
            UiFactory.SetRect(scroll, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 120f), new Vector2(-28f, -110f));

            UiWidgets.Header(content, Loc.Get("bindings.section.keyboard", "KLAVYE"), UiTheme.FontMedium);
            foreach (var action in InputBindingMap.All)
            {
                var a = action;
                var row = UiFactory.HorizontalList(content, 12f, 0, TextAnchor.MiddleLeft);
                UiFactory.LayoutSize(row, 0f, 52f, 1f);
                var label = UiFactory.Label(row, InputBindingMap.Label(a), UiTheme.FontNormal, TextAnchor.MiddleLeft, UiTheme.Text);
                UiFactory.LayoutSize(label, 380f, 52f);
                UiFactory.FlexibleSpacer(row);
                var btn = UiFactory.Button(row, InputBindings.Map.Display(a), () => BeginCapture(a), UiButtonStyle.Default);
                UiFactory.LayoutSize(btn, 320f, 48f);
                _buttons[a] = btn;
            }

            UiFactory.Spacer(content, 8f);
            UiWidgets.Header(content, Loc.Get("bindings.section.gamepad", "GAMEPAD"), UiTheme.FontMedium);
            UiWidgets.LabeledSlider(content, Loc.Get("bindings.stick_sensitivity", "Sağ çubuk hassasiyeti"), InputBindings.MinStick, InputBindings.MaxStick,
                InputBindings.StickSensitivity, v => InputBindings.StickSensitivity = v, v => v.ToString("0.00"));
            var hint = UiFactory.Label(content,
                Loc.Get("bindings.gamepad_hint", "Sol çubuk: hareket · Sağ çubuk: bakış · RT ateş · LT nişan · A zıpla · B eğil · X şarjör · Y etkileşim · D-pad silah · LB iyileş · RB bomba · Start duraklat · Select harita"),
                UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextMuted);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.LayoutSize(hint, 0f, 90f, 1f);

            _status = UiFactory.Label(window, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.SetRect(_status, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 92f), new Vector2(-40f, 116f));

            var bar = UiFactory.HorizontalList(window, 14f, 0, TextAnchor.MiddleRight);
            UiFactory.SetRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 26f), new Vector2(-40f, 26f + UiTheme.ButtonHeight));
            bar.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var reset = UiFactory.Button(bar, Loc.Get("bindings.btn.defaults", "VARSAYILAN"), () =>
            {
                CancelCapture();
                InputBindings.ResetDefaults();
                RefreshAll();
                Say(Loc.Get("bindings.status.reset", "Varsayılan tuşlar yüklendi."));
            }, UiButtonStyle.Ghost);
            UiFactory.LayoutSize(reset, 230f, UiTheme.ButtonHeight);
            UiFactory.FlexibleSpacer(bar);
            var back = UiFactory.Button(bar, Loc.Get("common.back", "GERİ"), Close, UiButtonStyle.Primary);
            UiFactory.LayoutSize(back, 230f, UiTheme.ButtonHeight);
        }

        private void BeginCapture(BindAction a)
        {
            CancelCapture();
            _capturing = a;
            IsCapturing = true;
            _captureStartFrame = Time.frameCount;
            if (_buttons.TryGetValue(a, out var b)) UiFactory.SetButtonLabel(b, Loc.Get("bindings.press_key", "tuşa basın…"));
            Say(Loc.Format("bindings.status.capture", "{0} için bir tuşa basın (Esc: iptal).", InputBindingMap.Label(a)));
        }

        private void CancelCapture()
        {
            _capturing = null;
            IsCapturing = false;
            RefreshAll();
        }

        private void Update()
        {
            if (_capturing == null || Time.frameCount == _captureStartFrame) return;
            var key = InputBindings.CapturePressedKey();
            if (key == Key.None) return;
            var action = _capturing.Value;
            if (key == Key.Escape)
            {
                CancelCapture();
                Say(Loc.Get("bindings.status.cancelled", "İptal edildi."));
                return;
            }
            if (ControlScheme.IsReserved(key.ToString()))
            {
                // Sabit sistem tuşu (emir/çark/İHA/konsol/skor...): başka eyleme atanamaz; yakalama sürer.
                Say(Loc.Format("bindings.status.reserved", "{0} sabit bir sistem tuşu (bkz. Docs/KONTROLLER.md) — başka bir tuş seçin.", key));
                return;
            }

            var owner = InputBindings.Map.FindOwner(key.ToString());
            InputBindings.Assign(action, key);
            CancelCapture();
            Say(owner.HasValue && owner.Value != action
                ? key + " tuşu \"" + InputBindingMap.Label(owner.Value) + "\" eyleminden alındı."
                : InputBindingMap.Label(action) + " = " + key);
        }

        private void RefreshAll()
        {
            foreach (var kv in _buttons)
                if (kv.Value != null) UiFactory.SetButtonLabel(kv.Value, InputBindings.Map.Display(kv.Key));
        }

        private void Say(string text)
        {
            if (_status != null) _status.text = text;
        }

        public void Close()
        {
            CancelCapture();
            var cb = _onClose;
            _onClose = null;
            Destroy(gameObject);
            cb?.Invoke();
        }


        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged(string _)
        {
            if (_status != null) _status.text = string.Empty;
        }

        private void OnDestroy() { IsCapturing = false; }
    }
}
