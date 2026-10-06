using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    /// <summary>
    /// Otorite olmayan istemcinin bomba/yakın dövüş isteklerini sunucuya ilettiği kanal.
    /// <see cref="INetworkSession"/> uygulaması (online) ayrıca bunu uygulayabilir; çevrimdışı oturum uygulamaz.
    /// </summary>
    public interface IPlayerActionSink
    {
        void SubmitThrow(ThrowRequest request);
        void SubmitMelee(MeleeRequest request);
    }

    /// <summary>
    /// Sunucunun patlama/topçu ıslığı görsel-işitsel efektlerini istemcilere yaydığı kanal (hasar yalnızca otoritede).
    /// </summary>
    public interface IWorldEffectRelay
    {
        void BroadcastExplosion(Float3 position, float radius, PlayerId attackerId);
        void BroadcastArtilleryWhistle(Float3 position);
    }
}
