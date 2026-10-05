using System;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// ESKİ: IMGUI tabanlı geçici maç HUD'u. Yerini <see cref="HudController"/> aldı; yalnızca eski sahne/prefab
    /// başvuruları kırılmasın diye derlenir ve hiçbir şey yapmaz.
    /// </summary>
    [Obsolete("HudController.Create(IPlayerHudSource) kullanın.")]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }
    }
}
