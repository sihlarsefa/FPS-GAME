using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Sağ üst öldürme akışı (KillFeedService kayıtları): "Öldüren [Silah] Ölen", dost yeşil / düşman kırmızı,
    /// yerel oyuncu vurgulu, kafadan vuruşta "KAFADAN". En yeni üstte, en fazla 5 satır, 7 sn sonra solar.
    /// Satırlar havuzludur; metin yalnızca kayıt eklenince oluşturulur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillFeedView : MonoBehaviour
    {
        public const float Width = 560f;
        private const int MaxRows = 5;
        private const float RowHeight = 28f;
        private const float RowSpacing = 4f;
        private const float RowLifetime = 7f;
        private const float FadeSeconds = 1f;

        private static string _allyHex;
        private static string _allyLocalHex;
        private static string _enemyHex;
        private static string _dimHex;
        private static string _amberHex;
        private static string _zoneName;
        private static string _fallName;

        private sealed class Row
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Accent;
            public Text Label;
            public float TimeLeft;
            public float Y;
        }

        private readonly Row[] _rows = new Row[MaxRows];
        private int _count;

        public RectTransform Root { get; private set; }

        public static KillFeedView Create(RectTransform parent, Vector2 topRightOffset)
        {
            var root = HudBuild.Rect("KillFeed", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), topRightOffset,
                new Vector2(Width, MaxRows * (RowHeight + RowSpacing)));
            var view = root.gameObject.AddComponent<KillFeedView>();
            view.Root = root;
            view.Build();
            return view;
        }

        private void Build()
        {
            HudBuild.PassiveGroup(Root);
            EnsureColors();

            for (var i = 0; i < MaxRows; i++)
            {
                var row = new Row();
                row.Rect = HudBuild.Rect("Row" + i, Root, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(200f, RowHeight));
                row.Group = HudBuild.PassiveGroup(row.Rect, 0f);
                row.Background = HudBuild.FillImage("Bg", row.Rect, UiSprites.White, UiTheme.WithAlpha(Color.black, 0.42f));
                row.Accent = HudBuild.Image("Accent", row.Rect, UiSprites.White, UiTheme.Success, new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f), Vector2.zero, new Vector2(3f, RowHeight));
                row.Label = HudBuild.Text("Text", row.Rect, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text,
                    FontStyle.Bold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(Width - 24f, RowHeight));
                HudBuild.SetActive(row.Rect, false);
                _rows[i] = row;
            }
        }

        private static void EnsureColors()
        {
            if (_allyHex != null)
                return;

            _allyHex = HudFormat.Hex(UiTheme.Success);
            _allyLocalHex = HudFormat.Hex(UiTheme.Lighten(UiTheme.Success, 0.35f));
            _enemyHex = HudFormat.Hex(UiTheme.EnemyRed);
            _dimHex = HudFormat.Hex(UiTheme.TextDim);
            _amberHex = HudFormat.Hex(UiTheme.Amber);
        }

        /// <summary>Yeni kayıt ekler (en üste).</summary>
        public void Add(KillFeedEntry entry)
        {
            EnsureColors();

            // En eski satırı yeniden kullan: diziyi bir aşağı kaydır.
            var recycled = _rows[MaxRows - 1];
            for (var i = MaxRows - 1; i > 0; i--)
                _rows[i] = _rows[i - 1];
            _rows[0] = recycled;
            _count = Mathf.Min(_count + 1, MaxRows);

            var text = Compose(entry);
            recycled.Label.text = text;
            var width = Mathf.Clamp(recycled.Label.preferredWidth + 30f, 120f, Width);
            recycled.Rect.sizeDelta = new Vector2(width, RowHeight);
            recycled.TimeLeft = RowLifetime;
            recycled.Y = 0f;
            HudBuild.SetPosition(recycled.Rect, new Vector2(0f, RowHeight));
            recycled.Group.alpha = 0f;

            var involvesLocal = entry.KillerIsLocal || entry.VictimIsLocal;
            recycled.Background.color = involvesLocal
                ? UiTheme.WithAlpha(UiTheme.PanelLight, 0.7f)
                : UiTheme.WithAlpha(Color.black, 0.42f);
            recycled.Accent.color = entry.VictimIsAlly ? UiTheme.EnemyRed : UiTheme.Success;
            HudBuild.SetActive(recycled.Rect, true);
        }

        /// <summary>Tüm satırları temizler.</summary>
        public void Clear()
        {
            for (var i = 0; i < _rows.Length; i++)
            {
                _rows[i].TimeLeft = 0f;
                HudBuild.SetActive(_rows[i].Rect, false);
            }

            _count = 0;
        }

        private static string Compose(KillFeedEntry entry)
        {
            var victimHex = entry.VictimIsAlly ? (entry.VictimIsLocal ? _allyLocalHex : _allyHex) : _enemyHex;
            var victim = HudFormat.Colorize(entry.VictimName, victimHex);
            var weapon = string.IsNullOrEmpty(entry.WeaponName) ? string.Empty : entry.WeaponName;

            if (string.IsNullOrEmpty(entry.KillerName))
            {
                // Çevresel ölüm (bölge, düşme): "Ölen — kaynak".
                return victim + HudFormat.Colorize("  —  " + EnvironmentText(weapon), _dimHex);
            }

            var killerHex = entry.KillerIsAlly ? (entry.KillerIsLocal ? _allyLocalHex : _allyHex) : _enemyHex;
            var killer = HudFormat.Colorize(entry.KillerName, killerHex);
            var middle = HudFormat.Colorize("  [" + weapon + "]  ", _dimHex);
            var head = entry.IsHeadshot ? HudFormat.Colorize("  KAFADAN", _amberHex) : string.Empty;
            return killer + middle + victim + head;
        }

        private static string EnvironmentText(string source)
        {
            if (string.IsNullOrEmpty(source))
                return "Şehit düştü";

            _zoneName ??= HudContext.WeaponName(DamageSourceIds.Zone, true);
            _fallName ??= HudContext.WeaponName(DamageSourceIds.Fall, true);

            if (source == DamageSourceIds.Zone || source == _zoneName)
                return "Harekât sınırı dışında kaldı";
            if (source == DamageSourceIds.Fall || source == _fallName)
                return "Yüksekten düştü";
            return source;
        }

        /// <summary>HUD denetleyicisi her karede çağırır (satır kaydırma ve solma).</summary>
        public void Tick(float deltaTime)
        {
            if (_count == 0)
                return;

            var active = 0;
            for (var i = 0; i < _count; i++)
            {
                var row = _rows[i];
                if (row.TimeLeft <= 0f)
                {
                    HudBuild.SetActive(row.Rect, false);
                    continue;
                }

                row.TimeLeft -= deltaTime;
                var targetY = -i * (RowHeight + RowSpacing);
                var current = row.Rect.anchoredPosition.y;
                var y = Mathf.Lerp(current, targetY, 1f - Mathf.Exp(-deltaTime * 14f));
                HudBuild.SetPosition(row.Rect, new Vector2(0f, y));

                var age = RowLifetime - row.TimeLeft;
                var alpha = Mathf.Min(Mathf.Clamp01(age / 0.15f), Mathf.Clamp01(row.TimeLeft / FadeSeconds));
                HudBuild.SetAlpha(row.Group, alpha);

                if (row.TimeLeft <= 0f)
                    HudBuild.SetActive(row.Rect, false);
                else
                    active = i + 1;
            }

            _count = active;
        }
    }
}
