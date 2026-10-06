using System;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// CoD tarzı isabet sesi seçimi (saf kural + ince oynatıcı): hasar arttıkça perde yükselir; zırh "tink",
    /// et "tok" katmanı; kafa ve öldürme ayrı ton. CrosshairView.ShowHit bunu çağırabilir (ENTEGRASYON).
    /// </summary>
    public static class HitTones
    {
        public const float MinPitch = 0.92f;
        public const float MaxPitch = 1.42f;
        public const float FullDamage = 100f;
        public const float MinGap = 0.045f;

        public readonly struct Tone
        {
            public readonly SoundId Marker;
            public readonly float MarkerVolume;
            public readonly float MarkerPitch;
            public readonly SoundId Layer;
            public readonly float LayerVolume;
            public readonly float LayerPitch;

            public Tone(SoundId marker, float markerVolume, float markerPitch, SoundId layer, float layerVolume, float layerPitch)
            {
                Marker = marker; MarkerVolume = markerVolume; MarkerPitch = markerPitch;
                Layer = layer; LayerVolume = layerVolume; LayerPitch = layerPitch;
            }

            public bool HasLayer => Layer != SoundId.None;
        }

        /// <summary>Hasara göre perde: 0 -> MinPitch, FullDamage ve üstü -> MaxPitch.</summary>
        public static float PitchForDamage(float damage)
        {
            var t = Math.Max(0f, Math.Min(1f, damage / FullDamage));
            return MinPitch + (MaxPitch - MinPitch) * t;
        }

        public static Tone Select(float damage, bool headshot, bool kill, bool armorAbsorbed)
        {
            var pitch = PitchForDamage(damage);
            if (kill)
                return new Tone(SoundId.KillConfirm, 0.8f, 1f, SoundId.None, 0f, 1f);

            var layer = armorAbsorbed ? (headshot ? SoundId.HitHelmet : SoundId.HitArmor) : SoundId.HitFlesh;
            var layerVol = armorAbsorbed ? 0.45f : 0.35f;
            // Zırhta tink biraz tiz, ette tok biraz pes.
            var layerPitch = armorAbsorbed ? pitch * 1.1f : pitch * 0.85f;
            var marker = headshot ? SoundId.Headshot : SoundId.HitMarker;
            return new Tone(marker, headshot ? 0.7f : 0.5f, headshot ? pitch * 1.08f : pitch, layer, layerVol, layerPitch);
        }

        /// <summary>Saçma isabetlerinde sesi çoğaltmamak için: öldürme her zaman geçer.</summary>
        public static bool ShouldPlay(bool kill, float now, float lastPlayed) => kill || now - lastPlayed > MinGap;

        public static void Play(float damage, bool headshot, bool kill, bool armorAbsorbed)
        {
            var tone = Select(damage, headshot, kill, armorAbsorbed);
            try
            {
                GameAudio.Play2D(tone.Marker, tone.MarkerVolume, tone.MarkerPitch);
                if (tone.HasLayer)
                    GameAudio.Play2D(tone.Layer, tone.LayerVolume, tone.LayerPitch);
            }
            catch (Exception)
            {
                // ses sistemi yoksa sessiz devam
            }
        }

        // ---- Uzun menzil isabet hissi (saf kurallar) ----
        public const float LongRangeMeters = 150f;
        public const float FarKillMeters = 250f;
        public const float DefaultMuzzleSpeed = 850f;
        public const float MaxConfirmDelay = 1.2f;
        public const float MinConfirmDelay = 0.08f;

        public static bool IsLongRange(float distance) => distance >= LongRangeMeters;
        public static bool IsFarKill(float distance, bool kill) => kill && distance >= FarKillMeters;

        /// <summary>Gecikmeli onay süresi (sn): mesafe/hız, [Min, Max] arası; 150 m altında 0 (anında).</summary>
        public static float ConfirmDelay(float distance, float muzzleSpeed)
        {
            if (!IsLongRange(distance))
                return 0f;
            var speed = muzzleSpeed > 1f ? muzzleSpeed : DefaultMuzzleSpeed;
            return Math.Max(MinConfirmDelay, Math.Min(MaxConfirmDelay, distance / speed));
        }

        /// <summary>Zırh çınlaması uzakta incelir: 150 m'de 1, 400 m ve ötesinde 0.35 çarpan.</summary>
        public static float ArmorRingScale(float distance)
        {
            if (distance <= LongRangeMeters) return 1f;
            var t = Math.Min(1f, (distance - LongRangeMeters) / 250f);
            return 1f - 0.65f * t;
        }

        /// <summary>Uzak onay tonu: ince çan; uzak kafa isabetinde ekstra tiz.</summary>
        public static Tone SelectFar(float distance, bool headshot, bool kill, bool armorAbsorbed)
        {
            var bell = headshot ? 1.55f : 1.3f;
            if (headshot && distance >= FarKillMeters) bell = 1.75f;
            var vol = 0.4f * (armorAbsorbed ? ArmorRingScale(distance) : 1f);
            var layer = armorAbsorbed ? (headshot ? SoundId.HitHelmet : SoundId.HitArmor) : SoundId.None;
            var layerVol = armorAbsorbed ? 0.3f * ArmorRingScale(distance) : 0f;
            return new Tone(kill ? SoundId.KillConfirm : (headshot ? SoundId.Headshot : SoundId.HitMarker),
                Math.Max(0.15f, vol + (kill ? 0.2f : 0f)), bell, layer, layerVol, 1.3f);
        }

        public static void PlayFar(float distance, bool headshot, bool kill, bool armorAbsorbed)
        {
            var tone = SelectFar(distance, headshot, kill, armorAbsorbed);
            try
            {
                GameAudio.Play2D(tone.Marker, tone.MarkerVolume, tone.MarkerPitch);
                if (tone.HasLayer)
                    GameAudio.Play2D(tone.Layer, tone.LayerVolume, tone.LayerPitch);
            }
            catch (Exception) { }
        }
    }
}
