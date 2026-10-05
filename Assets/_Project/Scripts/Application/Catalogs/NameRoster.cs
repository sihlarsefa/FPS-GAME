using System;
using System.Collections.Generic;
using Project.Core.Interfaces;

namespace Project.Application.Catalogs
{
    /// <summary>
    /// Bot çağrı adları ve soyadları (Kartal, Bozkurt, Atmaca, Şahin, Yılmaz, Demir...). Benzersiz adlar üretir;
    /// istenen sayı listeyi aşarsa "Kartal 2" gibi numaralı adlarla benzersizliği korur.
    /// </summary>
    public static class NameRoster
    {
        private static readonly string[] AllNames =
        {
            // Çağrı adları
            "Kartal", "Bozkurt", "Atmaca", "Şahin", "Doğan", "Kurt", "Aslan", "Pars", "Kaplan", "Çakır",
            "Toğrul", "Yıldırım", "Şimşek", "Tufan", "Kasırga", "Fırtına", "Poyraz", "Karayel", "Bora", "Tayfun",
            "Volkan", "Ateş", "Akıncı", "Kılıç", "Kalkan", "Mızrak", "Gökbörü", "Alpaslan", "Akbulut", "Turna",
            // Soyadları
            "Yılmaz", "Kaya", "Demir", "Çelik", "Yıldız", "Öztürk", "Aydın", "Özdemir", "Arslan", "Çetin",
            "Kara", "Koç", "Özkan", "Polat", "Korkmaz", "Akın", "Güneş", "Aksoy", "Erdem", "Bulut",
            "Turan", "Tekin", "Keskin", "Ünal", "Uçar", "Tunç", "Altun", "Sarı", "Karaca", "Avcı",
            "Yavuz", "Uysal", "Durmaz", "Yalçın", "Güler", "Aktaş", "Bayram", "Sezer", "Coşkun", "Tuncer",
            "Ekinci", "Işık", "Duman", "Karakaya", "Erol", "Akgül", "Toprak", "Taşkın", "Çınar", "Özer",
            "Sönmez", "Başaran", "Kocabaş", "Yüksel", "Acar", "Bilgin", "Kahraman", "Savaş", "Gündoğdu", "Esen",
            "Kılınç", "Bozdağ", "Yiğit", "Cesur", "Metin", "Önal", "Ergin", "Karabulut", "Atalay", "Tamer"
        };

        private static readonly IReadOnlyList<string> ReadOnlyNames = Array.AsReadOnly(AllNames);

        public static IReadOnlyList<string> Names => ReadOnlyNames;

        /// <summary>
        /// count adet benzersiz ad (karıştırılmış). random null ise liste sırası kullanılır.
        /// count listeyi aşarsa adlar "Ad 2", "Ad 3"... şeklinde numaralanır.
        /// </summary>
        public static List<string> CreateUnique(int count, IRandom random)
        {
            if (count <= 0)
                return new List<string>();

            var pool = new string[AllNames.Length];
            Array.Copy(AllNames, pool, AllNames.Length);
            Shuffle(pool, random);

            var result = new List<string>(count);
            var round = 1;
            while (result.Count < count)
            {
                for (var i = 0; i < pool.Length && result.Count < count; i++)
                    result.Add(round == 1 ? pool[i] : pool[i] + " " + round);

                round++;
                if (result.Count < count)
                    Shuffle(pool, random);
            }

            return result;
        }

        /// <summary>Rastgele tek bir ad (benzersizlik garantisi yok).</summary>
        public static string Pick(IRandom random)
        {
            if (random == null)
                return AllNames[0];

            return AllNames[random.Next(0, AllNames.Length)];
        }

        private static void Shuffle(string[] items, IRandom random)
        {
            if (random == null)
                return;

            for (var i = items.Length - 1; i > 0; i--)
            {
                var j = random.Next(0, i + 1);
                if (j < 0 || j > i)
                    continue;

                var tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }
    }
}
