#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class OtoEkranTests
    {
        [Test]
        public void NoFlag_NotParsed()
        {
            Assert.IsFalse(OtoEkranArgs.TryParse(new[] { "game", "-benchmark" }, out var o));
            Assert.AreEqual(OtoEkranMode.Yok, o.Mode);
            Assert.IsFalse(OtoEkranArgs.TryParse(null, out _));
        }

        [Test]
        public void Benchmark_ParsesDir_AndDefaults()
        {
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "game", "-otoekran", "/tmp/out dir" }, out var o));
            Assert.AreEqual(OtoEkranMode.Benchmark, o.Mode);
            Assert.AreEqual("/tmp/out dir", o.OutDir);
            Assert.AreEqual(120f, o.TimeoutSeconds);
            Assert.AreEqual(2.5f, o.SettleSeconds);
            Assert.AreEqual(3840, o.Width);
            Assert.AreEqual(2160, o.Height);
        }

        [Test]
        public void Resolution_FlagParsed_ClampedAndDefaults()
        {
            // Açık değer kullanılır.
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoekran", "/o", "-otoekran-coz", "1920", "1080" }, out var o));
            Assert.AreEqual(1920, o.Width);
            Assert.AreEqual(1080, o.Height);
            // Sınırlar: 640-7680 x 360-4320.
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoekran", "/o", "-otoekran-coz", "100000", "1" }, out o));
            Assert.AreEqual(7680, o.Width);
            Assert.AreEqual(360, o.Height);
            // Bozuk değer → 4K varsayılanı.
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoekran", "/o", "-otoekran-coz", "bozuk", "1080" }, out o));
            Assert.AreEqual(OtoEkranArgs.DefaultWidth, o.Width);
            Assert.AreEqual(OtoEkranArgs.DefaultHeight, o.Height);
            // Menü modu da aynı çözümleyiciyi kullanır; bayrak yoksa 4K.
            OtoEkranArgs.ParseResolution(new[] { "-otoekran-menu", "/o" }, out var w, out var h);
            Assert.AreEqual(3840, w);
            Assert.AreEqual(2160, h);
            // Çözünürlük bayrağı tek başına "çözümlenemedi" uyarısı üretmez (menü moduyla birlikte kullanım).
            Assert.IsFalse(OtoEkranArgs.HasAnyFlag(new[] { "-otoekran-menu", "/o", "-otoekran-coz", "3840", "2160" }));
        }

        [Test]
        public void Benchmark_MissingOrFlagValue_Fails()
        {
            Assert.IsFalse(OtoEkranArgs.TryParse(new[] { "game", "-otoekran" }, out _));
            Assert.IsFalse(OtoEkranArgs.TryParse(new[] { "game", "-otoekran", "-batchmode" }, out _));
        }

        [Test]
        public void Map_ParsesSceneAndDir_AndWinsOverBenchmark()
        {
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "x", "-OTOEKRAN-HARITA", "KuzgunVadisi", "/o" }, out var o));
            Assert.AreEqual(OtoEkranMode.Harita, o.Mode);
            Assert.AreEqual("KuzgunVadisi", o.Scene);
            Assert.AreEqual("/o", o.OutDir);
            Assert.IsFalse(OtoEkranArgs.TryParse(new[] { "x", "-otoekran-harita", "KuzgunVadisi" }, out _));
        }

        [Test]
        public void TimeoutAndSettle_AreParsedAndClamped()
        {
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoekran", "/o", "-otoekran-sure", "5000", "-otoekran-bekle", "0" }, out var o));
            Assert.AreEqual(900f, o.TimeoutSeconds);
            Assert.AreEqual(0.2f, o.SettleSeconds, 1e-4f);
            Assert.IsTrue(OtoEkranArgs.TryParse(new[] { "-otoekran", "/o", "-otoekran-sure", "90.5" }, out o));
            Assert.AreEqual(90.5f, o.TimeoutSeconds, 1e-4f);
        }

        [Test]
        public void Slug_And_FileName_AreAsciiSafe()
        {
            Assert.AreEqual("silah_yakin_plan", OtoEkranArgs.Slug("Silah yakın plan"));
            Assert.AreEqual("ev_ici", OtoEkranArgs.Slug("Ev içi"));
            Assert.AreEqual("hedeflere_ates", OtoEkranArgs.Slug("Hedeflere ateş"));
            Assert.AreEqual("plan", OtoEkranArgs.Slug("  "));
            Assert.AreEqual("03_kirpi.png", OtoEkranArgs.FileName(3, "Kirpi"));
        }

        [Test]
        public void Viewpoints_FiveNamedFinitePoints()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Kuzgun Köyü", Center = new Vector2(10f, 20f), Radius = 60f, IsMajor = true },
                new NamedLocation { Name = "Değirmen", Center = new Vector2(200f, -100f), Radius = 20f }
            };
            // Tepe (300,300), göl çukuru (-300,-250).
            System.Func<float, float, float> h = (x, z) =>
                30f + 80f * Mathf.Exp(-((x - 300f) * (x - 300f) + (z - 300f) * (z - 300f)) / 20000f)
                - 40f * Mathf.Exp(-((x + 300f) * (x + 300f) + (z + 250f) * (z + 250f)) / 20000f);

            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h);
            Assert.AreEqual(OtoEkranViewpoints.Count, views.Count);
            for (var i = 0; i < views.Count; i++)
            {
                Assert.AreEqual(OtoEkranViewpoints.Names[i], views[i].Name);
                Assert.IsFalse(float.IsNaN(views[i].Position.x + views[i].Position.y + views[i].Position.z));
                Assert.Greater((views[i].LookAt - views[i].Position).sqrMagnitude, 1f);
            }

            Assert.Less(Vector2.Distance(new Vector2(views[2].Position.x, views[2].Position.z), new Vector2(300f, 300f)), 40f, "dağ tepesi en yüksek nokta");
            Assert.Less(Vector2.Distance(new Vector2(views[0].LookAt.x, views[0].LookAt.z), new Vector2(10f, 20f)), 1f, "köy merkezine bakar");

            // Göl: kıyıdan bakış su (en alçak nokta) üzerinden geçip karşı tepelere uzanır.
            var gPos = new Vector2(views[4].Position.x, views[4].Position.z);
            var gLook = new Vector2(views[4].LookAt.x, views[4].LookAt.z);
            var lowPt = new Vector2(-300f, -250f);
            Assert.Greater(Vector2.Dot((gLook - gPos).normalized, (lowPt - gPos).normalized), 0.85f, "göl: bakış su üzerinden");
            Assert.Greater(Vector2.Distance(gPos, gLook), Vector2.Distance(gPos, lowPt), "göl: hedef suyun ötesinde (tepeler)");
        }

        [Test]
        public void KoyMerkezi_SinirDisindan_Yuksekten()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Kuzgun Köyü", Center = new Vector2(10f, 20f), Radius = 60f, IsMajor = true }
            };
            System.Func<float, float, float> h = (x, z) => 30f;
            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h);
            var p = new Vector2(views[0].Position.x, views[0].Position.z);
            var d = Vector2.Distance(p, new Vector2(10f, 20f)) - 60f; // yerleşim sınırının dışına uzaklık
            Assert.Greater(d, 24.9f, "köy kamerası sınırın en az 25 m dışında");
            Assert.Less(d, 35.1f, "köy kamerası sınırın en çok 35 m dışında");
            Assert.Greater(views[0].Position.y - 30f, 7.9f, "kamera zeminden en az 8 m yukarıda");
            Assert.Less(views[0].Position.y - 30f, 12.1f, "kamera zeminden en çok 12 m yukarıda");
        }

        [Test]
        public void AgacYakininda_FarkliAciSecilir()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Köy", Center = Vector2.zero, Radius = 50f, IsMajor = true }
            };
            System.Func<float, float, float> h = (x, z) => 30f;
            // Güneybatı çeyreği tamamen ağaçlı: varsayılan 225° açısı reddedilmeli.
            System.Func<Vector3, bool> treeNear = pos => pos.x < 0f && pos.z < 0f;
            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h, null, null, treeNear);
            var p = new Vector2(views[0].Position.x, views[0].Position.z);
            Assert.IsFalse(p.x < 0f && p.y < 0f, "köy kamerası ağaçsız açıya kaydı");
            Assert.AreEqual(80f, p.magnitude, 1.5f, "sınır dışı mesafe korunur");
        }

        [Test]
        public void EngelliBakis_YukariVeGeriKayar()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Köy", Center = Vector2.zero, Radius = 50f, IsMajor = true }
            };
            System.Func<float, float, float> h = (x, z) => 30f;
            // Köy kamerası (başlangıç y=40) 47 m altındayken engelli: iki kez 4 m yukarı+geri kaymalı.
            System.Func<Vector3, Vector3, bool> blocked = (pos, look) => pos.y < 47f && look.y < 40f;
            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h, null, blocked, null);
            Assert.AreEqual(48f, views[0].Position.y, 0.1f, "iki itmede y 40→48");
            var p = new Vector2(views[0].Position.x, views[0].Position.z);
            Assert.AreEqual(88f, p.magnitude, 0.5f, "geriye 2×4 m: 80→88");
        }

        [Test]
        public void Yol_AnaYolUzerinde_VirajBoyunca()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Köy", Center = Vector2.zero, Radius = 40f, IsMajor = true }
            };
            System.Func<float, float, float> h = (x, z) => 30f;
            var road = new RoadSpec { Name = "Ana Yol", Kind = RoadKind.Asphalt };
            for (var x = -200f; x <= 200f; x += 10f)
                road.Points.Add(new Vector2(x, 0f));
            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h, new[] { road }, null, null);
            var v = views[3];
            Assert.AreEqual(2f, v.Position.y - 30f, 0.1f, "yol kamerası yolun 2 m üstünde");
            Assert.Less(Mathf.Abs(v.Position.z), 1f, "kamera yol üzerinde");
            Assert.Less(Mathf.Abs(v.LookAt.z), 1f, "bakış yol üzerinde");
            var ahead = v.LookAt.x - v.Position.x;
            Assert.Greater(ahead, 50f, "yol boyunca ileriye bakar");
            Assert.Less(ahead, 90f, "bakış ~70 m ileride");
        }

        [Test]
        public void OrmanKenari_AcikAlanda_HatBoyunca()
        {
            var locs = new List<NamedLocation>
            {
                new NamedLocation { Name = "Köy", Center = Vector2.zero, Radius = 40f, IsMajor = true },
                new NamedLocation { Name = "Çam Sırtı", Center = new Vector2(200f, 200f), Radius = 100f }
            };
            System.Func<float, float, float> h = (x, z) => 30f;
            var views = OtoEkranViewpoints.Build(Vector2.zero, 512f, 18f, locs, h);
            var p = new Vector2(views[1].Position.x, views[1].Position.z);
            Assert.AreEqual(110f, Vector2.Distance(p, new Vector2(200f, 200f)), 1f, "ağaç hattından 10 m açıkta");
            var radial = (p - new Vector2(200f, 200f)).normalized;
            var look = (new Vector2(views[1].LookAt.x, views[1].LookAt.z) - p).normalized;
            Assert.Less(Mathf.Abs(Vector2.Dot(radial, look)), 0.1f, "bakış ağaç hattı boyunca (teğet)");
        }

        [Test]
        public void Viewpoints_NoLocationsNoWater_StillBuilds()
        {
            var views = OtoEkranViewpoints.Build(Vector2.zero, 100f, -50f, null, null);
            Assert.AreEqual(OtoEkranViewpoints.Count, views.Count);
        }
    }
}
#endif
