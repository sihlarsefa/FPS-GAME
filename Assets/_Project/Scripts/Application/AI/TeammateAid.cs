using Project.Core.Domain;

namespace Project.Application.AI
{
    public enum AidAction
    {
        None = 0,
        MoveToWounded = 1,
        Revive = 2,
        CoverWounded = 3,
        Wait = 4
    }

    /// <summary>
    /// Yaralıya yardım kararı (saf mantık). Sahada BotReviveBehaviour/ReviveService ile birlikte kullanılır;
    /// burası sadece "yardım edeyim mi, ne yapayım" kararını verir.
    /// </summary>
    public static class TeammateAid
    {
        public const float ReviveRange = 2f;
        public const float MaxHelpDistance = 40f;

        /// <summary>
        /// Karar: tehlikede (ateş altı, düşman yakın) önce siper/örtme; güvendeyse yaralıya git ve kaldır.
        /// Yalnızca en yakın sağlıklı üye gitsin diye <paramref name="isClosestHelper"/> kullanılır.
        /// </summary>
        public static AidAction Decide(Float3 self, Float3 wounded, bool woundedDown, bool isClosestHelper,
            bool selfUnderFire, bool enemyNearWounded, float selfHealth01)
        {
            if (!woundedDown || !isClosestHelper || selfHealth01 < 0.3f) return AidAction.None;
            var dist = Float3.DistanceXZ(self, wounded);
            if (dist > MaxHelpDistance) return AidAction.None;
            if (selfUnderFire || enemyNearWounded) return dist < 12f ? AidAction.CoverWounded : AidAction.Wait;
            return dist <= ReviveRange ? AidAction.Revive : AidAction.MoveToWounded;
        }

        /// <summary>Verilen yardımcılar arasında yaralıya en yakın olanın indeksi (-1: yok).</summary>
        public static int ClosestHelper(Float3 wounded, Float3[] helpers, bool[] eligible, int count)
        {
            var best = -1;
            var bestD = float.MaxValue;
            for (var i = 0; i < count && i < helpers.Length; i++)
            {
                if (eligible != null && !eligible[i]) continue;
                var d = Float3.DistanceXZ(wounded, helpers[i]);
                if (d < bestD) { bestD = d; best = i; }
            }

            return best;
        }
    }
}
