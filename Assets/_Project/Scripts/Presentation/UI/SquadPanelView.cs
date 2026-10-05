using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sol tim paneli: timin 10 askeri rütbe sırasıyla — görev kısaltması, rütbeli ad, can çubuğu, uzaklık;
    /// komutan yıldızla, yerel oyuncu amber, şehitler soluk ve "ŞEHİT" etiketli. Altta tim emri, komuta ve topçu
    /// durumu. Saniyede 5 kez güncellenir; satırlar havuzludur, metinler yalnızca değişince yazılır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadPanelView : MonoBehaviour
    {
        public const float Width = 330f;
        private const float HeaderHeight = 28f;
        private const float RowHeight = 30f;
        private const float FooterHeight = 52f;
        private const float UpdateInterval = 0.2f;

        private sealed class Row
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Highlight;
            public Text Role;
            public Image Star;
            public Text Name;
            public Text Distance;
            public Text Status;
            public Image HealthTrack;
            public Image HealthFill;
            public Combatant Combatant;
            public string ShownName;
            public int ShownHealth = int.MinValue;
            public int ShownDistance = int.MinValue;
            public int ShownState = -1;
            public bool ShownCommander;
        }

        private HudContext _ctx;
        private RectTransform _rowsRoot;
        private Text _header;
        private Text _headerCount;
        private Text _orderText;
        private Text _supportText;
        private readonly List<Row> _rows = new List<Row>(10);
        private int _shownSquadVersion = -1;
        private int _shownAlive = -1;
        private int _shownTotal = -1;
        private int _shownTeam = int.MinValue;
        private float _nextUpdate;
        private string _shownOrder;
        private int _shownArtillery = int.MinValue;
        private PlayerId _shownCommander = new PlayerId(int.MinValue);

        public RectTransform Root { get; private set; }

        public static SquadPanelView Create(RectTransform parent, HudContext context)
        {
            var root = HudBuild.Rect("SquadPanel", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -170f),
                new Vector2(Width, HeaderHeight + 10f * RowHeight + FooterHeight));
            var view = root.gameObject.AddComponent<SquadPanelView>();
            view._ctx = context;
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);

            var header = HudBuild.Rect("Header", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, HeaderHeight));
            HudBuild.FillImage("Bg", header, UiSprites.White, UiTheme.WithAlpha(UiTheme.PanelDark, 0.75f));
            HudBuild.Image("Accent", header, UiSprites.White, UiTheme.Accent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(UiTheme.AccentStripWidth, HeaderHeight));
            _header = HudBuild.Text("Title", header, "TİM", UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextHeader,
                FontStyle.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(Width - 90f, HeaderHeight));
            _headerCount = HudBuild.Text("Count", header, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text,
                FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(80f, HeaderHeight));

            _rowsRoot = HudBuild.Rect("Rows", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -HeaderHeight - 2f),
                new Vector2(Width, 10f * RowHeight));

            var footer = HudBuild.Rect("Footer", Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -HeaderHeight - 10f * RowHeight - 6f),
                new Vector2(Width, FooterHeight));
            HudBuild.FillImage("Bg", footer, UiSprites.White, UiTheme.WithAlpha(UiTheme.PanelDark, 0.55f));
            _orderText = HudBuild.Text("Order", footer, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.Text,
                FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(Width - 20f, 24f));
            _supportText = HudBuild.Text("Support", footer, string.Empty, UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.TextDim,
                FontStyle.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -26f), new Vector2(Width - 20f, 24f));
        }

        private Row GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                var i = _rows.Count;
                var row = new Row();
                row.Rect = HudBuild.Rect("Member" + i, _rowsRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * RowHeight),
                    new Vector2(Width, RowHeight - 2f));
                row.Group = HudBuild.PassiveGroup(row.Rect);
                row.Background = HudBuild.FillImage("Bg", row.Rect, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.38f));
                row.Highlight = HudBuild.Image("Local", row.Rect, UiSprites.White, UiTheme.Amber, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(3f, RowHeight - 2f));
                row.Highlight.enabled = false;

                row.Role = HudBuild.Text("Role", row.Rect, string.Empty, 12, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 1f), new Vector2(34f, RowHeight));
                row.Star = HudBuild.Image("Star", row.Rect, UiSprites.Star, UiTheme.Amber, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(42f, 2f), new Vector2(14f, 14f));
                row.Star.enabled = false;
                row.Name = HudBuild.Text("Name", row.Rect, string.Empty, 15, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 3f), new Vector2(190f, RowHeight - 8f));
                row.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
                row.Name.verticalOverflow = VerticalWrapMode.Truncate;

                row.Distance = HudBuild.Text("Distance", row.Rect, string.Empty, 12, TextAnchor.MiddleRight, UiTheme.TextDim, FontStyle.Normal,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 3f), new Vector2(64f, RowHeight - 8f));
                row.Status = HudBuild.Text("Status", row.Rect, "ŞEHİT", 12, TextAnchor.MiddleRight, UiTheme.HealthLow, FontStyle.Bold,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(64f, RowHeight));
                row.Status.enabled = false;

                row.HealthTrack = HudBuild.Image("HealthTrack", row.Rect, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.6f),
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(58f, 3f), new Vector2(Width - 58f - 8f, 4f));
                row.HealthFill = HudBuild.Image("HealthFill", row.HealthTrack.transform, UiSprites.White, UiTheme.HealthHigh,
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                UiFactory.Stretch(row.HealthFill);
                row.HealthFill.type = Image.Type.Filled;
                row.HealthFill.fillMethod = Image.FillMethod.Horizontal;
                row.HealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                row.HealthFill.fillAmount = 1f;

                _rows.Add(row);
            }

            return _rows[index];
        }

        /// <summary>HUD denetleyicisi her karede çağırır (iç zamanlayıcıyla seyreltilir).</summary>
        public void Tick(float deltaTime)
        {
            var squadChanged = _shownSquadVersion != _ctx.SquadVersion;
            if (!squadChanged && Time.unscaledTime < _nextUpdate)
                return;

            _nextUpdate = Time.unscaledTime + UpdateInterval;
            _shownSquadVersion = _ctx.SquadVersion;

            var squad = _ctx.Squad;
            var local = _ctx.Local;
            var commander = _ctx.CommanderId;
            var origin = _ctx.Position;
            var alive = 0;

            for (var i = 0; i < squad.Count; i++)
            {
                var member = squad[i];
                var row = GetRow(i);
                HudBuild.SetActive(row.Rect, true);
                var c = member.Combatant;
                if (c == null)
                {
                    HudBuild.SetActive(row.Rect, false);
                    continue;
                }

                if (!ReferenceEquals(row.Combatant, c))
                {
                    row.Combatant = c;
                    row.ShownHealth = int.MinValue;
                    row.ShownDistance = int.MinValue;
                    row.ShownState = -1;
                    UiFactory.SetText(row.Role, HudFormat.RoleShort(c.Role));
                }

                if (!ReferenceEquals(row.ShownName, member.Name))
                {
                    row.ShownName = member.Name;
                    UiFactory.SetText(row.Name, member.Name);
                }

                var isLocal = c == local;
                var isAlive = c.IsAlive;
                if (isAlive)
                    alive++;

                var state = (isAlive ? 1 : 0) | (isLocal ? 2 : 0);
                if (state != row.ShownState)
                {
                    row.ShownState = state;
                    row.Highlight.enabled = isLocal;
                    row.Status.enabled = !isAlive;
                    row.Distance.enabled = isAlive && !isLocal;
                    row.HealthTrack.enabled = isAlive;
                    row.HealthFill.enabled = isAlive;
                    row.Group.alpha = isAlive ? 1f : 0.55f;
                    UiFactory.SetColor(row.Name, !isAlive ? UiTheme.TextMuted : isLocal ? UiTheme.Amber : UiTheme.Text);
                    row.Background.color = isLocal
                        ? UiTheme.WithAlpha(UiTheme.PanelLight, 0.6f)
                        : UiTheme.WithAlpha(Color.black, isAlive ? 0.38f : 0.25f);
                }

                var isCommander = isAlive && commander.IsValid && c.Id == commander;
                if (isCommander != row.ShownCommander)
                {
                    row.ShownCommander = isCommander;
                    row.Star.enabled = isCommander;
                }

                if (!isAlive)
                    continue;

                var hs = c.State;
                var fraction = hs.Max > 0f ? Mathf.Clamp01(hs.Current / hs.Max) : 0f;
                var bucket = Mathf.RoundToInt(fraction * 200f);
                if (bucket != row.ShownHealth)
                {
                    row.ShownHealth = bucket;
                    HudBuild.SetFill(row.HealthFill, fraction);
                    UiFactory.SetColor(row.HealthFill, UiTheme.HealthColor(fraction));
                }

                if (!isLocal)
                {
                    var distance = Mathf.RoundToInt(Vector3.Distance(origin, c.transform.position));
                    if (distance != row.ShownDistance)
                    {
                        row.ShownDistance = distance;
                        UiFactory.SetText(row.Distance, HudFormat.Meters(distance));
                    }
                }
            }

            for (var i = squad.Count; i < _rows.Count; i++)
                HudBuild.SetActive(_rows[i].Rect, false);

            UpdateHeader(alive, squad.Count);
            UpdateFooter(commander);
        }

        private void UpdateHeader(int alive, int total)
        {
            var team = _ctx.LocalTeam;
            if (team != _shownTeam)
            {
                _shownTeam = team;
                var name = _ctx.TeamName(team);
                UiFactory.SetText(_header, string.IsNullOrEmpty(name) ? "TİM" : "TİM — " + name);
            }

            if (alive != _shownAlive || total != _shownTotal)
            {
                _shownAlive = alive;
                _shownTotal = total;
                UiFactory.SetText(_headerCount, total > 0 ? UiWidgets.Number(alive) + "/" + UiWidgets.Number(total) : string.Empty);
                UiFactory.SetColor(_headerCount, total > 0 && alive * 3 <= total ? UiTheme.HealthLow : UiTheme.Text);
            }
        }

        private void UpdateFooter(PlayerId commander)
        {
            // Emir satırı: komutan bilgisi + geçerli emir.
            var order = SquadOrder.Follow;
            var hasOrder = false;
            var pc = _ctx.PlayerController;
            if (_ctx.Orders != null && _ctx.LocalTeam >= 0)
            {
                try
                {
                    hasOrder = _ctx.Orders.TryGetOrder(_ctx.LocalTeam, out order, out _);
                }
                catch (Exception)
                {
                    hasOrder = false;
                }
            }

            if (!hasOrder && _ctx.PlayerValid)
            {
                try
                {
                    order = _ctx.Player.CurrentOrder;
                    hasOrder = true;
                }
                catch (Exception)
                {
                    hasOrder = false;
                }
            }

            var orderText = hasOrder ? HudFormat.OrderShort(order) : HudFormat.Dash;
            if (!ReferenceEquals(orderText, _shownOrder) || commander != _shownCommander)
            {
                _shownOrder = orderText;
                _shownCommander = commander;
                string commandText;
                if (!commander.IsValid)
                    commandText = "Komuta: —";
                else if (_ctx.IsLocal(commander))
                    commandText = "Komuta sizde  [F1-F4]";
                else
                    commandText = "Komutan: " + _ctx.NameOf(commander);

                UiFactory.SetText(_orderText, "EMİR: " + orderText + "   ·   " + commandText);
            }

            // Destek satırı: topçu hazırlığı.
            var canCall = false;
            var cooldown = 0f;
            if (pc != null)
            {
                try
                {
                    canCall = pc.CanCallArtillery;
                    cooldown = pc.ArtilleryCooldown;
                }
                catch (Exception)
                {
                    canCall = false;
                }
            }
            else if (_ctx.Artillery != null && _ctx.LocalTeam >= 0)
            {
                try
                {
                    cooldown = _ctx.Artillery.GetCooldownRemaining(_ctx.LocalTeam);
                    canCall = true;
                }
                catch (Exception)
                {
                    canCall = false;
                }
            }

            var incoming = 0f;
            if (_ctx.Artillery != null && _ctx.LocalTeam >= 0)
            {
                try
                {
                    incoming = _ctx.Artillery.GetSecondsUntilImpact(_ctx.LocalTeam);
                }
                catch (Exception)
                {
                    incoming = 0f;
                }
            }

            int code;
            if (incoming > 0.05f)
                code = 100000 + Mathf.CeilToInt(incoming);
            else if (!canCall)
                code = -1;
            else if (cooldown > 0.05f)
                code = Mathf.CeilToInt(cooldown);
            else
                code = 0;

            if (code == _shownArtillery)
                return;

            _shownArtillery = code;
            if (code >= 100000)
            {
                UiFactory.SetText(_supportText, "TOPÇU: ATEŞ YOLDA — " + UiWidgets.Clock(incoming));
                UiFactory.SetColor(_supportText, UiTheme.Amber);
            }
            else if (code < 0)
            {
                UiFactory.SetText(_supportText, "TOPÇU: Kullanılamıyor");
                UiFactory.SetColor(_supportText, UiTheme.TextMuted);
            }
            else if (code > 0)
            {
                UiFactory.SetText(_supportText, "TOPÇU: " + UiWidgets.Clock(cooldown) + " sonra hazır");
                UiFactory.SetColor(_supportText, UiTheme.TextDim);
            }
            else
            {
                UiFactory.SetText(_supportText, "TOPÇU: HAZIR  [V]");
                UiFactory.SetColor(_supportText, UiTheme.Success);
            }
        }
    }
}
