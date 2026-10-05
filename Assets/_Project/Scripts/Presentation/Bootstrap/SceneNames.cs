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

        /// <summary>Atış poligonu (antrenman) sahnesi.</summary>
        public const string Training = "TrainingRange";

        /// <summary>Sahnelerin bulunduğu klasör.</summary>
        public const string ScenesFolder = "Assets/_Project/Scenes";

        private static readonly string[] Order = { MainMenu, Operation, Training };

        /// <summary>Build Settings sırası: ana menü, harekât, atış poligonu.</summary>
        public static IReadOnlyList<string> BuildOrder => Order;

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
        public static bool IsKnown(string scene) => scene == MainMenu || scene == Operation || scene == Training;

        /// <summary>Yükleme ekranında gösterilecek Türkçe ileti.</summary>
        public static string LoadingMessageFor(string scene)
        {
            switch (scene)
            {
                case Operation:
                    return "Harekât bölgesi hazırlanıyor — Kuzgun Vadisi";
                case Training:
                    return "Atış poligonu hazırlanıyor";
                case MainMenu:
                    return "Karargâha dönülüyor";
                default:
                    return "Yükleniyor";
            }
        }
    }
}
