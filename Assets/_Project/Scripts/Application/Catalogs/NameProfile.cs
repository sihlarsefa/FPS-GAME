using System;
using System.Collections.Generic;

namespace Project.Application.Catalogs
{
    /// <summary>İsim profili: Gercek = gerçek üretici/model adları, Kurgusal = özgün uydurma adlar. Ağ kimlikleri/indeksleri değişmez, yalnızca görünen ad.</summary>
    public enum NameProfileKind
    {
        Gercek = 0,
        Kurgusal = 1
    }

    /// <summary>
    /// Silah ve araç görünen adlarının tek kaynağı. Yerelleştirme anahtarı: "name.&lt;id&gt;" (Gercek), "name.k.&lt;id&gt;" (Kurgusal).
    /// Çözücü (Resolver) anahtar bulursa onu, yoksa yerleşik tabloyu kullanır; tablo da yoksa verilen yedek ad döner.
    /// </summary>
    public static class NameProfile
    {
        public const string VehicleKirpi = "vehicle_kirpi";
        public const string VehicleHeli = "vehicle_t70";

        private static readonly Dictionary<string, (string Real, string Fiction)> Table = new(StringComparer.Ordinal)
        {
            { WeaponIds.Sar9, ("SAR 9", "Kartal-9") },
            { WeaponIds.Tp9, ("Canik TP9", "Şahin TP") },
            { WeaponIds.Sar109, ("SAR 109T", "Çakır-109") },
            { WeaponIds.Mpt55, ("MPT-55", "Yıldırım-55") },
            { WeaponIds.Mpt76, ("MPT-76", "Yıldırım-76") },
            { WeaponIds.G3, ("G3A7", "Bozkurt A7") },
            { WeaponIds.Knt76, ("KNT-76", "Atmaca-76") },
            { WeaponIds.Jng90, ("JNG-90", "Doğan-90") },
            { WeaponIds.Pmt76, ("PMT-76", "Kalkan-76") },
            { WeaponIds.Escort, ("Escort", "Fırtına Pompalı") },
            { WeaponIds.Sar223, ("SAR 223", "Kartal-223") },
            { WeaponIds.Mpt76K, ("MPT-76K", "Yıldırım-76K") },
            { WeaponIds.Mete, ("Canik METE SFT", "Şahin Mete") },
            { WeaponIds.Sar762Mt, ("SAR 762 MT", "Kartal-762 MT") },
            { WeaponIds.Mg3, ("MG3", "Demirkırat-3") },
            { WeaponIds.EscortMagnum, ("Escort Magnum", "Fırtına Magnum") },
            { VehicleKirpi, ("Kirpi Zırhlı Aracı", "Pusat Zırhlı Aracı") },
            { VehicleHeli, ("T-70 Helikopteri", "Akıncı Helikopteri") },
        };

        private static NameProfileKind _current = NameProfileKind.Gercek;

        /// <summary>İsteğe bağlı yerelleştirme çözücüsü: anahtar → metin (bulunamazsa null).</summary>
        public static Func<string, string> Resolver { get; set; }

        public static NameProfileKind Current
        {
            get => _current;
            set => _current = Normalize((int)value);
        }

        public static NameProfileKind Normalize(int value) =>
            value == (int)NameProfileKind.Kurgusal ? NameProfileKind.Kurgusal : NameProfileKind.Gercek;

        public static string Key(string id, NameProfileKind kind) =>
            (kind == NameProfileKind.Kurgusal ? "name.k." : "name.") + id;

        public static string ProfileLabel(NameProfileKind kind) => kind == NameProfileKind.Kurgusal ? "Kurgusal" : "Gerçek";

        public static bool Has(string id) => !string.IsNullOrEmpty(id) && Table.ContainsKey(id);

        /// <summary>Kimliğin geçerli profildeki adı; bilinmiyorsa <paramref name="fallback"/>.</summary>
        public static string Get(string id, string fallback) => Get(id, fallback, _current);

        public static string Get(string id, string fallback, NameProfileKind kind)
        {
            if (string.IsNullOrEmpty(id))
                return fallback;

            try
            {
                var resolved = Resolver?.Invoke(Key(id, kind));
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }
            catch (Exception)
            {
                // Çözücü hatası görünen adı bozmasın.
            }

            if (Table.TryGetValue(id, out var names))
                return kind == NameProfileKind.Kurgusal ? names.Fiction : names.Real;

            return fallback;
        }
    }
}
