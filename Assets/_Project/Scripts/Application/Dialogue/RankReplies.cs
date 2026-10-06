using Project.Core.Domain;

namespace Project.Application.Dialogue
{
    public enum RankClass
    {
        Enlisted = 0,
        Nco = 1,
        Officer = 2
    }

    /// <summary>Rütbe duyarlı yanıt seçimi: Er "Emredersiniz komutanım", üst astı "Anlaşıldı, devam edin". Saf mantık.</summary>
    public static class RankReplies
    {
        public static RankClass ClassOf(MilitaryRank rank)
        {
            if (rank <= MilitaryRank.SozlesmeliEr)
                return RankClass.Enlisted;
            if (rank < MilitaryRank.Astegmen)
                return RankClass.Nco;
            return RankClass.Officer;
        }

        /// <summary>Emri veren <paramref name="issuer"/>, alan <paramref name="speaker"/>: yanıt kategorisi.</summary>
        public static string AckCategory(MilitaryRank speaker, MilitaryRank issuer)
        {
            if (issuer > speaker)
                return ClassOf(speaker) == RankClass.Enlisted ? DialogueCats.AckEnlisted : DialogueCats.AckNco;
            if (issuer < speaker)
                return DialogueCats.AckSuperior;
            return DialogueCats.AckPeer;
        }

        /// <summary>Konuşmacı dinleyiciye "komutanım" diye hitap etmeli mi?</summary>
        public static bool UsesKomutanim(MilitaryRank speaker, MilitaryRank listener) => listener > speaker;
    }
}
