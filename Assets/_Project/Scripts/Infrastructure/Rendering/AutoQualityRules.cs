using System;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Otomatik kalite kararlarının saf mantığı (Unity bağımsız, test edilebilir).</summary>
    public static class AutoQualityRules
    {
        public const float ProbeSeconds = 5f;
        public const float WindowSeconds = 5f;
        public const float DropAboveMs = 20f;
        public const float RestoreBelowMs = 14f;
        public const int MaxNotch = 3;
        public const float NotchStep = 0.1f;
        public const float AbsoluteMinScale = 0.55f;

        /// <summary>İlk açılış kademesi: donanım puanı + ölçülen ortalama kare süresi (ms; 0 = ölçülmedi).</summary>
        public static int ChooseTier(string gpuName, int vramMb, int cpuCores, float avgFrameMs)
        {
            var score = HardwareScore(gpuName, vramMb, cpuCores);
            var tier = score >= 80 ? 3 : score >= 55 ? 2 : score >= 30 ? 1 : 0;
            if (avgFrameMs > 0f)
            {
                // Menü sahnesi hafiftir; yavaşsa kademeyi düşür, çok hızlıysa dokunma.
                if (avgFrameMs > 33f) tier -= 2;
                else if (avgFrameMs > 20f) tier -= 1;
            }
            return Math.Max(0, Math.Min(3, tier));
        }

        public static int HardwareScore(string gpuName, int vramMb, int cpuCores)
        {
            var score = 0;
            score += vramMb >= 12000 ? 45 : vramMb >= 8000 ? 38 : vramMb >= 6000 ? 30 : vramMb >= 4000 ? 20 : vramMb >= 2000 ? 10 : 3;
            score += cpuCores >= 12 ? 25 : cpuCores >= 8 ? 20 : cpuCores >= 6 ? 14 : cpuCores >= 4 ? 8 : 3;
            score += GpuClass(gpuName);
            return score;
        }

        /// <summary>GPU adından kaba sınıf puanı (0-30). Entegre/bilinmeyen düşük, RTX 30/40/50 ve RX 6/7/9 yüksek.</summary>
        public static int GpuClass(string name)
        {
            if (string.IsNullOrEmpty(name)) return 5;
            var n = name.ToLowerInvariant();
            if (n.Contains("intel") || n.Contains("uhd") || n.Contains("iris") || n.Contains("vega 8") || n.Contains("llvmpipe") || n.Contains("swiftshader") || n.Contains("microsoft basic"))
                return 0;
            if (n.Contains("apple m")) return n.Contains("pro") || n.Contains("max") || n.Contains("ultra") ? 26 : 18;
            if (n.Contains("rtx 50") || n.Contains("rtx 40") || n.Contains("rx 79") || n.Contains("rx 78") || n.Contains("rx 9")) return 30;
            if (n.Contains("rtx 30") || n.Contains("rx 69") || n.Contains("rx 68") || n.Contains("rx 67") || n.Contains("rx 76")) return 24;
            if (n.Contains("rtx 20") || n.Contains("gtx 16") || n.Contains("rx 66") || n.Contains("rx 58") || n.Contains("gtx 1080") || n.Contains("gtx 1070")) return 16;
            if (n.Contains("gtx") || n.Contains("rx ") || n.Contains("radeon")) return 8;
            if (n.Contains("nvidia") || n.Contains("geforce")) return 10;
            return 5;
        }

        /// <summary>Kademe taban ölçeği ve çentik için etkin render ölçeği (kademe sınırları içinde).</summary>
        public static float ScaleFor(float tierScale, int notch)
        {
            notch = Math.Max(0, Math.Min(MaxNotch, notch));
            var s = tierScale - NotchStep * notch;
            var floor = Math.Min(tierScale, Math.Max(AbsoluteMinScale, tierScale - NotchStep * MaxNotch));
            return Math.Max(floor, s);
        }

        /// <summary>Pencere ortalamasına göre yeni çentik: yavaşsa +1, bol boşluk varsa -1, aksi halde aynı.</summary>
        public static int NextNotch(int notch, float windowAvgMs)
        {
            if (windowAvgMs <= 0f) return notch;
            if (windowAvgMs > DropAboveMs) return Math.Min(MaxNotch, notch + 1);
            if (windowAvgMs < RestoreBelowMs) return Math.Max(0, notch - 1);
            return notch;
        }

        /// <summary>Bütçe aşımı kategorileri, aşım oranına göre azalan sırada. Boş = bütçe içinde.</summary>
        public static string[] RankOffenders(long drawCalls, long batches, long gcBytes, float frameMs)
        {
            var names = new string[4];
            var ratios = new float[4];
            var n = 0;
            Add(names, ratios, ref n, "DrawCall", drawCalls / 2500f);
            Add(names, ratios, ref n, "Batch", batches / 2000f);
            Add(names, ratios, ref n, "GC", gcBytes / 4096f);
            Add(names, ratios, ref n, "CPU/GPU", frameMs / 33f);
            var res = new string[n];
            Array.Copy(names, res, n);
            var r = new float[n];
            Array.Copy(ratios, r, n);
            Array.Sort(r, res);
            Array.Reverse(res);
            return res;
        }

        // ---- Cilalar: bildirim metni, histerezis, p95 önerisi, karar günlüğü ----
        public const float DynNoticeCooldownSeconds = 120f;
        public const float SlowP95Ms = 28f;
        public const float SlowP95HoldSeconds = 180f;
        public const float SuggestCooldownSeconds = 600f;

        public static string TierName(int tier)
        {
            switch (tier) { case 0: return "DÜŞÜK"; case 1: return "ORTA"; case 2: return "YÜKSEK"; default: return "ULTRA"; }
        }

        public static string FirstRunMessage(int tier)
            => "Donanımına göre " + TierName(tier) + " seçildi — Ayarlar'dan değiştirebilirsin";

        /// <summary>Dinamik çözünürlük bildirimi: yalnız düşüşte (yukarı dönüş sessiz), en az 2 dk arayla.</summary>
        public static bool ShouldNotifyDynScale(int oldNotch, int newNotch, float now, float lastNoticeTime)
        {
            if (newNotch <= oldNotch) return false;
            return lastNoticeTime < 0f || now - lastNoticeTime >= DynNoticeCooldownSeconds;
        }

        public const string SuggestMessage = "Performans düşük — kademeyi düşürmeyi dene (Ayarlar > Grafik)";

        /// <summary>Kademeli öneri: p95 &gt; 28 ms 3 dk sürerse true; sonra 10 dk sessiz.</summary>
        public struct SlowTracker
        {
            public float SlowSince;   // -1 = yavaş değil
            public float LastSuggest; // -1 = hiç
            public static SlowTracker New() => new SlowTracker { SlowSince = -1f, LastSuggest = -1f };

            public bool Update(float now, float p95Ms, int samples, int tier)
            {
                if (samples < 30 || tier <= 0 || p95Ms <= SlowP95Ms) { SlowSince = -1f; return false; }
                if (SlowSince < 0f) { SlowSince = now; return false; }
                if (now - SlowSince < SlowP95HoldSeconds) return false;
                if (LastSuggest >= 0f && now - LastSuggest < SuggestCooldownSeconds) return false;
                LastSuggest = now;
                SlowSince = now;
                return true;
            }
        }

        /// <summary>Benchmark CSV'sine eklenecek tek satır: autoquality,olay,kademe,çentik,ölçek,değer.</summary>
        public static string DecisionCsvLine(string kind, int tier, int notch, float scale, float value)
            => "autoquality," + (kind ?? "").Replace(',', ';') + "," + tier + "," + notch + ","
               + scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ","
               + value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static void Add(string[] names, float[] ratios, ref int n, string name, float ratio)
        {
            if (ratio <= 1f) return;
            names[n] = name; ratios[n] = ratio; n++;
        }
    }
}
