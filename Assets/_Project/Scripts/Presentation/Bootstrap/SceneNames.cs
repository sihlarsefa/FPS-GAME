using System.Collections.Generic;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Oyunun sahne adları ve proje içi yolları. Editör kurulumu sahneleri <see cref="PathOf"/> ile üretir ve
    /// <see cref="BuildOrder"/> sırasıyla Build Settings'e ekler.
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Ana menü sahnesi.</summary>
        public const string MainMenu = "MainMenu";

        /// <summary>Harekât (Battle Royale) sahnesi — "Kuzgun Vadisi".</summary>
        public const string Operation = "KuzgunVadisi";

        /// <summary>Harekât sahnesi — "Ayaz Geçidi" (karlı dağ geçidi).</summary>
        public const string AyazGecidi = "AyazGecidi";

        /// <summary>Harekât sahnesi — "Mavi Liman" (kıyı kasabası).</summary>
        public const string MaviLiman = "MaviLiman";

        /// <summary>Harekât sahnesi — "Kartal Yaylası" (yüksek yayla).</summary>
        public const string KartalYaylasi = "KartalYaylasi";

        /// <summary>Atış poligonu (antrenman) sahnesi.</summary>
        public const string Training = "TrainingRange";

        /// <summary>AAA görsel benchmark sahnesi (150×150 m; bkz. Docs/CURSOR_AAA_BENCHMARK.md).</summary>
        public const string AaaBenchmark = "AAA_Benchmark";

        /// <summary>Çatışma (hızlı maç) modu Kuzgun Vadisi sahnesini yeniden kullanır.</summary>
        public const string Skirmish = Operation;

        /// <summary>Sahnelerin bulunduğu klasör.</summary>
        public const string ScenesFolder = "Assets/_Project/Scenes";

        private static readonly string[] Order = { MainMenu, Operation, AyazGecidi, MaviLiman, KartalYaylasi, Training, AaaBenchmark };

        /// <summary>Build Settings sırası: ana menü, harekât, atış poligonu.</summary>
        public static IReadOnlyList<string> BuildOrder => Order;

        /// <summary>Harita kimliğine (<see cref="Project.Core.Domain.MapCatalog"/>) karşılık gelen harekât sahnesi.</summary>
        public static string OperationSceneFor(string mapId)
        {
            var id = Project.Core.Domain.MapCatalog.Normalize(mapId);
            if (id == Project.Core.Domain.MapCatalog.AyazGecidi)
                return AyazGecidi;
            if (id == Project.Core.Domain.MapCatalog.KartalYaylasi)
                return KartalYaylasi;
            return id == Project.Core.Domain.MapCatalog.MaviLiman ? MaviLiman : Operation;
        }

        /// <summary>Harekât sahnesinin harita kimliği (<see cref="Project.Core.Domain.MapCatalog"/>); harekât sahnesi değilse null.</summary>
        public static string MapIdForScene(string scene)
        {
            if (scene == Operation) return Project.Core.Domain.MapCatalog.Kuzgun;
            if (scene == AyazGecidi) return Project.Core.Domain.MapCatalog.AyazGecidi;
            if (scene == MaviLiman) return Project.Core.Domain.MapCatalog.MaviLiman;
            if (scene == KartalYaylasi) return Project.Core.Domain.MapCatalog.KartalYaylasi;
            return null;
        }

        /// <summary>Sahnenin proje yolu: "Assets/_Project/Scenes/KuzgunVadisi.unity".</summary>
        public static string PathOf(string scene)
        {
            if (string.IsNullOrEmpty(scene))
                scene = MainMenu;

            if (scene.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                return scene.StartsWith("Assets/", System.StringComparison.Ordinal) ? scene : ScenesFolder + "/" + scene;

            return ScenesFolder + "/" + scene + ".unity";
        }

        /// <summary>Sahne adının bilinen oyun sahnelerinden biri olup olmadığı.</summary>
        public static bool IsKnown(string scene) => scene == MainMenu || scene == Operation || scene == AyazGecidi || scene == MaviLiman || scene == KartalYaylasi || scene == Training || scene == AaaBenchmark;

        /// <summary>Yükleme ekranında gösterilecek Türkçe ileti.</summary>
        public static string LoadingMessageFor(string scene)
        {
            switch (scene)
            {
                case Operation:
                    return "Harekât bölgesi hazırlanıyor — Kuzgun Vadisi";
                case AyazGecidi:
                    return "Harekât bölgesi hazırlanıyor — Ayaz Geçidi";
                case MaviLiman:
                    return "Harekât bölgesi hazırlanıyor — Mavi Liman";
                case KartalYaylasi:
                    return "Harekât bölgesi hazırlanıyor — Kartal Yaylası";
                case Training:
                    return "Atış poligonu hazırlanıyor";
                case AaaBenchmark:
                    return "AAA test sahnesi hazırlanıyor";
                case MainMenu:
                    return "Karargâha dönülüyor";
                default:
                    return "Yükleniyor";
            }
        }
    }
}
