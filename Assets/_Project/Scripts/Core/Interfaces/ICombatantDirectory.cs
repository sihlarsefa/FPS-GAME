using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    /// <summary>Savaşanların görünen adlarını ve yerel oyuncu bilgisini sağlar (kill feed, skor tablosu).</summary>
    public interface ICombatantDirectory
    {
        string GetDisplayName(PlayerId id);
        bool IsLocalPlayer(PlayerId id);
    }
}
