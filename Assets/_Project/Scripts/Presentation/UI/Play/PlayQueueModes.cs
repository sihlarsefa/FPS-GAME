using System;

namespace Project.Presentation.UI.Play
{
    /// <summary>Eşleşme türü: Tim BR, İkili, Tekli (BR) ve Eğitim (poligon, kuyruksuz).</summary>
    public enum PlayQueueKind { TeamBr = 0, Duo = 1, Solo = 2, Training = 3 }

    /// <summary>Eşleşme türü tanımı (saf veri).</summary>
    public sealed class PlayQueueInfo
    {
        public PlayQueueKind Kind;
        public string Title;
        public string Short;
        public string Detail;
        /// <summary>Kuyruktaki tim büyüklüğü (hazır kontrolü bu kadar kişiyi bekler).</summary>
        public int SquadSize;
        /// <summary>Ortalama bekleme (sn) — talep eğrisi çarpanından önce.</summary>
        public float BaseWaitSeconds;
        public bool UsesQueue;
        /// <summary>Var olan başlatma akışındaki mod kimliği.</summary>
        public string LaunchModeId;
    }

    /// <summary>
    /// Eşleşme türü kataloğu. PUBG'deki Solo/Duo/Squad seçimi gibi; kalabalık az olan türde
    /// bekleme uzarsa daha dolu türe geçiş önerilir (bkz. <see cref="QueueEstimator.SuggestAlternative"/>).
    /// </summary>
    public static class PlayQueueModes
    {
        private static readonly PlayQueueInfo[] All =
        {
            new PlayQueueInfo { Kind = PlayQueueKind.TeamBr, Title = "TİM BR", Short = "TİM", SquadSize = 10, BaseWaitSeconds = 38f, UsesQueue = true,
                LaunchModeId = MenuModeCatalog.BattleRoyale, Detail = "10 kişilik tim, 100 asker. En dolu kuyruk." },
            new PlayQueueInfo { Kind = PlayQueueKind.Duo, Title = "İKİLİ", Short = "İKİLİ", SquadSize = 2, BaseWaitSeconds = 26f, UsesQueue = true,
                LaunchModeId = MenuModeCatalog.BattleRoyale, Detail = "İki kişilik ekip, geniş alanda hızlı eşleşme." },
            new PlayQueueInfo { Kind = PlayQueueKind.Solo, Title = "TEKLİ", Short = "TEKLİ", SquadSize = 1, BaseWaitSeconds = 17f, UsesQueue = true,
                LaunchModeId = MenuModeCatalog.BattleRoyale, Detail = "Tek başına; herkes rakip." },
            new PlayQueueInfo { Kind = PlayQueueKind.Training, Title = "EĞİTİM", Short = "EĞİTİM", SquadSize = 1, BaseWaitSeconds = 0f, UsesQueue = false,
                LaunchModeId = MenuModeCatalog.Range, Detail = "Poligon: kuyruk yok, anında başlar." }
        };

        /// <summary>Seçili tür (oturum boyunca; çıkışta sıfırlanır).</summary>
        public static PlayQueueKind Current = PlayQueueKind.TeamBr;

        public static int Count => All.Length;

        public static PlayQueueInfo At(int index) => All[Math.Max(0, Math.Min(All.Length - 1, index))];

        public static PlayQueueInfo Get(PlayQueueKind kind) => All[(int)kind];

        /// <summary>Kuyruk kullanan türler arasında döner (Eğitim dahil değil).</summary>
        public static PlayQueueKind NextQueued(PlayQueueKind kind, int dir)
        {
            var i = (int)kind + (dir >= 0 ? 1 : -1);
            var queued = 3;
            return (PlayQueueKind)(((i % queued) + queued) % queued);
        }
    }
}
