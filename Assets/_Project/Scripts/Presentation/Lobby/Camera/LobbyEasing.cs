using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Kamera geçişi eğrileri. Sinema kamerası kalkışta yavaş, ortada hızlı, varışta uzun süzülüp durur:
    /// konum için ease-in-out kübik, FOV için daha erken biten (çıkış ağırlıklı) eğri, odak için geciktirilmiş eğri.
    /// </summary>
    public static class LobbyEasing
    {
        public static float Clamp01(float t)
        {
            if (float.IsNaN(t)) return 0f;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        public static float Smoothstep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Smootherstep(float t)
        {
            t = Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static float InOutCubic(float t)
        {
            t = Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        public static float OutCubic(float t)
        {
            t = Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        public static float OutQuint(float t)
        {
            t = Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u * u * u;
        }

        /// <summary>t'yi [delay, 1] aralığına yeniden eşler: odak, kamera hareketinden sonra yetişir.</summary>
        public static float Delayed(float t, float delay)
        {
            delay = Mathf.Clamp(delay, 0f, 0.95f);
            return Clamp01((t - delay) / (1f - delay));
        }

        /// <summary>t'yi [0, end] aralığına sıkıştırır (end'de 1 olur).</summary>
        public static float Early(float t, float end)
        {
            end = Mathf.Clamp(end, 0.05f, 1f);
            return Clamp01(t / end);
        }
    }
}
