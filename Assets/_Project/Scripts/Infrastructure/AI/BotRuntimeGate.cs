using System;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Bot yürütme kapısı (bağımlılık tersine çevirme): Online assembly'si (ör. ServerBotGate) <see cref="ShouldRunBots"/>
    /// alanını atar; istemci modunda false dönerek botların yalnızca sunucuda çalışmasını sağlar. Varsayılan: çalışır.
    /// </summary>
    public static class BotRuntimeGate
    {
        public static Func<bool> ShouldRunBots;

        public static bool Allowed
        {
            get
            {
                var gate = ShouldRunBots;
                if (gate == null)
                    return true;
                try { return gate(); }
                catch (Exception) { return true; }
            }
        }
    }
}
