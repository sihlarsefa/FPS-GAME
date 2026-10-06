using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Kalite ekleri: silah sınıfına göre namlu alevleri, kar/kan sisi/patlama enkazı/duman sütunu/rotor halkası/adım tozu
    /// şablonları. Alt yayıcılar (<see cref="ParticleRecipe.DeathSubEmitter"/>) parçacık ölünce toz/duman tetikler.
    /// </summary>
    internal static partial class VfxEffectLibrary
    {
        private static readonly Color Flame0 = new Color(1f, 0.88f, 0.6f, 1f);
        private static readonly Color Flame1 = new Color(1f, 0.62f, 0.25f, 1f);

        private static ParticleRecipe[] ExtraRecipes(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.MuzzlePistol: return MuzzleVariant(0.2f, 0.32f, 3, 6f, 3, 1, 2, 0.12f, false);
                case EffectKind.MuzzleHeavy: return MuzzleHeavy();
                case EffectKind.MuzzleSuppressed: return MuzzleSuppressed();
                case EffectKind.ImpactSnow: return ImpactSnow();
                case EffectKind.BloodMist: return BloodMist();
                case EffectKind.ExplosionDebris: return ExplosionDebris();
                case EffectKind.ExplosionColumn: return ExplosionColumn();
                case EffectKind.ShockRing: return ShockRing();
                case EffectKind.CameraDust: return CameraDust();
                case EffectKind.SmokeBillow: return SmokeBillow();
                case EffectKind.RotorRing: return RotorRing();
                case EffectKind.FootDust: return FootPuff(new Color(0.46f, 0.39f, 0.29f, 0.5f), new Color(0.58f, 0.5f, 0.38f, 0.4f), 2, 3);
                case EffectKind.FootSnow: return FootPuff(new Color(0.93f, 0.96f, 1f, 0.6f), new Color(0.82f, 0.88f, 0.96f, 0.5f), 3, 4);
                case EffectKind.MuzzleSmoke: return FootPuff(new Color(0.62f, 0.62f, 0.64f, 0.35f), new Color(0.8f, 0.8f, 0.82f, 0.25f), 2, 3);
                default: return null;
            }
        }

        private static int ExtraCapacity(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.MuzzlePistol: return 10;
                case EffectKind.MuzzleHeavy: return 10;
                case EffectKind.MuzzleSuppressed: return 12;
                case EffectKind.ImpactSnow: return 10;
                case EffectKind.BloodMist: return 10;
                case EffectKind.ExplosionDebris: return 6;
                case EffectKind.ExplosionColumn: return 4;
                case EffectKind.ShockRing: return 6;
                case EffectKind.CameraDust: return 2;
                case EffectKind.SmokeBillow: return 6;
                case EffectKind.RotorRing: return 4;
                case EffectKind.FootDust: return 14;
                case EffectKind.FootSnow: return 14;
                case EffectKind.MuzzleSmoke: return 8;
                default: return 4;
            }
        }

        // ------------------------------------------------------------------ namlu alevleri
        /// <summary>Çekirdek + çok yapraklı yıldız + ön alev yaprakları + duman. Yıldız namlu eksenine dik yayılır.</summary>
        private static ParticleRecipe[] MuzzleVariant(float coreMin, float coreMax, int petals, float petalSpeed, int star,
            int smokeMin, int smokeMax, float smokeSize, bool wisp)
        {
            var core = new ParticleRecipe
            {
                Name = "Core", Material = VfxMaterials.AdditiveGlow, Duration = 0.05f, BurstMin = 1, BurstMax = 2, MaxParticles = 4,
                Lifetime = new Vector2(0.03f, 0.05f), Speed = Vector2.zero, Size = new Vector2(coreMin, coreMax),
                ColorA = Flame0, ColorB = Flame1, ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.5f, 0.2f)),
                SizeOverLifetime = ParticleRecipe.Curve(0.7f, 1.3f), ShapeEnabled = false, MaxParticleSize = 1.5f
            };
            var front = new ParticleRecipe
            {
                Name = "Petals", Material = VfxMaterials.AdditiveGlow, RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f, LengthScale = 3f, Duration = 0.05f, BurstMin = petals, BurstMax = petals + 2,
                MaxParticles = petals + 4, Lifetime = new Vector2(0.03f, 0.05f), Speed = new Vector2(petalSpeed, petalSpeed * 1.8f),
                Size = new Vector2(0.05f, 0.09f), ColorA = Flame0, ColorB = Flame1,
                ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.6f, 0.3f)), ShapeAngle = 12f, ShapeRadius = 0.01f
            };
            var starR = new ParticleRecipe
            {
                Name = "Star", Material = VfxMaterials.AdditiveGlow, RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.02f, LengthScale = 5f, Duration = 0.05f, BurstMin = star, BurstMax = star + 1,
                MaxParticles = star + 3, Lifetime = new Vector2(0.025f, 0.04f), Speed = new Vector2(5f, 9f),
                Size = new Vector2(0.04f, 0.07f), ColorA = new Color(1f, 0.92f, 0.7f, 1f), ColorB = Flame1,
                ColorOverLifetime = FadeOut(Color.white, Color.white), ShapeAngle = 88f, ShapeRadius = 0.005f
            };
            var smoke = new ParticleRecipe
            {
                Name = "Smoke", Material = VfxMaterials.AlphaPuff, Duration = 0.05f, BurstMin = smokeMin, BurstMax = smokeMax,
                MaxParticles = smokeMax + 3, Lifetime = wisp ? new Vector2(1f, 1.8f) : new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0.3f, 1f), Size = new Vector2(smokeSize, smokeSize * 1.7f),
                ColorA = new Color(0.62f, 0.6f, 0.58f, 0.3f), ColorB = new Color(0.7f, 0.68f, 0.65f, 0.22f),
                ColorOverLifetime = PuffFade(Color.white, Color.white), SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 2.6f),
                Gravity = -0.03f, Drag = 2f, ShapeAngle = 18f, ShapeRadius = 0.02f, MaxParticleSize = 1f
            };
            return new[] { core, front, starR, smoke };
        }

        private static ParticleRecipe[] MuzzleHeavy()
        {
            var set = MuzzleVariant(0.5f, 0.75f, 7, 7f, 6, 4, 6, 0.26f, false);
            // Fren patlaması: yanlara geniş, kısa jet.
            var blast = new ParticleRecipe
            {
                Name = "Blast", Material = VfxMaterials.AdditiveGlow, RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f, LengthScale = 3.5f, Duration = 0.05f, BurstMin = 4, BurstMax = 6, MaxParticles = 8,
                Lifetime = new Vector2(0.03f, 0.055f), Speed = new Vector2(6f, 11f), Size = new Vector2(0.07f, 0.12f),
                ColorA = Flame0, ColorB = Flame1, ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.55f, 0.2f)),
                ShapeAngle = 75f, ShapeRadius = 0.015f
            };
            var fire = new ParticleRecipe
            {
                Name = "Fireball", Material = VfxMaterials.AdditiveGlow, Duration = 0.05f, BurstMin = 1, BurstMax = 2, MaxParticles = 3,
                Lifetime = new Vector2(0.05f, 0.08f), Speed = new Vector2(1.5f, 3f), Size = new Vector2(0.25f, 0.4f),
                ColorA = new Color(1f, 0.7f, 0.35f, 0.8f), ColorB = new Color(1f, 0.5f, 0.2f, 0.7f),
                ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.4f, 0.15f)), SizeOverLifetime = ParticleRecipe.Curve(0.6f, 1.6f),
                ShapeAngle = 8f, ShapeRadius = 0.01f, MaxParticleSize = 1.5f
            };
            return new[] { set[0], set[1], set[2], set[3], blast, fire };
        }

        private static ParticleRecipe[] MuzzleSuppressed()
        {
            var core = new ParticleRecipe
            {
                Name = "Core", Material = VfxMaterials.AdditiveGlow, Duration = 0.04f, BurstMin = 1, BurstMax = 1, MaxParticles = 2,
                Lifetime = new Vector2(0.025f, 0.04f), Speed = Vector2.zero, Size = new Vector2(0.07f, 0.11f),
                ColorA = new Color(1f, 0.8f, 0.55f, 0.55f), ColorB = new Color(1f, 0.7f, 0.4f, 0.45f),
                ColorOverLifetime = FadeOut(Color.white, Color.white), ShapeEnabled = false
            };
            var wisp = new ParticleRecipe
            {
                Name = "Wisp", Material = VfxMaterials.AlphaPuff, Duration = 0.05f, BurstMin = 2, BurstMax = 3, MaxParticles = 6,
                Lifetime = new Vector2(1f, 1.8f), Speed = new Vector2(0.15f, 0.5f), Size = new Vector2(0.08f, 0.16f),
                ColorA = new Color(0.7f, 0.7f, 0.7f, 0.2f), ColorB = new Color(0.75f, 0.75f, 0.75f, 0.14f),
                ColorOverLifetime = PuffFade(Color.white, Color.white), SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 3f),
                Gravity = -0.02f, Drag = 2.5f, NoiseStrength = 0.2f, ShapeAngle = 20f, ShapeRadius = 0.015f, MaxParticleSize = 1f
            };
            return new[] { core, wisp };
        }

        // ------------------------------------------------------------------ kar / kan sisi
        private static ParticleRecipe[] ImpactSnow()
        {
            var puff = DustPuff("Puff", new Color(0.95f, 0.97f, 1f, 0.7f), new Color(0.84f, 0.9f, 0.98f, 0.6f), 6, 9, new Vector2(0.2f, 0.38f), 30f);
            var powder = new ParticleRecipe
            {
                Name = "Powder", Material = VfxMaterials.AlphaSoft, Duration = 0.05f, BurstMin = 8, BurstMax = 12, MaxParticles = 14,
                Lifetime = new Vector2(0.5f, 0.9f), Speed = new Vector2(1.5f, 4f), Size = new Vector2(0.03f, 0.06f),
                ColorA = new Color(1f, 1f, 1f, 0.9f), ColorB = new Color(0.86f, 0.92f, 1f, 0.8f),
                ColorOverLifetime = FadeOut(Color.white, Color.white), Gravity = 0.5f, Drag = 1f, ShapeAngle = 35f, ShapeRadius = 0.02f
            };
            var ice = Chunks("Ice", new Color(0.8f, 0.9f, 1f, 1f), new Color(0.95f, 0.98f, 1f, 1f), 3, 5, new Vector2(0.015f, 0.03f), new Vector2(2f, 4.5f), 1.2f);
            return new[] { puff, powder, ice };
        }

        private static ParticleRecipe[] BloodMist()
        {
            var mist = new ParticleRecipe
            {
                Name = "Mist", Material = VfxMaterials.AlphaPuff, Duration = 0.05f, BurstMin = 6, BurstMax = 9, MaxParticles = 12,
                Lifetime = new Vector2(0.35f, 0.7f), Speed = new Vector2(0.6f, 2.2f), Size = new Vector2(0.14f, 0.26f),
                ColorA = new Color(0.55f, 0.04f, 0.04f, 0.45f), ColorB = new Color(0.4f, 0.02f, 0.02f, 0.35f),
                ColorOverLifetime = PuffFade(Color.white, Color.white), SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 2.2f),
                Drag = 2.2f, ShapeAngle = 35f, ShapeRadius = 0.04f, MaxParticleSize = 0.8f
            };
            var drops = new ParticleRecipe
            {
                Name = "Drops", Material = VfxMaterials.AlphaSoft, RenderMode = ParticleSystemRenderMode.Stretch, VelocityScale = 0.05f,
                LengthScale = 2f, Duration = 0.05f, BurstMin = 5, BurstMax = 8, MaxParticles = 10, Lifetime = new Vector2(0.3f, 0.6f),
                Speed = new Vector2(2f, 5f), Size = new Vector2(0.012f, 0.022f), ColorA = new Color(0.5f, 0.02f, 0.02f, 0.9f),
                ColorB = new Color(0.35f, 0.01f, 0.01f, 0.9f), ColorOverLifetime = FadeOut(Color.white, Color.white),
                Gravity = 1.4f, ShapeAngle = 40f, ShapeRadius = 0.03f
            };
            return new[] { mist, drops };
        }

        // ------------------------------------------------------------------ patlama ekleri
        /// <summary>Enkaz parçaları; her parça ölünce küçük bir duman/toz izi bırakır (alt yayıcı).</summary>
        private static ParticleRecipe[] ExplosionDebris()
        {
            var chunks = Chunks("DebrisChunks", new Color(0.18f, 0.15f, 0.12f, 1f), new Color(0.42f, 0.36f, 0.3f, 1f), 12, 18,
                new Vector2(0.08f, 0.2f), new Vector2(6f, 15f), 2.2f);
            chunks.Lifetime = new Vector2(0.9f, 1.8f);
            chunks.ShapeAngle = 60f;
            chunks.ShapeRadius = 0.2f;
            chunks.MaxParticleSize = 0.4f;
            chunks.MaxParticles = 24;
            chunks.DeathSubEmitter = "DebrisPuff";
            var puff = DustPuff("DebrisPuff", new Color(0.4f, 0.37f, 0.33f, 0.5f), new Color(0.5f, 0.46f, 0.4f, 0.4f), 1, 2, new Vector2(0.4f, 0.8f), 40f);
            puff.MaxParticles = 48;
            puff.Lifetime = new Vector2(0.8f, 1.5f);
            puff.Speed = new Vector2(0.2f, 0.8f);
            puff.MaxParticleSize = 1.2f;
            return new[] { chunks, puff };
        }

        /// <summary>Yükselen kalıcı duman sütunu (yaklaşık 5-7 sn), rüzgâr gibi hafif sürüklenir.</summary>
        private static ParticleRecipe[] ExplosionColumn()
        {
            return new[]
            {
                new ParticleRecipe
                {
                    Name = "Column", Material = VfxMaterials.AlphaPuff, Duration = 4f, RateOverTime = 7f, MaxParticles = 40,
                    Lifetime = new Vector2(3.5f, 5.5f), Speed = new Vector2(1.6f, 2.6f), Size = new Vector2(1.2f, 2f),
                    ColorA = new Color(0.12f, 0.11f, 0.1f, 0.5f), ColorB = new Color(0.24f, 0.22f, 0.2f, 0.4f),
                    ColorOverLifetime = ParticleRecipe.Gradient(new Color(0.5f, 0.5f, 0.5f), new Color(1f, 1f, 1f), 0f, 0f, 0.15f, 1f, 0.7f, 0.7f, 1f, 0f),
                    SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 3.4f), Drag = 0.4f, NoiseStrength = 0.6f, NoiseFrequency = 0.3f,
                    ShapeAngle = 8f, ShapeRadius = 0.7f, ShapeRotation = Up, MaxParticleSize = 6f, SortByDistance = true
                }
            };
        }

        /// <summary>Yatay genişleyen şok dalgası halkası (1 parçacık, yere paralel).</summary>
        private static ParticleRecipe[] ShockRing()
        {
            return new[]
            {
                new ParticleRecipe
                {
                    Name = "ShockRing", Material = VfxMaterials.AlphaRing, RenderMode = ParticleSystemRenderMode.HorizontalBillboard,
                    Duration = 0.05f, BurstMin = 1, BurstMax = 1, MaxParticles = 2, Lifetime = new Vector2(0.4f, 0.5f),
                    Speed = Vector2.zero, Size = new Vector2(1f, 1f), RandomRotation = false,
                    ColorA = new Color(1f, 0.92f, 0.78f, 0.7f), ColorB = new Color(1f, 0.85f, 0.65f, 0.6f),
                    ColorOverLifetime = FadeOut(Color.white, new Color(0.8f, 0.75f, 0.7f)),
                    SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 11f), ShapeEnabled = false, MaxParticleSize = 40f
                }
            };
        }

        /// <summary>Kamera önünde süzülen toz/kırıntı zerreleri (yakın patlamada).</summary>
        private static ParticleRecipe[] CameraDust()
        {
            return new[]
            {
                new ParticleRecipe
                {
                    Name = "Motes", Material = VfxMaterials.AlphaSoft, Duration = 0.1f, BurstMin = 26, BurstMax = 34, MaxParticles = 40,
                    Lifetime = new Vector2(1.6f, 2.8f), Speed = new Vector2(0.05f, 0.4f), Size = new Vector2(0.012f, 0.03f),
                    ColorA = new Color(0.6f, 0.55f, 0.48f, 0.55f), ColorB = new Color(0.75f, 0.7f, 0.62f, 0.4f),
                    ColorOverLifetime = PuffFade(Color.white, Color.white), Gravity = 0.04f, Drag = 0.6f, NoiseStrength = 0.4f,
                    Shape = ParticleSystemShapeType.Box, ShapeRadius = 1f
                }
            };
        }

        // ------------------------------------------------------------------ sis bombası hacmi
        /// <summary>Yoğun, yuvarlak sis hacmi: ilk patlama + sürekli şişen gövde + kenar dumanı. SmokeCloud süreyi ayarlar.</summary>
        private static ParticleRecipe[] SmokeBillow()
        {
            var body = new ParticleRecipe
            {
                Name = "Body", Material = VfxMaterials.AlphaPuff, Duration = 20f, RateOverTime = 14f, MaxParticles = 160,
                Lifetime = new Vector2(6f, 8f), Speed = new Vector2(0.3f, 1.1f), Size = new Vector2(3.4f, 5.2f),
                ColorA = new Color(0.82f, 0.83f, 0.84f, 0.8f), ColorB = new Color(0.68f, 0.7f, 0.72f, 0.75f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 0f, 0.1f, 1f, 0.8f, 1f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.4f, 1.35f), Gravity = -0.01f, Drag = 0.8f, NoiseStrength = 0.25f,
                NoiseFrequency = 0.2f, Shape = ParticleSystemShapeType.Sphere, ShapeRadius = 1.8f, RadiusThickness = 1f,
                MaxParticleSize = 10f, SortByDistance = true
            };
            var pop = new ParticleRecipe
            {
                Name = "Pop", Material = VfxMaterials.AlphaPuff, Duration = 0.1f, BurstMin = 14, BurstMax = 20, MaxParticles = 24,
                Lifetime = new Vector2(3f, 5f), Speed = new Vector2(5f, 9f), Size = new Vector2(2.2f, 3.4f),
                ColorA = new Color(0.9f, 0.9f, 0.9f, 0.85f), ColorB = new Color(0.78f, 0.79f, 0.8f, 0.8f),
                ColorOverLifetime = PuffFade(Color.white, Color.white), SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 1.6f),
                Drag = 2.4f, ShapeAngle = 60f, ShapeRadius = 0.3f, MaxParticleSize = 9f, SortByDistance = true
            };
            return new[] { body, pop };
        }

        // ------------------------------------------------------------------ rotor / adım
        /// <summary>Rotor akışından yere paralel yayılan toz halkası + radyal toz perdesi (ölçek = yarıçap/5 m).</summary>
        private static ParticleRecipe[] RotorRing()
        {
            var ring = new ParticleRecipe
            {
                Name = "Ring", Material = VfxMaterials.AlphaRing, RenderMode = ParticleSystemRenderMode.HorizontalBillboard,
                Duration = 0.1f, BurstMin = 1, BurstMax = 1, MaxParticles = 2, Lifetime = new Vector2(0.9f, 1.1f), Speed = Vector2.zero,
                Size = new Vector2(1f, 1f), RandomRotation = false, ColorA = new Color(0.62f, 0.55f, 0.44f, 0.3f),
                ColorB = new Color(0.7f, 0.62f, 0.5f, 0.25f), ColorOverLifetime = FadeOut(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(1f, 9f), ShapeEnabled = false, MaxParticleSize = 40f
            };
            var curtain = new ParticleRecipe
            {
                Name = "Curtain", Material = VfxMaterials.AlphaPuff, Duration = 0.1f, BurstMin = 14, BurstMax = 20, MaxParticles = 24,
                Lifetime = new Vector2(1f, 1.7f), Speed = new Vector2(3f, 6f), Size = new Vector2(0.7f, 1.2f),
                ColorA = new Color(0.58f, 0.5f, 0.4f, 0.45f), ColorB = new Color(0.68f, 0.6f, 0.48f, 0.35f),
                ColorOverLifetime = PuffFade(Color.white, Color.white), SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 2.4f),
                Drag = 1.8f, Gravity = -0.02f, NoiseStrength = 0.3f, Shape = ParticleSystemShapeType.Circle, ShapeRadius = 0.6f,
                RadiusThickness = 0f, ShapeRotation = Up, MaxParticleSize = 3f
            };
            return new[] { ring, curtain };
        }

        private static ParticleRecipe[] FootPuff(Color a, Color b, int min, int max)
        {
            var puff = DustPuff("Puff", a, b, min, max, new Vector2(0.1f, 0.18f), 45f);
            puff.Lifetime = new Vector2(0.45f, 0.85f);
            puff.Speed = new Vector2(0.3f, 1.1f);
            puff.Gravity = -0.02f;
            return new[] { puff };
        }
    }
}
