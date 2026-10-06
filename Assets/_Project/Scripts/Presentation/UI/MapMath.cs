using System;
using System.Globalization;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Harita çerçevesi: haritanın dünya merkezi (XZ) ve yarı boyu. Dünya ↔ harita UV (0..1; (0,0) güneybatı köşe,
    /// v kuzeye artar) dönüşümlerini yapar. <see cref="WorldMetadata"/> yoksa Kuzgun Vadisi varsayılanı (merkez 0,0, 512 m).
    /// </summary>
    public readonly struct MapFrame : IEquatable<MapFrame>
    {
        /// <summary>Haritanın dünya merkezi (x, z).</summary>
        public Vector2 Center { get; }

        /// <summary>Haritanın yarı boyu (m). Harita x ∈ [Center.x - HalfSize, Center.x + HalfSize].</summary>
        public float HalfSize { get; }

        public MapFrame(Vector2 center, float halfSize)
        {
            Center = IsFinite(center.x) && IsFinite(center.y) ? center : Vector2.zero;
            HalfSize = IsFinite(halfSize) && halfSize > 1f ? halfSize : MapMath.DefaultHalfSize;
        }

        /// <summary>Harita kenar uzunluğu (m).</summary>
        public float Size => HalfSize * 2f;

        public float MinX => Center.x - HalfSize;
        public float MaxX => Center.x + HalfSize;
        public float MinZ => Center.y - HalfSize;
        public float MaxZ => Center.y + HalfSize;

        /// <summary>Kuzgun Vadisi varsayılanı: merkez (0,0), yarı boy 512 m.</summary>
        public static MapFrame Default => new MapFrame(Vector2.zero, MapMath.DefaultHalfSize);

        /// <summary>Dünya verisinden çerçeve (null ise <see cref="Default"/>).</summary>
        public static MapFrame FromWorld(WorldMetadata world)
        {
            return world != null ? new MapFrame(world.MapCenter, world.MapHalfSize) : Default;
        }

        /// <summary>Sahnedeki <see cref="WorldMetadata.Instance"/> çerçevesi (yoksa varsayılan).</summary>
        public static MapFrame Current => FromWorld(WorldMetadata.Instance);

        /// <summary>Dünya konumu → harita UV'si (sınırlandırılmaz).</summary>
        public Vector2 WorldToUV(Vector3 world) => WorldToUV(world.x, world.z);

        /// <summary>Dünya XZ → harita UV'si (sınırlandırılmaz).</summary>
        public Vector2 WorldToUV(float x, float z)
        {
            var size = Size;
            return new Vector2((x - MinX) / size, (z - MinZ) / size);
        }

        /// <summary>Harita UV'si → dünya konumu (Y = 0).</summary>
        public Vector3 UVToWorld(Vector2 uv)
        {
            var size = Size;
            return new Vector3(MinX + uv.x * size, 0f, MinZ + uv.y * size);
        }

        /// <summary>Nokta harita içinde mi (kenardan <paramref name="margin"/> m içeride)?</summary>
        public bool Contains(Vector3 world, float margin = 0f)
        {
            return world.x >= MinX + margin && world.x <= MaxX - margin && world.z >= MinZ + margin && world.z <= MaxZ - margin;
        }

        public bool Equals(MapFrame other) => Center == other.Center && Mathf.Approximately(HalfSize, other.HalfSize);
        public override bool Equals(object obj) => obj is MapFrame other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Center, HalfSize);
        public static bool operator ==(MapFrame a, MapFrame b) => a.Equals(b);
        public static bool operator !=(MapFrame a, MapFrame b) => !a.Equals(b);

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    /// <summary>
    /// Harita matematiği: pafta (grid) koordinatları (A-J sütun batı→doğu, 1-10 satır kuzey→güney; Kuzgun Vadisi'nde
    /// ~100 m), yön adları (K/KD/D/GD/G/GB/B/KB), kuzey-yukarı arayüz açıları, mesafe/süre biçimleri ve işaretçileri
    /// kenara sabitleme. Paylaşılan etiket dizgileri önbelleklidir (her karede tahsis yok).
    /// </summary>
    public static class MapMath
    {
        /// <summary>Varsayılan harita yarı boyu (Kuzgun Vadisi 1024 m × 1024 m).</summary>
        public const float DefaultHalfSize = 512f;

        /// <summary>Pafta bölme sayısı (10 × 10: A-J / 1-10).</summary>
        public const int GridDivisions = 10;

        private static readonly string[] ColumnLabels = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
        private static readonly string[] RowLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        private static readonly string[] CellLabels = BuildCellLabels();
        private static readonly string[] BearingNames = { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };
        private static readonly int[] ScaleSteps = { 10, 25, 50, 100, 200, 250, 500, 1000 };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ Pafta

        /// <summary>Sütun etiketi (0 → "A" … 9 → "J"); aralık dışı sınırlandırılır.</summary>
        public static string ColumnLabel(int column) => ColumnLabels[Mathf.Clamp(column, 0, GridDivisions - 1)];

        /// <summary>Satır etiketi (0 → "1" … 9 → "10"); aralık dışı sınırlandırılır.</summary>
        public static string RowLabel(int row) => RowLabels[Mathf.Clamp(row, 0, GridDivisions - 1)];

        /// <summary>Pafta hücresi etiketi ("C7"); önbellekli dizgi döner.</summary>
        public static string GridLabel(int column, int row)
        {
            column = Mathf.Clamp(column, 0, GridDivisions - 1);
            row = Mathf.Clamp(row, 0, GridDivisions - 1);
            return CellLabels[row * GridDivisions + column];
        }

        /// <summary>Bir pafta hücresinin kenar uzunluğu (m).</summary>
        public static float GridCellSize(MapFrame frame) => frame.Size / GridDivisions;

        /// <summary>X'in pafta sütunu (0 = batı kenarı "A").</summary>
        public static int GridColumn(MapFrame frame, float x)
        {
            var t = (x - frame.MinX) / frame.Size;
            return Mathf.Clamp(Mathf.FloorToInt(t * GridDivisions), 0, GridDivisions - 1);
        }

        /// <summary>Z'nin pafta satırı (0 = kuzey kenarı "1").</summary>
        public static int GridRow(MapFrame frame, float z)
        {
            var t = (frame.MaxZ - z) / frame.Size;
            return Mathf.Clamp(Mathf.FloorToInt(t * GridDivisions), 0, GridDivisions - 1);
        }

        /// <summary>Dünya konumunun pafta etiketi ("C7"; önbellekli dizgi, tahsis yok).</summary>
        public static string GridLabel(MapFrame frame, Vector3 world)
        {
            return GridLabel(GridColumn(frame, world.x), GridRow(frame, world.z));
        }

        /// <summary>Pafta hücresinin merkezinin harita UV'si.</summary>
        public static Vector2 GridCellCenterUV(int column, int row)
        {
            var step = 1f / GridDivisions;
            return new Vector2((column + 0.5f) * step, 1f - (row + 0.5f) * step);
        }

        // ------------------------------------------------------------------ Açılar / yönler

        /// <summary>Dereceyi [0, 360) aralığına getirir.</summary>
        public static float NormalizeDegrees(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return 0f;

            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        /// <summary>
        /// Dünya sapması (yaw, 0 = kuzey/+Z, saat yönünde artar) → kuzey-yukarı haritada arayüz Z dönüşü (derece).
        /// Yukarı bakan ok sprite'ı bu açıyla döndürülünce bakış yönünü gösterir.
        /// </summary>
        public static float YawToUiAngle(float yawDegrees) => -NormalizeDegrees(yawDegrees);

        /// <summary>Sapmadan harita yönü (x doğu, y kuzey; birim vektör).</summary>
        public static Vector2 YawToDirection(float yawDegrees)
        {
            var r = yawDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        /// <summary>İki nokta arası pusula kerterizi (derece; 0 = kuzey, 90 = doğu).</summary>
        public static float BearingTo(Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            if (dx * dx + dz * dz < 1e-6f)
                return 0f;
            return NormalizeDegrees(Mathf.Atan2(dx, dz) * Mathf.Rad2Deg);
        }

        /// <summary>Sapmanın sekiz yönlü Türkçe kısaltması: K, KD, D, GD, G, GB, B, KB.</summary>
        public static string BearingName(float yawDegrees)
        {
            var index = Mathf.RoundToInt(NormalizeDegrees(yawDegrees) / 45f) % 8;
            return BearingNames[index];
        }

        /// <summary>Sapmanın tam derece karşılığı (0..359).</summary>
        public static int BearingDegrees(float yawDegrees)
        {
            return Mathf.RoundToInt(NormalizeDegrees(yawDegrees)) % 360;
        }

        // ------------------------------------------------------------------ Mesafe / biçim

        /// <summary>Yatay (XZ) mesafe.</summary>
        public static float DistanceXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Mesafeyi Türkçe biçimler: "85 m", "1,2 km". (Tahsis eder; yalnızca değer değişince çağırın.)</summary>
        public static string FormatDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
                meters = 0f;

            if (meters < 1000f)
                return Mathf.RoundToInt(meters).ToString(Invariant) + " m";

            return FormatDecimal(meters / 1000f, 1) + " km";
        }

        /// <summary>Saniyeyi "dd:ss" biçiminde verir (negatif → "00:00").</summary>
        public static string FormatTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                seconds = 0f;

            var total = Mathf.CeilToInt(seconds);
            var minutes = total / 60;
            var secs = total % 60;
            return minutes.ToString("00", Invariant) + ":" + secs.ToString("00", Invariant);
        }

        /// <summary>Ondalık sayıyı Türkçe virgülle biçimler (ör. 22,5).</summary>
        public static string FormatDecimal(float value, int decimals)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;

            var format = decimals <= 0 ? "0" : decimals == 1 ? "0.#" : "0.##";
            return value.ToString(format, Invariant).Replace('.', ',');
        }

        /// <summary>
        /// Türkçe kurallarıyla büyük harfe çevirir ("Kuzgun Vadisi" → "KUZGUN VADİSİ"; i → İ, ı → I). Kültür
        /// tablolarına bağımlı değildir (değişmez kültür + Türkçe i/ı eşlemesi). Null → boş dizgi.
        /// </summary>
        public static string ToUpperTurkish(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c == 'i')
                    chars[i] = 'İ';
                else if (c == 'ı')
                    chars[i] = 'I';
                else
                    chars[i] = char.ToUpperInvariant(c);
            }

            return new string(chars);
        }

        // ------------------------------------------------------------------ Karelaj (askeri koordinat)

        /// <summary>Basitleştirilmiş karelaj bölge/şerit kodu (UTM benzeri, Türkiye için).</summary>
        public const string GridZoneCode = "36S";

        /// <summary>Basitleştirilmiş 100 km kare harfleri.</summary>
        public const string GridSquareCode = "KD";

        /// <summary>
        /// Askeri karelaj koordinatı (basitleştirilmiş MGRS): "36S KD 051 087". Doğu/kuzey, haritanın güneybatı köşesinden
        /// 10 m adımlarla üç hane (haritanın dışı sınırlandırılır). Tahsis eder; yalnızca değer değişince çağırın.
        /// </summary>
        public static string MilitaryGrid(MapFrame frame, Vector3 world)
        {
            GridDigits(frame, world, out var easting, out var northing);
            return FormatMilitaryGrid(easting, northing);
        }

        /// <summary>Karelaj hanelerini (0..999; 10 m adım) verir.</summary>
        public static void GridDigits(MapFrame frame, Vector3 world, out int easting, out int northing)
        {
            easting = Mathf.Clamp(Mathf.FloorToInt((world.x - frame.MinX) / 10f), 0, 999);
            northing = Mathf.Clamp(Mathf.FloorToInt((world.z - frame.MinZ) / 10f), 0, 999);
        }

        /// <summary>"36S KD 051 087" biçimi.</summary>
        public static string FormatMilitaryGrid(int easting, int northing)
        {
            easting = Mathf.Clamp(easting, 0, 999);
            northing = Mathf.Clamp(northing, 0, 999);
            return GridZoneCode + " " + GridSquareCode + " " + easting.ToString("000", Invariant) + " " + northing.ToString("000", Invariant);
        }

        /// <summary>İki nokta arası ölçüm yazısı: "320 m · 045° KD".</summary>
        public static string FormatMeasure(Vector3 from, Vector3 to)
        {
            var bearing = BearingTo(from, to);
            return FormatDistance(DistanceXZ(from, to)) + " · "
                   + BearingDegrees(bearing).ToString("000", Invariant) + "° " + BearingName(bearing);
        }

        // ------------------------------------------------------------------ Dönen mini harita / yakınlaştırma

        /// <summary>
        /// Kuzey-yukarı arayüz ofsetini, bakış yönü yukarıda olacak şekilde döndürür (saat yönünün tersine <paramref name="yawDegrees"/>).
        /// Doğuya (yaw 90) bakan oyuncu için doğudaki nokta yukarıya gelir.
        /// </summary>
        public static Vector2 RotateOffset(Vector2 offset, float yawDegrees)
        {
            var r = yawDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(r);
            var sin = Mathf.Sin(r);
            return new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        }

        /// <summary>Dünya sapması yawDegrees olan bir nesnenin, harita yawMap kadar döndürülmüşken arayüz Z açısı.</summary>
        public static float RotatedUiAngle(float objectYaw, float mapRotationYaw)
        {
            return YawToUiAngle(objectYaw) + NormalizeDegrees(mapRotationYaw);
        }

        /// <summary>Verilen noktaya en yakın bölge çemberi noktası (merkez=nokta ise merkezin kendisi değil +X yönü).</summary>
        public static Vector3 NearestPointOnCircle(Vector3 from, float centerX, float centerZ, float radius)
        {
            var dx = from.x - centerX;
            var dz = from.z - centerZ;
            var d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 1e-4f)
                return new Vector3(centerX + radius, from.y, centerZ);
            var k = radius / d;
            return new Vector3(centerX + dx * k, from.y, centerZ + dz * k);
        }

        /// <summary>Ping radyal menüsü dilimi: 0 = yukarı (Gidiyorum), 1 = sağ (Düşman), 2 = aşağı (Yağma), 3 = sol (Tehlike); ölü bölgede -1.</summary>
        public static int RadialSlice(Vector2 offset, float deadZone)
        {
            if (float.IsNaN(offset.x) || float.IsNaN(offset.y) || offset.sqrMagnitude < deadZone * deadZone)
                return -1;
            if (Mathf.Abs(offset.y) >= Mathf.Abs(offset.x))
                return offset.y >= 0f ? 0 : 2;
            return offset.x >= 0f ? 1 : 3;
        }

        /// <summary>Yakınlaştırma yüzdesi (1 = %100).</summary>
        public static int ZoomPercent(float zoom) => Mathf.RoundToInt(Mathf.Max(0f, zoom) * 100f);

        /// <summary>Yakınlaştırma sonrası harita odağı (UV): imlecin altındaki nokta ekranda sabit kalır.</summary>
        public static Vector2 ZoomFocus(Vector2 focus, Vector2 pivotUv, float oldPixels, float newPixels)
        {
            if (oldPixels <= 0f || newPixels <= 0f)
                return focus;
            var offsetPx = (pivotUv - focus) * oldPixels;
            return pivotUv - offsetPx / newPixels;
        }

        /// <summary>Harita odağını görünür alan haritanın içinde kalacak şekilde sınırlar.</summary>
        public static Vector2 ClampFocus(Vector2 focus, float zoom)
        {
            var half = 0.5f / Mathf.Max(1f, zoom);
            var x = float.IsNaN(focus.x) ? 0.5f : focus.x;
            var y = float.IsNaN(focus.y) ? 0.5f : focus.y;
            return new Vector2(Mathf.Clamp(x, half, 1f - half), Mathf.Clamp(y, half, 1f - half));
        }

        /// <summary>Ölçek çubuğu için en az <paramref name="minPixels"/> piksel genişliğinde yuvarlak mesafe (m).</summary>
        public static int NiceScaleMeters(float pixelsPerMeter, float minPixels)
        {
            for (var i = 0; i < ScaleSteps.Length; i++)
            {
                if (ScaleSteps[i] * pixelsPerMeter >= minPixels)
                    return ScaleSteps[i];
            }

            return ScaleSteps[ScaleSteps.Length - 1];
        }

        // ------------------------------------------------------------------ İşaretçi yardımcıları

        /// <summary>
        /// Merkeze göre konumu, merkezden geçen ışın boyunca yarı-boyutlu dikdörtgenin içine sabitler (yön korunur).
        /// Dışarıdaysa <paramref name="clamped"/> true.
        /// </summary>
        public static Vector2 ClampToRect(Vector2 point, Vector2 halfExtents, out bool clamped)
        {
            clamped = false;
            if (halfExtents.x <= 0f || halfExtents.y <= 0f)
                return point;

            var ax = Mathf.Abs(point.x) / halfExtents.x;
            var ay = Mathf.Abs(point.y) / halfExtents.y;
            var m = Mathf.Max(ax, ay);
            if (m <= 1f)
                return point;

            clamped = true;
            return point / m;
        }

        /// <summary>Merkeze göre konumu yarıçaplı daireye sabitler (yön korunur).</summary>
        public static Vector2 ClampToCircle(Vector2 point, float radius, out bool clamped)
        {
            clamped = false;
            var sqr = point.sqrMagnitude;
            if (radius <= 0f || sqr <= radius * radius)
                return point;

            clamped = true;
            return point * (radius / Mathf.Sqrt(sqr));
        }

        /// <summary>Core <see cref="Float3"/> → Unity vektörü.</summary>
        public static Vector3 ToVector3(Float3 value) => new Vector3(value.X, value.Y, value.Z);

        /// <summary>Unity vektörü → Core <see cref="Float3"/>.</summary>
        public static Float3 ToFloat3(Vector3 value) => new Float3(value.x, value.y, value.z);

        /// <summary>Vektörün tüm bileşenleri sonlu mu (NaN/sonsuz değil)?</summary>
        public static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                   && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        private static string[] BuildCellLabels()
        {
            var labels = new string[GridDivisions * GridDivisions];
            for (var row = 0; row < GridDivisions; row++)
            {
                for (var column = 0; column < GridDivisions; column++)
                    labels[row * GridDivisions + column] = ColumnLabels[column] + RowLabels[row];
            }

            return labels;
        }
    }
}
