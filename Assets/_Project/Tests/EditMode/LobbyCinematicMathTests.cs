using NUnit.Framework;
using Project.Infrastructure.World.Lobby;
using UnityEngine;

namespace Project.Tests
{
    public sealed class LobbyCinematicMathTests
    {
        [Test]
        public void FireFlicker_SinirlarIcinde()
        {
            for (var i = 0; i < 200; i++)
            {
                var f = LobbyCinematicMath.FireFlicker(i * 0.13f, 31.7f);
                Assert.IsTrue(f >= 0.65f && f <= 1.35f);
            }
        }

        [Test]
        public void LampFlicker_NeredeysSabit()
        {
            for (var i = 0; i < 200; i++)
            {
                var f = LobbyCinematicMath.LampFlicker(i * 0.37f, 5f);
                Assert.IsTrue(f >= 0.7f && f <= 1.03f);
            }
        }

        [Test]
        public void Parallax_Sinirli()
        {
            var o = LobbyCinematicMath.ParallaxOffset(new Vector2(50f, -50f), 12f, 0.2f);
            Assert.IsTrue(Mathf.Abs(o.x) <= 0.2f && Mathf.Abs(o.y) <= 0.2f);
            Assert.AreEqual(0f, o.z);
        }

        [Test]
        public void Damp_HedefeYaklasir()
        {
            var v = LobbyCinematicMath.Damp(Vector3.zero, Vector3.right, 1f, 3f);
            Assert.Greater(v.x, 0.9f);
            Assert.AreEqual(0f, LobbyCinematicMath.Damp(Vector3.zero, Vector3.right, 0f, 3f).x);
        }

        [Test]
        public void LayerFactor_UzakKatmanDahaAz()
        {
            Assert.Greater(LobbyCinematicMath.LayerFactor(5f), LobbyCinematicMath.LayerFactor(400f));
        }

        [Test]
        public void KademeButceleri_ArtarVeGolgeYalnizUltra()
        {
            Assert.Greater(LobbyCinematicMath.FogParticles(3), LobbyCinematicMath.FogParticles(0));
            Assert.Greater(LobbyCinematicMath.EmberParticles(2), LobbyCinematicMath.EmberParticles(1));
            Assert.Greater(LobbyCinematicMath.LampCount(3), LobbyCinematicMath.LampCount(0));
            Assert.IsTrue(LobbyCinematicMath.LampShadows(3));
            Assert.IsTrue(!LobbyCinematicMath.LampShadows(2));
        }
    }
}
