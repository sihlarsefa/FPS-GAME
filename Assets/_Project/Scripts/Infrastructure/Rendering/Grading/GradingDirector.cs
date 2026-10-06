using System;
using UnityEngine;

namespace Project.Infrastructure.Rendering.Grading
{
    /// <summary>
    /// KALDIRILDI: derecelendirme artık yalnızca PostProcessing (GradingPresets verisi + GradingWriter) üzerinden yazılır.
    /// Bu bileşen eski sahnelerde referans kırılmasın diye boş bırakıldı; hiçbir alana yazmaz.
    /// </summary>
    [Obsolete("PostProcessing.SetLobbyGrade / ApplyMapGrade kullanın; GradingDirector artık bir şey yapmaz.")]
    public sealed class GradingDirector : MonoBehaviour
    {
        public bool LobbyMode;
        public float BlendSeconds = 2.5f;
    }
}
