using System;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>System.Random tabanlı tohumlanabilir rastgelelik (aynı tohum → aynı dizi).</summary>
    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int Next(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);
        public float NextFloat() => (float)_random.NextDouble();
        public float Range(float min, float max) => min + (max - min) * (float)_random.NextDouble();

        /// <summary>chance olasılıkla true.</summary>
        public bool Chance(float chance) => chance > 0f && _random.NextDouble() < chance;
    }
}
