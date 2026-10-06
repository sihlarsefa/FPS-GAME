using System;
using System.Collections.Generic;

namespace Project.Application.Replay
{
    /// <summary>Tekrar olay türü.</summary>
    public enum ReplayEventType : byte
    {
        Shot = 0,
        Hit = 1,
        Death = 2,
        Explosion = 3,
        Zone = 4
    }

    /// <summary>Tekrardaki bir asker (maç başında sabit).</summary>
    public sealed class ReplayPlayer
    {
        public int Id;
        public string Name;
        public int Team;
        public bool IsBot;
    }

    /// <summary>Bir karedeki tek askerin durumu.</summary>
    public struct ReplayActorSample
    {
        public int Id;
        public float X, Y, Z;
        /// <summary>Derece, 0..360.</summary>
        public float Yaw;
        /// <summary>Derece, -90..90.</summary>
        public float Pitch;
        public byte Stance;
        /// <summary>ReplayData.Weapons indeksi, silahsız = -1.</summary>
        public int Weapon;
        public bool Alive;
    }

    /// <summary>Sabit aralıklı örnek (varsayılan 10 Hz).</summary>
    public sealed class ReplayFrame
    {
        public float Time;
        public float ZoneX, ZoneZ, ZoneRadius;
        public ReplayActorSample[] Actors = Array.Empty<ReplayActorSample>();
    }

    /// <summary>Anlık olay (atış, isabet, ölüm, patlama, bölge aşaması).</summary>
    public struct ReplayEvent
    {
        public float Time;
        public ReplayEventType Type;
        /// <summary>Atan / saldıran / öldüren (yoksa -1).</summary>
        public int Actor;
        /// <summary>Hedef / kurban (yoksa -1).</summary>
        public int Target;
        public float X, Y, Z;
        /// <summary>Hasar, patlama yarıçapı veya bölge aşama indeksi.</summary>
        public float Value;
        public int Weapon;
        /// <summary>Kafadan vuruş / öldürücü isabet.</summary>
        public bool Flag;
    }

    /// <summary>Bir maçın tüm tekrar verisi (bellekte).</summary>
    public sealed class ReplayData
    {
        public const int CurrentVersion = 1;
        public const float DefaultSampleInterval = 0.1f;

        public int Version = CurrentVersion;
        public string MapId = string.Empty;
        public int MatchSeed;
        public int WorldSeed;
        public int LocalPlayerId = -1;
        public float SampleInterval = DefaultSampleInterval;
        public long RecordedAtUtcTicks;
        public readonly List<ReplayPlayer> Players = new List<ReplayPlayer>();
        public readonly List<string> Weapons = new List<string>();
        public readonly List<ReplayFrame> Frames = new List<ReplayFrame>();
        public readonly List<ReplayEvent> Events = new List<ReplayEvent>();

        public float Duration => Frames.Count == 0 ? 0f : Frames[Frames.Count - 1].Time;

        /// <summary>Silah kimliğinin tablodaki indeksini döndürür (yoksa ekler); boş kimlik -1.</summary>
        public int WeaponIndex(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return -1;
            var i = Weapons.IndexOf(weaponId);
            if (i >= 0)
                return i;
            Weapons.Add(weaponId);
            return Weapons.Count - 1;
        }

        public string WeaponName(int index) => index >= 0 && index < Weapons.Count ? Weapons[index] : null;

        public ReplayPlayer FindPlayer(int id)
        {
            for (var i = 0; i < Players.Count; i++)
                if (Players[i].Id == id)
                    return Players[i];
            return null;
        }

        /// <summary>Zaman çizelgesi için ölüm olayları (zaman sıralı).</summary>
        public List<ReplayEvent> Kills()
        {
            var list = new List<ReplayEvent>();
            for (var i = 0; i < Events.Count; i++)
                if (Events[i].Type == ReplayEventType.Death)
                    list.Add(Events[i]);
            return list;
        }
    }
}
