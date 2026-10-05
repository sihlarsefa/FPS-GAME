using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// Eşit aralıklı (yeniden örneklenmiş) yatay rota. Noktalar XZ düzlemindedir (Y = 0); yükseklik araç tarafından
    /// belirlenir. Sorgular O(1) ve tahsissizdir (her karede güvenle çağrılır). Yalnızca oluşturma anında bellek ayırır.
    /// </summary>
    internal sealed class TransportPath
    {
        private readonly Vector3[] _points;
        private readonly float _spacing;

        private TransportPath(Vector3[] points, float spacing, float length)
        {
            _points = points;
            _spacing = spacing;
            Length = length;
        }

        /// <summary>Toplam yatay uzunluk (m).</summary>
        public float Length { get; }

        /// <summary>Nokta sayısı (en az 2).</summary>
        public int Count => _points.Length;

        /// <summary>Örnekleme aralığı (m).</summary>
        public float Spacing => _spacing;

        public Vector3 Start => _points[0];

        public Vector3 End => _points[_points.Length - 1];

        /// <summary>i. noktanın rota üzerindeki mesafesi.</summary>
        public float DistanceAt(int index)
        {
            if (index <= 0)
                return 0f;
            return index >= _points.Length - 1 ? Length : index * _spacing;
        }

        public Vector3 PointAt(int index) => _points[Mathf.Clamp(index, 0, _points.Length - 1)];

        /// <summary>Rota üzerindeki konum (s mesafede; sınırlandırılır).</summary>
        public Vector3 PositionAt(float s)
        {
            if (!(s > 0f))
                return _points[0];
            if (s >= Length)
                return _points[_points.Length - 1];

            var index = (int)(s / _spacing);
            if (index >= _points.Length - 1)
                return _points[_points.Length - 1];

            var segmentStart = index * _spacing;
            var segmentLength = (index + 1 >= _points.Length - 1 ? Length : (index + 1) * _spacing) - segmentStart;
            var t = segmentLength > 1e-5f ? (s - segmentStart) / segmentLength : 0f;
            return Vector3.LerpUnclamped(_points[index], _points[index + 1], Mathf.Clamp01(t));
        }

        /// <summary>s noktasındaki yatay ilerleme yönü (birim). Uçlarda sınırlandırılır.</summary>
        public Vector3 DirectionAt(float s, float halfWindow = 1.5f)
        {
            halfWindow = Mathf.Max(0.25f, halfWindow);
            var a = PositionAt(s - halfWindow);
            var b = PositionAt(s + halfWindow);
            var d = b - a;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f)
            {
                d = _points[_points.Length - 1] - _points[0];
                d.y = 0f;
            }

            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        /// <summary>İki örnek arasındaki tablo değerini (nokta başına bir değer) doğrusal ara değerler.</summary>
        public float SampleTable(float[] table, float s)
        {
            if (table == null || table.Length == 0)
                return 0f;

            var count = Mathf.Min(table.Length, _points.Length);
            if (!(s > 0f))
                return table[0];
            if (s >= Length)
                return table[count - 1];

            var index = (int)(s / _spacing);
            if (index >= count - 1)
                return table[count - 1];

            var segmentStart = index * _spacing;
            var segmentLength = (index + 1 >= _points.Length - 1 ? Length : (index + 1) * _spacing) - segmentStart;
            var t = segmentLength > 1e-5f ? (s - segmentStart) / segmentLength : 0f;
            return Mathf.Lerp(table[index], table[index + 1], t);
        }

        // ------------------------------------------------------------------ construction

        /// <summary>Çoklu çizgiyi eşit aralıkla yeniden örnekler (Y sıfırlanır). En az 2 nokta döner.</summary>
        public static TransportPath Resample(IReadOnlyList<Vector3> polyline, float spacing)
        {
            spacing = Mathf.Max(0.25f, spacing);
            if (polyline == null || polyline.Count == 0)
                return new TransportPath(new[] { Vector3.zero, Vector3.forward * spacing }, spacing, spacing);

            var total = 0f;
            for (var i = 1; i < polyline.Count; i++)
                total += FlatDistance(polyline[i - 1], polyline[i]);

            if (total < 0.01f)
            {
                var p = Flat(polyline[0]);
                return new TransportPath(new[] { p, p + Vector3.forward * 0.01f }, spacing, 0.01f);
            }

            var count = Mathf.Max(2, Mathf.CeilToInt(total / spacing) + 1);
            var points = new Vector3[count];
            points[0] = Flat(polyline[0]);
            points[count - 1] = Flat(polyline[polyline.Count - 1]);

            var segment = 1;
            var segmentStart = 0f;
            var segmentLength = FlatDistance(polyline[0], polyline[Mathf.Min(1, polyline.Count - 1)]);
            for (var k = 1; k < count - 1; k++)
            {
                var target = k * spacing;
                while (segment < polyline.Count - 1 && segmentStart + segmentLength < target)
                {
                    segmentStart += segmentLength;
                    segment++;
                    segmentLength = FlatDistance(polyline[segment - 1], polyline[segment]);
                }

                var t = segmentLength > 1e-5f ? (target - segmentStart) / segmentLength : 0f;
                points[k] = Flat(Vector3.Lerp(polyline[segment - 1], polyline[segment], Mathf.Clamp01(t)));
            }

            return new TransportPath(points, spacing, total);
        }

        /// <summary>İkinci derece Bezier eğrisi noktaları ekler.</summary>
        public static void AppendBezier(List<Vector3> output, Vector3 a, Vector3 control, Vector3 b, int segments, bool includeFirst)
        {
            segments = Mathf.Max(1, segments);
            for (var i = includeFirst ? 0 : 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var u = 1f - t;
                output.Add(u * u * a + 2f * u * t * control + t * t * b);
            }
        }

        /// <summary>Chaikin köşe yumuşatma (uç noktalar korunur).</summary>
        public static void Smooth(List<Vector3> points, int iterations)
        {
            if (points == null || points.Count < 3)
                return;

            var buffer = new List<Vector3>(points.Count * 2);
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                buffer.Clear();
                buffer.Add(points[0]);
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var p = points[i];
                    var q = points[i + 1];
                    buffer.Add(Vector3.Lerp(p, q, 0.25f));
                    buffer.Add(Vector3.Lerp(p, q, 0.75f));
                }

                buffer.Add(points[points.Count - 1]);
                points.Clear();
                points.AddRange(buffer);
            }
        }

        /// <summary>Ardışık çok yakın noktaları ayıklar (NavMesh köşeleri gibi).</summary>
        public static void RemoveDuplicates(List<Vector3> points, float minDistance)
        {
            if (points == null || points.Count < 3)
                return;

            var end = points[points.Count - 1];
            var write = 1;
            for (var read = 1; read < points.Count - 1; read++)
            {
                if (FlatDistance(points[write - 1], points[read]) >= minDistance)
                    points[write++] = points[read];
            }

            // Son nokta her zaman korunur; ondan önceki nokta çok yakınsa onun yerini alır.
            if (write > 1 && FlatDistance(points[write - 1], end) < minDistance)
                write--;

            points[write++] = end;
            points.RemoveRange(write, points.Count - write);
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = b.x - a.x;
            var dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
