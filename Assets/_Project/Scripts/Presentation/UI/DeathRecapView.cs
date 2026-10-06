using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Ölüm özeti kartı: öldüren (rütbe + ad), silah, mesafe, öldürenin kalan canı, vuruş bölgeleri ve son saniyelerin
    /// hasar çizelgesi. Verilen kök altında sol-orta konumlu tek bir panel kurar; <see cref="SetVisible"/> ile gizlenir.
    /// </summary>
    public sealed class DeathRecapView
    {
        private const float PanelWidth = 440f;
        private static readonly string[] PartOrder = { "Head", "Torso", "Arm", "Leg" };

        private readonly RectTransform _panel;

        public RectTransform Panel => _panel;

        public DeathRecapView(RectTransform root, DeathRecap recap)
        {
            var panel = UiFactory.Panel(root, UiTheme.PanelDark);
            UiFactory.Anchor(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 20f), new Vector2(PanelWidth, 560f));
            UiFactory.AddOutline(panel.GetComponent<Image>(), UiTheme.PanelBorder, 2f);
            _panel = panel;
            Build(recap);
        }

        public void SetVisible(bool visible) => UiFactory.SetVisible(_panel, visible);

        private Text Put(string text, int size, TextAnchor anchor, Color color, float x, float y, float w, float h, FontStyle style = FontStyle.Normal)
        {
            var l = UiFactory.Label(_panel, text, size, anchor, color, style);
            UiFactory.Anchor(l, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
            return l;
        }

        private RectTransform Box(Color color, float x, float y, float w, float h)
        {
            var p = UiFactory.Panel(_panel, color);
            UiFactory.Anchor(p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(Mathf.Max(0f, w), h));
            return p;
        }

        private void Build(DeathRecap r)
        {
            var y = 14f;
            Put("ÖLÜM ÖZETİ", 20, TextAnchor.MiddleLeft, UiTheme.TextHeader, 20f, y, PanelWidth - 40f, 28f, FontStyle.Bold);
            y += 36f;
            Box(UiTheme.PanelBorder, 20f, y, PanelWidth - 40f, 2f);
            y += 10f;

            if (r.HasKiller && !string.IsNullOrEmpty(r.KillerName))
            {
                Put("ÖLDÜREN", 13, TextAnchor.MiddleLeft, UiTheme.TextMuted, 20f, y, 200f, 18f);
                y += 18f;
                Put(r.KillerName, 24, TextAnchor.MiddleLeft, UiTheme.EnemyRed, 20f, y, PanelWidth - 40f, 32f, FontStyle.Bold);
                y += 36f;
                Put(r.WeaponName + "   ·   " + DeathRecapText.Distance(r.Distance) + (r.FinalHeadshot ? "   ·   KAFA" : string.Empty),
                    17, TextAnchor.MiddleLeft, UiTheme.Text, 20f, y, PanelWidth - 40f, 24f);
                y += 30f;

                var frac = r.KillerAlive ? r.KillerHealthFraction : 0f;
                Box(UiTheme.Track, 20f, y, PanelWidth - 40f, 14f);
                Box(frac > 0.5f ? UiTheme.HealthHigh : (frac > 0.25f ? UiTheme.HealthMid : UiTheme.HealthLow), 20f, y, (PanelWidth - 40f) * frac, 14f);
                y += 16f;
                Put(r.KillerAlive ? "Öldürenin kalan canı: " + DeathRecapText.KillerHealth(r.KillerHealth, r.KillerMaxHealth) : "Öldüren de düştü",
                    13, TextAnchor.MiddleLeft, UiTheme.TextDim, 20f, y, PanelWidth - 40f, 18f);
                y += 28f;
            }
            else
            {
                Put(r.WeaponName, 20, TextAnchor.MiddleLeft, UiTheme.Text, 20f, y, PanelWidth - 40f, 28f, FontStyle.Bold);
                y += 34f;
                Put("Çevresel hasar", 14, TextAnchor.MiddleLeft, UiTheme.TextDim, 20f, y, PanelWidth - 40f, 20f);
                y += 30f;
            }

            Put("VURUŞ BÖLGELERİ", 13, TextAnchor.MiddleLeft, UiTheme.TextMuted, 20f, y, 220f, 18f);
            Put(DeathRecapText.Summary(r), 13, TextAnchor.MiddleRight, UiTheme.TextDim, PanelWidth - 260f, y, 240f, 18f);
            y += 22f;
            var maxDmg = 1f;
            for (var i = 0; i < r.DamageByPart.Length; i++)
                if (r.DamageByPart[i] > maxDmg) maxDmg = r.DamageByPart[i];

            for (var i = 0; i < PartOrder.Length; i++)
            {
                var part = (BodyPart)System.Enum.Parse(typeof(BodyPart), PartOrder[i]);
                var idx = DeathRecapBuilder.PartIndex(part);
                var hits = r.HitsByPart[idx];
                var dmg = r.DamageByPart[idx];
                Put(DeathRecapText.PartName(part), 15, TextAnchor.MiddleLeft, hits > 0 ? UiTheme.Text : UiTheme.TextMuted, 20f, y, 90f, 22f);
                Box(UiTheme.Track, 112f, y + 5f, 200f, 12f);
                Box(part == BodyPart.Head ? UiTheme.Danger : UiTheme.Amber, 112f, y + 5f, 200f * (dmg / maxDmg), 12f);
                Put(hits > 0 ? hits + "x  " + DeathRecapText.Damage(dmg) : "-", 14, TextAnchor.MiddleRight, UiTheme.TextDim, 320f, y, 100f, 22f);
                y += 26f;
            }

            y += 10f;
            Put("SON SANİYELER", 13, TextAnchor.MiddleLeft, UiTheme.TextMuted, 20f, y, 220f, 18f);
            y += 22f;
            if (r.Timeline.Count == 0)
            {
                Put("Kayıtlı isabet yok", 14, TextAnchor.MiddleLeft, UiTheme.TextMuted, 20f, y, 300f, 20f);
                return;
            }

            for (var i = 0; i < r.Timeline.Count; i++)
            {
                var h = r.Timeline[i];
                var last = i == r.Timeline.Count - 1;
                var color = last ? UiTheme.EnemyRed : UiTheme.TextDim;
                Put(DeathRecapText.RelativeTime(h.Time, r.TimelineEnd), 14, TextAnchor.MiddleLeft, UiTheme.TextMuted, 20f, y, 90f, 20f);
                Put(DeathRecapText.PartName(h.Part), 14, TextAnchor.MiddleLeft, color, 112f, y, 120f, 20f);
                Put(DeathRecapText.Damage(h.Amount), 14, TextAnchor.MiddleRight, color, 320f, y, 100f, 20f, last ? FontStyle.Bold : FontStyle.Normal);
                y += 20f;
                if (y > 540f) break;
            }
        }
    }
}
