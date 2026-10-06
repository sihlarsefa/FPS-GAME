using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Saf hava soğurması ve kapalı alan reverb kuyruğu kuralları (Unity nesnesi yok).
    /// Referans eğri (tüfek): 30 m'ye kadar açık, 50 m'de ~16 kHz, 500 m'de ~3 kHz, sonrası yavaş düşer.
    /// </summary>
    public static class AirAbsorption
    {
        public const float OpenHz = 22000f;
        public const float OpenDistance = 30f;
        public const float Hz50 = 16000f;
        public const float Hz500 = 3000f;

        /// <summary>Referans alçak geçiren kesim (Hz), mesafe m. Mesafeyle tekdüze azalır.</summary>
        public static float CutoffHz(float distance)
        {
            if (distance <= OpenDistance) return OpenHz;
            if (distance <= 50f)
                return Mathf.Lerp(OpenHz, Hz50, (distance - OpenDistance) / (50f - OpenDistance));
            if (distance <= 500f)
            {
                var t = Mathf.Log10(distance / 50f); // 0..1
                return Hz50 + (Hz500 - Hz50) * t;
            }
            return Hz500 * Mathf.Pow(500f / distance, 0.6f);
        }

        /// <summary>Çapa göre ölçekli kesim: halfCutoffDist referans tüfeğin (60 m) oranıyla mesafeyi büyütür/küçültür.</summary>
        public static float CutoffHz(float distance, float halfCutoffDist)
        {
            var eff = distance <= OpenDistance ? distance : distance * (60f / Mathf.Max(1f, halfCutoffDist));
            return CutoffHz(eff);
        }

        /// <summary>Kapalı alan reverb kuyruğunun ikinci vuruş gecikmesi (sn): slapback'in ~2.3 katı, en çok 0.3 sn.</summary>
        public static float ReverbTapDelay(float slapbackDelay) => Mathf.Clamp(slapbackDelay * 2.3f, 0.04f, 0.3f);

        /// <summary>İkinci vuruş kazancı: ilkinin %50'si.</summary>
        public static float ReverbTapGain(float slapbackGain) => Mathf.Max(0f, slapbackGain) * 0.5f;
    }
}
