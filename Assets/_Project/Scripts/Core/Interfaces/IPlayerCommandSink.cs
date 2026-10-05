using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    /// <summary>Oyuncu komutlarını tüketen otorite. Çevrimdışı: yerel simülasyon. Online: sunucuya gönderen adaptör.</summary>
    public interface IPlayerCommandSink
    {
        void Submit(PlayerId playerId, PlayerCommand command);
    }
}
