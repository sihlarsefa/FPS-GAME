// HAREKAT ortak shader yardımcıları: küresel ıslaklık + ucuz prosedürel gürültü.
#ifndef HAREKAT_COMMON_INCLUDED
#define HAREKAT_COMMON_INCLUDED

// Küresel ıslaklık 0-1 (Shader.SetGlobalFloat("_HarekatWetness"); yağmur artırır, kuruma azaltır).
float _HarekatWetness;

float HK_Hash21(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float HK_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = HK_Hash21(i);
    float b = HK_Hash21(i + float2(1, 0));
    float c = HK_Hash21(i + float2(0, 1));
    float d = HK_Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float HK_Fbm(float2 p)
{
    return HK_ValueNoise(p) * 0.65 + HK_ValueNoise(p * 2.13 + 7.7) * 0.35;
}

#endif
