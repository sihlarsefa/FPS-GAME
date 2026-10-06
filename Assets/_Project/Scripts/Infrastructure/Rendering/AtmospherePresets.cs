using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Günün saati ön ayarları: güneş, ortam, sis, gökyüzü ve pozlama (Gündüz = RenderSettingsUtil.Gameplay).</summary>
    public readonly struct AtmospherePreset
    {
        public readonly Vector3 SunEuler;
        public readonly Color SunColor;
        public readonly float SunIntensity;
        public readonly Color AmbientSky, AmbientEquator, AmbientGround;
        public readonly Color FogColor;
        public readonly float FogDensity;
        public readonly Color SkyTint, GroundColor;
        public readonly float SkyExposure;
        public readonly float PostExposure;

        public AtmospherePreset(Vector3 sunEuler, Color sunColor, float sunIntensity, Color ambSky, Color ambEq, Color ambGr,
            Color fog, float fogDensity, Color skyTint, Color ground, float skyExposure, float postExposure)
        {
            SunEuler = sunEuler; SunColor = sunColor; SunIntensity = sunIntensity;
            AmbientSky = ambSky; AmbientEquator = ambEq; AmbientGround = ambGr;
            FogColor = fog; FogDensity = fogDensity; SkyTint = skyTint; GroundColor = ground;
            SkyExposure = skyExposure; PostExposure = postExposure;
        }

        public static AtmospherePreset For(TimeOfDay t)
        {
            var g = RenderSettingsUtil.Gameplay;
            switch (t)
            {
                case TimeOfDay.Safak:
                    return new AtmospherePreset(new Vector3(8f, -60f, 0f), new Color(1f, 0.7f, 0.5f), 0.95f,
                        new Color(0.20f, 0.22f, 0.34f), new Color(0.17f, 0.14f, 0.15f), new Color(0.07f, 0.06f, 0.06f),
                        new Color(0.72f, 0.58f, 0.55f), g.FogDensity, new Color(0.62f, 0.5f, 0.55f), new Color(0.3f, 0.27f, 0.27f), 0.95f, 0.0f);
                case TimeOfDay.Aksam:
                    return new AtmospherePreset(new Vector3(12f, -120f, 0f), new Color(1f, 0.55f, 0.28f), 1.25f,
                        new Color(0.22f, 0.17f, 0.24f), new Color(0.17f, 0.12f, 0.12f), new Color(0.08f, 0.06f, 0.05f),
                        new Color(0.78f, 0.52f, 0.38f), g.FogDensity, new Color(0.75f, 0.45f, 0.35f), new Color(0.3f, 0.24f, 0.2f), 1f, 0.05f);
                case TimeOfDay.Gece:
                    return new AtmospherePreset(new Vector3(-35f, 30f, 0f), new Color(0.45f, 0.55f, 0.85f), 0.06f,
                        new Color(0.02f, 0.03f, 0.06f), new Color(0.02f, 0.025f, 0.045f), new Color(0.02f, 0.02f, 0.03f),
                        new Color(0.03f, 0.04f, 0.08f), g.FogDensity, new Color(0.04f, 0.06f, 0.14f), new Color(0.03f, 0.03f, 0.05f), 0.12f, 0.20f);
                default:
                    return new AtmospherePreset(g.SunEuler, g.SunColor, g.SunIntensity, g.AmbientSky, g.AmbientEquator, g.AmbientGround,
                        g.FogColor, g.FogDensity, g.SkyTint, g.GroundColor, g.Exposure, -0.05f);
            }
        }
    }
}
