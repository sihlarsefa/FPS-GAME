using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Editörde üretilen malzeme/doku varlıkları (Resources/GameArtLibrary). Yoksa çalışma zamanı üretimi kullanılır.</summary>
    [CreateAssetMenu(menuName = "HAREKAT/Game Art Library", fileName = "GameArtLibrary")]
    public sealed class GameArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "GameArtLibrary";

        public Material[] materials = new Material[0];
        public Material terrainMaterial;
        public Material skybox;
        public Texture2D softParticle;

        public static GameArtLibrary Load()
        {
            return Resources.Load<GameArtLibrary>(ResourcePath);
        }
    }
}
