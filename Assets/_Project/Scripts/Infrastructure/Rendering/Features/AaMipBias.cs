using System;
using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>
    /// Zamansal AA / çözünürlük ölçeği &lt; 1 altında doku mip kayması (negatif LOD bias, ~ -0,5). URP kendi gölgelendiricilerinde yükseltmede
    /// _GlobalMipBias'ı uygular; özel/prosedürel dokular için: küresel _HkMipBias + kayıtlı dokulara Texture.mipMapBias.
    /// Kullanım: MaterialLibrary/ProceduralPbr dokuyu üretince AaMipBias.Track(tex) çağırır (ENTEGRASYON).
    /// </summary>
    public static class AaMipBias
    {
        private static readonly int GlobalId = Shader.PropertyToID("_HkMipBias");
        private static readonly List<WeakReference<Texture>> Tracked = new List<WeakReference<Texture>>();

        public static float Current { get; private set; }

        /// <summary>Kaymayı hesaplar, küresel değişkeni yazar, kayıtlı dokulara uygular.</summary>
        public static void Apply(float renderScale, AaMode mode)
        {
            var bias = AntiAliasingMath.MipBias(renderScale, mode);
            if (Mathf.Approximately(bias, Current) && Tracked.Count == 0)
            {
                Current = bias;
                return;
            }

            Current = bias;
            try { Shader.SetGlobalFloat(GlobalId, bias); } catch (Exception) { }
            for (var i = Tracked.Count - 1; i >= 0; i--)
            {
                if (!Tracked[i].TryGetTarget(out var tex) || tex == null)
                {
                    Tracked.RemoveAt(i);
                    continue;
                }
                tex.mipMapBias = bias;
            }
        }

        /// <summary>Dokuyu izler: şimdiki kayma hemen uygulanır, sonraki kademe/AA değişimlerinde güncellenir.</summary>
        public static void Track(Texture texture)
        {
            if (texture == null)
                return;
            Tracked.Add(new WeakReference<Texture>(texture));
            texture.mipMapBias = Current;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tracked.Clear();
            Current = 0f;
        }
    }
}
