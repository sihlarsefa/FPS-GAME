using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Şarjör pip şeridi: dolu pipler beyaz/kademe renginde, boşlar soluk. Kademe düşük/kritik olunca renk değişir, kritikte nabız atar.
    /// Hesaplar <see cref="AmmoPipLayout"/> içindedir. Pip'ler bir kez kurulur; Set yalnızca değişimde renk/aktiflik günceller.
    /// ENTEGRASYON: HudWeaponView.cs içinde AmmoPipStrip.Create(...) ile kurulup her karede Set(ammo, magSize, time) çağrılmalı.
    /// </summary>
    public sealed class AmmoPipStrip : MonoBehaviour
    {
        public const float PipWidth = 5f;
        public const float PipHeight = 10f;
        public const float PipGap = 2f;
        public const float GroupGap = 4f;

        private readonly List<Image> _pips = new List<Image>(AmmoPipLayout.MaxPips);
        private RectTransform _root;
        private int _shownCount = -1;
        private int _shownFilled = -1;
        private AmmoTier _shownTier = (AmmoTier)(-1);

        public RectTransform Root => _root;

        public static AmmoPipStrip Create(RectTransform parent, Vector2 anchor, Vector2 position)
        {
            var root = HudBuild.Rect("AmmoPips", parent, anchor, new Vector2(1f, 0.5f), position, new Vector2(240f, PipHeight));
            var view = root.gameObject.AddComponent<AmmoPipStrip>();
            view._root = root;
            HudBuild.PassiveGroup(root);
            return view;
        }

        public void Set(int ammo, int magazineSize, float time)
        {
            if (_root == null)
                return;
            var p = AmmoPipLayout.Compute(ammo, magazineSize);
            var tier = AmmoPipLayout.Tier(ammo, magazineSize);
            var pulse = AmmoPipLayout.WarnPulse(tier, time);
            var dirty = p.Count != _shownCount || p.Filled != _shownFilled || tier != _shownTier;

            if (p.Count != _shownCount)
            {
                while (_pips.Count < p.Count)
                    _pips.Add(HudBuild.Image("P" + _pips.Count, _root, UiSprites.White, Color.white, new Vector2(1f, 0.5f),
                        new Vector2(1f, 0.5f), Vector2.zero, new Vector2(PipWidth, PipHeight)));
                for (var i = 0; i < _pips.Count; i++)
                {
                    var active = i < p.Count;
                    HudBuild.SetActive(_pips[i], active);
                    if (active)
                    {
                        // Sağdan sola: sağdaki pip ilk mermi; sağ kenara hizalı.
                        var x = -AmmoPipLayout.PipX(i, PipWidth, PipGap, GroupGap, p.GroupSize);
                        HudBuild.SetPosition(_pips[i].rectTransform, new Vector2(x, 0f));
                    }
                }

                _shownCount = p.Count;
            }

            if (!dirty && tier < AmmoTier.Critical)
                return;

            var on = AmmoPipLayout.TierColor(tier);
            var a = tier >= AmmoTier.Critical ? Mathf.Lerp(0.55f, 1f, pulse) : 1f;
            for (var i = 0; i < p.Count; i++)
            {
                var filled = i < p.Filled;
                var c = filled ? on : new Color(1f, 1f, 1f, 0.22f);
                c.a = filled ? c.a * a : c.a;
                _pips[i].color = c;
            }

            _shownFilled = p.Filled;
            _shownTier = tier;
        }
    }
}
