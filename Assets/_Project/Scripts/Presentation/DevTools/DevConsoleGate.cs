using System;
using UnityEngine;

namespace Project.Presentation.DevTools
{
    /// <summary>
    /// Geliştirici konsolunun ne zaman aktif olacağını belirler.
    /// Editör ve Development Build'de açıktır; Release'te yalnızca <c>-dev</c> argümanıyla açılır.
    /// </summary>
    public static class DevConsoleGate
    {
        private static bool? _cached;

        /// <summary>Bu süreçte konsol kullanılabilir mi?</summary>
        public static bool IsAvailable
        {
            get
            {
                if (_cached.HasValue)
                    return _cached.Value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _cached = true;
#else
                _cached = HasDevFlag();
#endif
                return _cached.Value;
            }
        }

        /// <summary>Komut satırında <c>-dev</c> var mı?</summary>
        public static bool HasDevFlag()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args == null)
                    return false;

                for (var i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "-dev", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception)
            {
                // Argüman okunamazsa kapalı kalsın.
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cached = null;
        }
    }
}
