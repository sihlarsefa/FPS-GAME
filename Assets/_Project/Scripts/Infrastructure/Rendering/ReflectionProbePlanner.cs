using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    public enum ProbeSiteKind { Exterior = 0, Interior = 1 }

    /// <summary>S3-yansima: Bir yansıma probu yerleşim adayı (saf veri).</summary>
    public struct ProbeSite
    {
        public ProbeSiteKind Kind;
        public string Name;
        public Vector3 Center;
        public Vector3 Size;
        public int LocationIndex;
        public float Priority;
    }

    /// <summary>Bir kalitenin yansıma probu bütçesi.</summary>
    public readonly struct ProbeBudget
    {
        public readonly int MaxTotal;
        public readonly int MaxActive;
        public readonly int Resolution;
        public readonly int InteriorsPerLocation;
        public readonly float ActiveRadius;
        public readonly float RenderIntervalSeconds;
        /// <summary>true: yalnız ilk kez bir kere çizilir ("pişmiş" gibi); false: zaman dilimi değişince tekrar çizilir.</summary>
        public readonly bool BakeOnce;
        public readonly bool TimeSliced;

        public ProbeBudget(int total, int active, int res, int interiors, float radius, float interval, bool bakeOnce, bool sliced)
        {
            MaxTotal = total; MaxActive = active; Resolution = res; InteriorsPerLocation = interiors; ActiveRadius = radius;
            RenderIntervalSeconds = interval; BakeOnce = bakeOnce; TimeSliced = sliced;
        }
    }

    /// <summary>
    /// S3-yansima: Yansıma probu planlayıcı (saf, testli). Konum başına bir dış prob + bina içi (kutu izdüşümlü) probları,
    /// kademe bütçesine göre seçer; kameraya göre etkin kümeyi belirler.
    /// </summary>
    public static class ReflectionProbePlanner
    {
        private static readonly ProbeBudget[] Budgets =
        {
            //                 toplam aktif çöz. iç/lok yarıçap aralık(sn) bir kez dilim
            new ProbeBudget(6,  3, 32,  1, 150f, 4f, true,  true),
            new ProbeBudget(12, 5, 64,  2, 220f, 3f, true,  true),
            new ProbeBudget(24, 8, 128, 3, 300f, 2f, false, true),
            new ProbeBudget(40, 12, 256, 4, 400f, 1.5f, false, true)
        };

        /// <summary>İç prob için en küçük bina kenarı (m); daha küçük yapılar probsuz kalır.</summary>
        public const float MinInteriorSize = 4f;
        public const float ExteriorHeight = 50f;
        public const float InteriorMargin = 1.0f;

        public static ProbeBudget GetBudget(int tier) => Budgets[Mathf.Clamp(tier, 0, Budgets.Length - 1)];

        /// <summary>Konum dış probu: yarıçapa göre geniş kutu, yerden biraz yukarıda.</summary>
        public static ProbeSite ExteriorSite(string name, int locationIndex, Vector3 groundCenter, float radius)
        {
            var r = Mathf.Max(20f, radius);
            return new ProbeSite
            {
                Kind = ProbeSiteKind.Exterior, Name = name, LocationIndex = locationIndex,
                Center = groundCenter + Vector3.up * (ExteriorHeight * 0.4f),
                Size = new Vector3(r * 2f, ExteriorHeight, r * 2f),
                Priority = 1000f + r
            };
        }

        /// <summary>Bina içi probu: sınırlardan hafif büyük kutu. Çok küçükse false.</summary>
        public static bool TryInteriorSite(string name, int locationIndex, Bounds b, out ProbeSite site)
        {
            site = default;
            if (b.size.x < MinInteriorSize || b.size.z < MinInteriorSize || b.size.y < 2f)
                return false;
            var size = b.size + new Vector3(InteriorMargin * 2f, InteriorMargin, InteriorMargin * 2f);
            site = new ProbeSite
            {
                Kind = ProbeSiteKind.Interior, Name = name, LocationIndex = locationIndex,
                Center = b.center, Size = size, Priority = b.size.x * b.size.z
            };
            return true;
        }

        /// <summary>Bina sınırının en yakın konumunu bulur (yarıçap*1.2 içinde); yoksa -1.</summary>
        public static int NearestLocation(Vector2 p, IList<Vector2> centers, IList<float> radii)
        {
            var best = -1;
            var bestD = float.MaxValue;
            for (var i = 0; i < centers.Count; i++)
            {
                var d = (centers[i] - p).magnitude;
                var r = (radii != null && i < radii.Count ? radii[i] : 60f) * 1.2f;
                if (d <= r && d < bestD) { bestD = d; best = i; }
            }

            return best;
        }

        /// <summary>
        /// Adayları kademe bütçesine göre süzer: önce tüm dış problar (referansa yakın olanlar önde), sonra konum başına
        /// en fazla InteriorsPerLocation büyük iç prob; toplam MaxTotal. Düzen deterministik.
        /// </summary>
        public static List<ProbeSite> Select(IList<ProbeSite> sites, int tier, Vector3 reference)
        {
            var budget = GetBudget(tier);
            var result = new List<ProbeSite>();
            if (sites == null) return result;

            var ext = new List<ProbeSite>();
            var inter = new List<ProbeSite>();
            for (var i = 0; i < sites.Count; i++)
                (sites[i].Kind == ProbeSiteKind.Exterior ? ext : inter).Add(sites[i]);

            ext.Sort((a, b) => (a.Center - reference).sqrMagnitude.CompareTo((b.Center - reference).sqrMagnitude));
            inter.Sort((a, b) =>
            {
                var c = b.Priority.CompareTo(a.Priority);
                return c != 0 ? c : (a.Center - reference).sqrMagnitude.CompareTo((b.Center - reference).sqrMagnitude);
            });

            for (var i = 0; i < ext.Count && result.Count < budget.MaxTotal; i++)
                result.Add(ext[i]);

            var perLoc = new Dictionary<int, int>();
            for (var i = 0; i < inter.Count && result.Count < budget.MaxTotal; i++)
            {
                perLoc.TryGetValue(inter[i].LocationIndex, out var n);
                if (n >= budget.InteriorsPerLocation) continue;
                perLoc[inter[i].LocationIndex] = n + 1;
                result.Add(inter[i]);
            }

            return result;
        }

        /// <summary>Kamera çevresinde etkin olacak prob indeksleri (yakından uzağa, en çok MaxActive, ActiveRadius içi).</summary>
        public static List<int> ActiveSet(IList<Vector3> centers, IList<float> halfExtents, Vector3 camPos, ProbeBudget budget)
        {
            var scored = new List<KeyValuePair<float, int>>();
            for (var i = 0; i < centers.Count; i++)
            {
                var ext = halfExtents != null && i < halfExtents.Count ? halfExtents[i] : 0f;
                var d = (centers[i] - camPos).magnitude - ext;
                if (d <= budget.ActiveRadius) scored.Add(new KeyValuePair<float, int>(d, i));
            }

            scored.Sort((a, b) => a.Key.CompareTo(b.Key));
            var res = new List<int>(Math.Min(scored.Count, budget.MaxActive));
            for (var i = 0; i < scored.Count && res.Count < budget.MaxActive; i++)
                res.Add(scored[i].Value);
            return res;
        }

        /// <summary>Prob yeniden çizilmeli mi? Hiç çizilmediyse evet; BakeOnce'da asla; aksi hâlde zaman dilimi değiştiyse.</summary>
        public static bool NeedsRender(bool everRendered, bool timeOfDayChanged, ProbeBudget budget)
        {
            if (!everRendered) return true;
            return !budget.BakeOnce && timeOfDayChanged;
        }
    }
}
