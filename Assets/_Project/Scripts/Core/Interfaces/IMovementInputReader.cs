using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IMovementInputReader
    {
        MovementInputState Read();
    }
}
