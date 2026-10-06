using System;
using System.Collections.Generic;
using System.Text;

namespace Project.Application.Dialogue
{
    public sealed class DialogueLine
    {
        public string Id;
        public string Text;
        /// <summary>null = her stres hâlinde geçerli.</summary>
        public DialogueStress? Stress;
        public string Category;
        public string VoiceHint;

        public bool Matches(DialogueStress stress) => !Stress.HasValue || Stress.Value == stress;
    }

    /// <summary>
    /// Replik kitabı v2 (Design/Audio/telsiz_replikleri_v2.csv: id,text,stress,category,voiceHint).
    /// Kategori + stres ile arama; stres için komşu hâle düşme (panik -> çatışma -> sakin). Saf mantık.
    /// </summary>
    public sealed class DialogueLineBook
    {
        public const string ResourcePath = "Audio/dialogue_lines_v2";

        private readonly Dictionary<string, List<DialogueLine>> _byCategory = new Dictionary<string, List<DialogueLine>>(64);
        private readonly Dictionary<string, DialogueLine> _byId = new Dictionary<string, DialogueLine>(512);

        public int Count => _byId.Count;

        public static DialogueLineBook FromCsv(string csv)
        {
            var book = new DialogueLineBook();
            if (string.IsNullOrEmpty(csv))
                return book;

            var rows = ParseCsv(csv);
            for (var r = 1; r < rows.Count; r++)
            {
                var f = rows[r];
                if (f.Count < 4 || string.IsNullOrWhiteSpace(f[0]) || string.IsNullOrWhiteSpace(f[1]))
                    continue;

                book.Add(new DialogueLine
                {
                    Id = f[0].Trim(),
                    Text = f[1].Trim(),
                    Stress = ParseStress(f[2]),
                    Category = f[3].Trim(),
                    VoiceHint = f.Count > 4 ? f[4].Trim() : string.Empty
                });
            }

            return book;
        }

        public void Add(DialogueLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Id) || string.IsNullOrEmpty(line.Category) || _byId.ContainsKey(line.Id))
                return;

            _byId[line.Id] = line;
            if (!_byCategory.TryGetValue(line.Category, out var list))
            {
                list = new List<DialogueLine>(8);
                _byCategory[line.Category] = list;
            }

            list.Add(line);
        }

        public DialogueLine Get(string id) => id != null && _byId.TryGetValue(id, out var l) ? l : null;

        public bool HasCategory(string category) => category != null && _byCategory.ContainsKey(category);

        /// <summary>Kategori + stres için adayları <paramref name="output"/> listesine toplar (boşsa komşu stres).</summary>
        public int Collect(string category, DialogueStress stress, List<DialogueLine> output)
        {
            output.Clear();
            if (category == null || !_byCategory.TryGetValue(category, out var list))
                return 0;

            Fill(list, stress, output);
            if (output.Count == 0)
            {
                Fill(list, DialogueStress.Combat, output);
            }

            if (output.Count == 0)
                Fill(list, DialogueStress.Calm, output);
            if (output.Count == 0)
                Fill(list, DialogueStress.Panic, output);
            return output.Count;
        }

        private static void Fill(List<DialogueLine> source, DialogueStress stress, List<DialogueLine> output)
        {
            for (var i = 0; i < source.Count; i++)
                if (source[i].Matches(stress))
                    output.Add(source[i]);
        }

        public static DialogueStress? ParseStress(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            switch (s.Trim().ToLowerInvariant())
            {
                case "sakin": return DialogueStress.Calm;
                case "catisma":
                case "çatışma": return DialogueStress.Combat;
                case "panik": return DialogueStress.Panic;
                default: return null;
            }
        }

        /// <summary>RFC4180 benzeri CSV ayrıştırıcı (tırnak, "" kaçışı, CRLF).</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>(256);
            var row = new List<string>(6);
            var sb = new StringBuilder(64);
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(sb.ToString());
                    sb.Length = 0;
                    if (row.Count > 1 || row[0].Length > 0)
                        rows.Add(row);
                    row = new List<string>(6);
                }
                else
                {
                    sb.Append(ch);
                }
            }

            if (sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
}
