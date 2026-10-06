using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI.Lobby.Home
{
    /// <summary>Karusel haber karti verisi.</summary>
    public sealed class HomeNewsItem
    {
        public string Tag;
        public string Title;
        public string Body;
        public Color Accent;
    }

    /// <summary>Yerel haber listesi. Sunucu haberi gelince Provider ile degistirilir (ContentOverrides'a dokunmaz).</summary>
    public static class HomeNewsCatalog
    {
        /// <summary>Dis kaynak (varsa) yerel listenin yerine gecer; bos/null donerse yerel liste kullanilir.</summary>
        public static System.Func<IReadOnlyList<HomeNewsItem>> Provider;

        public static IReadOnlyList<HomeNewsItem> Items()
        {
            try
            {
                var external = Provider != null ? Provider() : null;
                if (external != null && external.Count > 0) return external;
            }
            catch (System.Exception e) { Debug.LogException(e); }
            return Local;
        }

        private static readonly List<HomeNewsItem> Local = new List<HomeNewsItem>
        {
            new HomeNewsItem { Tag = "SEZON", Title = "Sezon ödülleri açıldı", Body = "Her maç kademe XP'si kazandırır. Ücretsiz hattan kozmetik topla.", Accent = new Color(0.83f, 0.23f, 0.23f, 1f) },
            new HomeNewsItem { Tag = "ETKİNLİK", Title = "Hafta sonu çifte XP", Body = "Cuma akşamından Pazartesi'ye kadar maç XP'si iki katı.", Accent = new Color(0.95f, 0.72f, 0.20f, 1f) },
            new HomeNewsItem { Tag = "HARİTA", Title = "Kuzgun Vadisi: yeni üs noktaları", Body = "Dağ geçidi ve kuzey hattı yeniden düzenlendi; helikopter inişi daha güvenli.", Accent = new Color(0.30f, 0.62f, 0.85f, 1f) },
            new HomeNewsItem { Tag = "İPUCU", Title = "Tim ile intikal et", Body = "Dağılma. Kirpi ile birlikte inen tim ilk çatışmayı kazanır.", Accent = new Color(0.45f, 0.78f, 0.45f, 1f) },
        };
    }
}
