using System;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Maç sonu ekranı (geçici iskelet).</summary>
    public sealed class EndScreen : MonoBehaviour
    {
        public static EndScreen Show(MatchResult result, Action onRestart, Action onMainMenu)
        {
            return new GameObject("EndScreen").AddComponent<EndScreen>();
        }
    }
}
