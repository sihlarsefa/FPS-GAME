using System;
using Project.Core.Domain;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>Ağ üzerinden taşınan <see cref="PlayerCommand"/> paketı.</summary>
    public struct NetworkPlayerCommand : INetworkSerializable, IEquatable<NetworkPlayerCommand>
    {
        public uint Tick;
        public float MoveForward;
        public float MoveRight;
        public float Yaw;
        public float Pitch;
        public uint Buttons;
        public sbyte SelectSlot;
        public sbyte CycleWeapon;

        public static NetworkPlayerCommand From(PlayerCommand command) => new()
        {
            Tick = command.Tick,
            MoveForward = command.MoveForward,
            MoveRight = command.MoveRight,
            Yaw = command.Yaw,
            Pitch = command.Pitch,
            Buttons = (uint)command.Buttons,
            SelectSlot = command.SelectSlot,
            CycleWeapon = command.CycleWeapon
        };

        public PlayerCommand ToDomain() => new(
            Tick,
            MoveForward,
            MoveRight,
            Yaw,
            Pitch,
            (PlayerButtons)Buttons,
            SelectSlot,
            CycleWeapon);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref MoveForward);
            serializer.SerializeValue(ref MoveRight);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref Buttons);
            serializer.SerializeValue(ref SelectSlot);
            serializer.SerializeValue(ref CycleWeapon);
        }

        public bool Equals(NetworkPlayerCommand other) =>
            Tick == other.Tick
            && Buttons == other.Buttons
            && SelectSlot == other.SelectSlot
            && CycleWeapon == other.CycleWeapon
            && Mathf.Approximately(MoveForward, other.MoveForward)
            && Mathf.Approximately(MoveRight, other.MoveRight)
            && Mathf.Approximately(Yaw, other.Yaw)
            && Mathf.Approximately(Pitch, other.Pitch);

        public override bool Equals(object obj) => obj is NetworkPlayerCommand other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tick, Buttons, SelectSlot, CycleWeapon);
    }

    /// <summary>Ağ üzerinden taşınan <see cref="ShotRequest"/> paketı.</summary>
    public struct NetworkShotRequest : INetworkSerializable
    {
        public int ShooterId;
        public FixedString64Bytes WeaponId;
        public Vector3 Origin;
        public Vector3 Direction;
        public uint Tick;

        public static NetworkShotRequest From(ShotRequest request) => new()
        {
            ShooterId = request.ShooterId.Value,
            WeaponId = request.WeaponId ?? string.Empty,
            Origin = new Vector3(request.Origin.X, request.Origin.Y, request.Origin.Z),
            Direction = new Vector3(request.Direction.X, request.Direction.Y, request.Direction.Z),
            Tick = request.Tick
        };

        public ShotRequest ToDomain() => new(
            new PlayerId(ShooterId),
            WeaponId.ToString(),
            new Float3(Origin.x, Origin.y, Origin.z),
            new Float3(Direction.x, Direction.y, Direction.z),
            Tick);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ShooterId);
            serializer.SerializeValue(ref WeaponId);
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Tick);
        }
    }

    /// <summary>Ağ üzerinden taşınan <see cref="ThrowRequest"/> paketı.</summary>
    public struct NetworkThrowRequest : INetworkSerializable
    {
        public byte Kind;
        public Vector3 Origin;
        public Vector3 Velocity;
        public uint Tick;

        public static NetworkThrowRequest From(ThrowRequest r) => new()
        {
            Kind = (byte)r.Kind,
            Origin = new Vector3(r.Origin.X, r.Origin.Y, r.Origin.Z),
            Velocity = new Vector3(r.Velocity.X, r.Velocity.Y, r.Velocity.Z),
            Tick = r.Tick
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Tick);
        }
    }

    /// <summary>Ağ üzerinden taşınan <see cref="MeleeRequest"/> paketı.</summary>
    public struct NetworkMeleeRequest : INetworkSerializable
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Range;
        public uint Tick;

        public static NetworkMeleeRequest From(MeleeRequest r) => new()
        {
            Origin = new Vector3(r.Origin.X, r.Origin.Y, r.Origin.Z),
            Direction = new Vector3(r.Direction.X, r.Direction.Y, r.Direction.Z),
            Range = r.Range,
            Tick = r.Tick
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Range);
            serializer.SerializeValue(ref Tick);
        }
    }

    /// <summary>Sunucunun istemciye gönderdiği uzlaştırma anlık görüntüsü.</summary>
    public struct NetworkReconciliationSnapshot : INetworkSerializable
    {
        public uint Tick;
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public float Health;
        public float Armor;
        public int AmmoInMag;
        public int AmmoReserve;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref Armor);
            serializer.SerializeValue(ref AmmoInMag);
            serializer.SerializeValue(ref AmmoReserve);
        }
    }
}
