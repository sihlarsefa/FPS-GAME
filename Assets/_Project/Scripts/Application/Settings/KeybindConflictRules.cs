using System;
using System.Collections.Generic;

namespace Project.Application.Settings
{
    /// <summary>Bir tuş atama satırı (eylem, bağlam, tuş dizgesi).</summary>
    public readonly struct BindingEntry
    {
        public readonly string Action;
        /// <summary>Bağlam: aynı bağlamdaki aynı tuş çakışır; farklı bağlamlar (örn. araç / yürüyüş) birlikte kullanılabilir. "ortak" her bağlamla çakışır.</summary>
        public readonly string Context;
        public readonly string Key;

        public BindingEntry(string action, string context, string key)
        {
            Action = action;
            Context = string.IsNullOrEmpty(context) ? "ortak" : context;
            Key = key ?? string.Empty;
        }
    }

    public readonly struct BindingConflict
    {
        public readonly string Key;
        public readonly string ActionA;
        public readonly string ActionB;
        public BindingConflict(string key, string a, string b) { Key = key; ActionA = a; ActionB = b; }
    }

    /// <summary>Tuş atama çakışması ve ayrılmış tuş denetimi (saf).</summary>
    public static class KeybindConflictRules
    {
        /// <summary>Atanamayan/ayrılmış tuşlar (Windows ve menü için).</summary>
        public static readonly string[] Reserved = { "Escape", "LeftWindows", "RightWindows", "PrintScreen" };

        public static bool IsReserved(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            foreach (var r in Reserved)
                if (string.Equals(r, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>İki bağlam aynı anda aktif olabilir mi (çakışma sayılır mı)?</summary>
        public static bool ContextsOverlap(string a, string b)
        {
            return a == "ortak" || b == "ortak" || string.Equals(a, b, StringComparison.Ordinal);
        }

        public static List<BindingConflict> FindConflicts(IReadOnlyList<BindingEntry> entries)
        {
            var result = new List<BindingConflict>();
            if (entries == null)
                return result;
            for (var i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrEmpty(entries[i].Key))
                    continue;
                for (var j = i + 1; j < entries.Count; j++)
                {
                    if (!string.Equals(entries[i].Key, entries[j].Key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!ContextsOverlap(entries[i].Context, entries[j].Context))
                        continue;
                    result.Add(new BindingConflict(entries[i].Key, entries[i].Action, entries[j].Action));
                }
            }
            return result;
        }

        /// <summary>Yeni atama öncesi: çakışan eylem adı (yoksa null). Ayrılmış tuş için "(ayrılmış)" döner.</summary>
        public static string FindOwner(IReadOnlyList<BindingEntry> entries, string action, string context, string key)
        {
            if (IsReserved(key))
                return "(ayrılmış)";
            if (entries == null || string.IsNullOrEmpty(key))
                return null;
            var ctx = string.IsNullOrEmpty(context) ? "ortak" : context;
            foreach (var e in entries)
            {
                if (e.Action == action)
                    continue;
                if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase) && ContextsOverlap(e.Context, ctx))
                    return e.Action;
            }
            return null;
        }

        /// <summary>Tuş etiketi kısaltması (menüde gösterim): "leftShift" → "SOL SHIFT" gibi.</summary>
        public static string Pretty(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "—";
            var sb = new System.Text.StringBuilder(key.Length + 4);
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(key[i - 1]))
                    sb.Append(' ');
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }
}
