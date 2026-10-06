using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Çim ezilmesi: oyuncu/araç gibi ezicilerin küçük tamponu (en fazla 8). Kameraya en yakın N tanesi
    /// _HarekatInteractors (xyz konum, w yarıçap) ve _HarekatInteractorCount globaline yazılır.
    /// </summary>
    public static class GrassInteractors
    {
        public const int MaxInteractors = 8;
        private static readonly int ArrayId = Shader.PropertyToID("_HarekatInteractors");
        private static readonly int CountId = Shader.PropertyToID("_HarekatInteractorCount");

        private sealed class Entry
        {
            public Transform Target;
            public float Radius;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Vector4[] Buffer = new Vector4[MaxInteractors];
        private static float[] _dist2 = new float[16];
        private static readonly int[] Picked = new int[MaxInteractors];

        /// <summary>Etkin ezici sayısı (son Upload).</summary>
        public static int LastCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            LastCount = 0;
        }

        /// <summary>Ezici ekler (oyuncu ~0.9 m, araç ~2.5 m). Aynı Transform ikinci kez eklenirse yarıçap güncellenir.</summary>
        public static void Register(Transform target, float radius)
        {
            if (target == null) return;
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Target == target)
                {
                    Entries[i].Radius = radius;
                    return;
                }
            }

            Entries.Add(new Entry { Target = target, Radius = radius });
        }

        public static void Unregister(Transform target)
        {
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Target == target)
                    Entries.RemoveAt(i);
            }
        }

        public static int Count => Entries.Count;

        /// <summary>Kameraya en yakın en fazla max ezici shader globaline yazılır; max 0 ise ezilme kapanır.</summary>
        public static void Upload(Vector3 cameraPos, int max, float maxRange)
        {
            max = Mathf.Clamp(max, 0, MaxInteractors);
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Target == null)
                    Entries.RemoveAt(i);
            }

            var n = 0;
            if (max > 0 && Entries.Count > 0)
            {
                if (_dist2.Length < Entries.Count)
                    _dist2 = new float[Entries.Count * 2];
                for (var i = 0; i < Entries.Count; i++)
                {
                    var p = Entries[i].Target.position - cameraPos;
                    _dist2[i] = p.x * p.x + p.y * p.y + p.z * p.z;
                }

                n = GrassRules.PickNearest(_dist2, Entries.Count, max, maxRange * maxRange, Picked);
                for (var k = 0; k < n; k++)
                {
                    var e = Entries[Picked[k]];
                    var pos = e.Target.position;
                    Buffer[k] = new Vector4(pos.x, pos.y, pos.z, e.Radius);
                }
            }

            for (var k = n; k < MaxInteractors; k++)
                Buffer[k] = Vector4.zero;
            LastCount = n;
            Shader.SetGlobalVectorArray(ArrayId, Buffer);
            Shader.SetGlobalFloat(CountId, n);
        }
    }
}
