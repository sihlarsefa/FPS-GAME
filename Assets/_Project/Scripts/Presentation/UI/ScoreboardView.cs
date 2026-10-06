using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;
using Project.Infrastructure.Localization;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tam skor tablosu: CapsLock (UiInputState.HoldScoreboard) basılı tutulurken görünür. Timler hayatta kalana/derecesine göre
    /// sıralı; her üye için rütbeli ad, rol, öldürme, hasar, durum, ping (yer tutucu). Yerel tim vurgulu.
    /// Veri 4 Hz tazelenir; gizliyken hiçbir iş yapılmaz. Satırlar havuzludur ve ilk açılışta kurulur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreboardView : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private const int ColumnRows = 34;
        private const int Columns = 2;
        private const int MaxRows = ColumnRows * Columns;
        private const float RowHeight = 24f;
        private const float ColumnWidth = 900f;

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Background;
            public Text Name, Role, Kills, Damage, Status, Ping;
        }

        private struct Line
        {
            public bool Header;
            public bool Local;
            public bool Own;
            public string Name;
            public string Role;
            public int Kills;
            public int Damage;
            public string Status;
            public Color StatusColor;
        }

        private static readonly string[] IntCache = new string[1000];

        private readonly Row[] _rows = new Row[MaxRows];
        private readonly List<Line> _lines = new List<Line>(MaxRows + 8);
        private readonly List<ScoreboardTeamKey> _teamKeys = new List<ScoreboardTeamKey>(24);
        private readonly List<ScoreboardMemberKey> _memberKeys = new List<ScoreboardMemberKey>(16);
        private readonly List<PlayerId> _memberIds = new List<PlayerId>(16);

        private RectTransform _root;
        private GameObject _panel;
        private Text _title;
        private bool _built;
        private float _nextRefresh;

        public bool IsOpen { get; private set; }

        /// <summary>Kapalı bırakmak için dışarıdan (duraklatma, harita...) bastırma.</summary>
        public bool Suppressed { get; set; }

        public static ScoreboardView Create(Transform parent)
        {
            var go = new GameObject("[Skor Tablosu]", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ScoreboardView>();
            view._root = (RectTransform)go.transform;
            view._root.anchorMin = Vector2.zero;
            view._root.anchorMax = Vector2.one;
            view._root.offsetMin = Vector2.zero;
            view._root.offsetMax = Vector2.zero;
            return view;
        }

        private void Update()
        {
            var held = !Suppressed && IsHeld();
            if (held != IsOpen)
            {
                if (held)
                    Open();
                else
                    Close();
            }

            if (!IsOpen)
                return;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                Refresh();
            }
        }

        private static bool IsHeld()
        {
            var kb = Keyboard.current;
            return kb != null && kb.capsLockKey.isPressed;
        }

        private void Open()
        {
            if (!_built)
                Build();

            IsOpen = true;
            if (_panel != null)
                _panel.SetActive(true);
            _nextRefresh = 0f;
        }

        private void Close()
        {
            IsOpen = false;
            if (_panel != null)
                _panel.SetActive(false);
        }

        // ------------------------------------------------------------------ kurulum

        private void Build()
        {
            _built = true;
            var panel = UiFactory.Panel(_root, UiTheme.WithAlpha(UiTheme.PanelDark, 0.92f));
            panel.gameObject.name = "Panel";
            var pr = panel;
            pr.anchorMin = new Vector2(0.5f, 0.5f);
            pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(Columns * ColumnWidth + 60f, ColumnRows * RowHeight + 120f);
            pr.anchoredPosition = Vector2.zero;
            var img = panel.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = false;
            _panel = panel.gameObject;

            _title = HudBuild.Text("Baslik", panel, Loc.Get("scoreboard.title", "SKOR TABLOSU"), UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.TextHeader,
                FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1200f, 36f));

            for (var i = 0; i < MaxRows; i++)
            {
                var col = i / ColumnRows;
                var rowInCol = i % ColumnRows;
                var x = 30f + col * ColumnWidth;
                var y = -64f - rowInCol * RowHeight;
                var row = new Row();
                row.Rect = HudBuild.Rect("Satir" + i, panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y),
                    new Vector2(ColumnWidth - 12f, RowHeight - 2f));
                row.Background = HudBuild.FillImage("Zemin", row.Rect, UiSprites.White, Color.clear);
                row.Name = Cell(row.Rect, 6f, 380f, TextAnchor.MiddleLeft);
                row.Role = Cell(row.Rect, 392f, 60f, TextAnchor.MiddleCenter);
                row.Kills = Cell(row.Rect, 456f, 60f, TextAnchor.MiddleRight);
                row.Damage = Cell(row.Rect, 524f, 90f, TextAnchor.MiddleRight);
                row.Status = Cell(row.Rect, 640f, 130f, TextAnchor.MiddleLeft);
                row.Ping = Cell(row.Rect, 790f, 80f, TextAnchor.MiddleRight);
                row.Rect.gameObject.SetActive(false);
                _rows[i] = row;
            }

            _panel.SetActive(false);
        }

        private static Text Cell(RectTransform parent, float x, float width, TextAnchor anchor)
        {
            return HudBuild.Text("H", parent, string.Empty, UiTheme.FontTiny, anchor, UiTheme.Text, FontStyle.Normal,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, RowHeight));
        }

        // ------------------------------------------------------------------ veri

        private void Refresh()
        {
            _lines.Clear();
            Gather();
            Apply();
        }

        private void Gather()
        {
            var services = GameContext.Services;
            if (services == null || !services.TryResolve<MatchService>(out var match) || match == null)
                return;

            services.TryResolve<MatchStatsService>(out var stats);
            var localId = match.LocalPlayerId;
            var localTeam = match.LocalTeam;
            var teamCount = match.TeamCount;

            _teamKeys.Clear();
            for (var t = 0; t < teamCount; t++)
            {
                var members = match.GetTeamMembers(t);
                if (members.Count == 0)
                    continue;

                var kills = 0;
                for (var i = 0; i < members.Count; i++)
                {
                    if (stats != null && stats.TryGet(members[i], out var s))
                        kills += s.Kills;
                }

                _teamKeys.Add(new ScoreboardTeamKey
                {
                    Team = t,
                    Alive = match.GetAliveCountInTeam(t),
                    Placement = match.GetTeamPlacement(t),
                    Kills = kills
                });
            }

            _teamKeys.Sort(ScoreboardRules.TeamOrder);

            for (var k = 0; k < _teamKeys.Count; k++)
            {
                var key = _teamKeys[k];
                var members = match.GetTeamMembers(key.Team);
                var isLocalTeam = key.Team == localTeam;
                var remainingForOthers = MaxRows - _lines.Count - (_teamKeys.Count - k);
                if (_lines.Count >= MaxRows)
                    break;

                var header = new Line
                {
                    Header = true,
                    Local = isLocalTeam,
                    Name = TeamHeaderText(match, key, members.Count),
                    Kills = key.Kills,
                    Status = key.Placement > 0 ? "Elendi" : string.Empty,
                    StatusColor = UiTheme.TextDim
                };
                _lines.Add(header);

                _memberKeys.Clear();
                _memberIds.Clear();
                for (var i = 0; i < members.Count; i++)
                {
                    var id = members[i];
                    _memberIds.Add(id);
                    var kills = 0;
                    var damage = 0f;
                    if (stats != null && stats.TryGet(id, out var s))
                    {
                        kills = s.Kills;
                        damage = s.DamageDealt;
                    }

                    _memberKeys.Add(new ScoreboardMemberKey
                    {
                        Index = i,
                        Status = match.IsAlive(id) ? ScoreboardStatus.Alive : ScoreboardStatus.Dead,
                        Kills = kills,
                        Damage = damage
                    });
                }

                _memberKeys.Sort(ScoreboardRules.MemberOrder);

                var show = _memberKeys.Count;
                if (!isLocalTeam)
                    show = Mathf.Min(show, Mathf.Max(0, remainingForOthers + 1));

                for (var i = 0; i < show && _lines.Count < MaxRows; i++)
                {
                    var mk = _memberKeys[i];
                    var id = _memberIds[mk.Index];
                    var alive = mk.Status != ScoreboardStatus.Dead;
                    var status = ScoreboardRules.StatusText(mk.Status);
                    if (!alive)
                    {
                        var place = match.GetPlacement(id);
                        if (place > 0)
                            status = status + " #" + place;
                    }

                    _lines.Add(new Line
                    {
                        Local = isLocalTeam,
                        Own = id.Equals(localId),
                        Name = MemberName(match, id),
                        Role = ScoreboardRules.RoleCode(match.GetRole(id)),
                        Kills = mk.Kills,
                        Damage = Mathf.RoundToInt(mk.Damage),
                        Status = status,
                        StatusColor = mk.Status == ScoreboardStatus.Alive ? UiTheme.Success
                            : mk.Status == ScoreboardStatus.Downed ? UiTheme.Amber : UiTheme.EnemyRed
                    });
                }
            }
        }

        private static string TeamHeaderText(MatchService match, ScoreboardTeamKey key, int size)
        {
            var name = match.GetTeamName(key.Team);
            var s = name + "  (" + key.Alive + "/" + size + ")";
            return key.Placement > 0 ? s + "  #" + key.Placement : s;
        }

        private static string MemberName(MatchService match, PlayerId id)
        {
            var name = match.GetDisplayName(id);
            if (CombatantRegistry.TryGet(id, out var c) && c != null)
                return HudContext.SafeRankedName(c);
            return string.IsNullOrEmpty(name) ? "?" : name;
        }

        private void Apply()
        {
            var count = Mathf.Min(_lines.Count, MaxRows);
            for (var i = 0; i < MaxRows; i++)
            {
                var row = _rows[i];
                if (i >= count)
                {
                    if (row.Rect.gameObject.activeSelf)
                        row.Rect.gameObject.SetActive(false);
                    continue;
                }

                var line = _lines[i];
                if (!row.Rect.gameObject.activeSelf)
                    row.Rect.gameObject.SetActive(true);

                if (line.Header)
                    row.Background.color = line.Local ? UiTheme.WithAlpha(UiTheme.Amber, 0.28f) : UiTheme.WithAlpha(UiTheme.PanelLight, 0.9f);
                else if (line.Own)
                    row.Background.color = UiTheme.WithAlpha(UiTheme.Amber, 0.16f);
                else if (line.Local)
                    row.Background.color = UiTheme.WithAlpha(UiTheme.Success, 0.10f);
                else
                    row.Background.color = Color.clear;

                var main = line.Header ? UiTheme.TextHeader : UiTheme.Text;
                SetText(row.Name, line.Name, main);
                SetText(row.Role, line.Header ? string.Empty : line.Role, UiTheme.TextDim);
                SetText(row.Kills, Num(line.Kills), main);
                SetText(row.Damage, line.Header ? string.Empty : Num(line.Damage), main);
                SetText(row.Status, line.Status, line.StatusColor);
                SetText(row.Ping, line.Header ? string.Empty : "--", UiTheme.TextDim);
            }
        }

        private static void SetText(Text text, string value, Color color)
        {
            if (!string.Equals(text.text, value))
                text.text = value;
            if (text.color != color)
                text.color = color;
        }

        private static string Num(int value)
        {
            if (value < 0 || value >= IntCache.Length)
                return value.ToString();
            return IntCache[value] ?? (IntCache[value] = value.ToString());
        }
    }
}
