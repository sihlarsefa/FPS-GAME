using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio.Ambience;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Maç başı/sonu ses kurulumu tek noktada: <see cref="AudioMix.Bind"/> + <see cref="AmbienceBeds.Configure"/>.
    /// BallisticsSystem (her modda maç başında Create edilir) her karede <see cref="Tick"/> çağırır; olay veri yolu ya da konfig
    /// değişmedikçe tek referans karşılaştırmasıdır. Sahne/oyun sonunda BallisticsSystem yok olurken <see cref="End"/> çağrılır.
    /// </summary>
    public static class AudioMatchHooks
    {
        private const float LocalWaitSeconds = 3f;

        private static IEventBus _boundBus;
        private static float _firstSeen = -1f;

        public static bool IsBound => _boundBus != null;

        public static void Tick(IEventBus bus, MatchConfig config)
        {
            if (bus == null || ReferenceEquals(bus, _boundBus) || !GameAudio.Enabled)
                return;

            var now = Time.unscaledTime;
            if (_firstSeen < 0f)
                _firstSeen = now;

            // Yerel oyuncu kaydı gelene kadar (en çok birkaç sn) bekle; yoksa (başsız/eğitim) geçersiz kimlikle bağla.
            var local = CombatantRegistry.LocalPlayer;
            if (local == null && now - _firstSeen < LocalWaitSeconds)
                return;

            Begin(bus, config, local != null ? local.Id : PlayerId.Invalid);
        }

        public static void Begin(IEventBus bus, MatchConfig config, PlayerId localPlayer)
        {
            End();
            if (bus == null)
                return;
            _boundBus = bus;
            AudioMix.Bind(bus, localPlayer);
            var map = MapCatalog.Normalize(config != null ? config.MapName : null);
            AmbienceBeds.Configure(map, config != null ? config.TimeOfDay : TimeOfDay.Gunduz,
                config != null ? config.Weather : WeatherKind.Acik);
        }

        public static void End()
        {
            _firstSeen = -1f;
            if (_boundBus == null)
                return;
            _boundBus = null;
            AudioMix.Unbind();
            AmbienceBeds.Stop();
        }
    }
}
