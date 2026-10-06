using Project.Application.Services;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Mermi delme/sekme yardımcıları (Unity tarafı). Kurallar <see cref="PenetrationRules"/>'ta; burada yalnızca
    /// yüzey sınıfı eşleme, arka yüz ışınıyla kalınlık ölçümü ve sekme yönü bulunur. Maliyet: mermi/engel başına
    /// en fazla tek bir <c>Collider.Raycast</c>.
    /// </summary>
    public static class Penetration
    {
        private const float ExitEpsilon = 0.02f;

        /// <summary>Çıkış verisi (VFX/ses tüketicileri için): nokta, yön, enerji, malzeme.</summary>
        public struct ExitInfo
        {
            public Vector3 Point;
            public Vector3 Direction;
            public float EnergyJoules;
            public float Speed;
            public PenetrationMaterial Material;
            public float SpreadDeg;
        }

        /// <summary>Delme çıkışı olduğunda tetiklenir; BallisticsSystem kancası <see cref="Report"/> çağırır.</summary>
        public static event System.Action<ExitInfo> Exited;

        /// <summary>Son çıkış (olay kaçıranlar için).</summary>
        public static ExitInfo LastExit { get; private set; }

        /// <summary>
        /// v2 enerji modeli ile engeli değerlendirir (tek statik giriş). Delindiyse çıkış verisini yayınlar.
        /// </summary>
        public static BarrierExit EvaluateBarrier(PenetrationMaterial material, float entryEnergy, float speed,
            float thickness, Vector3 direction, Vector3 exitPoint)
        {
            var res = BarrierModel.Evaluate(material, entryEnergy, speed, thickness,
                direction.x, direction.y, direction.z, Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            if (res.Penetrated)
            {
                var info = new ExitInfo
                {
                    Point = exitPoint,
                    Direction = new Vector3(res.DirX, res.DirY, res.DirZ),
                    EnergyJoules = res.ExitEnergyJoules,
                    Speed = res.ExitSpeed,
                    Material = material,
                    SpreadDeg = res.SpreadDeg
                };
                Report(info);
            }
            return res;
        }

        public static void Report(ExitInfo info)
        {
            LastExit = info;
            Exited?.Invoke(info);
        }

        public static PenetrationMaterial ToMaterial(Collider collider, SurfaceKind surface)
        {
            if (collider is TerrainCollider)
                return PenetrationMaterial.Solid;

            switch (surface)
            {
                case SurfaceKind.Wood: return PenetrationMaterial.Wood;
                case SurfaceKind.Foliage: return PenetrationMaterial.Foliage;
                case SurfaceKind.Metal: return PenetrationMaterial.ThinMetal;
                case SurfaceKind.Concrete:
                    // SurfaceKind tuğlayı da beton sayar: malzeme/nesne adından tuğla ayrılır (v2 tablo: tüfek durur, ağır kalibre delebilir).
                    return SurfaceClassifier.HintOf(collider) == SurfaceClassifier.PenetrationHint.Brick
                        ? PenetrationMaterial.Brick
                        : PenetrationMaterial.Concrete;
                case SurfaceKind.Dirt:
                    // Kum torbası / hesco SurfaceKind'de toprak: delme modelinde tüm kalibreleri emen Sandbag.
                    return SurfaceClassifier.HintOf(collider) == SurfaceClassifier.PenetrationHint.Sandbag
                        ? PenetrationMaterial.Sandbag
                        : PenetrationMaterial.Solid;
                case SurfaceKind.Default: return PenetrationMaterial.Plaster;
                default: return PenetrationMaterial.Solid; // su, et, kar
            }
        }

        /// <summary>Çıkış efekti/sesi için delinen malzemenin yüzey türü (ToMaterial'ın tersi, yaklaşık).</summary>
        public static SurfaceKind SurfaceFor(PenetrationMaterial material)
        {
            switch (material)
            {
                case PenetrationMaterial.Wood: return SurfaceKind.Wood;
                case PenetrationMaterial.Foliage: return SurfaceKind.Foliage;
                case PenetrationMaterial.ThinMetal: return SurfaceKind.Metal;
                case PenetrationMaterial.Concrete:
                case PenetrationMaterial.Brick: return SurfaceKind.Concrete;
                case PenetrationMaterial.Sandbag: return SurfaceKind.Dirt;
                default: return SurfaceKind.Default; // sıva / bilinmeyen: genel toz
            }
        }

        /// <summary>
        /// Arka yüz ışını: engelin en fazla izin verilen kalınlığın ötesinden geri doğru bakıp çıkış yüzünü bulur.
        /// </summary>
        /// <returns>Delinebiliyorsa true; <paramref name="exitPoint"/> çıkış noktası (hafif ilerisinde).</returns>
        public static bool TryMeasure(Collider collider, Vector3 entry, Vector3 direction, float maxThickness,
            out float thickness, out Vector3 exitPoint)
        {
            thickness = 0f;
            exitPoint = entry;
            if (collider == null || maxThickness <= 0f)
                return false;

            var probeStart = entry + direction * (maxThickness + 0.01f);
            var ray = new Ray(probeStart, -direction);
            if (!collider.Raycast(ray, out var back, maxThickness + 0.01f))
                return false;

            thickness = Mathf.Max(0f, maxThickness + 0.01f - back.distance);
            if (thickness > maxThickness)
                return false;

            exitPoint = back.point + direction * ExitEpsilon;
            return true;
        }

        /// <summary>Sıyırma açısı (derece): yol ile yüzey düzlemi arasındaki açı.</summary>
        public static float GrazingAngle(Vector3 direction, Vector3 normal)
        {
            var d = Mathf.Abs(Vector3.Dot(direction, normal));
            return Mathf.Asin(Mathf.Clamp01(d)) * Mathf.Rad2Deg;
        }

        /// <summary>Yüzey normaline göre yansıma; küçük rastgele saçılma eklenir.</summary>
        public static Vector3 Reflect(Vector3 direction, Vector3 normal)
        {
            var r = Vector3.Reflect(direction, normal);
            r += Random.insideUnitSphere * 0.06f;
            // Yüzeyin içine geri gömülmesin.
            if (Vector3.Dot(r, normal) < 0.02f)
                r += normal * (0.02f - Vector3.Dot(r, normal));
            return r.normalized;
        }
    }
}
