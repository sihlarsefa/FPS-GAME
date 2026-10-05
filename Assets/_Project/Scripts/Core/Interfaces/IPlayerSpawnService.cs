namespace Project.Core.Interfaces
{
    public interface IPlayerSpawnService
    {
        bool TryGetSpawnPosition(out float x, out float y, out float z);
    }
}
