#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class EmblemArtTests
    {
        private static bool Same(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }

        [Test]
        public void Surface_PartialPixelCoverage_IsAntialiased()
        {
            var s = new ArtSurface(8, 8);
            s.Fill(new[] { new Vector2(1f, 1f), new Vector2(3.5f, 1f), new Vector2(3.5f, 3f), new Vector2(1f, 3f) }, Color.white);
            Assert.AreEqual(1f, s.Pixels[1 * 8 + 1].a, 0.01f);
            Assert.AreEqual(0.5f, s.Pixels[1 * 8 + 3].a, 0.06f);
            Assert.AreEqual(0f, s.Pixels[1 * 8 + 4].a, 0.001f);
        }

        [Test]
        public void Surface_RingLeavesHoleTransparent()
        {
            var s = new ArtSurface(40, 40);
            s.Ring(20f, 20f, 15f, 9f, Color.white);
            Assert.AreEqual(0f, s.Pixels[20 * 40 + 20].a, 0.001f);
            Assert.AreEqual(1f, s.Pixels[20 * 40 + 20 + 12].a, 0.01f);
        }

        [Test]
        public void Crescent_OpensToTheRight()
        {
            var s = new ArtSurface(100, 100);
            s.Fill(ArtSurface.CrescentPoly(50f, 50f, 30f, 24f, 11f), Color.white);
            Assert.Greater(s.Pixels[50 * 100 + 24].a, 0.9f);   // sol kalın taraf dolu
            Assert.AreEqual(0f, s.Pixels[50 * 100 + 66].a, 0.001f);   // iç boşluk
        }

        [Test]
        public void Emblem_IsDeterministicRoundAndRed()
        {
            var a = EmblemArt.RenderEmblem(128).ToColor32();
            var b = EmblemArt.RenderEmblem(128).ToColor32();
            Assert.IsTrue(Same(a, b));
            Assert.AreEqual(0, a[0].a);                       // köşe şeffaf
            Assert.AreEqual(255, a[64 * 128 + 64].a);         // merkez opak
            var red = 0;
            foreach (var p in a)
                if (p.a > 200 && p.r > 150 && p.g < 100 && p.b < 90)
                    red++;
            Assert.Greater(red, 150, "kırmızı vurgu bulunmalı");
        }

        [Test]
        public void Apolet_AllRanksHaveDistinctSymbols()
        {
            var seen = new HashSet<string>();
            foreach (MilitaryRank r in Enum.GetValues(typeof(MilitaryRank)))
            {
                var syms = EmblemArt.GetRankSymbols(r);
                Assert.Greater(syms.Length, 0);
                var key = "";
                foreach (var sym in syms)
                    key += sym.Mark + "/" + sym.Metal + ";";
                Assert.IsTrue(seen.Add(key), "yinelenen rütbe işareti: " + r);
            }

            Assert.AreEqual(19, seen.Count);
        }

        [Test]
        public void Apolet_RendersTransparentCornersAndMetal()
        {
            var s = EmblemArt.RenderApolet(MilitaryRank.Albay, 64);
            Assert.AreEqual(EmblemArt.ApoletWidth(64), s.Width);
            Assert.AreEqual(0f, s.Pixels[0].a, 0.001f);
            var gold = 0;
            foreach (var p in s.Pixels)
                if (p.a > 0.9f && p.r > 0.6f && p.g > 0.45f && p.b < 0.5f)
                    gold++;
            Assert.Greater(gold, 40);
            Assert.IsTrue(Same(s.ToColor32(), EmblemArt.RenderApolet(MilitaryRank.Albay, 64).ToColor32()));
        }

        [Test]
        public void Apolet_DiffersBetweenRanks()
        {
            var a = EmblemArt.RenderApolet(MilitaryRank.Er, 48).ToColor32();
            var b = EmblemArt.RenderApolet(MilitaryRank.Yuzbasi, 48).ToColor32();
            Assert.IsFalse(Same(a, b));
        }

        [Test]
        public void ModeCards_DeterministicOpaqueDistinctWithRedAccent()
        {
            var cards = new Color32[MapPreviewArt.ModeCardCount][];
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i] = MapPreviewArt.RenderModeCard(i, 192, 120);
                Assert.AreEqual(192 * 120, cards[i].Length);
                Assert.IsTrue(Same(cards[i], MapPreviewArt.RenderModeCard(i, 192, 120)), "kart " + i + " deterministik değil");
                var red = 0;
                foreach (var p in cards[i])
                {
                    Assert.AreEqual(255, p.a);
                    if (p.r > 140 && p.g < 90 && p.b < 80)
                        red++;
                }

                Assert.Greater(red, 20, "kart " + i + " kırmızı vurgu içermeli");
            }

            for (var i = 0; i < cards.Length; i++)
            for (var j = i + 1; j < cards.Length; j++)
                Assert.IsFalse(Same(cards[i], cards[j]), i + " ile " + j + " aynı");
        }
    }
}
#endif
