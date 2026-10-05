namespace Project.Core.Domain
{
    /// <summary>
    /// Sürecin ağdaki rolü. Oyun kuralları yalnızca otorite (Offline, Host, DedicatedServer) tarafında çalışır;
    /// Client yalnızca komut gönderir ve çoğaltılan durumu gösterir.
    /// </summary>
    public enum NetworkRole
    {
        Offline = 0,
        Host = 1,
        Client = 2,
        DedicatedServer = 3
    }
}
