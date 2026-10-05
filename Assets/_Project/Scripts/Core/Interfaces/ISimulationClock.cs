namespace Project.Core.Interfaces
{
    /// <summary>Sabit tick'li simülasyon saati (ağ senkronu için deterministik ilerleme).</summary>
    public interface ISimulationClock
    {
        uint CurrentTick { get; }
        float TickInterval { get; }
        float ElapsedSeconds { get; }
    }
}
