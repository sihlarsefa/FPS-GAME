using Project.Infrastructure.World;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Su yüzeyi yüksekliği. Öncelik: <see cref="GameVfx.WaterLevel"/> ile verilen açık değer; yoksa sahnedeki
    /// <see cref="WorldMetadata.Instance"/>.WaterLevel (küresel su düzlemi, sözleşme §3.11). Dünya yoksa (menü, test
    /// sahnesi) su yok sayılır. Ayırma yapmaz.
    /// </summary>
    internal static class WaterLevelSource
    {
        private static float _override = float.NaN;

        /// <summary>NaN = otomatik (WorldMetadata).</summary>
        public static float Override
        {
            get => _override;
            set => _override = value;
        }

        public static bool TryGet(out float level)
        {
            if (!float.IsNaN(_override) && !float.IsInfinity(_override))
            {
                level = _override;
                return true;
            }

            var metadata = WorldMetadata.Instance;
            if (metadata != null)
            {
                level = metadata.WaterLevel;
                return !float.IsNaN(level) && !float.IsInfinity(level);
            }

            level = 0f;
            return false;
        }

        /// <summary>Sahne geçişinde çağrılır (otomatik kaynak her çağrıda yeniden okunur; tutulan durum yok).</summary>
        public static void Reset()
        {
        }

        /// <summary>Oynatma oturumu başında (alan yeniden yüklemesi kapalıyken) açık değeri de temizler.</summary>
        public static void ResetAll()
        {
            _override = float.NaN;
        }
    }
}
