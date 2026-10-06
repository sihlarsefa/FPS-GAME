using System;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Combat;
using Project.Online.Sim;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Maça sonradan katılma (join-in-progress): sunucu bölge fazı/çemberleri ve hayatta kalan oyuncu listesini
    /// <see cref="JoinState"/> olarak toplar; istemci alınca <see cref="Received"/> olayını yükseltir.
    /// Bölge servisine uygulama (restore) ENTEGRASYON: olaya abone olan sistem yapar.
    /// </summary>
    public static class JoinStateProvider
    {
        /// <summary>İstemci: sunucudan JoinState geldi (son durum <see cref="Last"/>).</summary>
        public static event Action<JoinState> Received;
        public static JoinState Last { get; private set; }

        public static JoinState Build()
        {
            var s = new JoinState();
            try
            {
                if (GameContext.TryGet<IZoneService>(out var zone) && zone != null)
                {
                    var cur = zone.CurrentZone;
                    var next = zone.NextZone;
                    s.ZonePhaseIndex = (byte)Math.Max(0, Math.Min(255, zone.PhaseIndex));
                    s.ZoneStage = (byte)zone.Stage;
                    s.StageRemaining = zone.StageRemainingSeconds;
                    s.StageDuration = zone.StageDurationSeconds;
                    s.CenterX = cur.CenterX; s.CenterZ = cur.CenterZ; s.Radius = cur.Radius; s.Dps = cur.DamagePerSecond;
                    s.NextCenterX = next.CenterX; s.NextCenterZ = next.CenterZ; s.NextRadius = next.Radius;
                }
            }
            catch (Exception) { /* servis hazır değil: bölge alanları 0 kalır */ }

            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count && s.AliveIds.Count < JoinState.MaxAlive; i++)
            {
                var c = all[i];
                if (c != null && c.IsAlive && c.Id.IsValid)
                    s.AliveIds.Add(c.Id.Value);
            }

            return s;
        }

        internal static void Deliver(JoinState state)
        {
            Last = state;
            Received?.Invoke(state);
        }
    }
}
