using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Dialogue;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class DialogueV2Tests
    {
        private const string MiniCsv =
            "id,text,stress,category,voiceHint\n" +
            "open_cat_01,Temas!,catisma,part_open,sert\n" +
            "open_cat_02,Düşman!,catisma,part_open,sert\n" +
            "open_sak_01,Temas.,sakin,part_open,\n" +
            "open_pan_01,TEMAS TEMAS!,panik,part_open,\n" +
            "clk_03,Saat üç yönü,any,part_clock,\n" +
            "clks_03,Saat üç,panik,part_clock,\n" +
            "clk_12,Saat on iki yönü,any,part_clock,\n" +
            "tgt_piyade,düşman piyade,any,part_target,\n" +
            "tail_t_dikkat,Dikkat!,any,part_tail,\n" +
            "contact_full_cat_01,\"Temas! Saat üç yönü, yüz metre, düşman piyade!\",catisma,contact_full,\n" +
            "reload_cat_01,Yeniden dolduruyorum!,catisma,reload,\n" +
            "reload_cat_02,Şarjör değişiyor!,catisma,reload,\n" +
            "reload_cat_03,Dolduruyorum!,catisma,reload,\n";

        private static Func<float> Rolls(params float[] values)
        {
            var i = 0;
            return () => values[(i++) % values.Length];
        }

        // ---- sayılar / yön ----
        [Test]
        public void Numbers_ToWords()
        {
            Assert.AreEqual("yüz", TurkishNumbers.ToWords(100));
            Assert.AreEqual("yüz elli", TurkishNumbers.ToWords(150));
            Assert.AreEqual("iki yüz elli", TurkishNumbers.ToWords(250));
            Assert.AreEqual("bin", TurkishNumbers.ToWords(1000));
            Assert.AreEqual("bin iki yüz", TurkishNumbers.ToWords(1200));
            Assert.AreEqual("yirmi beş", TurkishNumbers.ToWords(25));
        }

        [Test]
        public void Numbers_PartIds()
        {
            var ids = new List<string>();
            TurkishNumbers.ToPartIds(350, ids);
            Assert.AreEqual(3, ids.Count);
            Assert.AreEqual("num_3", ids[0]);
            Assert.AreEqual("num_100", ids[1]);
            Assert.AreEqual("num_50", ids[2]);
            ids.Clear();
            TurkishNumbers.ToPartIds(100, ids);
            Assert.AreEqual(1, ids.Count);
            Assert.AreEqual("num_100", ids[0]);
        }

        [Test]
        public void Numbers_ClockHour()
        {
            Assert.AreEqual(12, TurkishNumbers.ClockHour(0f));
            Assert.AreEqual(3, TurkishNumbers.ClockHour(90f));
            Assert.AreEqual(9, TurkishNumbers.ClockHour(-90f));
            Assert.AreEqual(6, TurkishNumbers.ClockHour(180f));
            Assert.AreEqual(6, TurkishNumbers.ClockHour(-180f));
            Assert.AreEqual(1, TurkishNumbers.ClockHour(31f));
        }

        [Test]
        public void Numbers_RelativeAngleWraps()
        {
            Assert.AreEqual(-10f, TurkishNumbers.RelativeAngle(350f, 340f), 0.01f);
            Assert.AreEqual(20f, TurkishNumbers.RelativeAngle(350f, 10f), 0.01f);
        }

        [Test]
        public void Numbers_RoundDistance()
        {
            Assert.AreEqual(100, TurkishNumbers.RoundDistance(98f));
            Assert.AreEqual(250, TurkishNumbers.RoundDistance(243f));
            Assert.AreEqual(5, TurkishNumbers.RoundDistance(1f));
            Assert.AreEqual(2000, TurkishNumbers.RoundDistance(9999f));
        }

        // ---- kitap ----
        [Test]
        public void Book_ParsesQuotedCsvAndStress()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            Assert.AreEqual(13, book.Count);
            Assert.AreEqual("Temas! Saat üç yönü, yüz metre, düşman piyade!", book.Get("contact_full_cat_01").Text);
            Assert.AreEqual(DialogueStress.Combat, book.Get("open_cat_01").Stress.Value);
            Assert.IsFalse(book.Get("clk_03").Stress.HasValue);
        }

        [Test]
        public void Book_CollectFallsBackToNeighbourStress()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            var list = new List<DialogueLine>();
            Assert.AreEqual(3, book.Collect("reload", DialogueStress.Panic, list));
            Assert.AreEqual(1, book.Collect("part_open", DialogueStress.Calm, list));
        }

        // ---- tekrar önleme ----
        [Test]
        public void Memory_DoesNotRepeatRecentVariants()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            var list = new List<DialogueLine>();
            book.Collect("reload", DialogueStress.Combat, list);
            var mem = new BarkMemory(2, 2);
            var a = mem.Pick("reload", 1, list, 0.1f);
            var b = mem.Pick("reload", 1, list, 0.1f);
            Assert.AreNotEqual(a.Id, b.Id);
            var c = mem.Pick("reload", 1, list, 0.1f);
            Assert.AreNotEqual(a.Id, c.Id);
            Assert.AreNotEqual(b.Id, c.Id);
        }

        [Test]
        public void Memory_RelaxesWhenAllBlocked()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            var list = new List<DialogueLine>();
            book.Collect("part_open", DialogueStress.Calm, list);
            var mem = new BarkMemory();
            Assert.NotNull(mem.Pick("part_open", 1, list, 0.5f));
            Assert.NotNull(mem.Pick("part_open", 1, list, 0.5f));
        }

        // ---- kompozisyon ----
        [Test]
        public void Composer_BuildsClockDistanceTarget()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            var p = PhraseComposer.ComposeSpotted(book, new BarkMemory(), 1, DialogueStress.Combat, 90f, 98f, "tgt_piyade", Rolls(0.99f));
            Assert.NotNull(p);
            Assert.AreEqual(3, p.ClockHour);
            Assert.AreEqual(100, p.Meters);
            Assert.IsTrue(p.Text.Contains("Saat üç yönü, yüz metre, düşman piyade!"), p.Text);
            Assert.AreEqual("num_100", p.PartIds[p.PartIds.IndexOf("unit_metre") - 1]);
            Assert.AreEqual("tgt_piyade", p.PartIds[p.PartIds.Count - 1]);
            Assert.AreEqual("contact_full_cat_01", p.FallbackLineId);
        }

        [Test]
        public void Composer_PanicUsesShortClock()
        {
            var book = DialogueLineBook.FromCsv(MiniCsv);
            var p = PhraseComposer.ComposeSpotted(book, null, 1, DialogueStress.Panic, 90f, 40f, "tgt_piyade", Rolls(0.1f));
            Assert.NotNull(p);
            Assert.IsTrue(p.PartIds.Contains("clks_03"));
        }

        [Test]
        public void Composer_MissingPartsReturnsNull()
        {
            var book = DialogueLineBook.FromCsv("id,text,stress,category,voiceHint\nopen_cat_01,Temas!,catisma,part_open,\n");
            Assert.IsNull(PhraseComposer.ComposeSpotted(book, null, 1, DialogueStress.Combat, 0f, 50f, null, Rolls(0.5f)));
        }

        // ---- rütbe ----
        [Test]
        public void Rank_PrivateAnswersKomutanim()
        {
            Assert.AreEqual(DialogueCats.AckEnlisted, RankReplies.AckCategory(MilitaryRank.Er, MilitaryRank.Yuzbasi));
            Assert.AreEqual(DialogueCats.AckNco, RankReplies.AckCategory(MilitaryRank.UzmanCavus, MilitaryRank.Ustegmen));
            Assert.AreEqual(DialogueCats.AckSuperior, RankReplies.AckCategory(MilitaryRank.Yuzbasi, MilitaryRank.Er));
            Assert.AreEqual(DialogueCats.AckPeer, RankReplies.AckCategory(MilitaryRank.Er, MilitaryRank.Er));
            Assert.IsTrue(RankReplies.UsesKomutanim(MilitaryRank.Er, MilitaryRank.Cavus));
            Assert.IsFalse(RankReplies.UsesKomutanim(MilitaryRank.Yuzbasi, MilitaryRank.Er));
        }

        // ---- stres ----
        [Test]
        public void Stress_HeatRaisesStateAndDecays()
        {
            var t = new StressTracker();
            t.Tick(0.1f, 1f);
            Assert.AreEqual(DialogueStress.Calm, t.Current);
            t.AddHeat(0.5f);
            t.Tick(0.1f, 1f);
            Assert.AreEqual(DialogueStress.Combat, t.Current);
            t.AddHeat(1f);
            t.Tick(0.1f, 1f);
            Assert.AreEqual(DialogueStress.Panic, t.Current);
            for (var i = 0; i < 400; i++)
                t.Tick(0.1f, 1f);
            Assert.AreEqual(DialogueStress.Calm, t.Current);
        }

        [Test]
        public void Stress_LowHealthWithFirePanics()
        {
            var t = new StressTracker();
            t.AddHeat(0.5f);
            t.Tick(0.1f, 0.1f);
            Assert.AreEqual(DialogueStress.Panic, t.Current);
        }

        [Test]
        public void Stress_DownshiftHasDwell()
        {
            var t = new StressTracker();
            t.AddHeat(1f);
            t.Tick(0.1f, 1f);
            Assert.AreEqual(DialogueStress.Panic, t.Current);
            // ısı hemen sıfırlansa bile en az 3 sn panikte kalır
            t.Reset();
            t.AddHeat(1f);
            t.Tick(0.1f, 1f);
            t.Tick(0.5f, 1f);
            Assert.AreEqual(DialogueStress.Panic, t.Current);
        }

        // ---- ses kimlikleri ----
        [Test]
        public void Voices_EightDistinctAndConsistent()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < VoiceIdentities.Count; i++)
            {
                var v = VoiceIdentities.Get(i);
                Assert.AreEqual(i, v.VoiceId);
                seen.Add(v.Pitch + "/" + v.Formant + "/" + v.Speed);
            }

            Assert.AreEqual(8, seen.Count);
            Assert.AreEqual(VoiceIdentities.PreferredVoice(42), VoiceIdentities.PreferredVoice(42));
        }

        [Test]
        public void Voices_AssignerGivesUniqueVoicesInSquad()
        {
            var a = new VoiceAssigner();
            var used = new HashSet<int>();
            for (var id = 100; id < 108; id++)
                used.Add(a.Assign(id));
            Assert.AreEqual(8, used.Count);
            Assert.AreEqual(a.Assign(103), a.Assign(103));
        }

        [Test]
        public void Voices_PanicRaisesPitchAndSpeed()
        {
            var v = VoiceIdentities.Get(3);
            var calm = v.ForStress(DialogueStress.Calm);
            var panic = v.ForStress(DialogueStress.Panic);
            Assert.Greater(panic.Pitch, calm.Pitch);
            Assert.Greater(panic.Speed, calm.Speed);
            Assert.Greater(panic.Intensity, calm.Intensity);
        }

        // ---- yönlendirme ----
        [Test]
        public void Router_NearShoutsFarRadios()
        {
            var near = ProximityRouter.Route(10f, DialogueStress.Combat, false, true, true);
            Assert.AreEqual(DialogueChannel.Shout, near.Channel);
            var far = ProximityRouter.Route(120f, DialogueStress.Combat, false, true, true);
            Assert.AreEqual(DialogueChannel.Radio, far.Channel);
            Assert.AreEqual(DialogueChannel.Radio, ProximityRouter.Route(20f, DialogueStress.Calm, false, true, true).Channel);
            Assert.AreEqual(DialogueChannel.Shout, ProximityRouter.Route(40f, DialogueStress.Panic, false, true, true).Channel);
        }

        [Test]
        public void Router_ShoutOnlyFarIsSilent_AndRadioRangeLimited()
        {
            Assert.AreEqual(DialogueChannel.None, ProximityRouter.Route(200f, DialogueStress.Combat, false, true, false).Channel);
            Assert.AreEqual(DialogueChannel.None, ProximityRouter.Route(5000f, DialogueStress.Combat, false, false, true).Channel);
            Assert.Less(ProximityRouter.SignalQuality(800f), ProximityRouter.SignalQuality(300f));
        }

        // ---- hakem ----
        [Test]
        public void Arbiter_HighPriorityIsNeverCut()
        {
            var a = new DialogueArbiter();
            var high = new DialogueRequest("h", "wounded", DialogueChannel.Radio, DialoguePriority.High, 2f, 1, 1f);
            Assert.AreEqual(ArbiterResult.Play, a.Request(0f, high));
            var crit = new DialogueRequest("c", "grenade_incoming", DialogueChannel.Radio, DialoguePriority.Critical, 1f, 2, 1f);
            // Critical, High'ı KESMEZ: sıraya girer.
            Assert.AreEqual(ArbiterResult.Queued, a.Request(0.5f, crit));
            Assert.AreEqual(0, a.Preempted.Count);
            Assert.IsFalse(a.TryDequeue(1f, out _));
            Assert.IsTrue(a.TryDequeue(2.1f, out var next));
            Assert.AreEqual("c", next.LineId);
        }

        [Test]
        public void Arbiter_NormalCanBePreemptedByHigh()
        {
            var a = new DialogueArbiter();
            Assert.AreEqual(ArbiterResult.Play, a.Request(0f, new DialogueRequest("n", "reload", DialogueChannel.Radio, DialoguePriority.Normal, 2f, 1, 1f)));
            Assert.AreEqual(ArbiterResult.Play, a.Request(0.3f, new DialogueRequest("h", "wounded", DialogueChannel.Radio, DialoguePriority.High, 1f, 2, 1f)));
            Assert.AreEqual(1, a.Preempted.Count);
            Assert.AreEqual(1, a.Preempted[0]);
        }

        [Test]
        public void Arbiter_LowPriorityRejectedWhenBusy_AndCategoryCooldown()
        {
            var a = new DialogueArbiter();
            a.Request(0f, new DialogueRequest("n", "reload", DialogueChannel.Radio, DialoguePriority.Normal, 2f, 1, 5f));
            Assert.AreEqual(ArbiterResult.Rejected, a.Request(0.5f, new DialogueRequest("x", "clear", DialogueChannel.Radio, DialoguePriority.Chatter, 1f, 2, 1f)));
            Assert.AreEqual(ArbiterResult.Rejected, a.Request(3f, new DialogueRequest("r2", "reload", DialogueChannel.Radio, DialoguePriority.Normal, 1f, 3, 5f)));
        }

        [Test]
        public void Arbiter_ShoutChannelAllowsTwoAtOnce()
        {
            var a = new DialogueArbiter();
            Assert.AreEqual(ArbiterResult.Play, a.Request(0f, new DialogueRequest("a", "c1", DialogueChannel.Shout, DialoguePriority.Normal, 2f, 1, 0f)));
            Assert.AreEqual(ArbiterResult.Play, a.Request(0.1f, new DialogueRequest("b", "c2", DialogueChannel.Shout, DialoguePriority.Normal, 2f, 2, 0f)));
            Assert.AreEqual(ArbiterResult.Rejected, a.Request(0.2f, new DialogueRequest("c", "c3", DialogueChannel.Shout, DialoguePriority.Normal, 2f, 3, 0f)));
        }

        [Test]
        public void Arbiter_DuckingFollowsPriority()
        {
            var a = new DialogueArbiter();
            Assert.AreEqual(1f, a.DuckTarget(0f), 0.001f);
            a.Request(0f, new DialogueRequest("c", "fx", DialogueChannel.Shout, DialoguePriority.Critical, 2f, 1, 0f));
            Assert.AreEqual(0.35f, a.DuckTarget(1f), 0.001f);
            Assert.AreEqual(1f, a.DuckTarget(3f), 0.001f);
            var s = new DuckSmoother();
            var g = s.Step(0.1f, 0.35f);
            Assert.Less(g, 0.8f);
        }

        // ---- DSP ----
        private static float Goertzel(float[] x, int start, int len, double freq, int sr)
        {
            var w = 2.0 * Math.PI * freq / sr;
            var c = 2.0 * Math.Cos(w);
            double s0, s1 = 0, s2 = 0;
            for (var i = 0; i < len; i++)
            {
                s0 = x[start + i] + c * s1 - s2;
                s2 = s1;
                s1 = s0;
            }

            var p = s1 * s1 + s2 * s2 - c * s1 * s2;
            return (float)Math.Sqrt(Math.Max(0.0, p)) / len;
        }

        private static float[] Tone(double f, int sr, float seconds, float amp)
        {
            var buf = new float[(int)(sr * seconds)];
            for (var i = 0; i < buf.Length; i++)
                buf[i] = amp * (float)Math.Sin(2.0 * Math.PI * f * i / sr);
            return buf;
        }

        private static RadioDspSettings Clean()
        {
            var s = RadioDspSettings.For(1f, DialogueStress.Calm, false);
            s.HissLevel = 0f; s.CarrierLevel = 0f; s.SquelchLeadSeconds = 0f; s.SquelchTailSeconds = 0f; s.DropoutRate = 0f; s.BattleBleed = 0f;
            return s;
        }

        [Test]
        public void RadioDsp_BandPassesVoiceBand()
        {
            const int sr = 22050;
            var s = Clean();
            var low = RadioDsp.Process(Tone(80, sr, 1f, 0.5f), sr, s, 1);
            var mid = RadioDsp.Process(Tone(1000, sr, 1f, 0.5f), sr, s, 1);
            var high = RadioDsp.Process(Tone(7000, sr, 1f, 0.5f), sr, s, 1);
            var pLow = Goertzel(low, sr / 4, sr / 2, 80, sr);
            var pMid = Goertzel(mid, sr / 4, sr / 2, 1000, sr);
            var pHigh = Goertzel(high, sr / 4, sr / 2, 7000, sr);
            Assert.Greater(pMid, pLow * 8f, "düşük kesilmeli");
            Assert.Greater(pMid, pHigh * 8f, "yüksek kesilmeli");
        }

        [Test]
        public void RadioDsp_AddsSquelchLeadAndTail()
        {
            const int sr = 22050;
            var s = Clean();
            s.SquelchLeadSeconds = 0.1f; s.SquelchTailSeconds = 0.2f; s.SquelchLevel = 0.3f;
            var input = Tone(1000, sr, 0.5f, 0.4f);
            var output = RadioDsp.Process(input, sr, s, 7);
            Assert.AreEqual(input.Length + (int)(0.1f * sr) + (int)(0.2f * sr), output.Length);
            var tailEnergy = 0f;
            for (var i = output.Length - (int)(0.15f * sr); i < output.Length; i++)
                tailEnergy += Math.Abs(output[i]);
            Assert.Greater(tailEnergy, 1f, "kapanış kuyruğu duyulmalı");
        }

        [Test]
        public void RadioDsp_DropoutOnlyAtLongRange()
        {
            Assert.AreEqual(0f, RadioDspSettings.For(1f, DialogueStress.Combat, false).DropoutRate, 0.0001f);
            Assert.Greater(RadioDspSettings.For(0.2f, DialogueStress.Combat, false).DropoutRate, 0.3f);
            const int sr = 22050;
            var input = Tone(1000, sr, 2f, 0.5f);
            var s = Clean();
            s.DropoutRate = 3f;
            var o = RadioDsp.Process(input, sr, s, 5);
            var clean = RadioDsp.Process(input, sr, Clean(), 5);
            var diff = 0f;
            for (var i = 0; i < o.Length; i++)
                diff += Math.Abs(o[i] - clean[i]);
            Assert.Greater(diff, 50f);
        }

        [Test]
        public void RadioDsp_BattleBleedAddsEnergyInSilence()
        {
            const int sr = 22050;
            var silence = new float[sr];
            var s = Clean();
            var quiet = RadioDsp.Process(silence, sr, s, 3);
            s.BattleBleed = 0.9f;
            var loud = RadioDsp.Process(silence, sr, s, 3);
            var eq = 0f; var el = 0f;
            for (var i = 0; i < quiet.Length; i++) { eq += Math.Abs(quiet[i]); el += Math.Abs(loud[i]); }
            Assert.Greater(el, eq + 5f);
        }

        [Test]
        public void RadioDsp_IsDeterministicAndBounded()
        {
            const int sr = 22050;
            var input = Tone(900, sr, 0.4f, 1.5f);
            var s = RadioDspSettings.For(0.3f, DialogueStress.Panic, true);
            var a = RadioDsp.Process(input, sr, s, 99);
            var b = RadioDsp.Process(input, sr, s, 99);
            Assert.AreEqual(a.Length, b.Length);
            for (var i = 0; i < a.Length; i += 97)
            {
                Assert.AreEqual(a[i], b[i], 1e-6f);
                Assert.LessOrEqual(Math.Abs(a[i]), 0.98f + 1e-4f);
            }
        }

        [Test]
        public void RadioDsp_EmptyInputIsSafe()
        {
            Assert.AreEqual(0, RadioDsp.Process(null, 22050, Clean(), 1).Length);
            Assert.AreEqual(0, RadioDsp.Process(new float[0], 22050, Clean(), 1).Length);
        }

        [Test]
        public void VoiceDsp_PitchChangesLength_StitchJoinsParts()
        {
            var x = Tone(500, 22050, 0.5f, 0.5f);
            Assert.Less(VoiceDsp.Resample(x, 1.2f).Length, x.Length);
            Assert.Greater(VoiceDsp.Resample(x, 0.9f).Length, x.Length);
            var joined = VoiceDsp.Stitch(new List<float[]> { x, x, null, x }, 22050, 0.05f, 1f);
            Assert.Greater(joined.Length, x.Length * 3);
            Assert.AreEqual(0, VoiceDsp.Stitch(null, 22050, 0.05f, 1f).Length);
        }

        [Test]
        public void VoiceDsp_IdentityKeepsBoundedOutput()
        {
            var x = Tone(500, 22050, 0.3f, 0.9f);
            for (var v = 0; v < VoiceIdentities.Count; v++)
            {
                var o = VoiceDsp.ApplyIdentity(x, 22050, VoiceIdentities.Get(v).ForStress(DialogueStress.Panic));
                Assert.Greater(o.Length, 100);
                for (var i = 0; i < o.Length; i += 31)
                    Assert.LessOrEqual(Math.Abs(o[i]), 1f);
            }
        }

        [Test]
        public void ProceduralVoice_LengthScalesWithText()
        {
            var id = VoiceIdentities.Get(0);
            var shortLine = ProceduralVoice.Synthesize("Şarjör!", id, DialogueStress.Combat, 22050, 1);
            var longLine = ProceduralVoice.Synthesize("Temas! Saat üç yönü, yüz metre, düşman piyade!", id, DialogueStress.Combat, 22050, 1);
            Assert.Greater(shortLine.Length, 1000);
            Assert.Greater(longLine.Length, shortLine.Length * 3);
            Assert.AreEqual(0, ProceduralVoice.Synthesize("", id, DialogueStress.Calm, 22050, 1).Length);
        }

        [Test]
        public void Rules_CriticalCategoriesAreUncuttable()
        {
            Assert.IsTrue(DialogueArbiter.IsUncuttable(DialogueCategoryRules.Get(DialogueCats.GrenadeIncoming).Priority));
            Assert.IsTrue(DialogueArbiter.IsUncuttable(DialogueCategoryRules.Get(DialogueCats.HitSelf).Priority));
            Assert.IsFalse(DialogueArbiter.IsUncuttable(DialogueCategoryRules.Get(DialogueCats.Clear).Priority));
        }
    }
}
