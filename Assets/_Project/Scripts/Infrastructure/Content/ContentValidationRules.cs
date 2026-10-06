using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Content
{
    public enum ContentSeverity { Ok = 0, Uyari = 1, Hata = 2 }

    public enum ContentNameKind { None, Weapon, Vehicle, Rock, Prop, Vegetation, Building }

    /// <summary>İsim kuralı: HK_W_ar_mpt76 (silah), HK_V_t70 (araç), HK_R_large, HK_P_crate, HK_VEG_pine, HK_B_village.</summary>
    public static class ContentNameConvention
    {
        public static bool TryParse(string assetName, out ContentNameKind kind, out string id)
        {
            kind = ContentNameKind.None;
            id = null;
            if (string.IsNullOrEmpty(assetName))
                return false;

            // Uzantı ve yol at.
            var n = assetName.Replace('\\', '/');
            var slash = n.LastIndexOf('/');
            if (slash >= 0) n = n.Substring(slash + 1);
            var dot = n.LastIndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);

            // En uzun önek önce (VEG, V'den önce).
            if (Match(n, "HK_VEG_", ContentNameKind.Vegetation, ref kind, ref id)) return true;
            if (Match(n, "HK_W_", ContentNameKind.Weapon, ref kind, ref id)) return true;
            if (Match(n, "HK_V_", ContentNameKind.Vehicle, ref kind, ref id)) return true;
            if (Match(n, "HK_R_", ContentNameKind.Rock, ref kind, ref id)) return true;
            if (Match(n, "HK_P_", ContentNameKind.Prop, ref kind, ref id)) return true;
            if (Match(n, "HK_B_", ContentNameKind.Building, ref kind, ref id)) return true;
            return false;
        }

        private static bool Match(string n, string prefix, ContentNameKind k, ref ContentNameKind kind, ref string id)
        {
            if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || n.Length == prefix.Length)
                return false;
            kind = k;
            id = n.Substring(prefix.Length).ToLowerInvariant();
            // Varyant son eki (HK_VEG_pine_01) → ayrı tutulur; kimlik ilk parça olmayabilir (ar_mpt76), bu yüzden dokunma.
            return true;
        }

        /// <summary>Bitki/kaya/bina varyant sonekini (_01, _02) atar.</summary>
        public static string StripVariantSuffix(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var us = id.LastIndexOf('_');
            if (us <= 0 || us == id.Length - 1) return id;
            for (var i = us + 1; i < id.Length; i++)
                if (!char.IsDigit(id[i])) return id;
            return id.Substring(0, us);
        }
    }

    /// <summary>Doğrulayıcı kuralları (saf mantık; Editor menüsü ve testler kullanır).</summary>
    public static class ContentValidationRules
    {
        public static readonly string[] RequiredWeaponSockets =
        {
            ContentIds.WeaponMuzzle, ContentIds.WeaponGripR, ContentIds.WeaponGripL,
            ContentIds.WeaponMagazine, ContentIds.WeaponBolt, ContentIds.WeaponSight,
        };

        /// <summary>Muzzle/Grip_R/Grip_L zorunlu (HATA); Magazine/Bolt/Sight yoksa UYARI.</summary>
        public static ContentSeverity CheckSocket(string socket, bool present)
        {
            if (present) return ContentSeverity.Ok;
            return socket == ContentIds.WeaponMuzzle || socket == ContentIds.WeaponGripR || socket == ContentIds.WeaponGripL
                ? ContentSeverity.Hata
                : ContentSeverity.Uyari;
        }

        /// <summary>1 birim = 1 m. Boyut (en büyük kenar) beklenen aralıkta mı.</summary>
        public static ContentSeverity CheckScale(float largestExtentMeters, float minM, float maxM)
        {
            if (largestExtentMeters <= 0f || float.IsNaN(largestExtentMeters)) return ContentSeverity.Hata;
            if (largestExtentMeters < minM * 0.5f || largestExtentMeters > maxM * 2f) return ContentSeverity.Hata;
            if (largestExtentMeters < minM || largestExtentMeters > maxM) return ContentSeverity.Uyari;
            return ContentSeverity.Ok;
        }

        /// <summary>Pivot: modelin alt/merkez noktasından ne kadar uzak (metre). 0.25 m'ye kadar OK.</summary>
        public static ContentSeverity CheckPivot(float offsetMeters, float largestExtentMeters)
        {
            var tol = Math.Max(0.25f, largestExtentMeters * 0.1f);
            if (offsetMeters <= tol) return ContentSeverity.Ok;
            return offsetMeters <= tol * 4f ? ContentSeverity.Uyari : ContentSeverity.Hata;
        }

        public static ContentSeverity CheckTriangles(int tris, int budget)
        {
            if (budget <= 0 || tris <= budget) return ContentSeverity.Ok;
            return tris <= budget * 2 ? ContentSeverity.Uyari : ContentSeverity.Hata;
        }

        /// <summary>Slot başına LOD0 üçgen bütçesi (GERCEKCILIK §1.3 ile uyumlu, yaklaşık).</summary>
        public static int TriangleBudget(ContentNameKind kind)
        {
            switch (kind)
            {
                case ContentNameKind.Weapon: return 40000;
                case ContentNameKind.Vehicle: return 120000;
                case ContentNameKind.Vegetation: return 15000;
                case ContentNameKind.Rock: return 8000;
                case ContentNameKind.Prop: return 5000;
                case ContentNameKind.Building: return 40000;
                default: return 30000;
            }
        }

        public static bool RequiresLodGroup(ContentNameKind kind) =>
            kind == ContentNameKind.Vegetation || kind == ContentNameKind.Vehicle || kind == ContentNameKind.Building;

        public static ContentSeverity CheckLodGroup(ContentNameKind kind, bool hasLodGroup)
        {
            if (hasLodGroup || !RequiresLodGroup(kind)) return ContentSeverity.Ok;
            return kind == ContentNameKind.Vegetation ? ContentSeverity.Hata : ContentSeverity.Uyari;
        }

        public static bool IsUrpLitShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            return shaderName == "Universal Render Pipeline/Lit"
                || shaderName == "Universal Render Pipeline/Simple Lit"
                || shaderName.StartsWith("Universal Render Pipeline/Nature/", StringComparison.Ordinal)
                || shaderName.StartsWith("Shader Graphs/", StringComparison.Ordinal);
        }

        public static ContentSeverity CheckShader(string shaderName) =>
            IsUrpLitShader(shaderName) ? ContentSeverity.Ok : ContentSeverity.Uyari;

        /// <summary>Lisans kaydı: varlık adı veya klasör yolu README'de geçiyor mu.</summary>
        public static bool HasLicenseRecord(string readmeText, string assetName, string assetPath)
        {
            if (string.IsNullOrEmpty(readmeText)) return false;
            if (!string.IsNullOrEmpty(assetName) &&
                readmeText.IndexOf(assetName, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (string.IsNullOrEmpty(assetPath)) return false;
            // Üst klasör adları (ThirdParty/Weapons/Foo/x.fbx → Foo).
            var parts = assetPath.Replace('\\', '/').Split('/');
            for (var i = parts.Length - 2; i >= 0; i--)
            {
                var p = parts[i];
                if (p.Length < 4 || p == "ThirdParty" || p == "Assets" || p == "Weapons" || p == "Vehicles" ||
                    p == "Buildings" || p == "Environment" || p == "Materials" || p == "Audio" || p == "Characters")
                    continue;
                if (readmeText.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public static ContentSeverity Worst(IList<ContentSeverity> list)
        {
            var w = ContentSeverity.Ok;
            if (list == null) return w;
            for (var i = 0; i < list.Count; i++)
                if (list[i] > w) w = list[i];
            return w;
        }

        public static string Label(ContentSeverity s) =>
            s == ContentSeverity.Ok ? "OK" : s == ContentSeverity.Uyari ? "UYARI" : "HATA";
    }
}
