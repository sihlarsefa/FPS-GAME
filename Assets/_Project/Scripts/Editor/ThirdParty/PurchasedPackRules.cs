using System;
using System.Collections.Generic;
using System.Text;

namespace Project.EditorTools
{
    public enum PackWeaponClass { None, AssaultRifle, Sniper, Shotgun, Pistol, Smg, Lmg }

    /// <summary>
    /// Satın alınan paket otomatik karşılama sezgilerinin SAF kısmı (UnityEngine çağrısı yok → EditMode testi koşar).
    /// İsim ipucu puanlama, sınır (bounds) denetimi, silah sınıfı → weaponId eşlemesi, aday puanları.
    /// </summary>
    public static class PurchasedPackRules
    {
        public const float WeaponMinLength = 0.2f;
        public const float WeaponMaxLength = 1.4f;
        public const int HeroTriangleThreshold = 5000;

        private static readonly string[] CharacterHints = { "soldier", "military", "swat", "operator", "character", "army", "ranger", "commando", "marine", "trooper" };
        private static readonly string[] CharacterBad = { "zombie", "civilian", "child", "kid", "animal", "dog", "enemy_dummy", "dummy", "mannequin", "skeleton" };
        private static readonly string[] WeaponBad = { "magazine", "mag", "scope", "silencer", "suppressor", "holster", "case", "box", "ammo", "bullet", "casing", "shell", "knife", "grenade", "attachment", "stand", "rack", "icon", "arms", "hands", "crate", "sling", "bag" };

        private static readonly string[] ArIds = { "ar_mpt76", "ar_mpt55", "ar_sar223", "ar_g3a7", "ar_mpt76k", "ar_mk1" };
        private static readonly string[] SniperIds = { "sr_jng90", "dmr_knt76", "dmr_sar762mt" };
        private static readonly string[] ShotgunIds = { "sg_escort", "sg_escort_magnum" };
        private static readonly string[] PistolIds = { "pistol_sar9", "pistol_tp9", "pistol_mete", "pistol_mk1", "pistol_kills" };
        private static readonly string[] SmgIds = { "smg_sar109t" };
        private static readonly string[] LmgIds = { "lmg_mg3", "lmg_pmt76" };

        /// <summary>"AK47_Rifle-low" → ak, 47, rifle, low (camelCase, rakam ve ayraçlara göre).</summary>
        public static List<string> Tokenize(string name)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(name)) return tokens;
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c)) { Flush(sb, tokens); continue; }
                if (sb.Length > 0)
                {
                    char p = sb[sb.Length - 1];
                    bool split = char.IsDigit(p) != char.IsDigit(c)
                        || (char.IsLower(p) && char.IsUpper(c))
                        || (char.IsUpper(p) && char.IsUpper(c) && i + 1 < name.Length && char.IsLower(name[i + 1]));
                    if (split) Flush(sb, tokens);
                }
                sb.Append(c);
            }
            Flush(sb, tokens);
            return tokens;
        }

        private static void Flush(StringBuilder sb, List<string> tokens)
        {
            if (sb.Length > 0) tokens.Add(sb.ToString().ToLowerInvariant());
            sb.Length = 0;
        }

        private static bool Has(List<string> tokens, string lower, params string[] exact)
        {
            foreach (var e in exact)
            {
                if (tokens.Contains(e)) return true;
                for (int i = 0; i + 1 < tokens.Count; i++) if (tokens[i] + tokens[i + 1] == e) return true; // m + 4 → m4
            }
            return false;
        }

        public static bool IsExcludedPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return true;
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/", StringComparison.Ordinal)) return true;
            if (p.StartsWith("Assets/_Project/", StringComparison.Ordinal)) return true;
            if (p.StartsWith("Assets/ThirdParty/", StringComparison.Ordinal)) return true;
            if (p.IndexOf("/Editor/", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        public static PackWeaponClass ClassifyWeapon(string name)
        {
            var t = Tokenize(name);
            if (t.Count == 0) return PackWeaponClass.None;
            foreach (var bad in WeaponBad) if (t.Contains(bad)) return PackWeaponClass.None;
            string lower = name.ToLowerInvariant();
            if (Has(t, lower, "sniper", "awp", "svd", "dmr", "barrett", "l96", "m24", "m82", "dragunov") || lower.Contains("sniper")) return PackWeaponClass.Sniper;
            if (Has(t, lower, "shotgun", "benelli", "saiga", "sg", "m1014", "spas") || lower.Contains("shotgun")) return PackWeaponClass.Shotgun;
            if (Has(t, lower, "pistol", "handgun", "glock", "m9", "usp", "revolver", "deagle", "beretta") || lower.Contains("pistol")) return PackWeaponClass.Pistol;
            if (Has(t, lower, "lmg", "machinegun", "mg", "mg42", "mg3", "m249", "pkm", "saw", "minigun") || lower.Contains("machinegun")) return PackWeaponClass.Lmg;
            if (Has(t, lower, "smg", "mp5", "mp7", "mp9", "uzi", "p90", "vector", "submachine") || lower.Contains("submachine")) return PackWeaponClass.Smg;
            if (Has(t, lower, "ar", "ak", "m4", "m16", "rifle", "scar", "g36", "hk416", "carbine", "g3", "aug", "fal", "gun", "weapon")
                || lower.Contains("rifle") || lower.Contains("ak47") || lower.Contains("m4a1")) return PackWeaponClass.AssaultRifle;
            return PackWeaponClass.None;
        }

        /// <summary>İsim ipucu gücü 0 (yok) … 30 (sınıf belirgin).</summary>
        public static int WeaponNameScore(string name)
        {
            var c = ClassifyWeapon(name);
            if (c == PackWeaponClass.None) return 0;
            var t = Tokenize(name);
            // "gun"/"weapon" gibi genel ipucu zayıf; açık sınıf adı güçlü.
            bool generic = c == PackWeaponClass.AssaultRifle && !Has(t, "", "ar", "ak", "m4", "m16", "rifle", "scar", "g36", "hk416", "carbine", "g3", "aug", "fal");
            return generic ? 10 : 30;
        }

        /// <summary>Uzun + ince: en uzun kenar 0.2–1.4 m ve diğer kenarlara göre belirgin uzun.</summary>
        public static bool IsWeaponBounds(float sx, float sy, float sz)
        {
            var a = new[] { Math.Abs(sx), Math.Abs(sy), Math.Abs(sz) };
            Array.Sort(a);
            float longest = a[2], mid = a[1], shortest = a[0];
            if (float.IsNaN(longest) || longest < WeaponMinLength || longest > WeaponMaxLength) return false;
            if (shortest <= 0f) shortest = 0.001f;
            if (mid <= 0f) mid = 0.001f;
            return longest / mid >= 1.2f && longest / shortest >= 2.0f;
        }

        public static int ScoreWeapon(string name, float sx, float sy, float sz, int triangles)
        {
            if (WeaponNameScore(name) == 0 || !IsWeaponBounds(sx, sy, sz)) return 0;
            int score = WeaponNameScore(name) + 20;
            if (triangles >= 300 && triangles <= 60000) score += 10;
            if (triangles > 2000 && triangles <= 30000) score += 5;
            return score;
        }

        public static int ScoreCharacter(string name, bool isHumanoid, int triangles, bool hasLod, float height)
        {
            int score = 0;
            var t = Tokenize(name);
            if (isHumanoid) score += 40;
            if (triangles > HeroTriangleThreshold) score += 20;
            if (triangles > 20000) score += 10;
            if (hasLod) score += 10;
            foreach (var h in CharacterHints) if (t.Contains(h)) { score += 25; break; }
            foreach (var b in CharacterBad) if (t.Contains(b)) { score -= 40; break; }
            if (height >= 1.5f && height <= 2.1f) score += 10;
            return score;
        }

        /// <summary>Sınıf için weaponId listesi (öncelik sırasıyla).</summary>
        public static string[] WeaponIdsFor(PackWeaponClass c)
        {
            switch (c)
            {
                case PackWeaponClass.AssaultRifle: return ArIds;
                case PackWeaponClass.Sniper: return SniperIds;
                case PackWeaponClass.Shotgun: return ShotgunIds;
                case PackWeaponClass.Pistol: return PistolIds;
                case PackWeaponClass.Smg: return SmgIds;
                case PackWeaponClass.Lmg: return LmgIds;
                default: return Array.Empty<string>();
            }
        }

        /// <summary>Hedef uzunluk (m) — ThirdPartyWeaponBinder ile aynı ölçekler.</summary>
        public static float TargetLength(PackWeaponClass c)
        {
            switch (c)
            {
                case PackWeaponClass.Sniper: return 1.15f;
                case PackWeaponClass.Shotgun: return 1.0f;
                case PackWeaponClass.Pistol: return 0.21f;
                case PackWeaponClass.Smg: return 0.6f;
                case PackWeaponClass.Lmg: return 1.05f;
                default: return 0.95f;
            }
        }

        /// <summary>İlk weaponId'den başlayıp modelleri sırayla döngüyle eşler (en iyi model = ilk id).</summary>
        public static List<KeyValuePair<string, int>> AssignIds(PackWeaponClass c, int modelCount)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (modelCount <= 0) return result;
            var ids = WeaponIdsFor(c);
            for (int i = 0; i < ids.Length; i++) result.Add(new KeyValuePair<string, int>(ids[i], i % modelCount));
            return result;
        }

        /// <summary>Öncelik: satın alınan paket > diğer. Mevcut satın alınmışsa yalnızca yeni puan YÜKSEKSE değişir.</summary>
        public static bool ShouldReplace(bool existingIsPurchased, int existingScore, int newScore, bool existingMissing)
        {
            if (existingMissing) return true;
            if (!existingIsPurchased) return true;
            return newScore > existingScore;
        }

        /// <summary>Etiket "pk_score_NN" → NN (yoksa -1).</summary>
        public static int ParseScoreLabel(string label)
        {
            const string p = "pk_score_";
            if (label == null || !label.StartsWith(p, StringComparison.Ordinal)) return -1;
            return int.TryParse(label.Substring(p.Length), out var n) ? n : -1;
        }
    }
}
