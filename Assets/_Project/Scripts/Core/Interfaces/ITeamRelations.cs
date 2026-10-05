using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    /// <summary>Tim üyeliği ve dost/düşman ilişkisi.</summary>
    public interface ITeamRelations
    {
        int GetTeam(PlayerId id);
        bool AreAllies(PlayerId a, PlayerId b);
        string GetTeamName(int team);
        TeamRole GetRole(PlayerId id);
    }
}
