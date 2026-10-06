using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Ping dünya işaretinin saf boyut/alfa matematiği (ekran-sabit, dünya uzaklığına göre ölçeklenmez).</summary>
    public static class PingMarkerMath
    {
        public const float IconBase = 32f;
        public const float IconMin = 28f;
        public const float IconMax = 36f;
        public const float BeamWidth = 2f;
        public const float BeamWorldHeight = 8f;
        public const float BeamMinPx = 24f;
        public const float BeamMaxPx = 420f;
        public const float OccludedAlpha = 0.4f;
        public const float FadeWindow01 = 0.25f;
        public const float BeamAlpha = 0.55f;

        /// <summary>İkon boyutu (px): sabit 32, nabız ±%10; her zaman 28–36 aralığında. Mesafeden bağımsız.</summary>
        public static float IconSize(float pulse01)
        {
            var size = IconBase * (1f + 0.1f * Mathf.Clamp(pulse01, -1f, 1f));
            return Mathf.Clamp(size, IconMin, IconMax);
        }

        /// <summary>Işın yüksekliği: ekrandaki 8 m'lik izdüşüm, okunur aralığa kıstırılır.</summary>
        public static float BeamHeightPx(float groundScreenY, float topScreenY)
        {
            return Mathf.Clamp(topScreenY - groundScreenY, BeamMinPx, BeamMaxPx);
        }

        /// <summary>Ömrün son bölümünde solar; gizliyse %40 alfa.</summary>
        public static float Alpha(float life01, bool occluded)
        {
            var fade = Mathf.Clamp01(life01 / FadeWindow01);
            return fade * (occluded ? OccludedAlpha : 1f);
        }

        public static string FormatDistance(float meters)
        {
            return Mathf.Max(0, Mathf.RoundToInt(meters)) + " m";
        }

        /// <summary>Kök noktayı ekranda tutar; üstte ikon+etiket+ışın sığsın diye dikey sınır.</summary>
        public static Vector2 ClampAnchored(Vector2 anchored, Vector2 canvasSize, float stackHeight, float margin)
        {
            var halfX = Mathf.Max(0f, canvasSize.x * 0.5f - margin);
            var minY = -canvasSize.y * 0.5f + margin;
            var maxY = Mathf.Max(minY, canvasSize.y * 0.5f - margin - stackHeight);
            anchored.x = Mathf.Clamp(anchored.x, -halfX, halfX);
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
            return anchored;
        }
    }
}
