#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Presentation.UI;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RadioChatterTests
    {
        private const string Json = "{\"lines\":[" +
            "{\"key\":\"contact_front\",\"role\":\"Rifleman\",\"text\":\"Temas!\",\"tone\":\"acil\",\"dur\":1.2}," +
            "{\"key\":\"contact_left\",\"role\":\"Marksman\",\"text\":\"Sol!\",\"tone\":\"acil\",\"dur\":1.2}," +
            "{\"key\":\"reload_mag\",\"role\":\"Rifleman\",\"text\":\"Sarjor!\",\"tone\":\"sakin\",\"dur\":1}]}";

        [System.Serializable]
        private sealed class JsonProbe { public int x = 1; }

        /// <summary>JsonUtility yerel köprüsü yoksa (Unity dışı doğrulayıcı) true döner; testler atlanır.</summary>
        private static bool NativeJsonMissing()
        {
            try
            {
                UnityEngine.JsonUtility.ToJson(new JsonProbe());
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return true;
            }
        }

        [Test]
        public void Book_ParsesAndPicksByKeyAndRole()
        {
            if (NativeJsonMissing()) Assert.Ignore("JsonUtility yerel köprüsü yok");
            var book = RadioLineBook.FromJson(Json);
            Assert.AreEqual(3, book.Count);
            Assert.AreEqual("Temas!", book.Pick("contact_front", "Rifleman", 0f).text);
            Assert.IsNull(book.Pick("contact_front", "Marksman", 0f));
            Assert.AreEqual("Temas!", book.Pick("contact_front", "Marksman", 0f, true).text);
        }

        [Test]
        public void Book_PrefixMatchesWholeSegmentOnly()
        {
            if (NativeJsonMissing()) Assert.Ignore("JsonUtility yerel köprüsü yok");
            var book = RadioLineBook.FromJson(Json);
            Assert.IsNotNull(book.Pick("contact", null, 0.9f));
            Assert.IsNull(book.Pick("conta", null, 0f));
            Assert.IsTrue(book.Has("reload", "Rifleman"));
        }

        [Test]
        public void Book_BadJsonIsEmpty()
        {
            Assert.AreEqual(0, RadioLineBook.FromJson("not json").Count);
            Assert.AreEqual(0, RadioLineBook.FromJson(null).Count);
        }

        [Test]
        public void Scheduler_CategoryCooldownBlocksRepeat()
        {
            var s = new RadioScheduler();
            Assert.IsTrue(s.TryAccept(10f, RadioPriority.Normal, "contact", 8f, 3f));
            Assert.IsFalse(s.TryAccept(14f, RadioPriority.Normal, "contact", 8f, 3f));
            Assert.IsTrue(s.TryAccept(19f, RadioPriority.Normal, "contact", 8f, 3f));
        }

        [Test]
        public void Scheduler_BusyLineOnlyInterruptedByHigherPriority()
        {
            var s = new RadioScheduler();
            Assert.IsTrue(s.TryAccept(0f, RadioPriority.Normal, "a", 1f, 3f));
            Assert.IsFalse(s.TryAccept(1f, RadioPriority.Normal, "b", 1f, 3f));
            Assert.IsTrue(s.TryAccept(1f, RadioPriority.High, "c", 1f, 3f));
        }

        [Test]
        public void Scheduler_LowPriorityWaitsQuietGap()
        {
            var s = new RadioScheduler();
            Assert.IsTrue(s.TryAccept(0f, RadioPriority.Normal, "a", 1f, 3f));
            Assert.IsFalse(s.TryAccept(4f, RadioPriority.Low, "b", 1f, 3f));
            Assert.IsTrue(s.TryAccept(7.5f, RadioPriority.Low, "b", 1f, 3f));
        }

        [Test]
        public void Direction_MapsRelativeBearing()
        {
            Assert.AreEqual("contact_front", RadioChatterSystem.DirectionKey(0f, 10f));
            Assert.AreEqual("contact_right", RadioChatterSystem.DirectionKey(0f, 90f));
            Assert.AreEqual("contact_left", RadioChatterSystem.DirectionKey(0f, 270f));
            Assert.AreEqual("contact_rear", RadioChatterSystem.DirectionKey(350f, 170f));
        }

        [Test]
        public void Subtitle_FormatsSpeakerAndText()
        {
            Assert.AreEqual("[Yzb. Ali]: Temas!", RadioSubtitleView.Format("Yzb. Ali", "Temas!"));
            Assert.AreEqual("Temas!", RadioSubtitleView.Format("", "Temas!"));
            Assert.AreEqual(string.Empty, RadioSubtitleView.Format("x", null));
        }
    }
}
#endif
