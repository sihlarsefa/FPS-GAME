using Project.Core.Domain;

namespace Project.Core.Events
{
    /// <summary>İkmal sandığı aşaması.</summary>
    public enum AirdropStage
    {
        /// <summary>T-70 sandığı bıraktı: harita işareti + telsiz duyurusu, paraşütle iniyor.</summary>
        Announced = 0,

        /// <summary>Sandık yere indi (henüz kilitli; duman gösterilir).</summary>
        Landed = 1,

        /// <summary>Sandık açıldı: yüksek kademe yağma düşürülür.</summary>
        Opened = 2
    }

    /// <summary>İkmal sandığının aşama değişimi. Konumun Y'si 0'dır (yer yüksekliğini altyapı bulur).</summary>
    public readonly struct AirdropEvent : IGameEvent
    {
        public int Id { get; }
        public AirdropStage Stage { get; }
        public Float3 Position { get; }

        /// <summary>Sonraki aşamaya kalan süre (Announced: inişe, Landed: açılışa; Opened: 0).</summary>
        public float SecondsToNextStage { get; }

        public AirdropEvent(int id, AirdropStage stage, Float3 position, float secondsToNextStage)
        {
            Id = id;
            Stage = stage;
            Position = position;
            SecondsToNextStage = secondsToNextStage;
        }
    }
}
