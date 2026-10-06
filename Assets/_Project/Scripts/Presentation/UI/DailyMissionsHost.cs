using System;
using Project.Application.Services;
using Project.Presentation.Bootstrap;

namespace Project.Presentation.UI
{
    /// <summary>Paylaşılan <see cref="DailyMissions"/> örneği (GameSession deposu üzerinde kalıcı) + toast metni olayı.</summary>
    public static class DailyMissionsHost
    {
        private static DailyMissions _shared;

        /// <summary>Görev tamamlandı: toast metni. AchievementToastView abone olur.</summary>
        public static event Action<string> Notified;

        public static DailyMissions Shared
        {
            get
            {
                if (_shared != null) return _shared;
                var store = GameSession.Store;
                if (store == null) return null;
                _shared = new DailyMissions(store);
                _shared.Completed += m =>
                    Notified?.Invoke("GÜNLÜK GÖREV TAMAM: " + DailyMissions.Describe(m) + "  +" + m.Tp + " TP  +" + m.Keys + " ANAHTAR");
                return _shared;
            }
        }

        public static void Reset() { _shared?.Dispose(); _shared = null; }
    }
}
