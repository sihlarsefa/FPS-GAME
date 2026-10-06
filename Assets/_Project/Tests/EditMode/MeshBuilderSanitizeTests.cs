using NUnit.Framework;
using Project.Infrastructure.Diagnostics;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests.EditMode
{
    public sealed class MeshBuilderSanitizeTests
    {
        [Test]
        public void SanitizeNonFinite_DropsBadTriangle()
        {
            var b = new MeshBuilder(1);
            var i0 = b.AddVertex(Vector3.zero, Vector3.up, Vector2.zero);
            var i1 = b.AddVertex(Vector3.right, Vector3.up, Vector2.zero);
            var i2 = b.AddVertex(new Vector3(float.NaN, 0, 0), Vector3.up, Vector2.zero);
            var i3 = b.AddVertex(Vector3.forward, Vector3.up, Vector2.zero);
            b.AddTriangle(0, i0, i1, i2);
            b.AddTriangle(0, i0, i1, i3);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("geçersiz köşe"));
            Assert.AreEqual(1, b.SanitizeNonFinite("t"));
        }

        [Test]
        public void Backoff_GrowsAndCaps()
        {
            Assert.AreEqual(0, ClientErrorReporter.BackoffSeconds(0));
            Assert.AreEqual(5, ClientErrorReporter.BackoffSeconds(1));
            Assert.AreEqual(20, ClientErrorReporter.BackoffSeconds(3));
            Assert.AreEqual(300, ClientErrorReporter.BackoffSeconds(20));
        }
    }
}
