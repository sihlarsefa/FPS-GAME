using System;
using Project.Application.Replay;

namespace Project.Presentation.Replay
{
    /// <summary>Ölüm killcam planı: tekrar zamanında [Start, End] aralığı, isabet ve öldürme anı.</summary>
    public struct KillCamPlan
    {
        public bool Valid;
        public int KillerId;
        public int VictimId;
        /// <summary>Tekrar zamanı (sn): oynatma başı, son N sn.</summary>
        public float Start;
        public float End;
        /// <summary>Katilin son isabeti (yoksa öldürme anı).</summary>
        public float HitTime;
        public float KillTime;
        public int WeaponIndex;
        public bool Headshot;
    }

    /// <summary>Kamera pozu (dünya uzayı, saf float).</summary>
    public struct KillCamPose
    {
        public bool Valid;
        public float PosX, PosY, PosZ;
        public float LookX, LookY, LookZ;
    }

    /// <summary>
    /// Killcam saf mantığı (Unity'den bağımsız): ReplayData'dan son N sn'lik plan çıkarma, yavaşlatılmış OYNATIM hız eğrisi
    /// (Time.timeScale DEĞİL), kill anında kırmızı vinyet vurgusu ve katilin omuz arkası kamera pozu.
    /// Replay biçimi değişmedi; eski kayıtlar aynen çalışır.
    /// </summary>
    public static class KillCamTimeline
    {
        public const float WindowSeconds = 6f;
        /// <summary>Kill anından sonra gösterilen kuyruk.</summary>
        public const float TailSeconds = 0.8f;
        public const float SlowSpeed = 0.3f;
        /// <summary>Yavaş oynatma penceresi (tekrar saniyesi): isabetten önce / sonra.</summary>
        public const float SlowBefore = 0.1f;
        public const float SlowAfter = 0.2f;
        public const float VignetteFade = 0.9f;
        /// <summary>Çok eski isabeti "son isabet" saymamak için üst sınır.</summary>
        private const float HitLookback = 1.5f;
        private const float MinSpan = 0.5f;

        /// <summary>
        /// Kurbanın son ölümünden plan çıkarır. Tekrar yok, ölüm olayı yok, katil yok/kendi/intihar, katilin örneği yok ya da
        /// kare sayısı yetersizse Valid=false (çağıran doğrudan ölüm özetine geçer).
        /// </summary>
        public static KillCamPlan Build(ReplayData data, int victimId, float windowSeconds = WindowSeconds)
        {
            var plan = new KillCamPlan();
            if (data == null || data.Frames.Count < 2 || victimId < 0)
                return plan;

            var deathIndex = -1;
            for (var i = data.Events.Count - 1; i >= 0; i--)
            {
                var e = data.Events[i];
                if (e.Type == ReplayEventType.Death && e.Target == victimId)
                {
                    deathIndex = i;
                    break;
                }
            }
            if (deathIndex < 0)
                return plan;

            var death = data.Events[deathIndex];
            if (death.Actor < 0 || death.Actor == victimId)
                return plan;

            var first = data.Frames[0].Time;
            var last = data.Frames[data.Frames.Count - 1].Time;
            var kill = death.Time;
            if (kill < first)
                return plan;
            if (kill > last)
                kill = last;

            // Katil, kill anına yakın karede bulunmalı.
            if (!ReplayTimeline.Sample(data, death.Actor, kill, out _))
                return plan;

            var hit = kill;
            for (var i = deathIndex; i >= 0; i--)
            {
                var e = data.Events[i];
                if (e.Time < death.Time - HitLookback)
                    break;
                if (e.Type == ReplayEventType.Hit && e.Actor == death.Actor && e.Target == victimId && e.Time <= death.Time + 1e-4f)
                {
                    hit = Math.Min(e.Time, kill);
                    break;
                }
            }

            var window = windowSeconds > MinSpan ? windowSeconds : WindowSeconds;
            var start = Math.Max(first, kill - window);
            var end = Math.Min(last, kill + TailSeconds);
            if (end - start < MinSpan)
                return plan;

            plan.Valid = true;
            plan.KillerId = death.Actor;
            plan.VictimId = victimId;
            plan.Start = start;
            plan.End = end;
            plan.KillTime = kill;
            plan.HitTime = Math.Max(start, Math.Min(hit, end));
            plan.WeaponIndex = death.Weapon;
            plan.Headshot = death.Flag;
            return plan;
        }

        /// <summary>Tekrar zamanında oynatma hızı: isabet penceresinde 0.3x, dışarıda 1x.</summary>
        public static float PlaybackSpeed(in KillCamPlan plan, float replayTime)
        {
            if (!plan.Valid)
                return 1f;
            return replayTime >= plan.HitTime - SlowBefore && replayTime <= plan.HitTime + SlowAfter ? SlowSpeed : 1f;
        }

        /// <summary>Gerçek dt'yi tekrar zamanına çevirir (yavaşlatma bölgesine girişte hızı o noktadan uygular).</summary>
        public static float Advance(in KillCamPlan plan, float replayTime, float realDt)
        {
            if (realDt <= 0f)
                return replayTime;
            return replayTime + realDt * PlaybackSpeed(plan, replayTime);
        }

        public static bool IsFinished(in KillCamPlan plan, float replayTime) => !plan.Valid || replayTime >= plan.End;

        /// <summary>Gerçek saniye cinsinden tahmini toplam süre (yavaşlatma dahil).</summary>
        public static float RealDuration(in KillCamPlan plan)
        {
            if (!plan.Valid)
                return 0f;
            var span = plan.End - plan.Start;
            var slowLo = Math.Max(plan.Start, plan.HitTime - SlowBefore);
            var slowHi = Math.Min(plan.End, plan.HitTime + SlowAfter);
            var slow = Math.Max(0f, slowHi - slowLo);
            return (span - slow) + slow / SlowSpeed;
        }

        /// <summary>Kill anında 1'e fırlayıp VignetteFade sn'de sönen kırmızı vinyet gücü (0..1); öncesinde hafif hazırlık.</summary>
        public static float VignetteIntensity(in KillCamPlan plan, float replayTime)
        {
            if (!plan.Valid)
                return 0f;
            var dt = replayTime - plan.KillTime;
            if (dt < -0.15f)
                return 0f;
            if (dt < 0f)
                return 0.25f * (1f + dt / 0.15f);
            if (dt >= VignetteFade)
                return 0f;
            var k = 1f - dt / VignetteFade;
            return 0.25f + 0.75f * k * k;
        }

        /// <summary>
        /// Katilin omuz arkası pozu: gözünün 1.7 m arkası, 0.45 m sağı ve 0.25 m üstü; bakış, katilin nişan yönünde ileri
        /// (hedefe doğru hafif harmanlanır). Katil o anda örneklenemezse Valid=false.
        /// </summary>
        public static KillCamPose Pose(ReplayData data, in KillCamPlan plan, float replayTime)
        {
            var pose = new KillCamPose();
            if (!plan.Valid || data == null || !ReplayTimeline.Sample(data, plan.KillerId, replayTime, out var k))
                return pose;

            var eye = EyeHeight(k.Stance);
            var yaw = k.Yaw * DegToRad;
            var pitch = k.Pitch * DegToRad;
            var cp = (float)Math.Cos(pitch);
            // Birim ileri (Unity: yaw Y ekseni etrafında, +Z ileri).
            var fx = (float)Math.Sin(yaw) * cp;
            var fy = -(float)Math.Sin(pitch);
            var fz = (float)Math.Cos(yaw) * cp;
            // Yatay sağ.
            var rx = (float)Math.Cos(yaw);
            var rz = -(float)Math.Sin(yaw);

            var ex = k.X;
            var ey = k.Y + eye;
            var ez = k.Z;

            pose.PosX = ex - fx * 1.7f + rx * 0.45f;
            pose.PosY = ey - fy * 1.7f + 0.25f;
            pose.PosZ = ez - fz * 1.7f + rz * 0.45f;

            // Nişan noktası: katilin baktığı yönde 25 m.
            var ax = ex + fx * 25f;
            var ay = ey + fy * 25f;
            var az = ez + fz * 25f;
            pose.LookX = ax;
            pose.LookY = ay;
            pose.LookZ = az;

            if (ReplayTimeline.Sample(data, plan.VictimId, replayTime, out var v))
            {
                // Kurban nişan çizgisine yakınsa hedefe harmanla: kurban hep kadrajda kalsın.
                var vx = v.X;
                var vy = v.Y + 1.1f;
                var vz = v.Z;
                pose.LookX = ax + (vx - ax) * 0.35f;
                pose.LookY = ay + (vy - ay) * 0.35f;
                pose.LookZ = az + (vz - az) * 0.35f;
            }

            pose.Valid = true;
            return pose;
        }

        public static float EyeHeight(byte stance)
        {
            switch (stance)
            {
                case 1: return 1.05f;
                case 2: return 0.45f;
                default: return 1.6f;
            }
        }

        private const float DegToRad = 0.017453292519943295f;
    }
}
