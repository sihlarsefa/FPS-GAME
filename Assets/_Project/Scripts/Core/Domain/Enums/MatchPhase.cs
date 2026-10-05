namespace Project.Core.Domain
{
    public enum MatchPhase
    {
        None = 0,
        Lobby = 1,
        PreMatch = 2,

        /// <summary>Timler helikopter / zırhlı araçla harekât bölgesine intikal ediyor.</summary>
        Insertion = 3,

        InMatch = 4,
        Ending = 5
    }
}
