using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>LOD denetim sınıfı: LODGroup geçiş oranlarının durumu.</summary>
    public enum LodVerdict { Ok, AsiriDetayli, TekSeviye, Hatali }

    /// <summary>Performans denetimi saf matematiği (Unity nesnesi gerektirmez): LOD oranı, statik batch, particle/ses kısma kararları.</summary>
    public static class PerfMath
    {
        /// <summary>İlk LOD'dan sonraki geçişin bu orandan yüksek olması gerekir; ilk LOD ekranın bu kadarından fazlasında kalıyorsa aşırı detaylı sayılır.</summary>
        public const float OverDetailedFirstTransition = 0.60f;
        public const float MinLastTransition = 0.01f;

        /// <summary>LOD geçiş yüksekliklerini (azalan, 1→0) sınıflar.</summary>
        public static LodVerdict ClassifyLod(float[] transitions)
        {
            if (transitions == null || transitions.Length == 0) return LodVerdict.Hatali;
            if (transitions.Length == 1) return LodVerdict.TekSeviye;
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i] < 0f || transitions[i] > 1f) return LodVerdict.Hatali;
                if (i > 0 && transitions[i] > transitions[i - 1]) return LodVerdict.Hatali;
            }
            // LOD0 → LOD1 geçişi çok yüksekse (nesne ekranı doldurana dek yüksek detay) fazla detaylı.
            if (transitions[0] > OverDetailedFirstTransition) return LodVerdict.AsiriDetayli;
            // Son LOD çok küçük ekran oranına kadar çizilir: uzakta gereksiz maliyet.
            if (transitions[transitions.Length - 1] < MinLastTransition) return LodVerdict.AsiriDetayli;
            return LodVerdict.Ok;
        }

        /// <summary>Statik batch adayı: statik, tek malzemeli/az malzemeli, küçük mesh, henüz batch'li değil.</summary>
        public static bool IsStaticBatchCandidate(bool isStatic, bool alreadyBatched, bool hasMeshRenderer, int vertexCount, int maxVerts = 30000)
        {
            return !isStatic == false && !alreadyBatched && hasMeshRenderer && vertexCount > 0 && vertexCount <= maxVerts;
        }

        /// <summary>Particle sistemi kameranın arkasında mı (ön yönle nokta çarpımı eşik altı)? Çok yakındaki sistemler asla kısılmaz.</summary>
        public static bool ShouldPauseParticle(Vector3 camPos, Vector3 camForward, Vector3 sysPos, float safeRadius, float dotThreshold = -0.2f)
        {
            Vector3 d = sysPos - camPos;
            float dist = d.magnitude;
            if (dist <= safeRadius) return false;
            return Vector3.Dot(camForward.normalized, d / dist) < dotThreshold;
        }

        /// <summary>Ses kaynağı kısma mesafesi (kalite kademesine göre; düşükte daha agresif).</summary>
        public static float AudioCullDistance(int tier, float maxDistance)
        {
            float mul = tier <= 0 ? 1.0f : tier == 1 ? 1.15f : 1.3f;
            return Mathf.Max(5f, maxDistance * mul);
        }

        /// <summary>Kaynak dinleyiciden cullDistance'tan uzaksa devre dışı bırakılmalı mı (histerezisli: tekrar açılma %10 daha yakın).</summary>
        public static bool ShouldDisableAudio(float distance, float cullDistance, bool currentlyDisabled)
        {
            return currentlyDisabled ? distance > cullDistance * 0.9f : distance > cullDistance;
        }
    }
}
