using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Askerî harita stilinde mini harita dokusu üretici. STUB — uygulanıyor.</summary>
    public static class MinimapTextureGenerator
    {
        public static Texture2D Generate(Terrain terrain, MapLayout layout, IReadOnlyList<Bounds> structures, int size)
        {
            size = Mathf.Clamp(size, 64, 4096);
            return new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HK_Minimap" };
        }
    }
}
