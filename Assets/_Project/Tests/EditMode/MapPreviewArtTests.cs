#if UNITY_EDITOR
using NUnit.Framework;
using Project.Core.Domain;
using Project.Infrastructure.World;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class MapPreviewArtTests
    {
        private const int Size = 256;

        private static Color32[] RenderMap(string id, float h = 40f)
        {
            var layout = MapLayout.Create(id, 1);
            var n = Size - 2 * MapPreviewArt.Margin(Size);
            var heights = new float[n * n];
            for (var i = 0; i < heights.Length; i++)
                heights[i] = h;
            return MapPreviewArt.Render(layout, heights, Size, MapCatalog.DisplayName(id));
        }

        [Test]
        public void Render_ReturnsFullOpaqueImage()
        {
            var px = RenderMap(MapCatalog.Kuzgun);
            Assert.AreEqual(Size * Size, px.Length);
            for (var i = 0; i < px.Length; i += 97)
                Assert.AreEqual(255, px[i].a);
        }

        [Test]
        public void Render_IsDeterministic()
        {
            var a = RenderMap(MapCatalog.MaviLiman);
            var b = RenderMap(MapCatalog.MaviLiman);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Render_DiffersPerMap()
        {
            var a = RenderMap(MapCatalog.Kuzgun);
            var b = RenderMap(MapCatalog.AyazGecidi);
            var diff = false;
            for (var i = 0; i < a.Length && !diff; i++)
                diff = !a[i].Equals(b[i]);
            Assert.IsTrue(diff);
        }

        [Test]
        public void Render_LowHeights_ProduceBlueWater()
        {
            var px = RenderMap(MapCatalog.Kuzgun, 0f);
            var p = px[(Size / 2) * Size + Size / 2 + 40];
            Assert.Greater(p.b, p.r);
        }

        [Test]
        public void Render_WrongHeightLength_DoesNotThrow()
        {
            var layout = MapLayout.Create(MapCatalog.KartalYaylasi, 1);
            Assert.DoesNotThrow(() => MapPreviewArt.Render(layout, new float[3], Size, "Kartal Yaylası"));
        }

        [Test]
        public void TextWidth_ScalesWithLengthAndScale()
        {
            Assert.AreEqual(0, MapPreviewArt.TextWidth("", 2));
            Assert.AreEqual(34, MapPreviewArt.TextWidth("ABC", 2));
        }

        [Test]
        public void Decompose_TurkishLetters()
        {
            MapPreviewArt.Decompose('ş', out var b, out var a);
            Assert.AreEqual('S', b);
            Assert.AreNotEqual(0, a);
            MapPreviewArt.Decompose('ı', out b, out a);
            Assert.AreEqual('I', b);
            Assert.AreEqual(0, a);
            MapPreviewArt.Decompose('i', out b, out a);
            Assert.AreEqual('I', b);
            Assert.AreNotEqual(0, a);
        }
    }
}
#endif
