using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Maç durumu paneli (BR tarzı): P tuşu BASILI tutulurken görünür. Üst şerit: hayatta tim / oyuncu, sıralama tahmini;
    /// tim tablosu: rütbeli ad, durum (sağ/yaralı/ölü), leş, hasar; ilk 3 leş lideri; bölge fazı + sonraki daralma sayacı.
    /// Tab zaten envanter, CapsLock tam skor tablosu olduğu için ayrı tuş (P) seçildi; imleç serbestken (envanter, harita,
    /// konsol, duraklatma) açılmaz. Veri 4 Hz tazelenir; kapalıyken iş yapılmaz.
    /// ENTEGRASYON HudController: <c>Make("ScorePanel", () => ScorePanel.Create(Root, _ctx))</c> satırı Build()'e eklenmeli.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScorePanel : MonoBehaviour
    {
        public static readonly Color Red = new Color32(0xD4, 0x3A, 0x2E, 0xFF);

        private const float Refresh = 0.25f;
        private const int MaxRows = 10;
        private const float PanelW = 980f;
        private const float RowH = 34f;

        private sealed class Row
        {
            public GameObject Go;
            public Text Name, Status, Kills, Damage;
        }

        private HudContext _ctx;
        private RectTransform _root;
        private GameObject _panel;
        private Text _teamsAlive, _playersAlive, _placement, _zone, _leaders;
        private readonly Row[] _rows = new Row[MaxRows];
        private readonly List<int> _aliveTeams = new List<int>(24);
        private readonly List<KeyValuePair<int, string>> _top = new List<KeyValuePair<int, string>>(8);
        private float _next;

        public bool IsOpen { get; private set; }

        public static ScorePanel Create(RectTransform parent, HudContext ctx)
        {
            var root = HudBuild.Fill("ScorePanel", parent);
            var view = root.gameObject.AddComponent<ScorePanel>();
            view._ctx = ctx;
            view._root = root;
            view.Build();
            view._panel.SetActive(false);
            return view;
        }

        /// <summary>P basılı mı ve oyun girdisi açık mı (imleç kilitli)?</summary>
        internal static bool WantsOpen(bool pHeld, bool cursorLocked, bool localPresent) => pHeld && cursorLocked && localPresent;

        private void Build()
        {
            var c = new Vector2(0.5f, 0.5f);
            var panel = HudBuild.Rect("Panel", _root, c, c, Vector2.zero, new Vector2(PanelW, 560f));
            _panel = panel.gameObject;
            HudBuild.FillImage("Bg", panel, UiSprites.White, UiTheme.WithAlpha(new Color(0.05f, 0.05f, 0.06f), 0.9f));
            HudBuild.Image("TopLine", panel, UiSprites.White, Red, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PanelW, 4f));

            var top = new Vector2(0f, 1f);
            _teamsAlive = HudBuild.Text("Teams", panel, "", 26, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold, top, top, new Vector2(24f, -16f), new Vector2(300f, 36f));
            _playersAlive = HudBuild.Text("Players", panel, "", 26, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold, top, top, new Vector2(340f, -16f), new Vector2(300f, 36f));
            _placement = HudBuild.Text("Place", panel, "", 26, TextAnchor.MiddleRight, Red, FontStyle.Bold, top, top, new Vector2(PanelW - 344f, -16f), new Vector2(320f, 36f));
            _zone = HudBuild.Text("Zone", panel, "", 18, TextAnchor.MiddleLeft, UiTheme.Amber, FontStyle.Bold, top, top, new Vector2(24f, -54f), new Vector2(PanelW - 48f, 26f));
            HudBuild.Image("Div", panel, UiSprites.White, UiTheme.WithAlpha(Red, 0.7f), top, top, new Vector2(24f, -84f), new Vector2(PanelW - 48f, 2f));

            HudBuild.Text("HName", panel, "TİMİM", 16, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold, top, top, new Vector2(24f, -92f), new Vector2(400f, 22f));
            HudBuild.Text("HStat", panel, "DURUM", 16, TextAnchor.MiddleLeft, UiTheme.TextMuted, FontStyle.Bold, top, top, new Vector2(460f, -92f), new Vector2(160f, 22f));
            HudBuild.Text("HKill", panel, "LEŞ", 16, TextAnchor.MiddleRight, UiTheme.TextMuted, FontStyle.Bold, top, top, new Vector2(640f, -92f), new Vector2(100f, 22f));
            HudBuild.Text("HDmg", panel, "HASAR", 16, TextAnchor.MiddleRight, UiTheme.TextMuted, FontStyle.Bold, top, top, new Vector2(760f, -92f), new Vector2(150f, 22f));

            for (var i = 0; i < MaxRows; i++)
            {
                var y = -118f - i * RowH;
                var r = new Row();
                var rect = HudBuild.Rect("Row" + i, panel, top, top, new Vector2(0f, y), new Vector2(PanelW, RowH));
                r.Go = rect.gameObject;
                if (i % 2 == 0)
                    HudBuild.FillImage("Stripe", rect, UiSprites.White, new Color(1f, 1f, 1f, 0.04f));
                r.Name = HudBuild.Text("N", rect, "", 18, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Normal, top, top, new Vector2(24f, 0f), new Vector2(420f, RowH));
                r.Status = HudBuild.Text("S", rect, "", 18, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold, top, top, new Vector2(460f, 0f), new Vector2(160f, RowH));
                r.Kills = HudBuild.Text("K", rect, "", 18, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold, top, top, new Vector2(640f, 0f), new Vector2(100f, RowH));
                r.Damage = HudBuild.Text("D", rect, "", 18, TextAnchor.MiddleRight, UiTheme.TextDim, FontStyle.Normal, top, top, new Vector2(760f, 0f), new Vector2(150f, RowH));
                _rows[i] = r;
            }

            var bottom = new Vector2(0f, 0f);
            HudBuild.Image("Div2", panel, UiSprites.White, UiTheme.WithAlpha(Red, 0.7f), bottom, bottom, new Vector2(24f, 78f), new Vector2(PanelW - 48f, 2f));
            _leaders = HudBuild.Text("Leaders", panel, "", 18, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold, bottom, bottom, new Vector2(24f, 10f), new Vector2(PanelW - 48f, 62f));
        }

        private void Update()
        {
            if (_ctx == null || _panel == null)
                return;
            var kb = Keyboard.current;
            var want = WantsOpen(kb != null && kb.pKey.isPressed, Cursor.lockState == CursorLockMode.Locked, _ctx.Local != null);
            if (want != IsOpen)
            {
                IsOpen = want;
                _panel.SetActive(want);
                _next = 0f;
            }

            if (!IsOpen || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + Refresh;
            try { Fill(); }
            catch (Exception e) { Debug.LogException(e); enabled = false; }
        }

        private void Fill()
        {
            _aliveTeams.Clear();
            var players = 0;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsAlive)
                    continue;
                players++;
                if (!_aliveTeams.Contains(c.Team))
                    _aliveTeams.Add(c.Team);
            }

            UiFactory.SetText(_teamsAlive, "HAYATTA TİM  " + _aliveTeams.Count);
            UiFactory.SetText(_playersAlive, "OYUNCU  " + players);

            var place = _aliveTeams.Count;
            if (_ctx.Stats != null && _ctx.Stats.TryGet(_ctx.LocalId, out var mine) && mine.Placement > 0)
                place = mine.Placement;
            UiFactory.SetText(_placement, "SIRALAMA ~ #" + Mathf.Max(1, place));

            UiFactory.SetText(_zone, ZoneLine());

            var squad = _ctx.Squad;
            for (var i = 0; i < MaxRows; i++)
            {
                var row = _rows[i];
                if (i >= squad.Count)
                {
                    if (row.Go.activeSelf) row.Go.SetActive(false);
                    continue;
                }

                if (!row.Go.activeSelf) row.Go.SetActive(true);
                var m = squad[i];
                var c = m.Combatant;
                var alive = c != null && c.IsAlive;
                var downed = alive && c.IsDowned;
                UiFactory.SetText(row.Name, m.Name ?? string.Empty);
                UiFactory.SetText(row.Status, alive ? (downed ? "YARALI" : "SAĞ") : "ÖLÜ");
                row.Status.color = !alive ? UiTheme.EnemyRed : downed ? UiTheme.Amber : UiTheme.Success;
                row.Name.color = alive ? UiTheme.Text : UiTheme.TextMuted;
                var kills = 0;
                var dmg = 0f;
                if (c != null && _ctx.Stats != null && _ctx.Stats.TryGet(c.Id, out var s))
                {
                    kills = s.Kills;
                    dmg = s.DamageDealt;
                }

                UiFactory.SetText(row.Kills, kills.ToString());
                UiFactory.SetText(row.Damage, Mathf.RoundToInt(dmg).ToString());
            }

            UiFactory.SetText(_leaders, LeadersLine(all));
        }

        private string ZoneLine()
        {
            var z = _ctx.Zone;
            if (z == null || !z.IsActive || z.Stage == ZoneStage.Idle)
                return "BÖLGE  —";
            if (z.Stage == ZoneStage.Finished)
                return "BÖLGE  KAPANDI";
            var phase = "FAZ " + Mathf.Clamp(z.PhaseIndex + 1, 1, Mathf.Max(1, z.PhaseCount)) + "/" + Mathf.Max(1, z.PhaseCount);
            var label = z.Stage == ZoneStage.Shrinking ? "DARALIYOR" : "SONRAKİ DARALMA";
            return "BÖLGE  " + phase + "   " + label + "  " + UiWidgets.Clock(z.StageRemainingSeconds);
        }

        private string LeadersLine(IReadOnlyList<Combatant> all)
        {
            _top.Clear();
            if (_ctx.Stats != null)
            {
                for (var i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || !_ctx.Stats.TryGet(c.Id, out var s) || s.Kills <= 0)
                        continue;
                    var pair = new KeyValuePair<int, string>(s.Kills, c.RankedName);
                    var at = _top.Count;
                    while (at > 0 && _top[at - 1].Key < pair.Key)
                        at--;
                    if (at >= 3)
                        continue;
                    _top.Insert(at, pair);
                    if (_top.Count > 3)
                        _top.RemoveAt(3);
                }
            }

            if (_top.Count == 0)
                return "LEŞ LİDERLERİ  —";
            var text = "LEŞ LİDERLERİ";
            for (var i = 0; i < _top.Count; i++)
                text += "\n" + (i + 1) + ". " + _top[i].Value + "  " + _top[i].Key;
            return text;
        }
    }
}
