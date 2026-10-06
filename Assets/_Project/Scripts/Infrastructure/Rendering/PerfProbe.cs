using System;
using System.Globalization;
using System.Text;
using Unity.Profiling;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Tek metrik için 60 sn'lik yuvarlanan pencere ozeti (saf matematik).</summary>
    public struct PerfStat
    {
        public float Last, Avg, P95, Worst;
    }

    /// <summary>Saf (UnityEngine native cagrisiz) yuvarlanan pencere; testlenebilir.</summary>
    public sealed class PerfWindow
    {
        private readonly float[] _v;
        private int _write, _count;

        public PerfWindow(int capacity = PerfProbe.WindowSeconds) { _v = new float[Math.Max(1, capacity)]; }

        public int Count => _count;

        public void Add(float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
            _v[_write] = x;
            _write = (_write + 1) % _v.Length;
            if (_count < _v.Length) _count++;
        }

        public PerfStat Aggregate()
        {
            var s = new PerfStat();
            if (_count == 0) return s;
            var tmp = new float[_count];
            double sum = 0; float worst = float.MinValue;
            for (var i = 0; i < _count; i++)
            {
                var x = _v[i]; tmp[i] = x; sum += x;
                if (x > worst) worst = x;
            }
            Array.Sort(tmp);
            // en yakin sira (nearest-rank) p95
            var rank = (int)Math.Ceiling(0.95 * _count) - 1;
            if (rank < 0) rank = 0; if (rank >= _count) rank = _count - 1;
            s.Last = _v[(_write - 1 + _v.Length) % _v.Length];
            s.Avg = (float)(sum / _count);
            s.P95 = tmp[rank];
            s.Worst = worst;
            return s;
        }
    }

    /// <summary>Pencere ozeti anlik goruntusu. Kayit yoksa Valid=false, tum degerler 0.</summary>
    public struct PerfSnapshot
    {
        public bool Valid;
        public int Samples;
        public PerfStat FrameCpuMs, RenderThreadMs, DrawCalls, SetPass, Triangles, GcAllocKb, TextureMb;

        public const string CsvHeader =
            "samples,cpu_avg,cpu_p95,cpu_worst,rt_avg,rt_p95,rt_worst,draw_avg,draw_p95,draw_worst," +
            "setpass_avg,setpass_p95,setpass_worst,tris_avg,tris_p95,tris_worst,gc_kb_avg,gc_kb_p95,gc_kb_worst,tex_mb";

        private static void S(StringBuilder b, PerfStat s)
        {
            b.Append(',').Append(s.Avg.ToString("0.##", CultureInfo.InvariantCulture))
             .Append(',').Append(s.P95.ToString("0.##", CultureInfo.InvariantCulture))
             .Append(',').Append(s.Worst.ToString("0.##", CultureInfo.InvariantCulture));
        }

        /// <summary>Tek CSV satiri (InvariantCulture, basliksiz); basligi CsvHeader verir.</summary>
        public string ToCsvLine()
        {
            var b = new StringBuilder(160);
            b.Append(Samples.ToString(CultureInfo.InvariantCulture));
            S(b, FrameCpuMs); S(b, RenderThreadMs); S(b, DrawCalls); S(b, SetPass); S(b, Triangles); S(b, GcAllocKb);
            b.Append(',').Append(TextureMb.Last.ToString("0.#", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }

    /// <summary>
    /// ProfilerRecorder tabanli perf sondasi: 1 Hz ornek, 60 sn yuvarlanan ozet.
    /// Kullanim: PerfProbe.Snapshot(). Ilk cagrida kendini baslatir ve gizli surucu olusturur.
    /// Kayitci yoksa (build/platform) sifir doner, hata atmaz.
    /// </summary>
    public sealed class PerfProbe : IDisposable
    {
        public const int WindowSeconds = 60;
        public const double SampleInterval = 1.0;

        private readonly PerfWindow _cpu = new PerfWindow(), _rt = new PerfWindow(), _draw = new PerfWindow(),
            _setPass = new PerfWindow(), _tris = new PerfWindow(), _gc = new PerfWindow(), _tex = new PerfWindow();
        private ProfilerRecorder _rCpu, _rRt, _rDraw, _rSet, _rTris, _rGc, _rTex;
        private double _next = double.NaN;

        public PerfProbe()
        {
            _rCpu = Start(ProfilerCategory.Internal, "CPU Main Thread Frame Time");
            _rRt = Start(ProfilerCategory.Internal, "CPU Render Thread Frame Time");
            _rDraw = Start(ProfilerCategory.Render, "Draw Calls Count");
            _rSet = Start(ProfilerCategory.Render, "SetPass Calls Count");
            _rTris = Start(ProfilerCategory.Render, "Triangles Count");
            _rGc = Start(ProfilerCategory.Memory, "GC Allocated In Frame");
            _rTex = Start(ProfilerCategory.Memory, "Texture Memory");
        }

        private static ProfilerRecorder Start(ProfilerCategory c, string n)
        {
            try { return ProfilerRecorder.StartNew(c, n); }
            catch (Exception) { return default; }
        }

        private static float Read(ProfilerRecorder r)
        {
            try { return r.Valid ? r.LastValue : 0f; }
            catch (Exception) { return 0f; }
        }

        /// <summary>Saat ile surulur; 1 sn'de bir ornek alir. Dondurur: ornek alindi mi.</summary>
        public bool Tick(double nowSeconds)
        {
            if (double.IsNaN(_next)) _next = nowSeconds + SampleInterval;
            if (nowSeconds < _next) return false;
            _next = nowSeconds + SampleInterval;
            _cpu.Add(Read(_rCpu) / 1e6f);
            _rt.Add(Read(_rRt) / 1e6f);
            _draw.Add(Read(_rDraw));
            _setPass.Add(Read(_rSet));
            _tris.Add(Read(_rTris));
            _gc.Add(Read(_rGc) / 1024f);
            _tex.Add(Read(_rTex) / (1024f * 1024f));
            return true;
        }

        public PerfSnapshot Capture()
        {
            return new PerfSnapshot
            {
                Valid = _cpu.Count > 0,
                Samples = _cpu.Count,
                FrameCpuMs = _cpu.Aggregate(),
                RenderThreadMs = _rt.Aggregate(),
                DrawCalls = _draw.Aggregate(),
                SetPass = _setPass.Aggregate(),
                Triangles = _tris.Aggregate(),
                GcAllocKb = _gc.Aggregate(),
                TextureMb = _tex.Aggregate()
            };
        }

        public void Dispose()
        {
            _rCpu.Dispose(); _rRt.Dispose(); _rDraw.Dispose(); _rSet.Dispose();
            _rTris.Dispose(); _rGc.Dispose(); _rTex.Dispose();
        }

        // ---- statik erisim ----
        private static PerfProbe _instance;

        public static PerfProbe Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new PerfProbe();
                if (UnityEngine.Application.isPlaying) PerfProbeDriver.Create();
                return _instance;
            }
        }

        /// <summary>Son 60 sn ozeti; kayit yoksa Valid=false.</summary>
        public static PerfSnapshot Snapshot() => Instance.Capture();

        internal static void DriveTick(double now) => _instance?.Tick(now);
    }

    internal sealed class PerfProbeDriver : UnityEngine.MonoBehaviour
    {
        internal static void Create()
        {
            var go = new UnityEngine.GameObject("[PerfProbe]") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<PerfProbeDriver>();
        }

        private void Update() => PerfProbe.DriveTick(UnityEngine.Time.unscaledTimeAsDouble);
    }
}
