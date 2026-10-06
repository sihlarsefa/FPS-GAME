using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Sürülebilir araca (Kirpi) yolcu olarak binme/inme için BotController kancaları. Biniş sırasında denetleyici
    /// kapalı kalır (bkz. <see cref="BotVehicleBoarding"/>); burası yarım kalan ateş/hareket/talep durumunu temizler,
    /// inişte NavMesh'e oturtup ajanı güvenle geri açar.
    /// </summary>
    public sealed partial class BotController
    {
        /// <summary>Binmeden hemen önce: devam eden atış/revive/hareket/siper talebi bırakılır.</summary>
        internal void PrepareForVehicle()
        {
            _burstRemaining = 0;
            _triggerWasHeld = false;
            _wantsMove = false;
            _velocity = Vector3.zero;
            _hasRequestedDestination = false;
            State = BotState.Idle;
            AbortReviveAttempt();
            _director?.ReleaseClaims(this);

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
        }

        /// <summary>
        /// Araçtan indikten sonra: konum NavMesh'e (yoksa zemine) oturtulur, çarpıştırıcı ve ajan yalnızca NavMesh
        /// varsa açılır, LOD/algı zamanlayıcıları sıfırlanır. Ölü bot için çağrılmaz.
        /// </summary>
        internal void ResumeAfterVehicle(Vector3 point, float yaw)
        {
            LandAt(point, yaw, DisembarkSearchRadius);
            if (gameObject.activeInHierarchy)
                EnableAgentIfPossible();

            _velocity = Vector3.zero;
            _lodTier = 0;
            _lodAccum = 0f;
            _lodNextTick = 0f;
            _lodNextEval = 0f;
            var now = Time.time;
            _nextPerception = now;
            _nextDecision = now + 0.1f;
            RequestDecision();
        }

        /// <summary>Bot ölü mü (HandleDeath çalıştı).</summary>
        internal bool IsDeadBot => _dead;
    }
}
