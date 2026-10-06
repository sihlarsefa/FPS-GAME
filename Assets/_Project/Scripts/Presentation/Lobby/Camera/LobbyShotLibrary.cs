using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Sekme başına çekim tablosu. Dizin sırası <c>MenuBackdrop.Shot</c> sırasıyla aynıdır:
    /// 0 Ana, 1 Kurulum, 2 Kariyer, 3 Ayarlar, 4 Oyna (görevler / harita masası), 5 Donanım (vitrin / silah masası), 6 Tim.
    /// ANA: geniş kahraman çekimi, komutan sol üçte bir çizgisinde, gözleri üst üçte birde; sağ boşluk ateş ve tim için bırakılır.
    /// VİTRİN: dar FOV (30), yakın plan, güçlü arka plan bulanıklığı. GÖREVLER: yüksekten bakış, masaya eğik, hafif ufuk eğimi.
    /// AYARLAR: karartılmış (-1,2 EV), yoğun bulanık, el titremesi azaltılmış (arayüz okunur kalır).
    /// </summary>
    public static class LobbyShotLibrary
    {
        public const int Main = 0, Setup = 1, Career = 2, Settings = 3, Play = 4, Loadout = 5, Team = 6;
        public const int Count = 7;

        /// <summary>Varsayılan ekran oranı (16:9). Çözücü, çalışma anında gerçek orana göre yeniden kurulabilir.</summary>
        public const float DefaultAspect = 16f / 9f;

        // Dioramanın komutan konumu (MenuDioramaBuilder.SoldierPosition ile aynı: 0,2 / 0 / 2,6).
        private static readonly Vector3 CommanderEyes = new Vector3(0.2f, 1.62f, 2.6f);

        public static LobbyShot[] Build(float aspect)
        {
            if (aspect < 0.5f || float.IsNaN(aspect)) aspect = DefaultAspect;

            var shots = new LobbyShot[Count];

            // ANA: kahraman sol üçte bir çizgisinde, gözler ~üst üçte birin hemen altında (portre için biraz aşağı).
            var mainPos = new Vector3(0.45f, 1.5f, -0.5f);
            var mainTarget = LobbyThirdsFraming.SolveTarget(mainPos, CommanderEyes, 38f, aspect,
                LobbyThirdsFraming.LeftThird, 0.26f);
            shots[Main] = LobbyShot.Focused(mainPos, mainTarget, 38f, 0.30f, 0f, 1f, 0f);
            // Odak mesafesi komutana uzaklık (hedef noktası değil).
            shots[Main].FocusDistance = Vector3.Distance(mainPos, CommanderEyes);

            shots[Setup] = LobbyShot.Focused(new Vector3(-0.8f, 1.5f, 0.3f), new Vector3(-2.5f, 1.3f, 10.5f), 40f, 0.15f, 0f, 1f, 0f);
            shots[Career] = LobbyShot.Focused(new Vector3(0.2f, 1.65f, 1.0f), new Vector3(0.7f, 1.55f, 3.2f), 32f, 0.45f, 0f, 0.9f, 0f);

            // AYARLAR: karartılmış, geniş, yoğun bulanık; titreme düşük.
            shots[Settings] = LobbyShot.Focused(new Vector3(-0.5f, 1.6f, 0f), new Vector3(1.5f, 1.4f, 8f), 46f, 0.9f, -1.2f, 0.4f, 0f);
            shots[Settings].FocusDistance = 1.0f; // arka plan tamamen odak dışı kalır; menü kartları öne çıkar

            // GÖREVLER (harita masası): yüksek, eğik bakış, 1,2 derece ufuk eğimi.
            shots[Play] = LobbyShot.Focused(new Vector3(0.5f, 2.1f, 0.4f), new Vector3(1.0f, 1.0f, 3.4f), 44f, 0.5f, -0.1f, 0.8f, 1.2f);

            // VİTRİN (silah masası): dar FOV, alçak ve yakın; sığ alan derinliği.
            shots[Loadout] = LobbyShot.Focused(new Vector3(-0.6f, 1.15f, 1.3f), new Vector3(-1.8f, 0.6f, 4.4f), 30f, 0.85f, 0f, 0.6f, -0.8f);

            shots[Team] = LobbyShot.Focused(new Vector3(0f, 1.6f, 0.3f), new Vector3(0.6f, 1.3f, 3.2f), 38f, 0.35f, 0f, 1f, 0f);
            return shots;
        }

        public static int Clamp(int index) => index < 0 || index >= Count ? Main : index;
    }
}
