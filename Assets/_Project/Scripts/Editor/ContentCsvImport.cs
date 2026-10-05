using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Project.Infrastructure.Content;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Design/Assets/*.csv okuyucu — C3-1 kolon adlarıyla uyumlu.</summary>
    public static class ContentCsvImport
    {
        public sealed class CsvTable
        {
            public string[] Headers = Array.Empty<string>();
            public List<string[]> Rows = new List<string[]>();
        }

        public static string DesignAssetsAbsolutePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ContentCsvSchema.DesignAssetsFolder));

        public static bool TryLoad(string fileName, out CsvTable table, out string error)
        {
            table = null;
            error = null;
            var path = Path.Combine(DesignAssetsAbsolutePath, fileName);
            if (!File.Exists(path))
            {
                error = "CSV yok: " + path;
                return false;
            }

            try
            {
                table = Parse(File.ReadAllText(path, Encoding.UTF8));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static CsvTable Parse(string text)
        {
            var table = new CsvTable();
            if (string.IsNullOrEmpty(text))
                return table;

            using (var reader = new StringReader(text))
            {
                string line;
                var first = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.Length > 0 && line[0] == '#')
                        continue;

                    var cells = SplitCsvLine(line);
                    if (first)
                    {
                        table.Headers = cells;
                        first = false;
                    }
                    else
                    {
                        table.Rows.Add(cells);
                    }
                }
            }

            return table;
        }

        public static int FindColumn(CsvTable table, string header)
        {
            if (table?.Headers == null)
                return -1;
            for (var i = 0; i < table.Headers.Length; i++)
            {
                if (string.Equals(table.Headers[i]?.Trim(), header, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public static string Cell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length)
                return string.Empty;
            return row[index]?.Trim() ?? string.Empty;
        }

        public static string[] SplitCsvLine(string line)
        {
            var cells = new List<string>(8);
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    cells.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                sb.Append(c);
            }

            cells.Add(sb.ToString());
            return cells.ToArray();
        }
    }
}
