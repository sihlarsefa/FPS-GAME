using System;
using UnityEngine;
using static Project.Infrastructure.Audio.SynthDsp;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Tüm <see cref="SoundId"/> seslerini prosedürel olarak üretir (44.1 kHz mono).
    /// <see cref="Render"/> saf C#'tır ve iş parçacığı güvenlidir (paralel sentez için); <see cref="CreateClip"/>
    /// yalnızca ana iş parçacığında çağrılmalıdır. Aynı kimlik her zaman aynı dalga biçimini üretir (sabit tohum).
    /// </summary>
    public static partial class ProceduralAudioSynth
    {
        public const int SampleRate = SynthDsp.SampleRate;

        /// <summary>Kesintisiz döngü olarak tasarlanmış sesler.</summary>
        public static bool IsLoop(SoundId id)
        {
            switch (id)
            {
                case SoundId.HelicopterRotor:
                case SoundId.VehicleEngine:
                case SoundId.Wind:
                case SoundId.Ambience:
                case SoundId.DistantBattle:
                case SoundId.MenuMusic:
                case SoundId.Heartbeat:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Kimlik için PCM örneklerini üretir (None veya bilinmeyen kimlik için null).</summary>
        public static float[] Render(SoundId id)
        {
            var rng = new SynthRng(SeedFor(id));
            switch (id)
            {
                // Silahlar
                case SoundId.ShotPistol: return Gunshot(PistolSpec, ref rng);
                case SoundId.ShotSmg: return Gunshot(SmgSpec, ref rng);
                case SoundId.ShotRifle556: return Gunshot(Rifle556Spec, ref rng);
                case SoundId.ShotRifle762: return Gunshot(Rifle762Spec, ref rng);
                case SoundId.ShotDmr: return Gunshot(DmrSpec, ref rng);
                case SoundId.ShotSniper: return Gunshot(SniperSpec, ref rng);
                case SoundId.ShotShotgun: return Gunshot(ShotgunSpec, ref rng);
                case SoundId.ShotMachineGun: return Gunshot(MachineGunSpec, ref rng);

                // Mekanik
                case SoundId.DryFire: return DryFire(ref rng);
                case SoundId.ReloadMagOut: return ReloadMagOut(ref rng);
                case SoundId.ReloadMagIn: return ReloadMagIn(ref rng);
                case SoundId.ReloadBolt: return ReloadBolt(ref rng);
                case SoundId.ShellInsert: return ShellInsert(ref rng);
                case SoundId.FireModeSwitch: return FireModeSwitch(ref rng);
                case SoundId.WeaponEquip: return WeaponEquip(ref rng);

                // Hareket
                case SoundId.Footstep: return Footstep(ref rng);
                case SoundId.Jump: return Jump(ref rng);
                case SoundId.Land: return Land(ref rng);

                // Geri bildirim ve isabetler
                case SoundId.HitMarker: return HitMarker(ref rng);
                case SoundId.KillConfirm: return KillConfirm(ref rng);
                case SoundId.Headshot: return Headshot(ref rng);
                case SoundId.HitFlesh: return HitFlesh(ref rng);
                case SoundId.HitHelmet: return HitHelmet(ref rng);
                case SoundId.HitArmor: return HitArmor(ref rng);
                case SoundId.BulletImpact: return BulletImpact(ref rng);
                case SoundId.BulletImpactMetal: return BulletImpactMetal(ref rng);
                case SoundId.BulletWhiz: return BulletWhiz(ref rng);
                case SoundId.Punch: return Punch(ref rng);

                // Patlayıcılar
                case SoundId.Explosion: return Explosion(ref rng);
                case SoundId.ArtilleryWhistle: return ArtilleryWhistle(ref rng);
                case SoundId.GrenadePin: return GrenadePin(ref rng);
                case SoundId.GrenadeBounce: return GrenadeBounce(ref rng);
                case SoundId.SmokeHiss: return SmokeHiss(ref rng);

                // Eşya
                case SoundId.Bandage: return Bandage(ref rng);
                case SoundId.Drink: return Drink(ref rng);
                case SoundId.Pickup: return Pickup(ref rng);

                // Arayüz
                case SoundId.UiClick: return UiClick(ref rng);
                case SoundId.UiHover: return UiHover();
                case SoundId.UiConfirm: return UiConfirm(ref rng);
                case SoundId.ZoneWarning: return ZoneWarning();
                case SoundId.ZoneDamage: return ZoneDamage(ref rng);

                // Döngüler
                case SoundId.HelicopterRotor: return HelicopterRotorLoop(ref rng);
                case SoundId.VehicleEngine: return VehicleEngineLoop(ref rng);
                case SoundId.Wind: return WindLoop(ref rng);
                case SoundId.Ambience: return AmbienceLoop(ref rng);
                case SoundId.DistantBattle: return DistantBattleLoop(ref rng);
                case SoundId.MenuMusic: return MenuMusicLoop(ref rng);
                case SoundId.Heartbeat: return HeartbeatLoop(ref rng);

                // Telsiz ve diğer
                case SoundId.RadioBeep: return RadioBeep(ref rng);
                case SoundId.RadioChatter: return RadioChatter(ref rng);
                case SoundId.Death: return Death(ref rng);
                case SoundId.VehicleDoor: return VehicleDoor(ref rng);

                default:
                    return null;
            }
        }

        /// <summary>Kimlik için AudioClip üretir (ana iş parçacığı). Başarısızlıkta null.</summary>
        public static AudioClip CreateClip(SoundId id) => ToClip(id, Render(id));

        /// <summary>Önceden üretilmiş örneklerden AudioClip oluşturur (ana iş parçacığı).</summary>
        public static AudioClip ToClip(SoundId id, float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return null;

            var clip = AudioClip.Create("sfx_" + id, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        private static uint SeedFor(SoundId id) => 0x5EED1234u ^ ((uint)id * 2654435761u);

        private static float[] Finish(float[] b, float peak, float fadeOut = 0.012f)
        {
            Normalize(b, peak);
            FadeEdges(b, 0f, fadeOut);
            return b;
        }

        // =====================================================================================
        //  SİLAH SESLERİ
        // =====================================================================================

        private sealed class GunSpec
        {
            public float Duration = 1f;
            public float CrackAmp = 0.8f, CrackTau = 0.004f, CrackHighpass = 2200f;
            public float NWaveAmp;
            public float BodyAmp = 1f, BodyCut = 2000f, BodyTau = 0.05f;
            public float ThumpAmp = 0.8f, ThumpF0 = 110f, ThumpF1 = 50f, ThumpTau = 0.06f;
            public float TailAmp = 0.25f, TailCut = 800f, TailTau = 0.3f;
            public float Drive = 2f;
            public float MechAmp, MechTime = 0.03f, MechRing = 2400f;

            /// <summary>Üçlüler halinde: gecikme (s), genlik, alçak geçiren (Hz).</summary>
            public float[] Echoes;

            public float Peak = 0.95f;
        }

        private static readonly GunSpec PistolSpec = new GunSpec
        {
            Duration = 0.6f,
            CrackAmp = 0.55f, CrackTau = 0.0035f, CrackHighpass = 2500f,
            BodyAmp = 0.9f, BodyCut = 2600f, BodyTau = 0.03f,
            ThumpAmp = 0.55f, ThumpF0 = 160f, ThumpF1 = 70f, ThumpTau = 0.035f,
            TailAmp = 0.22f, TailCut = 1100f, TailTau = 0.14f,
            Drive = 1.6f, MechAmp = 0.12f, MechTime = 0.035f, MechRing = 2900f,
            Echoes = new[] { 0.11f, 0.08f, 1400f }
        };

        private static readonly GunSpec SmgSpec = new GunSpec
        {
            Duration = 0.5f,
            CrackAmp = 0.5f, CrackTau = 0.003f, CrackHighpass = 3000f,
            BodyAmp = 0.85f, BodyCut = 2800f, BodyTau = 0.025f,
            ThumpAmp = 0.5f, ThumpF0 = 170f, ThumpF1 = 75f, ThumpTau = 0.03f,
            TailAmp = 0.16f, TailCut = 1200f, TailTau = 0.1f,
            Drive = 1.8f, MechAmp = 0.1f, MechTime = 0.03f, MechRing = 3100f
        };

        private static readonly GunSpec Rifle556Spec = new GunSpec
        {
            Duration = 1.0f,
            CrackAmp = 0.9f, CrackTau = 0.004f, CrackHighpass = 2200f, NWaveAmp = 0.5f,
            BodyAmp = 0.9f, BodyCut = 2300f, BodyTau = 0.045f,
            ThumpAmp = 0.75f, ThumpF0 = 120f, ThumpF1 = 52f, ThumpTau = 0.055f,
            TailAmp = 0.28f, TailCut = 800f, TailTau = 0.3f,
            Drive = 2f, MechAmp = 0.08f, MechTime = 0.032f, MechRing = 2500f,
            Echoes = new[] { 0.16f, 0.12f, 1100f, 0.34f, 0.06f, 800f }
        };

        private static readonly GunSpec Rifle762Spec = new GunSpec
        {
            Duration = 1.2f,
            CrackAmp = 0.95f, CrackTau = 0.0045f, CrackHighpass = 2000f, NWaveAmp = 0.6f,
            BodyAmp = 1f, BodyCut = 1800f, BodyTau = 0.055f,
            ThumpAmp = 1f, ThumpF0 = 100f, ThumpF1 = 45f, ThumpTau = 0.075f,
            TailAmp = 0.32f, TailCut = 700f, TailTau = 0.38f,
            Drive = 2.2f, MechAmp = 0.08f, MechTime = 0.034f, MechRing = 2200f,
            Echoes = new[] { 0.18f, 0.14f, 1000f, 0.4f, 0.07f, 700f }
        };

        private static readonly GunSpec DmrSpec = new GunSpec
        {
            Duration = 1.6f,
            CrackAmp = 1f, CrackTau = 0.005f, CrackHighpass = 1800f, NWaveAmp = 0.7f,
            BodyAmp = 1f, BodyCut = 1700f, BodyTau = 0.06f,
            ThumpAmp = 1.05f, ThumpF0 = 92f, ThumpF1 = 42f, ThumpTau = 0.085f,
            TailAmp = 0.36f, TailCut = 650f, TailTau = 0.5f,
            Drive = 2.2f,
            Echoes = new[] { 0.2f, 0.18f, 950f, 0.45f, 0.1f, 750f, 0.8f, 0.05f, 550f }
        };

        private static readonly GunSpec SniperSpec = new GunSpec
        {
            Duration = 2.8f,
            CrackAmp = 1f, CrackTau = 0.006f, CrackHighpass = 1600f, NWaveAmp = 0.85f,
            BodyAmp = 1f, BodyCut = 1500f, BodyTau = 0.075f,
            ThumpAmp = 1.2f, ThumpF0 = 85f, ThumpF1 = 34f, ThumpTau = 0.12f,
            TailAmp = 0.42f, TailCut = 500f, TailTau = 0.75f,
            Drive = 2.4f,
            Echoes = new[] { 0.24f, 0.32f, 1200f, 0.52f, 0.22f, 900f, 0.9f, 0.14f, 700f, 1.4f, 0.07f, 500f }
        };

        private static readonly GunSpec ShotgunSpec = new GunSpec
        {
            Duration = 1.3f,
            CrackAmp = 0.7f, CrackTau = 0.005f, CrackHighpass = 1800f,
            BodyAmp = 1.1f, BodyCut = 1400f, BodyTau = 0.075f,
            ThumpAmp = 1.1f, ThumpF0 = 95f, ThumpF1 = 40f, ThumpTau = 0.09f,
            TailAmp = 0.35f, TailCut = 700f, TailTau = 0.42f,
            Drive = 2.3f,
            Echoes = new[] { 0.19f, 0.14f, 950f, 0.42f, 0.07f, 700f }
        };

        private static readonly GunSpec MachineGunSpec = new GunSpec
        {
            Duration = 0.75f,
            CrackAmp = 0.85f, CrackTau = 0.004f, CrackHighpass = 2200f, NWaveAmp = 0.55f,
            BodyAmp = 1f, BodyCut = 1900f, BodyTau = 0.04f,
            ThumpAmp = 1.1f, ThumpF0 = 115f, ThumpF1 = 48f, ThumpTau = 0.05f,
            TailAmp = 0.22f, TailCut = 750f, TailTau = 0.2f,
            Drive = 2.6f, MechAmp = 0.07f, MechTime = 0.028f, MechRing = 2300f,
            Echoes = new[] { 0.15f, 0.08f, 1000f }
        };

        /// <summary>
        /// Atış: süpersonik çat (N-dalga + yüksek geçirilmiş gürültü) + gövde (alçak geçirilmiş gürültü) +
        /// aşağı kayan düşük frekanslı darbe + ortam kuyruğu + vadi yankıları.
        /// </summary>
        private static float[] Gunshot(GunSpec s, ref SynthRng rng)
        {
            var buf = Buffer(s.Duration);
            var n = buf.Length;

            var crackHp = Biquad.Create(FilterKind.Highpass, s.CrackHighpass, 0.7f);
            var bodyLp = Biquad.Create(FilterKind.Lowpass, s.BodyCut, 0.8f);
            var bodyGain = BandwidthGain(FilterKind.Lowpass, s.BodyCut, 0.8f);
            var tailLp = Biquad.Create(FilterKind.Lowpass, s.TailCut, 0.7f);
            var tailGain = BandwidthGain(FilterKind.Lowpass, s.TailCut, 0.7f);

            var crackK = DecayFactor(s.CrackTau);
            var bodyK = DecayFactor(s.BodyTau);
            var bodyFastK = DecayFactor(s.BodyTau * 0.25f);
            var thumpK = DecayFactor(s.ThumpTau);
            var tailK = DecayFactor(s.TailTau);
            var tailAttackK = DecayFactor(0.012f);
            var sweepK = DecayFactor(0.025f);

            float crackEnv = 1f, bodyEnv = 1f, bodyFast = 1f, thumpEnv = 1f, tailEnv = 1f, tailAttack = 1f, sweep = 1f;
            var phase = 0.0;
            var attack = Math.Max(1, (int)(0.0004f * SampleRate));

            for (var i = 0; i < n; i++)
            {
                var a = i < attack ? (float)i / attack : 1f;
                var w = rng.Bipolar();
                var crack = crackHp.Process(w) * crackEnv * s.CrackAmp;
                var body = bodyLp.Process(w) * (bodyEnv * 0.7f + bodyFast * 0.6f) * s.BodyAmp * bodyGain;

                var f = s.ThumpF1 + (s.ThumpF0 - s.ThumpF1) * sweep;
                phase += f * Dt;
                var thump = Sin(phase) * thumpEnv * s.ThumpAmp;

                var tail = tailLp.Process(rng.Bipolar()) * tailEnv * (1f - tailAttack) * s.TailAmp * tailGain;

                buf[i] = (crack + body + thump) * a + tail;

                crackEnv *= crackK;
                bodyEnv *= bodyK;
                bodyFast *= bodyFastK;
                thumpEnv *= thumpK;
                tailEnv *= tailK;
                tailAttack *= tailAttackK;
                sweep *= sweepK;
            }

            if (s.NWaveAmp > 0f)
                NWave(buf, 0f, s.NWaveAmp * 2f, 0.0005f);

            if (s.MechAmp > 0f)
                Clack(buf, ref rng, s.MechTime, s.MechAmp * 2.5f, s.MechRing);

            if (s.Echoes != null && s.Echoes.Length >= 3)
            {
                var dry = (float[])buf.Clone();
                for (var e = 0; e + 2 < s.Echoes.Length; e += 3)
                    Echo(buf, dry, s.Echoes[e], s.Echoes[e + 1], s.Echoes[e + 2]);
            }

            Normalize(buf, 1f);
            SoftClip(buf, s.Drive);
            return Finish(buf, s.Peak, Math.Min(0.25f, s.Duration * 0.3f));
        }

        // =====================================================================================
        //  MEKANİK (şarjör, sürgü, seçici)
        // =====================================================================================

        private static float[] DryFire(ref SynthRng rng)
        {
            var b = Buffer(0.15f);
            Click(b, ref rng, 0f, 0.9f, 2500f, 0.002f);
            Ring(b, 0.0005f, 0.35f, 0.018f, false, 1850f, 2720f, 4100f);
            NoiseBurst(b, ref rng, 0f, 0.0005f, 0.01f, 0.3f, FilterKind.Lowpass, 600f);
            return Finish(b, 0.6f);
        }

        private static float[] ReloadMagOut(ref SynthRng rng)
        {
            var b = Buffer(0.4f);
            Click(b, ref rng, 0f, 0.8f, 2000f, 0.002f);
            Ring(b, 0f, 0.2f, 0.02f, false, 1400f, 2300f);
            NoiseSwell(b, ref rng, 0.02f, 0.13f, 0.25f, FilterKind.Bandpass, 1200f, 2200f, 2f, 0.5f);
            NoiseBurst(b, ref rng, 0.2f, 0.001f, 0.03f, 0.7f, FilterKind.Lowpass, 500f);
            Tone(b, 0.2f, 180f, 120f, 0.02f, 0.001f, 0.03f, 0.4f);
            return Finish(b, 0.6f);
        }

        private static float[] ReloadMagIn(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            NoiseSwell(b, ref rng, 0f, 0.085f, 0.25f, FilterKind.Bandpass, 1800f, 1100f, 2f, 0.6f);
            Clack(b, ref rng, 0.09f, 1f, 2400f);
            Click(b, ref rng, 0.106f, 0.6f, 3000f);
            NoiseBurst(b, ref rng, 0.09f, 0.0008f, 0.025f, 0.5f, FilterKind.Lowpass, 800f);
            return Finish(b, 0.7f);
        }

        private static float[] ReloadBolt(ref SynthRng rng)
        {
            var b = Buffer(0.6f);
            Click(b, ref rng, 0f, 0.8f, 2500f, 0.002f);
            Ring(b, 0f, 0.2f, 0.02f, false, 1600f, 2500f);
            NoiseSwell(b, ref rng, 0.01f, 0.12f, 0.35f, FilterKind.Bandpass, 900f, 2000f, 2f, 0.6f);
            Clack(b, ref rng, 0.15f, 1f, 2000f);
            NoiseSwell(b, ref rng, 0.27f, 0.09f, 0.3f, FilterKind.Bandpass, 2000f, 1000f, 2f, 0.6f);
            Clack(b, ref rng, 0.38f, 1f, 1700f);
            NoiseBurst(b, ref rng, 0.38f, 0.001f, 0.03f, 0.6f, FilterKind.Lowpass, 600f);
            return Finish(b, 0.7f);
        }

        private static float[] ShellInsert(ref SynthRng rng)
        {
            var b = Buffer(0.3f);
            NoiseSwell(b, ref rng, 0f, 0.05f, 0.2f, FilterKind.Bandpass, 1500f, 1500f, 1.2f);
            Click(b, ref rng, 0.06f, 0.7f, 1500f, 0.003f);
            NoiseBurst(b, ref rng, 0.06f, 0.001f, 0.025f, 0.7f, FilterKind.Lowpass, 700f);
            Tone(b, 0.06f, 260f, 200f, 0.02f, 0.001f, 0.02f, 0.3f);
            return Finish(b, 0.6f);
        }

        private static float[] FireModeSwitch(ref SynthRng rng)
        {
            var b = Buffer(0.1f);
            Click(b, ref rng, 0f, 0.8f, 3500f, 0.001f);
            Ring(b, 0f, 0.25f, 0.01f, false, 3100f);
            Click(b, ref rng, 0.035f, 0.6f, 3500f, 0.001f);
            Ring(b, 0.035f, 0.2f, 0.008f, false, 3600f);
            return Finish(b, 0.45f);
        }

        private static float[] WeaponEquip(ref SynthRng rng)
        {
            var b = Buffer(0.5f);
            NoiseSwell(b, ref rng, 0f, 0.28f, 0.35f, FilterKind.Bandpass, 1100f, 1700f, 0.7f, 0.5f);
            Clack(b, ref rng, 0.32f, 0.8f, 2200f);
            Click(b, ref rng, 0.34f, 0.5f);
            return Finish(b, 0.55f);
        }

        // =====================================================================================
        //  HAREKET
        // =====================================================================================

        private static float[] Footstep(ref SynthRng rng)
        {
            var b = Buffer(0.25f);
            // Topuk: düşük gövde + çakıl/toprak çıtırtısı
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.02f, 0.8f, FilterKind.Lowpass, 250f);
            Tone(b, 0f, 85f, 60f, 0.015f, 0.002f, 0.02f, 0.5f);
            NoiseBurst(b, ref rng, 0.004f, 0.002f, 0.035f, 0.7f, FilterKind.Bandpass, 900f, 0.9f);
            Grit(b, ref rng, 0.005f, 0.08f, 12, 0.18f);
            // Parmak ucu
            NoiseBurst(b, ref rng, 0.045f, 0.002f, 0.025f, 0.4f, FilterKind.Bandpass, 1300f, 1f);
            Grit(b, ref rng, 0.045f, 0.1f, 6, 0.1f);
            return Finish(b, 0.6f);
        }

        private static float[] Jump(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            NoiseSwell(b, ref rng, 0f, 0.25f, 0.3f, FilterKind.Bandpass, 1000f, 1500f, 0.8f, 0.5f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.03f, 0.6f, FilterKind.Bandpass, 800f, 0.9f);
            Grit(b, ref rng, 0f, 0.06f, 8, 0.12f);
            Ring(b, 0.06f, 0.1f, 0.03f, false, 2900f, 4300f);
            Ring(b, 0.12f, 0.07f, 0.03f, false, 3100f, 4600f);
            return Finish(b, 0.5f);
        }

        private static float[] Land(ref SynthRng rng)
        {
            var b = Buffer(0.5f);
            NoiseBurst(b, ref rng, 0f, 0.002f, 0.06f, 1f, FilterKind.Lowpass, 180f);
            Tone(b, 0f, 70f, 45f, 0.03f, 0.002f, 0.06f, 0.8f);
            NoiseBurst(b, ref rng, 0.003f, 0.002f, 0.05f, 0.8f, FilterKind.Bandpass, 700f, 0.9f);
            Grit(b, ref rng, 0.005f, 0.15f, 18, 0.2f);
            Ring(b, 0.04f, 0.1f, 0.03f, false, 2400f, 3700f);
            Ring(b, 0.09f, 0.08f, 0.03f, false, 2600f, 3900f);
            Ring(b, 0.13f, 0.06f, 0.025f, false, 2300f, 3500f);
            NoiseSwell(b, ref rng, 0f, 0.3f, 0.2f, FilterKind.Bandpass, 1200f, 900f, 0.8f, 0.5f);
            return Finish(b, 0.75f);
        }

        /// <summary>Rastgele küçük çıtırtı taneleri (çakıl, kırıntı).</summary>
        private static void Grit(float[] b, ref SynthRng rng, float start, float span, int count, float amp)
        {
            for (var g = 0; g < count; g++)
            {
                var t = start + rng.Unit() * span;
                var a = amp * rng.Range(0.3f, 1f) * (1f - (t - start) / (span * 1.2f));
                NoiseBurst(b, ref rng, t, 0.0002f, rng.Range(0.001f, 0.004f), a, FilterKind.Bandpass,
                    rng.Range(2000f, 5000f), 1.5f);
            }
        }

        // =====================================================================================
        //  GERİ BİLDİRİM VE İSABETLER
        // =====================================================================================

        private static float[] HitMarker(ref SynthRng rng)
        {
            var b = Buffer(0.08f);
            Tone(b, 0f, 1900f, 1900f, 0f, 0.0005f, 0.012f, 1f);
            Tone(b, 0f, 3800f, 3800f, 0f, 0.0005f, 0.006f, 0.3f);
            Click(b, ref rng, 0f, 0.3f, 4000f, 0.001f);
            return Finish(b, 0.55f, 0.005f);
        }

        private static float[] KillConfirm(ref SynthRng rng)
        {
            var b = Buffer(0.34f);
            Click(b, ref rng, 0f, 0.25f, 4000f, 0.001f);
            Note(b, 0f, 0.06f, 880f, 0.002f, 0.05f, 0.8f, 2);
            Note(b, 0.075f, 0.11f, 1320f, 0.003f, 0.12f, 0.9f, 2);
            return Finish(b, 0.6f);
        }

        private static float[] Headshot(ref SynthRng rng)
        {
            var b = Buffer(0.55f);
            Click(b, ref rng, 0f, 0.5f, 3000f, 0.0015f);
            const float f = 2100f;
            Tone(b, 0f, f, f, 0f, 0.0005f, 0.22f, 1f);
            Tone(b, 0f, f * 2.32f, f * 2.32f, 0f, 0.0005f, 0.12f, 0.5f);
            Tone(b, 0f, f * 4.25f, f * 4.25f, 0f, 0.0005f, 0.07f, 0.3f);
            Tone(b, 0f, f * 5.4f, f * 5.4f, 0f, 0.0005f, 0.05f, 0.15f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.02f, 0.4f, FilterKind.Lowpass, 500f);
            return Finish(b, 0.65f);
        }

        private static float[] HitFlesh(ref SynthRng rng)
        {
            var b = Buffer(0.2f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.028f, 1f, FilterKind.Lowpass, 700f);
            Tone(b, 0f, 130f, 70f, 0.02f, 0.001f, 0.03f, 0.7f);
            NoiseBurst(b, ref rng, 0.003f, 0.001f, 0.015f, 0.3f, FilterKind.Bandpass, 1300f, 1.2f);
            return Finish(b, 0.7f);
        }

        private static float[] HitHelmet(ref SynthRng rng)
        {
            var b = Buffer(0.4f);
            Click(b, ref rng, 0f, 0.8f, 3000f, 0.0015f);
            Ring(b, 0f, 0.7f, 0.11f, false, 1650f, 2930f, 4310f, 5600f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.02f, 0.4f, FilterKind.Lowpass, 600f);
            return Finish(b, 0.7f);
        }

        private static float[] HitArmor(ref SynthRng rng)
        {
            var b = Buffer(0.25f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.035f, 1f, FilterKind.Lowpass, 900f);
            Tone(b, 0f, 180f, 110f, 0.02f, 0.001f, 0.03f, 0.5f);
            Ring(b, 0f, 0.25f, 0.03f, false, 720f, 1350f);
            Click(b, ref rng, 0f, 0.4f, 2000f, 0.0015f);
            return Finish(b, 0.7f);
        }

        private static float[] BulletImpact(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            Click(b, ref rng, 0f, 1f, 2500f, 0.002f);
            NoiseBurst(b, ref rng, 0f, 0.0008f, 0.04f, 0.8f, FilterKind.Lowpass, 1600f);
            Tone(b, 0f, 150f, 90f, 0.015f, 0.001f, 0.02f, 0.3f);
            Grit(b, ref rng, 0.02f, 0.23f, 10, 0.25f);
            return Finish(b, 0.7f);
        }

        private static float[] BulletImpactMetal(ref SynthRng rng)
        {
            var b = Buffer(0.5f);
            Click(b, ref rng, 0f, 1f, 3000f, 0.0015f);
            Ring(b, 0f, 0.8f, 0.12f, false, 2250f, 3720f, 5140f, 6900f);
            Ring(b, 0.0005f, 0.3f, 0.09f, false, 2263f, 3741f);
            NoiseBurst(b, ref rng, 0f, 0.0005f, 0.02f, 0.4f, FilterKind.Lowpass, 2500f);
            return Finish(b, 0.7f);
        }

        /// <summary>Yakından geçen mermi: kayan bant geçiren gürültü + hafif ıslık.</summary>
        private static float[] BulletWhiz(ref SynthRng rng)
        {
            var b = Buffer(0.4f);
            var n = b.Length;
            var filter = Biquad.Create(FilterKind.Bandpass, 4200f, 3f);
            var peakSample = (int)(0.12f * SampleRate);
            var decayK = DecayFactor(0.09f);
            var decay = 1f;
            var phase = 0.0;
            for (var i = 0; i < n; i++)
            {
                var u = (float)i / n;
                var center = 4200f * (float)Math.Pow(1300f / 4200f, u);
                if ((i & 31) == 0)
                    filter.Set(FilterKind.Bandpass, center, 3f);

                float env;
                if (i < peakSample)
                {
                    var r = (float)i / peakSample;
                    env = r * r;
                }
                else
                {
                    decay *= decayK;
                    env = decay;
                }

                phase += center * 0.55f * Dt;
                var gain = BandwidthGain(FilterKind.Bandpass, center, 3f);
                b[i] = (filter.Process(rng.Bipolar()) * gain + Sin(phase) * 0.15f) * env;
            }

            return Finish(b, 0.6f);
        }

        private static float[] Punch(ref SynthRng rng)
        {
            var b = Buffer(0.22f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.03f, 1f, FilterKind.Lowpass, 400f);
            Tone(b, 0f, 100f, 60f, 0.02f, 0.001f, 0.035f, 0.8f);
            NoiseBurst(b, ref rng, 0f, 0.0005f, 0.008f, 0.5f, FilterKind.Bandpass, 1500f, 0.8f);
            NoiseSwell(b, ref rng, 0f, 0.1f, 0.15f, FilterKind.Bandpass, 900f, 900f, 0.8f, 0.5f);
            return Finish(b, 0.7f);
        }

        // =====================================================================================
        //  PATLAYICILAR
        // =====================================================================================

        /// <summary>Patlama: ani çat + kesimi düşen gövde + alt bas darbesi + uzun gürleme + enkaz çıtırtısı.</summary>
        private static float[] Explosion(ref SynthRng rng)
        {
            var b = Buffer(3.8f);
            var n = b.Length;

            // Gövde: kesim frekansı 3 kHz'den 180 Hz'e düşen gürültü.
            var body = Biquad.Create(FilterKind.Lowpass, 3000f, 0.8f);
            var rumble = Biquad.Create(FilterKind.Lowpass, 110f, 0.7f);
            var rumbleGain = BandwidthGain(FilterKind.Lowpass, 110f, 0.7f);
            var bodyK = DecayFactor(0.45f);
            var rumbleK = DecayFactor(1.2f);
            var rumbleAttackK = DecayFactor(0.03f);
            var cutK = DecayFactor(0.12f);
            float bodyEnv = 1f, rumbleEnv = 1f, rumbleAttack = 1f, cut = 1f, bodyGain = 1f;
            var attack = (int)(0.004f * SampleRate);
            for (var i = 0; i < n; i++)
            {
                if ((i & 63) == 0)
                {
                    var fc = 180f + 3000f * cut;
                    body.Set(FilterKind.Lowpass, fc, 0.8f);
                    bodyGain = BandwidthGain(FilterKind.Lowpass, fc, 0.8f);
                }

                var a = i < attack ? (float)i / attack : 1f;
                var w = rng.Bipolar();
                b[i] = body.Process(w) * bodyEnv * a * bodyGain
                       + rumble.Process(rng.Bipolar()) * rumbleEnv * (1f - rumbleAttack) * 0.9f * rumbleGain;

                bodyEnv *= bodyK;
                rumbleEnv *= rumbleK;
                rumbleAttack *= rumbleAttackK;
                cut *= cutK;
            }

            Click(b, ref rng, 0f, 1.2f, 800f, 0.006f);
            NWave(b, 0f, 1.5f, 0.0012f);
            Tone(b, 0f, 60f, 28f, 0.15f, 0.003f, 0.35f, 1.6f);

            // Enkaz ve taş çıtırtısı
            for (var g = 0; g < 40; g++)
            {
                var t = 0.15f + rng.Unit() * 1.85f;
                var falloff = 1f - (t - 0.15f) / 2.2f;
                NoiseBurst(b, ref rng, t, 0.0003f, rng.Range(0.003f, 0.01f), rng.Range(0.05f, 0.3f) * falloff,
                    FilterKind.Bandpass, rng.Range(1500f, 5000f), 1.2f);
            }

            // Vadi yankıları (yalnızca ilk 0.6 s'den)
            var dry = new float[Samples(0.6f)];
            Array.Copy(b, dry, Math.Min(dry.Length, n));
            Echo(b, dry, 0.35f, 0.3f, 300f);
            Echo(b, dry, 0.8f, 0.18f, 220f);

            Normalize(b, 1f);
            SoftClip(b, 1.8f);
            return Finish(b, 1f, 0.3f);
        }

        /// <summary>Gelen topçu mermisi: üstel olarak alçalan ıslık + hava sürtünmesi; yaklaştıkça yükselir.</summary>
        private static float[] ArtilleryWhistle(ref SynthRng rng)
        {
            const float duration = 2.6f;
            var b = Buffer(duration);
            var n = b.Length;
            var air = Biquad.Create(FilterKind.Bandpass, 1900f, 6f);
            var phase = 0.0;
            var vib = 0.0;
            var fadeStart = n - Samples(0.06f);
            for (var i = 0; i < n; i++)
            {
                var u = (float)i / n;
                var f = 1900f * (float)Math.Pow(420f / 1900f, u);
                vib += 7.0 * Dt;
                f *= 1f + 0.004f * Sin(vib);
                phase += f * Dt;
                if ((i & 31) == 0)
                    air.Set(FilterKind.Bandpass, f, 6f);

                var env = 0.15f + 0.85f * (float)Math.Pow(u, 1.6);
                if (i > fadeStart)
                    env *= 1f - (float)(i - fadeStart) / (n - fadeStart);

                var tone = Sin(phase) + Sin(phase * 2.0) * 0.18f;
                b[i] = (tone * 0.8f + air.Process(rng.Bipolar()) * 2.2f) * env;
            }

            return Finish(b, 0.8f, 0.01f);
        }

        private static float[] GrenadePin(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            Click(b, ref rng, 0f, 0.7f, 3500f, 0.0015f);
            Ring(b, 0f, 0.5f, 0.05f, false, 3200f, 4900f, 6800f);
            Clack(b, ref rng, 0.16f, 0.8f, 2600f);
            Ring(b, 0.16f, 0.25f, 0.04f, false, 2600f, 3900f);
            return Finish(b, 0.6f);
        }

        private static float[] GrenadeBounce(ref SynthRng rng)
        {
            var b = Buffer(0.25f);
            NoiseBurst(b, ref rng, 0f, 0.001f, 0.025f, 1f, FilterKind.Lowpass, 500f);
            Tone(b, 0f, 160f, 110f, 0.02f, 0.001f, 0.03f, 0.5f);
            Ring(b, 0f, 0.5f, 0.04f, false, 420f, 1130f, 1870f);
            Click(b, ref rng, 0f, 0.5f, 1500f, 0.0015f);
            return Finish(b, 0.7f);
        }

        private static float[] SmokeHiss(ref SynthRng rng)
        {
            const float duration = 3f;
            var b = Buffer(duration);
            var n = b.Length;
            var hp = Biquad.Create(FilterKind.Highpass, 2000f, 0.7f);
            var bp = Biquad.Create(FilterKind.Bandpass, 5000f, 1f);
            var bpGain = BandwidthGain(FilterKind.Bandpass, 5000f, 1f);
            var flutter = 1f;
            var flutterTarget = 1f;
            var attack = Samples(0.08f);
            var release = Samples(0.8f);
            for (var i = 0; i < n; i++)
            {
                if (i % 3675 == 0)
                    flutterTarget = rng.Range(0.75f, 1.2f);
                flutter += (flutterTarget - flutter) * 0.0015f;

                float env = 1f;
                if (i < attack)
                    env = (float)i / attack;
                else if (i > n - release)
                    env = (float)(n - i) / release;

                var w = rng.Bipolar();
                b[i] = (hp.Process(w) * 0.7f + bp.Process(w) * bpGain * 0.5f) * env * flutter;
            }

            Click(b, ref rng, 0f, 1.5f, 1200f, 0.004f);
            return Finish(b, 0.5f, 0.05f);
        }

        // =====================================================================================
        //  EŞYA KULLANIMI
        // =====================================================================================

        private static float[] Bandage(ref SynthRng rng)
        {
            var b = Buffer(1.4f);
            NoiseSwell(b, ref rng, 0f, 1.35f, 0.2f, FilterKind.Lowpass, 1500f, 1500f, 0.7f, 0.5f);
            for (var r = 0; r < 3; r++)
            {
                var start = 0.05f + r * 0.4f;
                NoiseSwell(b, ref rng, start, 0.25f, 0.6f, FilterKind.Bandpass, 2600f, 1900f, 1.2f, 0.85f);
                for (var g = 0; g < 28; g++)
                    Click(b, ref rng, start + rng.Unit() * 0.24f, rng.Range(0.05f, 0.18f), 3000f, 0.0015f);
            }

            return Finish(b, 0.55f);
        }

        private static float[] Drink(ref SynthRng rng)
        {
            var b = Buffer(1.1f);
            NoiseSwell(b, ref rng, 0f, 1.05f, 0.1f, FilterKind.Bandpass, 600f, 700f, 2f, 0.6f);
            for (var g = 0; g < 3; g++)
            {
                var t = 0.15f + g * 0.3f + rng.Range(-0.02f, 0.02f);
                Tone(b, t, 300f, 170f, 0.03f, 0.005f, 0.05f, 0.8f);
                NoiseBurst(b, ref rng, t, 0.004f, 0.04f, 0.4f, FilterKind.Lowpass, 500f);
                NoiseBurst(b, ref rng, t + 0.01f, 0.001f, 0.006f, 0.2f, FilterKind.Bandpass, 900f, 1.5f);
            }

            return Finish(b, 0.55f);
        }

        private static float[] Pickup(ref SynthRng rng)
        {
            var b = Buffer(0.35f);
            NoiseSwell(b, ref rng, 0f, 0.2f, 0.4f, FilterKind.Bandpass, 1100f, 1300f, 0.8f, 0.5f);
            Clack(b, ref rng, 0.12f, 0.5f, 2000f);
            Click(b, ref rng, 0.14f, 0.3f);
            return Finish(b, 0.5f);
        }

        // =====================================================================================
        //  ARAYÜZ VE BÖLGE
        // =====================================================================================

        private static float[] UiClick(ref SynthRng rng)
        {
            var b = Buffer(0.06f);
            Tone(b, 0f, 1250f, 1250f, 0f, 0.0005f, 0.009f, 1f);
            Click(b, ref rng, 0f, 0.4f, 4000f, 0.0008f);
            return Finish(b, 0.45f, 0.004f);
        }

        private static float[] UiHover()
        {
            var b = Buffer(0.05f);
            Tone(b, 0f, 2400f, 2400f, 0f, 0.0008f, 0.006f, 1f);
            return Finish(b, 0.25f, 0.004f);
        }

        private static float[] UiConfirm(ref SynthRng rng)
        {
            var b = Buffer(0.32f);
            Click(b, ref rng, 0f, 0.2f, 4000f, 0.001f);
            Note(b, 0f, 0.05f, 660f, 0.003f, 0.06f, 0.8f, 2);
            Note(b, 0.07f, 0.08f, 990f, 0.003f, 0.12f, 0.9f, 2);
            return Finish(b, 0.5f);
        }

        private static float[] ZoneWarning()
        {
            var b = Buffer(1.5f);
            for (var p = 0; p < 4; p++)
            {
                var f = (p & 1) == 0 ? 720f : 960f;
                Note(b, p * 0.38f, 0.23f, f, 0.01f, 0.03f, 0.8f, 2, 0f, 6f, 0.01f);
            }

            Filter(b, FilterKind.Lowpass, 3500f);
            return Finish(b, 0.55f);
        }

        private static float[] ZoneDamage(ref SynthRng rng)
        {
            var b = Buffer(0.45f);
            var n = b.Length;
            var k = DecayFactor(0.15f);
            var env = 1f;
            var attack = Samples(0.01f);
            var phase = 0.0;
            var hp = Biquad.Create(FilterKind.Highpass, 3000f, 0.7f);
            var gate = 0f;
            for (var i = 0; i < n; i++)
            {
                var a = i < attack ? (float)i / attack : 1f;
                phase += 110.0 * Dt;
                if ((i & 255) == 0)
                    gate = rng.Chance(0.4f) ? rng.Range(0.4f, 1f) : 0f;

                var buzz = Saw(phase) * 0.6f + Saw(phase * 2.0) * 0.25f;
                var crackle = hp.Process(rng.Bipolar()) * gate * 0.5f;
                b[i] = (buzz + crackle) * env * a;
                env *= k;
            }

            Filter(b, FilterKind.Lowpass, 3000f);
            return Finish(b, 0.55f);
        }

        // =====================================================================================
        //  TELSİZ, ÖLÜM, KAPI
        // =====================================================================================

        private static float[] RadioBeep(ref SynthRng rng)
        {
            var b = Buffer(0.38f);
            NoiseSwell(b, ref rng, 0f, 0.04f, 0.5f, FilterKind.Bandpass, 1800f, 1800f, 0.7f);
            Note(b, 0.04f, 0.13f, 1000f, 0.004f, 0.01f, 0.5f, 2);
            NoiseSwell(b, ref rng, 0.2f, 0.06f, 0.35f, FilterKind.Highpass, 1500f, 1500f, 0.7f);
            RadioBand(b);
            return Finish(b, 0.5f, 0.004f);
        }

        /// <summary>
        /// Telsiz konuşması: susturucu açılışı, sesli/sessiz hecelere benzeyen bant geçirilmiş gürültü patlamaları
        /// (formant çifti), arka plan cızırtısı, bozulma ve kapanış "kşş" sesi.
        /// </summary>
        private static float[] RadioChatter(ref SynthRng rng)
        {
            const float duration = 2.8f;
            var b = Buffer(duration);
            var n = b.Length;

            NoiseSwell(b, ref rng, 0f, 0.06f, 0.5f, FilterKind.Bandpass, 2000f, 2000f, 0.6f);
            NoiseSwell(b, ref rng, 0.04f, duration - 0.12f, 0.05f, FilterKind.Bandpass, 2500f, 2500f, 0.5f);

            var f1 = new Biquad();
            var f2 = new Biquad();
            var t = 0.12f;
            var pitchBase = rng.Range(110f, 140f);
            var phase = 0.0;
            while (t < duration - 0.3f)
            {
                var syl = rng.Range(0.08f, 0.2f);
                f1.Set(FilterKind.Bandpass, rng.Range(350f, 900f), 4f);
                f2.Set(FilterKind.Bandpass, rng.Range(900f, 2400f), 5f);
                var voiced = rng.Chance(0.75f);
                var pitch = pitchBase * rng.Range(0.9f, 1.15f);
                var s0 = (int)(t * SampleRate);
                var len = Samples(syl);
                for (var j = 0; j < len && s0 + j < n; j++)
                {
                    var u = (float)j / len;
                    var env = (float)Math.Sin(Math.PI * u);
                    env *= env;
                    phase += pitch * (1f - 0.08f * u) * Dt;
                    var src = voiced ? Saw(phase) * 0.8f + rng.Bipolar() * 0.25f : rng.Bipolar();
                    var y = f1.Process(src) * 3f + f2.Process(src) * 2.2f;
                    b[s0 + j] += y * env * 0.6f;
                }

                t += syl + (rng.Chance(0.15f) ? rng.Range(0.15f, 0.25f) : rng.Range(0.02f, 0.07f));
            }

            NoiseSwell(b, ref rng, duration - 0.14f, 0.12f, 0.6f, FilterKind.Bandpass, 2200f, 2200f, 0.6f);
            RadioBand(b);
            Normalize(b, 1f);
            SoftClip(b, 3f);
            return Finish(b, 0.5f, 0.005f);
        }

        /// <summary>Telsiz bant genişliği (300 Hz – 3 kHz).</summary>
        private static void RadioBand(float[] b)
        {
            Filter(b, FilterKind.Highpass, 300f);
            Filter(b, FilterKind.Lowpass, 3000f);
        }

        private static float[] Death(ref SynthRng rng)
        {
            var b = Buffer(1.1f);
            var n = b.Length;

            // Kısa inilti: formant filtreli darbe dizisi
            var fA = Biquad.Create(FilterKind.Bandpass, 650f, 4f);
            var fB = Biquad.Create(FilterKind.Bandpass, 1100f, 5f);
            var breath = Biquad.Create(FilterKind.Lowpass, 1200f, 0.7f);
            var groanLen = Math.Min(n, Samples(0.34f));
            var phase = 0.0;
            for (var i = 0; i < groanLen; i++)
            {
                var u = (float)i / groanLen;
                var f0 = 135f - 40f * u;
                phase += f0 * Dt;
                float env;
                if (u < 0.06f)
                    env = u / 0.06f;
                else if (u < 0.4f)
                    env = 1f;
                else
                    env = (float)Math.Exp(-(u - 0.4f) * 6f);

                var src = Saw(phase);
                var voice = fA.Process(src) * 2.5f + fB.Process(src) * 1.5f;
                var air = breath.Process(rng.Bipolar()) * 0.5f;
                b[i] += (voice * 0.5f + air * 0.3f) * env;
            }

            // Yere düşüş
            NoiseBurst(b, ref rng, 0.38f, 0.002f, 0.07f, 1f, FilterKind.Lowpass, 220f);
            Tone(b, 0.38f, 75f, 48f, 0.03f, 0.002f, 0.06f, 0.8f);
            NoiseBurst(b, ref rng, 0.52f, 0.002f, 0.05f, 0.6f, FilterKind.Lowpass, 260f);
            Tone(b, 0.52f, 80f, 55f, 0.03f, 0.002f, 0.04f, 0.5f);
            Ring(b, 0.4f, 0.12f, 0.03f, false, 2600f, 3900f);
            Ring(b, 0.45f, 0.1f, 0.03f, false, 2400f, 3600f);
            Ring(b, 0.55f, 0.08f, 0.03f, false, 2800f, 4100f);
            NoiseSwell(b, ref rng, 0.35f, 0.35f, 0.2f, FilterKind.Bandpass, 1200f, 900f, 0.8f, 0.5f);
            Grit(b, ref rng, 0.38f, 0.2f, 10, 0.12f);
            return Finish(b, 0.7f);
        }

        private static float[] VehicleDoor(ref SynthRng rng)
        {
            var b = Buffer(0.9f);
            var n = b.Length;
            Clack(b, ref rng, 0f, 0.6f, 1900f);

            // Menteşe gıcırtısı
            var creak = Biquad.Create(FilterKind.Bandpass, 900f, 3f);
            var s0 = Samples(0.05f);
            var len = Samples(0.25f);
            var phase = 0.0;
            for (var j = 0; j < len && s0 + j < n; j++)
            {
                var u = (float)j / len;
                var env = (float)Math.Sin(Math.PI * u);
                phase += (190f + 25f * Sin(u * 9.0) + rng.Bipolar() * 12f) * Dt;
                b[s0 + j] += creak.Process(Saw(phase)) * env * 0.5f;
            }

            // Çarpma
            NoiseBurst(b, ref rng, 0.38f, 0.002f, 0.08f, 1f, FilterKind.Lowpass, 260f);
            Tone(b, 0.38f, 68f, 45f, 0.04f, 0.002f, 0.08f, 0.9f);
            Ring(b, 0.38f, 0.45f, 0.15f, false, 380f, 1080f, 1720f);
            NoiseBurst(b, ref rng, 0.38f, 0.0008f, 0.02f, 0.5f, FilterKind.Lowpass, 2000f);
            Click(b, ref rng, 0.385f, 0.5f, 2000f, 0.002f);
            return Finish(b, 0.8f);
        }
    }
}
