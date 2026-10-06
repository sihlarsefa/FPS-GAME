#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    public class PerfProbeTests
    {
        [Test]
        public void Window_AvgP95Worst()
        {
            var w = new PerfWindow(100);
            for (var i = 1; i <= 100; i++) w.Add(i);
            var s = w.Aggregate();
            Assert.AreEqual(50.5f, s.Avg, 0.001f);
            Assert.AreEqual(95f, s.P95, 0.001f);
            Assert.AreEqual(100f, s.Worst, 0.001f);
            Assert.AreEqual(100f, s.Last, 0.001f);
        }

        [Test]
        public void Window_RollsOver()
        {
            var w = new PerfWindow(3);
            for (var i = 1; i <= 5; i++) w.Add(i);
            var s = w.Aggregate();
            Assert.AreEqual(3, w.Count);
            Assert.AreEqual(4f, s.Avg, 0.001f);
        }

        [Test]
        public void Window_EmptyAndNaN()
        {
            var w = new PerfWindow(4);
            Assert.AreEqual(0f, w.Aggregate().Worst);
            w.Add(float.NaN);
            Assert.AreEqual(0f, w.Aggregate().Avg);
        }

        [Test]
        public void Csv_ColumnCountMatchesHeader()
        {
            var snap = new PerfSnapshot { Samples = 5 };
            snap.FrameCpuMs.Avg = 12.345f;
            var cols = snap.ToCsvLine().Split(',').Length;
            Assert.AreEqual(PerfSnapshot.CsvHeader.Split(',').Length, cols);
            StringAssert.StartsWith("5,12.35", snap.ToCsvLine());
        }
    }
}
#endif
