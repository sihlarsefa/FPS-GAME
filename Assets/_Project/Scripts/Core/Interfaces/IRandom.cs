namespace Project.Core.Interfaces
{
    /// <summary>Tohumlanabilir rastgelelik (sunucu/istemci tekrar üretilebilirliği ve testler için).</summary>
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
        float NextFloat();
        float Range(float min, float max);
    }
}
