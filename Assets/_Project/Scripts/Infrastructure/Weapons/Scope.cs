using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Dürbün ortak durumu: silah başına sıfırlama menzili (PageUp/PageDown), aktif büyütme.
    /// Balistik buradan <see cref="ZeroingAngle"/> ile namlu açısını okur.
    /// </summary>
    public static class Scope
    {
        private static readonly Dictionary<string, int> ZeroIndex = new Dictionary<string, int>();

        /// <summary>Aktif dürbünün büyütmesi (1 = yok). PlayerWeaponHandler CurrentZoom hedefi olarak kullanabilir.</summary>
        public static float CurrentMagnification { get; internal set; } = 1f;

        /// <summary>PiP dürbün lensi aktif mi (true ise ana kamera yakınlaşmamalı).</summary>
        public static bool PipActive { get; internal set; }

        public static int DefaultZeroingIndex(string weaponId)
        {
            if (!string.IsNullOrEmpty(weaponId) && WeaponCatalog.TryGet(weaponId, out var d) && d != null)
            {
                if (d.Category == WeaponCategory.Sniper)
                    return ScopeMath.NearestZeroingIndex(300f);
                if (d.Category == WeaponCategory.Dmr)
                    return ScopeMath.NearestZeroingIndex(200f);
            }

            return 0;
        }

        public static int GetZeroingIndex(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return 0;
            return ZeroIndex.TryGetValue(weaponId, out var i) ? i : DefaultZeroingIndex(weaponId);
        }

        /// <summary>Sıfırlama menzili (m).</summary>
        public static int ZeroingMeters(string weaponId) => ScopeMath.ZeroingSteps[GetZeroingIndex(weaponId)];

        /// <summary>Basamağı değiştirir (+1 uzağa, -1 yakına); yeni menzili (m) döner.</summary>
        public static int StepZeroing(string weaponId, int direction)
        {
            if (string.IsNullOrEmpty(weaponId))
                return ScopeMath.ZeroingSteps[0];
            var next = ScopeMath.StepZeroing(GetZeroingIndex(weaponId), direction);
            ZeroIndex[weaponId] = next;
            return ScopeMath.ZeroingSteps[next];
        }

        /// <summary>Yalnız anlık durumu (büyütme/PiP) sıfırlar; sıfırlama menzilleri kalır.</summary>
        public static void ClearRuntime()
        {
            CurrentMagnification = 1f;
            PipActive = false;
        }

        public static void ResetAll()
        {
            ZeroIndex.Clear();
            CurrentMagnification = 1f;
            PipActive = false;
        }

        /// <summary>
        /// Namlu yukarı açısı (derece): mermi başlangıç yönüne pitch olarak eklenir (yukarı = pozitif).
        /// Dürbünsüz, hiç ayarlanmamış silahlarda 0 döner (mevcut balistik değişmez).
        /// </summary>
        public static float ZeroingAngle(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || !WeaponCatalog.TryGet(weaponId, out var d) || d == null)
                return 0f;
            if (!d.HasScope && !ZeroIndex.ContainsKey(weaponId))
                return 0f;
            return ScopeMath.ZeroingAngleDegrees(ZeroingMeters(weaponId), d.MuzzleVelocity);
        }
    }
}
