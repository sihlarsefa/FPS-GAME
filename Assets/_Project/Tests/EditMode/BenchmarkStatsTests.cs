using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public sealed class BenchmarkStatsTests
    {
        [Test]
        public void Summarize_AvgLowWorst()
        {
            var f = new List<float>();
            for (var i = 0; i < 99; i++) f.Add(10f);
            f.Add(110f);
            var s = BenchmarkStats.Summarize(f);
            Assert.AreEqual(100, s.Frames);
            Assert.AreEqual(11f, s.AvgMs, 0.001f);
            Assert.AreEqual(110f, s.OnePercentLowMs, 0.001f);
            Assert.AreEqual(110f, s.WorstMs, 0.001f);
            Assert.AreEqual(100f, s.AvgFps > 0 ? 100f : 0f, 0.001f);
        }

        [Test]
        public void Summarize_Empty_IsZero()
        {
            var s = BenchmarkStats.Summarize(new List<float>());
            Assert.AreEqual(0, s.Frames);
            Assert.AreEqual(0f, s.AvgFps, 0.0001f);
        }

        [Test]
        public void Csv_HasHeaderAndRow()
        {
            var s = BenchmarkStats.Summarize(new List<float> { 10f, 20f });
            var csv = BenchmarkStats.ToCsv("kuzgun", "orta", "1920x1080", 60f, s);
            Assert.IsTrue(csv.StartsWith(BenchmarkStats.CsvHeader));
            Assert.IsTrue(csv.Contains("kuzgun,orta,1920x1080,2,60.0,15.000"));
        }

        [Test]
        public void Path_IsDeterministicAndClamped()
        {
            BenchmarkStats.EvaluatePath(0f, 0f, 0f, 512f, out var x0, out var z0, out _, out _);
            BenchmarkStats.EvaluatePath(0f, 0f, 0f, 512f, out var x1, out var z1, out _, out _);
            Assert.AreEqual(x0, x1, 0.0001f);
            Assert.AreEqual(z0, z1, 0.0001f);
            BenchmarkStats.EvaluatePath(5f, 0f, 0f, 512f, out var xe, out var ze, out _, out _);
            Assert.AreEqual(256f, xe, 0.01f);
            Assert.AreEqual(256f, ze, 0.01f);
        }
    }
}
