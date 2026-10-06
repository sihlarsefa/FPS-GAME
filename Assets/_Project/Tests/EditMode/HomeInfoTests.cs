using System;
using NUnit.Framework;
using Project.Presentation.UI.Lobby.Home;

namespace Project.Tests.EditMode
{
    public sealed class HomeInfoTests
    {
        private static DateTime Utc(int y, int m, int d, int h = 0, int mi = 0) => new DateTime(y, m, d, h, mi, 0, DateTimeKind.Utc);

        [Test]
        public void Carousel_AdvancesAfterInterval()
        {
            var c = new HomeCarouselModel(3, 7f, 0.4f);
            Assert.IsFalse(c.Tick(6.5f));
            Assert.AreEqual(0, c.Index);
            Assert.IsTrue(c.Tick(1f));
            Assert.AreEqual(1, c.Index);
            Assert.AreEqual(0, c.Previous);
        }

        [Test]
        public void Carousel_WrapsAround()
        {
            var c = new HomeCarouselModel(2, 1f, 0.1f);
            c.Tick(1.1f);
            c.Tick(1.1f);
            Assert.AreEqual(0, c.Index);
        }

        [Test]
        public void Carousel_PausedDoesNotAdvance()
        {
            var c = new HomeCarouselModel(3, 1f, 0.1f);
            c.Paused = true;
            Assert.IsFalse(c.Tick(10f));
            Assert.AreEqual(0, c.Index);
        }

        [Test]
        public void Carousel_ManualSelectGivesGrace()
        {
            var c = new HomeCarouselModel(4, 7f, 0.4f);
            c.Select(2);
            Assert.AreEqual(2, c.Index);
            Assert.IsFalse(c.Tick(8f));      // 7 + 3 = 10 sn dolmadan donmez
            Assert.IsTrue(c.Tick(3f));
            Assert.AreEqual(3, c.Index);
        }

        [Test]
        public void Carousel_SelectNegativeWraps()
        {
            var c = new HomeCarouselModel(3);
            c.Select(-1);
            Assert.AreEqual(2, c.Index);
        }

        [Test]
        public void Carousel_BlendCrossfades()
        {
            var c = new HomeCarouselModel(2, 1f, 1f);
            c.Tick(1.01f);
            c.Paused = true; // gecis sirasinda yeni donus tetiklenmesin
            Assert.IsTrue(c.InTransition);
            Assert.AreEqual(1f, c.AlphaOf(0) + c.AlphaOf(1), 0.001f);
            c.Tick(0.5f);
            Assert.AreEqual(1f, c.AlphaOf(0) + c.AlphaOf(1), 0.001f);
            c.Tick(1f);
            Assert.IsFalse(c.InTransition);
            Assert.AreEqual(1f, c.AlphaOf(1), 0.001f);
            Assert.AreEqual(0f, c.AlphaOf(0), 0.001f);
        }

        [Test]
        public void Carousel_SingleOrEmptyNeverAdvances()
        {
            var one = new HomeCarouselModel(1, 1f, 0.1f);
            Assert.IsFalse(one.Tick(5f));
            var none = new HomeCarouselModel(0);
            Assert.IsFalse(none.Tick(5f));
            none.Select(3);
            Assert.AreEqual(0, none.Index);
        }

        [Test]
        public void Carousel_ProgressClamped()
        {
            var c = new HomeCarouselModel(3, 4f, 0.1f);
            c.Tick(2f);
            Assert.AreEqual(0.5f, c.Progress, 0.001f);
            c.Select(1);
            Assert.AreEqual(0f, c.Progress, 0.001f);
        }

        [Test]
        public void Season_FirstWindowStartsAtAnchor()
        {
            var w = HomeCalendar.SeasonAt(Utc(2026, 10, 6));
            Assert.AreEqual(1, w.Number);
            Assert.AreEqual(HomeCalendar.SeasonAnchorUtc, w.StartUtc);
            Assert.AreEqual(Utc(2026, 11, 26), w.EndUtc);
        }

        [Test]
        public void Season_RollsOverAndNeverNegative()
        {
            Assert.AreEqual(2, HomeCalendar.SeasonAt(Utc(2026, 11, 27)).Number);
            Assert.AreEqual(1, HomeCalendar.SeasonAt(Utc(2020, 1, 1)).Number);
        }

        [Test]
        public void Season_ElapsedFraction()
        {
            var w = HomeCalendar.SeasonAt(Utc(2026, 10, 29));
            Assert.AreEqual(0.5f, w.Elapsed01(Utc(2026, 10, 29)), 0.01f);
            Assert.AreEqual(TimeSpan.Zero, w.Remaining(w.EndUtc.AddDays(1)));
        }

        [Test]
        public void Weekend_ActiveOnSaturday()
        {
            var e = HomeCalendar.WeekendEventAt(Utc(2026, 10, 10, 12)); // Cumartesi
            Assert.IsTrue(e.Active);
            Assert.AreEqual(Utc(2026, 10, 12), e.EndUtc);
        }

        [Test]
        public void Weekend_FridayBefore18CountsDownToStart()
        {
            var now = Utc(2026, 10, 9, 12); // Cuma 12:00
            var e = HomeCalendar.WeekendEventAt(now);
            Assert.IsFalse(e.Active);
            Assert.AreEqual(Utc(2026, 10, 9, 18), e.StartUtc);
            Assert.AreEqual(6.0, e.Remaining(now).TotalHours, 0.001);
        }

        [Test]
        public void Weekend_TuesdayPointsToNextFriday()
        {
            var e = HomeCalendar.WeekendEventAt(Utc(2026, 10, 6, 9)); // Sali
            Assert.IsFalse(e.Active);
            Assert.AreEqual(Utc(2026, 10, 9, 18), e.StartUtc);
        }

        [Test]
        public void Weekend_MondayMidnightIsOver()
        {
            var e = HomeCalendar.WeekendEventAt(Utc(2026, 10, 12));
            Assert.IsFalse(e.Active);
            Assert.AreEqual(Utc(2026, 10, 16, 18), e.StartUtc);
        }

        [Test]
        public void Format_PicksUnitsByMagnitude()
        {
            Assert.AreEqual("3G 04S", HomeCalendar.FormatRemaining(new TimeSpan(3, 4, 30, 10)));
            Assert.AreEqual("05S 07D", HomeCalendar.FormatRemaining(new TimeSpan(5, 7, 59)));
            Assert.AreEqual("12D 05SN", HomeCalendar.FormatRemaining(new TimeSpan(0, 12, 5)));
            Assert.AreEqual("00D 00SN", HomeCalendar.FormatRemaining(TimeSpan.FromSeconds(-9)));
        }

        [Test]
        public void Urgent_OnlyInLastHour()
        {
            Assert.IsTrue(HomeCalendar.IsUrgent(TimeSpan.FromMinutes(59)));
            Assert.IsFalse(HomeCalendar.IsUrgent(TimeSpan.FromMinutes(61)));
            Assert.IsFalse(HomeCalendar.IsUrgent(TimeSpan.Zero));
        }
    }
}
