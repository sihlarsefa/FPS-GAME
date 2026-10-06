using NUnit.Framework;
using Project.Core.Domain;
using Project.Presentation.UI.Lobby.Squad;

namespace Project.Tests
{
    public sealed class SquadRosterTests
    {
        private static SquadMember M(string id, MilitaryRank r = MilitaryRank.Er, bool local = false) => new SquadMember(id, id, r, local);

        [Test]
        public void YerelOyuncu_Slot0a_Yerlesir()
        {
            var r = new SquadRoster();
            Assert.IsTrue(r.TryAdd(M("a")));
            Assert.IsTrue(r.TryAdd(M("me", MilitaryRank.Er, true)));
            Assert.AreEqual("me", r[0].Id);
        }

        [Test]
        public void DortSlotDolunca_EklemeReddedilir()
        {
            var r = new SquadRoster();
            for (var i = 0; i < 4; i++) Assert.IsTrue(r.TryAdd(M("p" + i)));
            Assert.IsFalse(r.TryAdd(M("x")));
            Assert.IsTrue(r.IsFull);
        }

        [Test]
        public void AyniKimlik_Reddedilir()
        {
            var r = new SquadRoster();
            Assert.IsTrue(r.TryAdd(M("a")));
            Assert.IsFalse(r.TryAdd(M("a")));
        }

        [Test]
        public void LiderAyrilinca_EnKidemliDevralir()
        {
            var r = new SquadRoster();
            r.TryAdd(M("a", MilitaryRank.Er));
            r.TryAdd(M("b", MilitaryRank.Cavus));
            r.TryAdd(M("c", MilitaryRank.Yuzbasi));
            Assert.AreEqual("a", r.Leader.Id);
            r.Remove("a");
            Assert.AreEqual("c", r.Leader.Id);
            Assert.AreEqual(2, r.MemberCount);
        }

        [Test]
        public void LiderHazirIsaretlenmez_BaslatmaKurali()
        {
            var r = new SquadRoster();
            r.TryAdd(M("a"));
            r.TryAdd(M("b"));
            Assert.IsFalse(r.SetReady("a", true));
            Assert.IsFalse(r.CanStart());
            Assert.AreEqual(1, r.WaitingNames().Count);
            r.SetReady("b", true);
            Assert.IsTrue(r.CanStart());
        }

        [Test]
        public void TekKisi_BaslatabilirBosTimBaslatamaz()
        {
            var r = new SquadRoster();
            Assert.IsFalse(r.CanStart());
            r.TryAdd(M("a"));
            Assert.IsTrue(r.CanStart());
        }

        [Test]
        public void Davet_ZamanAsimindaDuser()
        {
            var r = new SquadRoster();
            Assert.IsTrue(r.Invite(2, "Ali"));
            Assert.IsFalse(r.Invite(2, "Veli"));
            Assert.AreEqual(SquadSlotState.Invited, r.StateOf(2));
            Assert.AreEqual(0, r.Tick(10f));
            Assert.AreEqual(1, r.Tick(25f));
            Assert.AreEqual(SquadSlotState.Empty, r.StateOf(2));
        }

        [Test]
        public void Susturma_KonusmayiKapatir()
        {
            var r = new SquadRoster();
            r.TryAdd(M("a"));
            r.SetSpeaking("a", true);
            Assert.IsTrue(r[0].Speaking);
            r.SetMuted("a", true);
            Assert.IsFalse(r[0].Speaking);
        }

        [Test]
        public void Ozet_BekleyenSayisiniGosterir()
        {
            var r = new SquadRoster();
            r.TryAdd(M("a"));
            r.TryAdd(M("b"));
            r.TryAdd(M("c"));
            Assert.AreEqual("TİM 3/4  ·  2 BEKLENİYOR", r.SummaryText());
            r.SetReady("b", true);
            r.SetReady("c", true);
            Assert.AreEqual("TİM 3/4  ·  HAZIR", r.SummaryText());
        }

        [Test]
        public void Degisim_OlayiTetiklenir()
        {
            var r = new SquadRoster();
            var n = 0;
            r.Changed += () => n++;
            r.TryAdd(M("a"));
            r.Invite(1, "x");
            Assert.Greater(n, 1);
        }
    }
}
