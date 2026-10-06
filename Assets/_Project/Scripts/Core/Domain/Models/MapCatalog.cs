using System;

namespace Project.Core.Domain
{
    /// <summary>Oynanabilir haritalar: kimlik ("kuzgun", "ayaz"), görünen ad ve yarı boyut. Saf C#.</summary>
    public static class MapCatalog
    {
        public const string Kuzgun = "kuzgun";
        public const string AyazGecidi = "ayaz";
        public const string MaviLiman = "liman";
        public const string KartalYaylasi = "kartal";

        public const string KuzgunName = "Kuzgun Vadisi";
        public const string AyazGecidiName = "Ayaz Geçidi";
        public const string MaviLimanName = "Mavi Liman";
        public const string KartalYaylasiName = "Kartal Yaylası";

        private static readonly string[] Ids = { Kuzgun, AyazGecidi, MaviLiman, KartalYaylasi };
        private static readonly string[] Names = { KuzgunName, AyazGecidiName, MaviLimanName, KartalYaylasiName };

        public static int Count => Ids.Length;

        /// <summary>Görünen adların kopyası (menü seçicisi için; indeks = <see cref="IdAt"/>).</summary>
        public static string[] DisplayNames() => (string[])Names.Clone();

        public static string IdAt(int index) => Ids[Math.Max(0, Math.Min(Ids.Length - 1, index))];

        /// <summary>Kimlik ya da görünen ad → kimlik; bilinmeyen/boş → Kuzgun.</summary>
        public static string Normalize(string idOrName)
        {
            var i = IndexOf(idOrName);
            return Ids[i < 0 ? 0 : i];
        }

        public static int IndexOf(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName))
                return -1;
            var key = idOrName.Trim();
            for (var i = 0; i < Ids.Length; i++)
            {
                if (string.Equals(Ids[i], key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Names[i], key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public static string DisplayName(string idOrName) => Names[Math.Max(0, IndexOf(idOrName))];

        public static float HalfSize(string idOrName) => Normalize(idOrName) != Kuzgun ? 500f : 512f;
    }
}
