using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IZoneService
    {
        ZoneState CurrentZone { get; }

        /// <summary>Bir sonraki güvenli bölge (beyaz çember).</summary>
        ZoneState NextZone { get; }

        ZoneStage Stage { get; }
        int PhaseIndex { get; }
        int PhaseCount { get; }
        float StageRemainingSeconds { get; }
        float StageDurationSeconds { get; }
        bool IsActive { get; }

        void Initialize(float centerX, float centerZ, float radius, float damagePerSecond);

        /// <summary>Faz planını başlatır (ilk bekleme).</summary>
        void Start();

        void Shrink(float newRadius);
        bool IsInsideZone(float x, float z);
        float GetDamagePerSecond(float x, float z);

        /// <summary>Bir sonraki güvenli bölgeye kalan mesafe (içerideyse 0).</summary>
        float DistanceToSafeZone(float x, float z);
    }
}
