using System;
using Project.Infrastructure.Config;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// ESKİ test arenası. Artık atış poligonu kurulumuna (<see cref="TrainingBootstrap"/>) yönlendirir.
    /// <c>movementConfig</c> alanı eski sahne/editör araçlarıyla uyumluluk için korunur.
    /// </summary>
    [Obsolete("TrainingBootstrap kullanın.")]
    [DisallowMultipleComponent]
    public sealed class TestArenaBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig movementConfig;

        public PlayerMovementConfig MovementConfig => movementConfig;

        private void Awake()
        {
            if (FindAnyObjectByType<TrainingBootstrap>() != null || FindAnyObjectByType<MatchBootstrap>() != null)
                return;

            GameSession.Mode = Core.Domain.GameMode.Training;
            gameObject.AddComponent<TrainingBootstrap>();
        }
    }
}
