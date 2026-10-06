using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>Havuzlanan parçacık efekti türleri.</summary>
    internal enum EffectKind
    {
        MuzzleFlash = 0,
        ImpactDirt,
        ImpactConcrete,
        ImpactMetal,
        ImpactWood,
        ImpactWater,
        ImpactFoliage,
        Blood,
        Explosion,
        Smoke,
        Dust,
        MuzzlePistol,
        MuzzleHeavy,
        MuzzleSuppressed,
        ImpactSnow,
        BloodMist,
        ExplosionDebris,
        ExplosionColumn,
        ShockRing,
        CameraDust,
        SmokeBillow,
        RotorRing,
        FootDust,
        FootSnow,
        MuzzleSmoke,
        Count
    }

    /// <summary>
    /// Efekt şablonlarını koddan kurar. Her efekt bir kök ParticleSystem + alt sistemlerden oluşur; kök oynatılınca
    /// alt sistemler de oynar. Darbe efektleri yerel +Z = yüzey normali olacak şekilde döndürülür.
    /// Patlama ve sis 5 m yarıçap için, toz 1 ölçek için tasarlanmıştır (dönüşüm ölçeğiyle büyütülür).
    /// </summary>
    internal static partial class VfxEffectLibrary
    {
        /// <summary>Patlama/sis tariflerinin tasarlandığı yarıçap (m).</summary>
        public const float ReferenceRadius = 5f;

        private static readonly Vector3 Up = new Vector3(-90f, 0f, 0f);

        public static int Capacity(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.MuzzleFlash: return 20;
                case EffectKind.ImpactDirt: return 14;
                case EffectKind.ImpactConcrete: return 14;
                case EffectKind.ImpactMetal: return 12;
                case EffectKind.ImpactWood: return 10;
                case EffectKind.ImpactWater: return 10;
                case EffectKind.ImpactFoliage: return 8;
                case EffectKind.Blood: return 16;
                case EffectKind.Explosion: return 10;
                case EffectKind.Smoke: return 8;
                case EffectKind.Dust: return 24;
                default: return ExtraCapacity(kind);
            }
        }

        /// <summary>Şablonu <paramref name="parent"/> altına kurar; <paramref name="lifetime"/> = en uzun görünürlük süresi.</summary>
        public static ParticleSystem Build(EffectKind kind, Transform parent, out float lifetime)
        {
            var recipes = Recipes(kind);
            lifetime = 0.1f;
            if (recipes == null || recipes.Length == 0)
                return null;

            var root = recipes[0].Build(parent);
            root.gameObject.name = kind.ToString();
            lifetime = recipes[0].MaxLifetime;
            for (var i = 1; i < recipes.Length; i++)
            {
                recipes[i].Build(root.transform);
                lifetime = Mathf.Max(lifetime, recipes[i].MaxLifetime);
            }

            LinkSubEmitters(root, recipes);

            return root;
        }

        /// <summary>Tarifteki DeathSubEmitter adlarını aynı efektteki çocuk sistemlere bağlar (hata olursa yok sayılır).</summary>
        private static void LinkSubEmitters(ParticleSystem root, ParticleRecipe[] recipes)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                var name = recipes[i].DeathSubEmitter;
                if (string.IsNullOrEmpty(name))
                    continue;

                var owner = i == 0 ? root : FindChild(root, recipes[i].Name);
                var target = FindChild(root, name);
                if (owner == null || target == null || owner == target)
                    continue;

                var subs = owner.subEmitters;
                subs.enabled = true;
                subs.AddSubEmitter(target, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            }
        }

        private static ParticleSystem FindChild(ParticleSystem root, string name)
        {
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child.name == name && child.TryGetComponent(out ParticleSystem ps))
                    return ps;
            }

            return root.name == name ? root : null;
        }

        private static ParticleRecipe[] Recipes(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.MuzzleFlash: return MuzzleFlash();
                case EffectKind.ImpactDirt: return ImpactDirt();
                case EffectKind.ImpactConcrete: return ImpactConcrete();
                case EffectKind.ImpactMetal: return ImpactMetal();
                case EffectKind.ImpactWood: return ImpactWood();
                case EffectKind.ImpactWater: return ImpactWater();
                case EffectKind.ImpactFoliage: return ImpactFoliage();
                case EffectKind.Blood: return Blood();
                case EffectKind.Explosion: return Explosion();
                case EffectKind.Smoke: return Smoke();
                case EffectKind.Dust: return Dust();
                default: return ExtraRecipes(kind);
            }
        }

        // ------------------------------------------------------------------ alfa eğrileri
        private static Gradient FadeOut(Color c0, Color c1)
        {
            return ParticleRecipe.Gradient(c0, c1, 0f, 1f, 1f, 0f);
        }

        private static Gradient PuffFade(Color c0, Color c1)
        {
            return ParticleRecipe.Gradient(c0, c1, 0f, 0.6f, 0.12f, 1f, 1f, 0f);
        }

        private static Gradient SolidThenFade()
        {
            return ParticleRecipe.Gradient(Color.white, Color.white, 0f, 1f, 0.8f, 1f, 1f, 0f);
        }

        // ------------------------------------------------------------------ namlu alevi
        private static ParticleRecipe[] MuzzleFlash()
        {
            var core = new ParticleRecipe
            {
                Name = "Core",
                Material = VfxMaterials.AdditiveGlow,
                Duration = 0.05f,
                BurstMin = 1,
                BurstMax = 2,
                MaxParticles = 4,
                Lifetime = new Vector2(0.035f, 0.055f),
                Speed = Vector2.zero,
                Size = new Vector2(0.3f, 0.45f),
                ColorA = new Color(1f, 0.88f, 0.6f, 1f),
                ColorB = new Color(1f, 0.74f, 0.38f, 1f),
                ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.5f, 0.2f)),
                SizeOverLifetime = ParticleRecipe.Curve(0.7f, 1.25f),
                ShapeEnabled = false,
                MaxParticleSize = 1.5f
            };

            var petals = new ParticleRecipe
            {
                Name = "Petals",
                Material = VfxMaterials.AdditiveGlow,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 3f,
                Duration = 0.05f,
                BurstMin = 4,
                BurstMax = 6,
                MaxParticles = 12,
                Lifetime = new Vector2(0.03f, 0.05f),
                Speed = new Vector2(4f, 8f),
                Size = new Vector2(0.06f, 0.1f),
                ColorA = new Color(1f, 0.85f, 0.5f, 1f),
                ColorB = new Color(1f, 0.62f, 0.25f, 1f),
                ColorOverLifetime = FadeOut(Color.white, new Color(1f, 0.6f, 0.3f)),
                ShapeAngle = 14f,
                ShapeRadius = 0.01f
            };

            var side = new ParticleRecipe
            {
                Name = "Side",
                Material = VfxMaterials.AdditiveGlow,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 2.2f,
                Duration = 0.05f,
                BurstMin = 2,
                BurstMax = 4,
                MaxParticles = 8,
                Lifetime = new Vector2(0.025f, 0.04f),
                Speed = new Vector2(2f, 4f),
                Size = new Vector2(0.04f, 0.07f),
                ColorA = new Color(1f, 0.7f, 0.35f, 1f),
                ColorB = new Color(1f, 0.55f, 0.2f, 1f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                ShapeAngle = 80f,
                ShapeRadius = 0.01f
            };

            var smoke = new ParticleRecipe
            {
                Name = "Smoke",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.05f,
                BurstMin = 2,
                BurstMax = 3,
                MaxParticles = 8,
                Lifetime = new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0.3f, 1f),
                Size = new Vector2(0.16f, 0.28f),
                ColorA = new Color(0.62f, 0.6f, 0.58f, 0.3f),
                ColorB = new Color(0.7f, 0.68f, 0.65f, 0.22f),
                ColorOverLifetime = PuffFade(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 2.4f),
                Gravity = -0.03f,
                Drag = 2f,
                ShapeAngle = 18f,
                ShapeRadius = 0.02f,
                MaxParticleSize = 1f
            };

            return new[] { core, petals, side, smoke };
        }

        // ------------------------------------------------------------------ isabetler
        private static ParticleRecipe DustPuff(string name, Color a, Color b, int min, int max, Vector2 size, float angle)
        {
            return new ParticleRecipe
            {
                Name = name,
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.05f,
                BurstMin = min,
                BurstMax = max,
                MaxParticles = max + 2,
                Lifetime = new Vector2(0.6f, 1.2f),
                Speed = new Vector2(0.7f, 2.2f),
                Size = size,
                ColorA = a,
                ColorB = b,
                ColorOverLifetime = PuffFade(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 2.2f),
                Gravity = 0.05f,
                Drag = 2.5f,
                ShapeAngle = angle,
                ShapeRadius = 0.03f,
                MaxParticleSize = 1f
            };
        }

        private static ParticleRecipe Chunks(string name, Color a, Color b, int min, int max, Vector2 size, Vector2 speed, float gravity)
        {
            return new ParticleRecipe
            {
                Name = name,
                Material = VfxMaterials.AlphaChunk,
                Duration = 0.05f,
                BurstMin = min,
                BurstMax = max,
                MaxParticles = max + 2,
                Lifetime = new Vector2(0.45f, 0.9f),
                Speed = speed,
                Size = size,
                ColorA = a,
                ColorB = b,
                ColorOverLifetime = SolidThenFade(),
                Gravity = gravity,
                Spin = 9f,
                ShapeAngle = 35f,
                ShapeRadius = 0.02f
            };
        }

        private static ParticleRecipe Sparks(string name, int min, int max, Vector2 lifetime, Vector2 speed, Vector2 size, float angle, float gravity)
        {
            return new ParticleRecipe
            {
                Name = name,
                Material = VfxMaterials.AdditiveGlow,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 1f,
                Duration = 0.05f,
                BurstMin = min,
                BurstMax = max,
                MaxParticles = Mathf.Max(2, max + 2),
                Lifetime = lifetime,
                Speed = speed,
                Size = size,
                ColorA = new Color(1f, 0.9f, 0.6f, 1f),
                ColorB = new Color(1f, 0.72f, 0.32f, 1f),
                ColorOverLifetime = ParticleRecipe.Gradient3(Color.white, new Color(1f, 0.7f, 0.3f), 0.4f, new Color(1f, 0.35f, 0.1f), 0f, 1f, 0.7f, 1f, 1f, 0f),
                Gravity = gravity,
                Drag = 1.2f,
                ShapeAngle = angle,
                ShapeRadius = 0.01f
            };
        }

        private static ParticleRecipe[] ImpactDirt()
        {
            var dust = DustPuff("Dust", new Color(0.46f, 0.39f, 0.29f, 0.65f), new Color(0.56f, 0.48f, 0.36f, 0.55f), 5, 7, new Vector2(0.22f, 0.4f), 28f);
            var clods = Chunks("Clods", new Color(0.25f, 0.19f, 0.13f, 1f), new Color(0.36f, 0.28f, 0.2f, 1f), 5, 8, new Vector2(0.025f, 0.05f), new Vector2(2.5f, 5f), 1.3f);
            var spray = new ParticleRecipe
            {
                Name = "Spray",
                Material = VfxMaterials.AlphaSoft,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.05f,
                LengthScale = 1.5f,
                Duration = 0.05f,
                BurstMin = 4,
                BurstMax = 6,
                MaxParticles = 8,
                Lifetime = new Vector2(0.2f, 0.35f),
                Speed = new Vector2(4f, 7f),
                Size = new Vector2(0.03f, 0.05f),
                ColorA = new Color(0.4f, 0.33f, 0.24f, 0.6f),
                ColorB = new Color(0.5f, 0.42f, 0.3f, 0.5f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                Gravity = 1f,
                ShapeAngle = 14f,
                ShapeRadius = 0.01f
            };
            clods.DeathSubEmitter = "ClodPuff";
            var clodPuff = DustPuff("ClodPuff", new Color(0.46f, 0.39f, 0.29f, 0.35f), new Color(0.56f, 0.48f, 0.36f, 0.3f), 1, 1, new Vector2(0.08f, 0.14f), 40f);
            clodPuff.MaxParticles = 24;
            clodPuff.Lifetime = new Vector2(0.3f, 0.6f);
            clodPuff.Speed = new Vector2(0.1f, 0.4f);
            return new[] { dust, clods, spray, clodPuff };
        }

        private static ParticleRecipe[] ImpactConcrete()
        {
            var dust = DustPuff("Dust", new Color(0.66f, 0.64f, 0.6f, 0.6f), new Color(0.74f, 0.72f, 0.68f, 0.5f), 5, 7, new Vector2(0.18f, 0.34f), 25f);
            var chips = Chunks("Chips", new Color(0.55f, 0.54f, 0.5f, 1f), new Color(0.72f, 0.7f, 0.66f, 1f), 5, 8, new Vector2(0.018f, 0.035f), new Vector2(3f, 6.5f), 1.3f);
            chips.ShapeAngle = 38f;
            var sparks = Sparks("Sparks", 0, 3, new Vector2(0.06f, 0.14f), new Vector2(5f, 9f), new Vector2(0.015f, 0.025f), 40f, 0.5f);
            chips.DeathSubEmitter = "ChipDust";
            var chipDust = DustPuff("ChipDust", new Color(0.7f, 0.68f, 0.64f, 0.3f), new Color(0.76f, 0.74f, 0.7f, 0.25f), 1, 1, new Vector2(0.06f, 0.11f), 40f);
            chipDust.MaxParticles = 24;
            chipDust.Lifetime = new Vector2(0.25f, 0.5f);
            chipDust.Speed = new Vector2(0.1f, 0.3f);
            return new[] { dust, chips, sparks, chipDust };
        }

        private static ParticleRecipe[] ImpactMetal()
        {
            var sparks = Sparks("Sparks", 10, 16, new Vector2(0.15f, 0.4f), new Vector2(4f, 10f), new Vector2(0.012f, 0.022f), 50f, 1.1f);
            var flash = new ParticleRecipe
            {
                Name = "Flash",
                Material = VfxMaterials.AdditiveGlow,
                Duration = 0.05f,
                BurstMin = 1,
                BurstMax = 1,
                MaxParticles = 2,
                Lifetime = new Vector2(0.04f, 0.06f),
                Speed = Vector2.zero,
                Size = new Vector2(0.18f, 0.26f),
                ColorA = new Color(1f, 0.82f, 0.5f, 1f),
                ColorB = new Color(1f, 0.72f, 0.4f, 1f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                ShapeEnabled = false
            };
            var smoke = DustPuff("Smoke", new Color(0.5f, 0.5f, 0.5f, 0.35f), new Color(0.58f, 0.58f, 0.58f, 0.28f), 2, 3, new Vector2(0.1f, 0.2f), 20f);
            smoke.Lifetime = new Vector2(0.4f, 0.8f);
            return new[] { sparks, flash, smoke };
        }

        private static ParticleRecipe[] ImpactWood()
        {
            var splinters = Chunks("Splinters", new Color(0.55f, 0.4f, 0.24f, 1f), new Color(0.78f, 0.62f, 0.4f, 1f), 6, 10, new Vector2(0.02f, 0.04f), new Vector2(2.5f, 5.5f), 1.2f);
            splinters.RenderMode = ParticleSystemRenderMode.Stretch;
            splinters.VelocityScale = 0f;
            splinters.LengthScale = 2.6f;
            splinters.Spin = 0f;
            var dust = DustPuff("Dust", new Color(0.62f, 0.52f, 0.38f, 0.5f), new Color(0.7f, 0.6f, 0.45f, 0.42f), 3, 4, new Vector2(0.14f, 0.26f), 25f);
            return new[] { splinters, dust };
        }

        private static ParticleRecipe[] ImpactWater()
        {
            var column = new ParticleRecipe
            {
                Name = "Column",
                Material = VfxMaterials.AlphaSoft,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.06f,
                LengthScale = 1.2f,
                Duration = 0.05f,
                BurstMin = 12,
                BurstMax = 18,
                MaxParticles = 20,
                Lifetime = new Vector2(0.45f, 0.8f),
                Speed = new Vector2(2.5f, 5.5f),
                Size = new Vector2(0.035f, 0.07f),
                ColorA = new Color(0.85f, 0.92f, 1f, 0.85f),
                ColorB = new Color(0.95f, 0.98f, 1f, 0.7f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 1f, 0.7f, 0.8f, 1f, 0f),
                Gravity = 1f,
                ShapeAngle = 12f,
                ShapeRadius = 0.05f
            };
            var mist = DustPuff("Mist", new Color(0.9f, 0.95f, 1f, 0.4f), new Color(0.95f, 0.97f, 1f, 0.32f), 3, 5, new Vector2(0.2f, 0.35f), 25f);
            mist.Lifetime = new Vector2(0.5f, 0.9f);
            mist.Gravity = 0.1f;
            var ring = new ParticleRecipe
            {
                Name = "Ring",
                Material = VfxMaterials.AlphaRing,
                RenderMode = ParticleSystemRenderMode.HorizontalBillboard,
                Duration = 0.05f,
                BurstMin = 1,
                BurstMax = 1,
                MaxParticles = 2,
                Lifetime = new Vector2(0.8f, 1.1f),
                Speed = Vector2.zero,
                Size = new Vector2(1f, 1.2f),
                RandomRotation = false,
                ColorA = new Color(0.9f, 0.95f, 1f, 0.6f),
                ColorB = new Color(0.9f, 0.95f, 1f, 0.5f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.15f, 1.4f),
                ShapeEnabled = false,
                MaxParticleSize = 3f
            };
            return new[] { column, mist, ring };
        }

        private static ParticleRecipe[] ImpactFoliage()
        {
            var leaves = Chunks("Leaves", new Color(0.24f, 0.36f, 0.14f, 1f), new Color(0.36f, 0.48f, 0.2f, 1f), 6, 10, new Vector2(0.03f, 0.06f), new Vector2(1.2f, 3.2f), 0.35f);
            leaves.Lifetime = new Vector2(0.9f, 1.6f);
            leaves.Drag = 2.2f;
            leaves.Spin = 6f;
            leaves.ShapeAngle = 50f;
            var dust = DustPuff("Dust", new Color(0.45f, 0.48f, 0.32f, 0.35f), new Color(0.5f, 0.52f, 0.36f, 0.3f), 2, 3, new Vector2(0.12f, 0.22f), 30f);
            return new[] { leaves, dust };
        }

        // ------------------------------------------------------------------ kan
        private static ParticleRecipe[] Blood()
        {
            var puff = new ParticleRecipe
            {
                Name = "Puff",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.05f,
                BurstMin = 4,
                BurstMax = 6,
                MaxParticles = 8,
                Lifetime = new Vector2(0.25f, 0.5f),
                Speed = new Vector2(0.6f, 2f),
                Size = new Vector2(0.1f, 0.2f),
                ColorA = new Color(0.42f, 0.02f, 0.02f, 0.85f),
                ColorB = new Color(0.58f, 0.05f, 0.04f, 0.75f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, new Color(0.8f, 0.8f, 0.8f), 0f, 1f, 0.5f, 0.7f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 2.4f),
                Gravity = 0.25f,
                Drag = 3f,
                ShapeAngle = 32f,
                ShapeRadius = 0.03f,
                MaxParticleSize = 1f
            };
            var droplets = new ParticleRecipe
            {
                Name = "Droplets",
                Material = VfxMaterials.AlphaChunk,
                RenderMode = ParticleSystemRenderMode.Stretch,
                VelocityScale = 0.03f,
                LengthScale = 1.4f,
                Duration = 0.05f,
                BurstMin = 7,
                BurstMax = 11,
                MaxParticles = 13,
                Lifetime = new Vector2(0.3f, 0.6f),
                Speed = new Vector2(2f, 4.5f),
                Size = new Vector2(0.012f, 0.026f),
                ColorA = new Color(0.35f, 0.01f, 0.01f, 1f),
                ColorB = new Color(0.5f, 0.03f, 0.03f, 1f),
                ColorOverLifetime = SolidThenFade(),
                Gravity = 1.6f,
                ShapeAngle = 28f,
                ShapeRadius = 0.02f
            };
            var mist = new ParticleRecipe
            {
                Name = "Mist",
                Material = VfxMaterials.AlphaSoft,
                Duration = 0.05f,
                BurstMin = 1,
                BurstMax = 2,
                MaxParticles = 3,
                Lifetime = new Vector2(0.35f, 0.6f),
                Speed = new Vector2(0.2f, 0.6f),
                Size = new Vector2(0.3f, 0.45f),
                ColorA = new Color(0.5f, 0.04f, 0.04f, 0.3f),
                ColorB = new Color(0.45f, 0.03f, 0.03f, 0.25f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 1.8f),
                Drag = 2f,
                ShapeAngle = 20f,
                ShapeRadius = 0.02f,
                MaxParticleSize = 1f
            };
            return new[] { puff, droplets, mist };
        }

        // ------------------------------------------------------------------ patlama (5 m referans)
        private static ParticleRecipe[] Explosion()
        {
            var fireball = new ParticleRecipe
            {
                Name = "Fireball",
                Material = VfxMaterials.AdditiveGlow,
                Duration = 0.1f,
                BurstMin = 10,
                BurstMax = 14,
                MaxParticles = 18,
                Lifetime = new Vector2(0.35f, 0.7f),
                Speed = new Vector2(1.5f, 4.5f),
                Size = new Vector2(1.6f, 2.8f),
                ColorA = new Color(1f, 0.85f, 0.55f, 1f),
                ColorB = new Color(1f, 0.7f, 0.35f, 1f),
                ColorOverLifetime = ParticleRecipe.Gradient3(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.55f, 0.15f), 0.45f, new Color(0.45f, 0.1f, 0.02f), 0f, 1f, 0.5f, 0.8f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 1.4f),
                Gravity = -0.15f,
                Drag = 3f,
                Shape = ParticleSystemShapeType.Sphere,
                ShapeRadius = 0.8f,
                MaxParticleSize = 4f
            };
            var flash = new ParticleRecipe
            {
                Name = "Flash",
                Material = VfxMaterials.AdditiveGlow,
                Duration = 0.1f,
                BurstMin = 1,
                BurstMax = 1,
                MaxParticles = 2,
                Lifetime = new Vector2(0.12f, 0.16f),
                Speed = Vector2.zero,
                Size = new Vector2(6f, 7f),
                ColorA = new Color(1f, 0.92f, 0.75f, 0.9f),
                ColorB = new Color(1f, 0.88f, 0.7f, 0.85f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                ShapeEnabled = false,
                MaxParticleSize = 6f
            };
            var smoke = new ParticleRecipe
            {
                Name = "Smoke",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.1f,
                BurstMin = 14,
                BurstMax = 18,
                MaxParticles = 22,
                Lifetime = new Vector2(2.8f, 4.8f),
                Speed = new Vector2(1.2f, 3.8f),
                Size = new Vector2(2.2f, 3.8f),
                ColorA = new Color(0.16f, 0.15f, 0.14f, 0.85f),
                ColorB = new Color(0.3f, 0.28f, 0.26f, 0.75f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, new Color(0.85f, 0.85f, 0.85f), 0f, 0f, 0.06f, 1f, 0.6f, 0.7f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.45f, 2.1f),
                Gravity = -0.06f,
                Drag = 1.4f,
                NoiseStrength = 0.4f,
                ShapeAngle = 65f,
                ShapeRadius = 1.2f,
                ShapeRotation = Up,
                SortByDistance = true,
                SortingFudge = 10f,
                MaxParticleSize = 5f
            };
            var debris = Chunks("Debris", new Color(0.18f, 0.15f, 0.12f, 1f), new Color(0.32f, 0.27f, 0.2f, 1f), 16, 24, new Vector2(0.06f, 0.16f), new Vector2(7f, 15f), 1.6f);
            debris.Lifetime = new Vector2(1.2f, 2.2f);
            debris.ShapeAngle = 55f;
            debris.ShapeRadius = 0.5f;
            debris.ShapeRotation = Up;
            var sparks = Sparks("Sparks", 18, 26, new Vector2(0.4f, 0.9f), new Vector2(8f, 18f), new Vector2(0.03f, 0.05f), 0f, 0.9f);
            sparks.Shape = ParticleSystemShapeType.Sphere;
            sparks.ShapeRadius = 0.4f;
            var dustRing = new ParticleRecipe
            {
                Name = "DustRing",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.1f,
                BurstMin = 14,
                BurstMax = 20,
                MaxParticles = 24,
                Lifetime = new Vector2(1.2f, 2.4f),
                Speed = new Vector2(5f, 9f),
                Size = new Vector2(1f, 1.8f),
                ColorA = new Color(0.48f, 0.41f, 0.31f, 0.6f),
                ColorB = new Color(0.58f, 0.5f, 0.38f, 0.5f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 0f, 0.1f, 1f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 2.2f),
                Gravity = 0.02f,
                Drag = 3f,
                Shape = ParticleSystemShapeType.Circle,
                ShapeRadius = 0.8f,
                RadiusThickness = 0.3f,
                ShapeRotation = Up,
                SortingFudge = 8f,
                MaxParticleSize = 3f
            };
            var shockwave = new ParticleRecipe
            {
                Name = "Shockwave",
                Material = VfxMaterials.AlphaRing,
                RenderMode = ParticleSystemRenderMode.HorizontalBillboard,
                Duration = 0.1f,
                BurstMin = 1,
                BurstMax = 1,
                MaxParticles = 2,
                Lifetime = new Vector2(0.3f, 0.35f),
                Speed = Vector2.zero,
                Size = new Vector2(10f, 10f),
                RandomRotation = false,
                ColorA = new Color(1f, 0.95f, 0.85f, 0.35f),
                ColorB = new Color(1f, 0.95f, 0.85f, 0.35f),
                ColorOverLifetime = FadeOut(Color.white, Color.white),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.1f, 1f),
                ShapeEnabled = false,
                ShapePosition = Vector3.zero,
                MaxParticleSize = 20f
            };
            return new[] { fireball, flash, smoke, debris, sparks, dustRing, shockwave };
        }

        // ------------------------------------------------------------------ sis bulutu (5 m referans; süre çalışma zamanında)
        private static ParticleRecipe[] Smoke()
        {
            var cloud = new ParticleRecipe
            {
                Name = "Cloud",
                Material = VfxMaterials.AlphaPuff,
                Duration = 10f,
                RateOverTime = 7f,
                BurstMin = 12,
                BurstMax = 16,
                MaxParticles = 160,
                Lifetime = new Vector2(6f, 8f),
                Speed = new Vector2(0.3f, 1.1f),
                Size = new Vector2(3.6f, 5.2f),
                ColorA = new Color(0.8f, 0.8f, 0.78f, 0.9f),
                ColorB = new Color(0.9f, 0.9f, 0.88f, 0.85f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, new Color(0.92f, 0.92f, 0.92f), 0f, 0f, 0.08f, 1f, 0.75f, 0.9f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.35f, 1.15f),
                Gravity = -0.008f,
                Drag = 0.8f,
                NoiseStrength = 0.25f,
                NoiseFrequency = 0.25f,
                ShapeAngle = 75f,
                ShapeRadius = 2.2f,
                ShapeRotation = Up,
                SortByDistance = true,
                SortingFudge = 5f,
                MaxParticleSize = 6f
            };
            var skirt = new ParticleRecipe
            {
                Name = "Skirt",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.1f,
                BurstMin = 8,
                BurstMax = 10,
                MaxParticles = 12,
                Lifetime = new Vector2(4f, 6f),
                Speed = new Vector2(1.5f, 3f),
                Size = new Vector2(2.4f, 3.4f),
                ColorA = new Color(0.84f, 0.84f, 0.82f, 0.8f),
                ColorB = new Color(0.9f, 0.9f, 0.88f, 0.75f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 0f, 0.1f, 1f, 0.7f, 0.85f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.4f, 1.3f),
                Drag = 1.5f,
                Shape = ParticleSystemShapeType.Circle,
                ShapeRadius = 1f,
                ShapeRotation = Up,
                SortByDistance = true,
                SortingFudge = 5f,
                MaxParticleSize = 6f
            };
            return new[] { cloud, skirt };
        }

        // ------------------------------------------------------------------ toz (iniş / rotor)
        private static ParticleRecipe[] Dust()
        {
            var ring = new ParticleRecipe
            {
                Name = "Ring",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.1f,
                BurstMin = 8,
                BurstMax = 12,
                MaxParticles = 14,
                Lifetime = new Vector2(0.8f, 1.6f),
                Speed = new Vector2(1.2f, 2.8f),
                Size = new Vector2(0.4f, 0.7f),
                ColorA = new Color(0.56f, 0.48f, 0.36f, 0.55f),
                ColorB = new Color(0.64f, 0.56f, 0.43f, 0.45f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 0f, 0.1f, 1f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.5f, 2.2f),
                Gravity = -0.01f,
                Drag = 2.5f,
                Shape = ParticleSystemShapeType.Circle,
                ShapeRadius = 0.25f,
                ShapeRotation = Up,
                MaxParticleSize = 2f
            };
            var puff = new ParticleRecipe
            {
                Name = "Puff",
                Material = VfxMaterials.AlphaPuff,
                Duration = 0.1f,
                BurstMin = 2,
                BurstMax = 4,
                MaxParticles = 6,
                Lifetime = new Vector2(0.8f, 1.3f),
                Speed = new Vector2(0.3f, 0.8f),
                Size = new Vector2(0.5f, 0.8f),
                ColorA = new Color(0.58f, 0.5f, 0.38f, 0.45f),
                ColorB = new Color(0.66f, 0.58f, 0.45f, 0.38f),
                ColorOverLifetime = ParticleRecipe.Gradient(Color.white, Color.white, 0f, 0f, 0.12f, 1f, 1f, 0f),
                SizeOverLifetime = ParticleRecipe.EaseOut(0.6f, 2f),
                Drag = 1.5f,
                ShapeAngle = 30f,
                ShapeRadius = 0.2f,
                ShapeRotation = Up,
                MaxParticleSize = 2f
            };
            return new[] { ring, puff };
        }
    }
}
