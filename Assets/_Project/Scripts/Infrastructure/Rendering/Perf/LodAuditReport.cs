using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Sahnedeki LODGroup'ları tarar; aşırı detaylı / hatalı olanları raporlar (denetim aracı, runtime'a yük bindirmez).</summary>
    public static class LodAuditReport
    {
        public readonly struct Entry
        {
            public readonly string Path; public readonly LodVerdict Verdict; public readonly float FirstTransition;
            public Entry(string path, LodVerdict v, float first) { Path = path; Verdict = v; FirstTransition = first; }
        }

        public static List<Entry> Scan()
        {
            var list = new List<Entry>();
            var groups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            foreach (var g in groups)
            {
                var lods = g.GetLODs();
                var t = new float[lods.Length];
                for (int i = 0; i < lods.Length; i++) t[i] = lods[i].screenRelativeTransitionHeight;
                var v = PerfMath.ClassifyLod(t);
                if (v != LodVerdict.Ok) list.Add(new Entry(PathOf(g.transform), v, t.Length > 0 ? t[0] : 0f));
            }
            return list;
        }

        public static string BuildReport()
        {
            var sb = new StringBuilder("[LOD denetimi]\n");
            var items = Scan();
            sb.Append("Sorunlu LODGroup: ").Append(items.Count).Append('\n');
            foreach (var e in items) sb.Append(" - ").Append(e.Path).Append(" => ").Append(e.Verdict).Append(" (LOD0 gecis ").Append(e.FirstTransition.ToString("0.00")).Append(")\n");
            return sb.ToString();
        }

        private static string PathOf(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
