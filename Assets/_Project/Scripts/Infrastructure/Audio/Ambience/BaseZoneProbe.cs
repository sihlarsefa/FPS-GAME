using Project.Core.Domain;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Audio.Ambience
{
    /// <summary>Üs bölgesi mesafe matematiği (saf; Unity nesnesi yok). XZ düzleminde dinleyici-merkez mesafesi.</summary>
    public static class BaseZoneMath
    {
        /// <summary>Yakınlık eşiği: bölge yarıçapına eklenen pay (m).</summary>
        public const float Margin = 25f;
        /// <summary>Çıkış histerezisi: bölgeden çıkarken ek pay (m); sınırda titremeyi önler.</summary>
        public const float ExitHysteresis = 15f;

        public static bool IsBase(LocationKind k) => k == LocationKind.Karakol || k == LocationKind.ForwardBase || k == LocationKind.Outpost;

        public static bool Inside(float dx, float dz, float radius, bool wasNear)
        {
            var r = Mathf.Max(10f, radius) + Margin + (wasNear ? ExitHysteresis : 0f);
            return dx * dx + dz * dz <= r * r;
        }
    }

    /// <summary>
    /// 1 Hz: dinleyici üs/karakol/mevzi bölgesine girince <see cref="AmbienceBeds.SetNearBase"/>; ayrıca canlı yağış/rüzgârı
    /// (WeatherSystem + WindSystem, salt okunur) ortam yataklarına aktarır. [AmbienceBeds] nesnesine otomatik eklenir.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class BaseZoneProbe : MonoBehaviour
    {
        private const float Period = 1f;
        private float _next;
        private bool _near;

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now < _next)
                return;
            _next = now + Period;
            ProbeBase();
            SyncWeather();
        }

        private void ProbeBase()
        {
            var layout = WorldGenerator.LastLayout;
            if (layout == null || layout.Locations == null || !AudioMix.TryListenerPosition(out var p))
                return;
            var near = false;
            for (var i = 0; i < layout.Locations.Count && !near; i++)
            {
                var loc = layout.Locations[i];
                if (loc == null || !BaseZoneMath.IsBase(loc.Kind))
                    continue;
                near = BaseZoneMath.Inside(p.x - loc.Center.x, p.z - loc.Center.y, loc.Radius, _near);
            }

            if (near == _near)
                return;
            _near = near;
            AmbienceBeds.SetNearBase(near);
        }

        private static void SyncWeather()
        {
            var ws = WeatherSystem.Instance;
            var rain = ws != null ? ws.Current.Rain : 0f;
            AmbienceBeds.SetLiveWeather(rain, WindSystem.Current.Strength);
        }
    }
}
