using System;
using NUnit.Framework;
using Project.Online.Backend;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class BackendJsonTests
    {
        [Test]
        public void ParseAuthResult_RoundTripFields()
        {
            var playerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var expires = new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero);
            var json =
                "{"
                + "\"accessToken\":\"acc-1\","
                + "\"refreshToken\":\"ref-1\","
                + "\"expiresAt\":\"" + expires.ToString("o") + "\","
                + "\"player\":{"
                + "\"id\":\"" + playerId + "\","
                + "\"username\":\"Komutan\","
                + "\"email\":\"k@test.local\","
                + "\"rank\":3,"
                + "\"eloRating\":1200,"
                + "\"stats\":{\"matches\":10,\"wins\":4,\"kills\":55,\"experience\":900}"
                + "}"
                + "}";

            var result = BackendJson.ParseAuthResult(json);
            Assert.AreEqual("acc-1", result.AccessToken);
            Assert.AreEqual("ref-1", result.RefreshToken);
            Assert.AreEqual(expires, result.ExpiresAt);
            Assert.IsNotNull(result.Player);
            Assert.AreEqual(playerId, result.Player.Id);
            Assert.AreEqual("Komutan", result.Player.Username);
            Assert.AreEqual(1200, result.Player.EloRating);
            Assert.IsNotNull(result.Player.Stats);
            Assert.AreEqual(55, result.Player.Stats.Kills);
            Assert.AreEqual(900, result.Player.Stats.Experience);
        }

        [Test]
        public void ParseSquadInfo_MembersAndInvite()
        {
            var squadId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var leader = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var m1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var json =
                "{"
                + "\"id\":\"" + squadId + "\","
                + "\"name\":\"Aslan\","
                + "\"leaderId\":\"" + leader + "\","
                + "\"memberIds\":[\"" + leader + "\",\"" + m1 + "\"],"
                + "\"readyMemberIds\":[\"" + leader + "\"],"
                + "\"inviteCode\":\"ABCD12\","
                + "\"region\":\"tr\","
                + "\"openSlots\":8,"
                + "\"allReady\":false"
                + "}";

            var squad = BackendJson.ParseSquadInfo(json);
            Assert.AreEqual(squadId, squad.Id);
            Assert.AreEqual("Aslan", squad.Name);
            Assert.AreEqual(leader, squad.LeaderId);
            Assert.AreEqual(2, squad.MemberIds.Count);
            Assert.AreEqual(m1, squad.MemberIds[1]);
            Assert.AreEqual(1, squad.ReadyMemberIds.Count);
            Assert.AreEqual("ABCD12", squad.InviteCode);
            Assert.AreEqual(8, squad.OpenSlots);
            Assert.IsFalse(squad.AllReady);
        }

        [Test]
        public void ParseQueueInfo_AndLeaderboardRow()
        {
            var ticket = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var at = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
            var qJson =
                "{\"ticketId\":\"" + ticket + "\",\"status\":\"Queued\",\"region\":\"tr\",\"enqueuedAt\":\"" + at.ToString("o") + "\"}";
            var q = BackendJson.ParseQueueInfo(qJson);
            Assert.AreEqual(ticket, q.TicketId);
            Assert.AreEqual("Queued", q.Status);
            Assert.AreEqual(at, q.EnqueuedAt);

            var pid = Guid.Parse("55555555-5555-5555-5555-555555555555");
            var row = BackendJson.ParseLeaderboardRow(
                "{\"rank\":1,\"playerId\":\"" + pid + "\",\"username\":\"Alpha\",\"militaryRank\":5,\"value\":12000,\"elo\":1500}");
            Assert.AreEqual(1, row.Rank);
            Assert.AreEqual(pid, row.PlayerId);
            Assert.AreEqual("Alpha", row.Username);
            Assert.AreEqual(12000, row.Value);
            Assert.AreEqual(1500, row.Elo);
        }

        [Test]
        public void BuildBodies_EscapeQuotes()
        {
            var login = BackendJson.BuildLoginBody("a\"b", "p\\w");
            Assert.IsTrue(login.Contains("\"username\":\"a\\\"b\""));
            Assert.IsTrue(login.Contains("\"password\":\"p\\\\w\""));

            var reg = BackendJson.BuildRegisterBody("u", "e@x.com", "pw", "tr");
            Assert.IsTrue(reg.Contains("\"email\":\"e@x.com\""));
            Assert.IsTrue(reg.Contains("\"region\":\"tr\""));

            var refresh = BackendJson.BuildRefreshBody("tok");
            Assert.AreEqual("{\"refreshToken\":\"tok\"}", refresh);

            var squad = BackendJson.BuildCreateSquadBody("Tim", "tr");
            Assert.IsTrue(squad.Contains("\"name\":\"Tim\""));

            var join = BackendJson.BuildJoinSquadBody("XYZ");
            Assert.AreEqual("{\"inviteCode\":\"XYZ\"}", join);

            var queue = BackendJson.BuildQueueBody("tr", 80);
            Assert.IsTrue(queue.Contains("\"region\":\"tr\""));
            Assert.IsTrue(queue.Contains("\"maxPingMs\":80"));
        }

        [Test]
        public void Escape_HandlesControlChars()
        {
            Assert.AreEqual("a\\nb", BackendJson.Escape("a\nb"));
            Assert.AreEqual("", BackendJson.Escape(null));
            Assert.AreEqual("\"hi\"", BackendJson.Quote("hi"));
        }
    }
}
