using System;
using System.Collections.Generic;

namespace Project.Application.Dialogue
{
    /// <summary>
    /// Tekrar önleme belleği: aynı kategoride son N varyant ve aynı askerin son M varyantı tekrar seçilmez
    /// (aday azsa ve hepsi yasaksa kısıtlar gevşer). Saf mantık.
    /// </summary>
    public sealed class BarkMemory
    {
        private readonly int _categoryDepth;
        private readonly int _speakerDepth;
        private readonly Dictionary<string, List<string>> _category = new Dictionary<string, List<string>>(32);
        private readonly Dictionary<long, List<string>> _speaker = new Dictionary<long, List<string>>(32);

        public BarkMemory(int categoryDepth = 3, int speakerDepth = 4)
        {
            _categoryDepth = Math.Max(0, categoryDepth);
            _speakerDepth = Math.Max(0, speakerDepth);
        }

        /// <summary>
        /// Adaylardan birini seçer. roll 0..1. Önce hem kategori hem konuşmacı belleğini, sonra yalnız konuşmacıyı,
        /// sonra yalnız kategoriyi, en son hiçbirini yok sayar.
        /// </summary>
        public DialogueLine Pick(string category, int speakerId, IList<DialogueLine> candidates, float roll)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            _category.TryGetValue(category ?? string.Empty, out var catHist);
            var key = Key(category, speakerId);
            _speaker.TryGetValue(key, out var spkHist);

            var pool = new List<DialogueLine>(candidates.Count);
            // pass 0: ikisini de engelle, 1: yalnız konuşmacı, 2: yalnız kategori, 3: serbest.
            for (var pass = 0; pass < 4 && pool.Count == 0; pass++)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    var id = candidates[i].Id;
                    var blockCat = (pass == 0 || pass == 2) && Contains(catHist, id, _categoryDepth);
                    var blockSpk = (pass == 0 || pass == 1) && Contains(spkHist, id, _speakerDepth);
                    if (blockCat || blockSpk)
                        continue;
                    pool.Add(candidates[i]);
                }
            }

            if (pool.Count == 0)
                pool.AddRange(candidates);

            var index = (int)(Clamp01(roll) * pool.Count);
            if (index >= pool.Count)
                index = pool.Count - 1;
            var picked = pool[index];
            Remember(category, speakerId, picked.Id);
            return picked;
        }

        public void Remember(string category, int speakerId, string id)
        {
            Push(_category, category ?? string.Empty, id, _categoryDepth);
            var key = Key(category, speakerId);
            if (!_speaker.TryGetValue(key, out var list))
            {
                list = new List<string>(_speakerDepth + 1);
                _speaker[key] = list;
            }

            list.Add(id);
            while (list.Count > _speakerDepth)
                list.RemoveAt(0);
        }

        public void Clear()
        {
            _category.Clear();
            _speaker.Clear();
        }

        private static void Push(Dictionary<string, List<string>> map, string key, string id, int depth)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<string>(depth + 1);
                map[key] = list;
            }

            list.Add(id);
            while (list.Count > depth)
                list.RemoveAt(0);
        }

        private static bool Contains(List<string> list, string id, int depth)
        {
            if (list == null || depth <= 0)
                return false;
            for (var i = Math.Max(0, list.Count - depth); i < list.Count; i++)
                if (list[i] == id)
                    return true;
            return false;
        }

        private static long Key(string category, int speakerId)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                var c = category ?? string.Empty;
                for (var i = 0; i < c.Length; i++)
                    h = (h ^ c[i]) * 1099511628211L;
                return h ^ ((long)speakerId << 17);
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 0.9999999f ? 0.9999999f : v;
    }
}
