using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Aşınma haritalarından boyama kuralları (saf, test edilebilir).</summary>
    public static partial class TerrainPaintRules
    {
        /// <summary>Sırt (dışbükey) ve dik yamaç → çıplak kaya [0,0.75].</summary>
        public static float RidgeRock(float curvature, float slopeDegrees)
        {
            var convex = TerrainNoise.SmoothStep(0.3f, 0.8f, curvature);
            return convex * TerrainNoise.SmoothStep(14f, 28f, slopeDegrees) * 0.75f;
        }

        /// <summary>Uçurum dibi döküntü çakılı [0,0.65].</summary>
        public static float ScreeGravel(float scree, float noise01)
        {
            return Mathf.Clamp01(scree) * (0.45f + 0.2f * noise01) ;
        }

        /// <summary>Uçurum dibi iri taş: seyrek lekeler [0,0.3].</summary>
        public static float ScreeRock(float scree, float noise)
        {
            return Mathf.Clamp01(scree) * 0.3f * TerrainNoise.SmoothStep(0.1f, 0.6f, noise * 0.5f + 0.5f);
        }

        /// <summary>Oluk çamuru: akış yüksek, eğim az (&lt;~14°), içbükey [0,0.7].</summary>
        public static float GullyMud(float flow, float slopeDegrees, float curvature)
        {
            var f = TerrainNoise.SmoothStep(0.25f, 0.7f, flow);
            var gentle = 1f - TerrainNoise.SmoothStep(10f, 20f, slopeDegrees);
            var concave = 0.5f + 0.5f * Mathf.Clamp(-curvature * 2f, -1f, 1f);
            return f * gentle * concave * 0.7f;
        }

        /// <summary>Dik olukta (taşınan) çakıl [0,0.6].</summary>
        public static float GullyGravel(float flow, float slopeDegrees, float curvature)
        {
            var f = TerrainNoise.SmoothStep(0.3f, 0.75f, flow);
            var steep = TerrainNoise.SmoothStep(9f, 18f, slopeDegrees) * (1f - TerrainNoise.SmoothStep(32f, 42f, slopeDegrees));
            var concave = 0.4f + 0.6f * Mathf.Clamp01(-curvature * 2f + 0.3f);
            return f * steep * concave * 0.6f;
        }
    }
}
