using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface ICombatInputReader
    {
        CombatInputState Read();
    }
}
