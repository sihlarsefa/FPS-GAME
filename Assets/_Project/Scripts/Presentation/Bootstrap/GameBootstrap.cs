using System;
using Project.Infrastructure;
using UnityEngine;
using IServiceProvider = Project.Core.Interfaces.IServiceProvider;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// ESKİ giriş noktası. Yerini <see cref="MatchBootstrap"/> (harekât), <see cref="TrainingBootstrap"/> (poligon) ve
    /// <see cref="MainMenuBootstrap"/> (menü) aldı. Eski sahnelerde bulunursa harekât kurulumunu başlatır.
    /// </summary>
    [Obsolete("MatchBootstrap / TrainingBootstrap / MainMenuBootstrap kullanın.")]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Eski erişim noktası: artık GameContext'in servis sağlayıcısını döndürür.</summary>
        public static IServiceProvider Services => GameContext.Services;

        private void Awake()
        {
            if (FindAnyObjectByType<MatchBootstrap>() != null || FindAnyObjectByType<TrainingBootstrap>() != null)
                return;

            if (GameContext.IsReady)
                return;

            gameObject.AddComponent<MatchBootstrap>();
        }
    }
}
