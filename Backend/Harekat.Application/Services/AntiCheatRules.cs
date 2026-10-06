using Harekat.Application.Dtos;

namespace Harekat.Application.Services;

/// <summary>
/// HAREKÂT'a özgü hile telemetrisi kuralları (sunucu tarafı eşik tanımları).
/// Telemetry servisi bu kuralları olay akışında uygular.
/// </summary>
public static class AntiCheatRules
{
    /// <summary>JNG-90 ile 600 m üstü sürekli kafa vuruşu şüphesi.</summary>
    public const float Jng90MaxLegitHeadshotMeters = 600f;
    public const int Jng90HeadshotBurstThreshold = 3;

    /// <summary>Duvar arkası isabet — kalibre delme: 7.62 tahtayı deler, beton/çelik delmez.</summary>
    public const float Caliber762WoodPenMeters = 0.35f;
    public const float Caliber762ConcretePenMeters = 0f;

    /// <summary>Kirpi azami hız (km/h) — aşım telemetri olayı.</summary>
    public const float KirpiMaxSpeedKmh = 85f;
    public const float KirpiSpeedGraceKmh = 8f;

    public static IReadOnlyList<AntiCheatRuleDto> Catalog { get; } =
    [
        new("jng90_hs_600m", "JNG-90 600m+ kafa serisi",
            "sr_jng90 ile 600 m üstü ardışık kafa vuruşları", Jng90HeadshotBurstThreshold),
        new("wallbang_invalid", "Geçersiz duvar arkası isabet",
            "Kalibre/malzeme delme tablosuna aykırı isabet (7.62 tahta OK, beton değil)", 1),
        new("kirpi_overspeed", "Kirpi hız aşımı",
            $"Kirpi hızı {KirpiMaxSpeedKmh + KirpiSpeedGraceKmh:0} km/h üstü", 1)
    ];

    public static bool IsSuspiciousJng90Headshot(string weaponId, float distanceMeters, int consecutiveHs) =>
        string.Equals(weaponId, "sr_jng90", StringComparison.OrdinalIgnoreCase)
        && distanceMeters > Jng90MaxLegitHeadshotMeters
        && consecutiveHs >= Jng90HeadshotBurstThreshold;

    public static bool IsInvalidWallbang(string ammoCaliber, string material, float thicknessMeters)
    {
        // 7.62 tahtayı sınırlı deler; beton/çelik aşımında şüpheli.
        if (ammoCaliber.Contains("7.62", StringComparison.OrdinalIgnoreCase)
            || ammoCaliber.Contains("762", StringComparison.OrdinalIgnoreCase))
        {
            if (material.Contains("wood", StringComparison.OrdinalIgnoreCase)
                || material.Contains("tahta", StringComparison.OrdinalIgnoreCase)
                || material.Contains("ahşap", StringComparison.OrdinalIgnoreCase))
                return thicknessMeters > Caliber762WoodPenMeters;
            return true; // beton/metal vb. — 7.62 ile delinmemeli
        }

        return thicknessMeters > 0.05f;
    }

    public static bool IsKirpiOverspeed(float speedKmh) =>
        speedKmh > KirpiMaxSpeedKmh + KirpiSpeedGraceKmh;
}

public sealed record AntiCheatRuleDto(string Id, string Title, string Description, int Threshold);
