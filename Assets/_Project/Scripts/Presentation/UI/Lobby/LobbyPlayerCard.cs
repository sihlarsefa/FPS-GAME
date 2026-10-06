using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>Oyuncu kartı veri paketi (Unity bağımsız; çağıran taraf kariyer/rütbe servisinden doldurur).</summary>
    public struct LobbyPlayerInfo
    {
        public string RankName;
        public string PlayerName;
        public int Level;
        public float XpProgress01;
        public int Kills;
        public int Deaths;
        public int Wins;
        public int Matches;
    }

    /// <summary>
    /// Oyuncu kartı: sol kırmızı şerit, rütbe + isim, XP çubuğu ve istatistik üçlüsü (K/D, KAZANMA %, MAÇ).
    /// <see cref="Set"/> ile güncellenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyPlayerCard : MonoBehaviour
    {
        private Text _rank;
        private Text _name;
        private Text _kd;
        private Text _win;
        private Text _matches;
        private RectTransform _xpFill;

        /// <summary>Kartı kurar.</summary>
        public static LobbyPlayerCard Create(Transform parent, float width = 420f, float height = 150f)
        {
            var root = UiFactory.CreateRect("LobbyPlayerCard", parent);
            UiFactory.SetSize(root, width, height);
            var bg = UiFactory.Image(root, UiSprites.ChamferRect, LobbyTheme.Panel);
            bg.type = Image.Type.Sliced;
            UiFactory.Stretch(bg);
            UiFactory.AddOutline(bg, LobbyTheme.Border, 1f);

            var stripe = UiFactory.Image(root, null, LobbyTheme.Red);
            stripe.raycastTarget = false;
            UiFactory.SetRect(stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f));

            var card = root.gameObject.AddComponent<LobbyPlayerCard>();
            card._rank = UiFactory.Label(root, string.Empty, 18, TextAnchor.MiddleLeft, LobbyTheme.Gold, FontStyle.Bold);
            UiFactory.SetRect(card._rank, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -34f), new Vector2(-14f, -10f));
            card._name = UiFactory.Label(root, string.Empty, 30, TextAnchor.MiddleLeft, LobbyTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(card._name, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -72f), new Vector2(-14f, -34f));

            var track = UiFactory.Image(root, null, new Color(1f, 1f, 1f, 0.08f));
            track.raycastTarget = false;
            UiFactory.SetRect(track, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -86f), new Vector2(-14f, -80f));
            var fill = UiFactory.Image(track.rectTransform, null, LobbyTheme.Red);
            fill.raycastTarget = false;
            card._xpFill = fill.rectTransform;

            card._kd = Stat(root, 0, "K/D");
            card._win = Stat(root, 1, "KAZANMA");
            card._matches = Stat(root, 2, "MAÇ");
            return card;
        }

        private static Text Stat(RectTransform root, int col, string caption)
        {
            var w = 1f / 3f;
            var cell = UiFactory.CreateRect("Stat_" + caption, root);
            UiFactory.SetRect(cell, new Vector2(w * col, 0f), new Vector2(w * (col + 1), 0f), new Vector2(col == 0 ? 22f : 0f, 8f), new Vector2(0f, 62f));
            var value = UiFactory.Label(cell, "0", 26, TextAnchor.LowerLeft, LobbyTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(value, Vector2.zero, Vector2.one, new Vector2(0f, 18f), Vector2.zero);
            var cap = UiFactory.Label(cell, caption, 13, TextAnchor.LowerLeft, LobbyTheme.TextDim);
            UiFactory.SetRect(cap, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 18f));
            return value;
        }

        /// <summary>Kart içeriğini günceller.</summary>
        public void Set(LobbyPlayerInfo info)
        {
            _rank.text = (info.RankName ?? string.Empty).ToUpper(new System.Globalization.CultureInfo("tr-TR")) + "  ·  SEV " + info.Level;
            _name.text = info.PlayerName ?? string.Empty;
            var p = Mathf.Clamp01(info.XpProgress01);
            _xpFill.anchorMin = Vector2.zero;
            _xpFill.anchorMax = new Vector2(p, 1f);
            _xpFill.offsetMin = _xpFill.offsetMax = Vector2.zero;
            _kd.text = LobbyTheme.KdRatio(info.Kills, info.Deaths).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            _win.text = "%" + LobbyTheme.WinPercent(info.Wins, info.Matches);
            _matches.text = info.Matches.ToString();
        }
    }
}
