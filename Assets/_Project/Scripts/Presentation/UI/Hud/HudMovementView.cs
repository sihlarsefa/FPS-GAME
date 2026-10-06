using Project.Core.Domain;
using Project.Infrastructure.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Duruş göstergesi (AYAKTA / ÇÖMEL / YATIK + koşu) ve yalnızca dolu değilken görünen stamina çubuğu.
    /// Mermi sayacının üstünde, ekranın altında ortada; sürekli aynı yerde durur, değişince hafif parlar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudMovementView : MonoBehaviour
    {
        private const float BarWidth = 180f;
        private const float BarHeight = 5f;

        private HudContext _ctx;
        private CanvasGroup _stanceGroup;
        private CanvasGroup _staminaGroup;
        private Text _stance;
        private Image _fill;
        private Image _icon;
        private int _shownStance = -1;
        private bool _shownSprint;
        private bool _shownExhausted;
        private float _flash;
        private float _staminaAlpha;

        public RectTransform Root { get; private set; }

        public static HudMovementView Create(RectTransform parent, HudContext context)
        {
            var y = HudVitalsView.BottomMargin + HudVitalsView.Height + 8f + 76f + 4f;
            var root = HudBuild.Rect("Movement", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y),
                new Vector2(BarWidth + 40f, 40f));
            var view = root.gameObject.AddComponent<HudMovementView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            var stanceRoot = HudBuild.Rect("Stance", Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(BarWidth, 22f));
            _stanceGroup = HudBuild.PassiveGroup(stanceRoot);
            _icon = HudBuild.Image("Icon", stanceRoot, UiSprites.Chevron, UiTheme.TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-46f, 0f), new Vector2(10f, 10f));
            _stance = HudBuild.Text("Label", stanceRoot, "AYAKTA", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextDim, FontStyle.Bold,
                HudBuild.Center, HudBuild.Center, Vector2.zero, new Vector2(BarWidth, 22f));

            var staminaRoot = HudBuild.Rect("Stamina", Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f),
                new Vector2(BarWidth, BarHeight));
            _staminaGroup = HudBuild.PassiveGroup(staminaRoot, 0f);
            HudBuild.FillImage("Track", staminaRoot, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.55f));
            _fill = HudBuild.FillImage("Fill", staminaRoot, UiSprites.White, UiTheme.Text);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        public void Tick(float dt)
        {
            CharacterControllerMotor motor = null;
            var pc = _ctx.PlayerController;
            if (pc != null)
                motor = pc.Motor;

            if (motor == null)
            {
                _stanceGroup.alpha = 0f;
                _staminaGroup.alpha = 0f;
                return;
            }

            var stance = (int)motor.CurrentStance;
            var sprint = motor.IsSprinting;
            if (stance != _shownStance || sprint != _shownSprint)
            {
                if (stance != _shownStance && _shownStance >= 0)
                    _flash = 1f;
                _shownStance = stance;
                _shownSprint = sprint;
                var label = sprint && stance == (int)Stance.Standing ? "KOŞU" : HudRules.StanceLabel((Stance)stance);
                UiFactory.SetText(_stance, label);
                _icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, stance == 0 ? 90f : stance == 1 ? 0f : -90f);
            }

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 1.5f);
            var baseAlpha = stance == (int)Stance.Standing && !sprint ? 0.45f : 0.9f;
            _stanceGroup.alpha = Mathf.Lerp(baseAlpha, 1f, _flash);
            UiFactory.SetColor(_stance, Color.Lerp(UiTheme.TextDim, UiTheme.Amber, _flash));

            var n = Mathf.Clamp01(motor.StaminaNormalized);
            var exhausted = motor.IsExhausted;
            _staminaAlpha = Mathf.MoveTowards(_staminaAlpha, HudRules.StaminaTargetAlpha(n, exhausted), dt * 4f);
            _staminaGroup.alpha = _staminaAlpha;
            if (_staminaAlpha > 0.01f)
            {
                HudBuild.SetFill(_fill, n);
                if (exhausted != _shownExhausted || _staminaAlpha > 0.01f)
                {
                    _shownExhausted = exhausted;
                    _fill.color = exhausted || n < 0.25f ? UiTheme.HealthLow : n < 0.5f ? UiTheme.Amber : UiTheme.Text;
                }
            }
        }
    }
}
