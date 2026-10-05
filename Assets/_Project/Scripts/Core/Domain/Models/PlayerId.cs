using System;

namespace Project.Core.Domain
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public int Value { get; }

        public PlayerId(int value) => Value = value;

        public bool IsValid => Value >= 0;

        public static PlayerId Invalid => new(-1);

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;

        public override string ToString() => Value.ToString();
    }
}
