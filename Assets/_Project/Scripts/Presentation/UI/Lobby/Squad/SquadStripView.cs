using System;
using Project.Application.Catalogs;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby.Squad
{
    /// <summary>
    /// Lobinin altındaki 4 slotlu tim şeridi. Dolu slot: rütbe rozeti + ad + hazır şeridi + mikrofon noktası;
    /// boş slot: "+ DAVET ET"; davetli slot: ad + kalan süre. Yerel oyuncunun kartına tıklamak hazır durumunu değiştirir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadStripView : MonoBehaviour
    {
        public const float SlotWidth = 210f;
        public const float SlotHeight = 74f;
        public const float SlotGap = 8f;

        private sealed class Slot
        {
            public Image Back, Accent, MicDot;
            public Text Rank, Name, Status;
            public Button Click;
        }

        private readonly Slot[] _slots = new Slot[SquadRoster.SlotCount];
        private Text _summary;
        private bool _animate = true;
        private bool _dirty = true;

        public SquadRoster Roster { get; private set; }
        /// <summary>Boş slota tıklanınca (slot indeksi); davet arayüzünü çağıran açar.</summary>
        public Action<int> InviteRequested;
        /// <summary>Yerel oyuncu hazır durumunu değiştirince.</summary>
        public Action<bool> LocalReadyChanged;

        public static SquadStripView Create(RectTransform parent, SquadRoster roster)
        {
            var root = UiFactory.CreateRect("SquadStrip", parent);
            var width = SquadRoster.SlotCount * SlotWidth + (SquadRoster.SlotCount - 1) * SlotGap;
            UiFactory.Anchor(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 48f), new Vector2(width, SlotHeight + 26f));
            var view = root.gameObject.AddComponent<SquadStripView>();
            view._animate = QualitySettings.GetQualityLevel() > 1;
            view.Roster = roster ?? new SquadRoster();
            view.Build(root);
            view.Roster.Changed += view.MarkDirty;
            return view;
        }

        private void OnDestroy() { if (Roster != null) Roster.Changed -= MarkDirty; }
        private void MarkDirty() { _dirty = true; }

        private void Build(RectTransform root)
        {
            _summary = UiFactory.Label(root, string.Empty, 14, TextAnchor.MiddleLeft, LobbyTheme.TextDim, FontStyle.Bold);
            _summary.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_summary, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -22f), Vector2.zero);

            for (var i = 0; i < SquadRoster.SlotCount; i++)
            {
                var index = i;
                var rect = UiFactory.CreateRect("Slot_" + i, root);
                UiFactory.SetRect(rect, new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(i * (SlotWidth + SlotGap), 0f), new Vector2(i * (SlotWidth + SlotGap) + SlotWidth, SlotHeight));
                var s = new Slot();
                s.Back = rect.gameObject.AddComponent<Image>();
                s.Back.color = LobbyTheme.Panel;
                s.Click = rect.gameObject.AddComponent<Button>();
                s.Click.targetGraphic = s.Back;
                s.Click.onClick.AddListener(() => OnSlotClicked(index));

                s.Accent = UiFactory.Image(rect, null, LobbyTheme.Border);
                s.Accent.raycastTarget = false;
                UiFactory.SetRect(s.Accent, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f));

                s.Rank = UiFactory.Label(rect, string.Empty, 15, TextAnchor.MiddleCenter, LobbyTheme.Gold, FontStyle.Bold);
                s.Rank.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(s.Rank, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(64f, 0f));

                s.Name = UiFactory.Label(rect, string.Empty, 18, TextAnchor.LowerLeft, LobbyTheme.Text, FontStyle.Bold);
                s.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(s.Name, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(68f, 0f), new Vector2(-26f, -6f));

                s.Status = UiFactory.Label(rect, string.Empty, 13, TextAnchor.UpperLeft, LobbyTheme.TextDim, FontStyle.Bold);
                s.Status.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(s.Status, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(68f, 6f), new Vector2(-8f, 0f));

                s.MicDot = UiFactory.Image(rect, UiSprites.SoftCircle, LobbyTheme.Border);
                s.MicDot.raycastTarget = false;
                UiFactory.SetRect(s.MicDot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(-8f, -8f));
                _slots[i] = s;
            }
        }

        private void OnSlotClicked(int index)
        {
            var m = Roster[index];
            switch (Roster.StateOf(index))
            {
                case SquadSlotState.Empty: InviteRequested?.Invoke(index); break;
                case SquadSlotState.Invited: Roster.CancelInvite(index); break;
                default:
                    if (m != null && m.IsLocal && Roster.ToggleReady(m.Id)) LocalReadyChanged?.Invoke(m.Ready);
                    break;
            }
        }

        private void Update()
        {
            Roster.Tick(Time.unscaledDeltaTime);
            if (_dirty) { _dirty = false; Refresh(); }
            if (!_animate) return;
            for (var i = 0; i < _slots.Length; i++)
            {
                var m = Roster[i];
                if (m == null || !m.Speaking) continue;
                var p = LobbyTheme.PlayPulse(Time.unscaledTime * 2.2f);
                _slots[i].MicDot.color = Color.Lerp(LobbyTheme.Text, LobbyTheme.RedBright, p);
            }
        }

        private void Refresh()
        {
            _summary.text = Roster.SummaryText();
            for (var i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                var m = Roster[i];
                var state = Roster.StateOf(i);
                s.Back.color = state == SquadSlotState.Empty ? new Color(LobbyTheme.Panel.r, LobbyTheme.Panel.g, LobbyTheme.Panel.b, 0.55f) : LobbyTheme.Panel;
                switch (state)
                {
                    case SquadSlotState.Empty:
                        s.Rank.text = "+";
                        s.Rank.color = LobbyTheme.Red;
                        s.Name.text = "DAVET ET";
                        s.Name.color = LobbyTheme.TextDim;
                        s.Status.text = "BOŞ SLOT";
                        s.Status.color = LobbyTheme.TextDim;
                        s.Accent.color = LobbyTheme.Border;
                        s.MicDot.color = Color.clear;
                        break;
                    case SquadSlotState.Invited:
                        s.Rank.text = "…";
                        s.Rank.color = LobbyTheme.Gold;
                        s.Name.text = Roster.InvitedName(i);
                        s.Name.color = LobbyTheme.Text;
                        s.Status.text = "DAVET BEKLENİYOR";
                        s.Status.color = LobbyTheme.Gold;
                        s.Accent.color = LobbyTheme.Gold;
                        s.MicDot.color = Color.clear;
                        break;
                    default:
                        s.Rank.text = RankCatalog.GetShortName(m.Rank);
                        s.Rank.color = LobbyTheme.Gold;
                        s.Name.text = m.IsLeader ? m.Name + "  ★" : m.Name;
                        s.Name.color = LobbyTheme.Text;
                        s.Status.text = m.IsLeader ? "LİDER" : state == SquadSlotState.Ready ? "HAZIR" : "HAZIR DEĞİL";
                        s.Status.color = state == SquadSlotState.Ready || m.IsLeader ? LobbyTheme.Gold : LobbyTheme.TextDim;
                        s.Accent.color = state == SquadSlotState.Ready ? LobbyTheme.RedBright : m.IsLeader ? LobbyTheme.Red : LobbyTheme.Border;
                        s.MicDot.color = m.Muted ? LobbyTheme.RedDeep : m.Speaking ? LobbyTheme.RedBright : LobbyTheme.Border;
                        break;
                }
            }
        }
    }
}
