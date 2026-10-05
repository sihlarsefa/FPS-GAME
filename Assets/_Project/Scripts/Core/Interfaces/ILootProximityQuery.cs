namespace Project.Core.Interfaces
{
    public interface ILootProximityQuery
    {
        bool TryGetNearestPickup(float originX, float originY, float originZ, float radius, out ILootPickup pickup);
    }
}
