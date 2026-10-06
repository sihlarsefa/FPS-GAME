using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Hacimsel sis + ışık huzmesi canlı ayarları (inspector'dan ya da kodla değiştirilebilir). Değerler VolumetricFogMath.Sanitize
    /// kurallarıyla güvenli aralığa çekilir. Ön ayar için VolumetricFog.ApplyPreset kullanın.
    /// </summary>
    [Serializable]
    public sealed class VolumetricFogSettings
    {
        public bool Enabled = true;
        [Min(0f)] public float Density = 0.0035f;
        [Min(0f)] public float HeightFalloff = 0.025f;
        public float BaseHeight;
        [Range(-0.95f, 0.95f)] public float Anisotropy = 0.65f;
        [Min(0f)] public float SunIntensity = 1f;
        [Min(0f)] public float AmbientIntensity = 0.3f;
        [Min(10f)] public float MaxDistance = 260f;
        public Color Tint = new Color(0.80f, 0.86f, 0.95f, 1f);

        public VolumetricFogParams ToParams()
        {
            return new VolumetricFogParams
            {
                Density = Density, HeightFalloff = HeightFalloff, BaseHeight = BaseHeight, Anisotropy = Anisotropy,
                SunIntensity = SunIntensity, AmbientIntensity = AmbientIntensity, MaxDistance = MaxDistance,
                TintR = Tint.r, TintG = Tint.g, TintB = Tint.b
            }.Sanitized();
        }

        public void Apply(VolumetricFogParams p)
        {
            p = p.Sanitized();
            Density = p.Density; HeightFalloff = p.HeightFalloff; BaseHeight = p.BaseHeight; Anisotropy = p.Anisotropy;
            SunIntensity = p.SunIntensity; AmbientIntensity = p.AmbientIntensity; MaxDistance = p.MaxDistance;
            Tint = new Color(p.TintR, p.TintG, p.TintB, 1f);
        }

        public VolumetricFogSettings Clone() => (VolumetricFogSettings)MemberwiseClone();
    }
}
