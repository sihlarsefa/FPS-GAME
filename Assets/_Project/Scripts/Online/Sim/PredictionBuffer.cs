using Project.Core.Domain;
using UnityEngine;

namespace Project.Online.Sim
{
    /// <summary>
    /// İstemci tahmin tamponu: gönderilen komutlar (sıra numarası = PlayerCommand.Tick) ve o komut
    /// sonrası tahmin edilen konum. Sunucu onayı (ack) gelince onaylanan komutlar atılır, tahmin ile
    /// sunucu konumu karşılaştırılır; fark büyükse kalan komutlar yeniden oynatılır. Saf mantık.
    /// </summary>
    public sealed class PredictionBuffer
    {
        public struct Entry
        {
            public uint Sequence;
            public PlayerCommand Command;
            public Vector3 PredictedPosition;
            public bool HasPrediction;
        }

        private readonly Entry[] _ring;
        private int _start;
        private int _count;
        private uint _nextSequence = 1;

        public PredictionBuffer(int capacity = 128)
        {
            _ring = new Entry[Mathf.Max(8, capacity)];
        }

        public int Count => _count;
        public int Capacity => _ring.Length;
        public uint LastAckedSequence { get; private set; }
        public uint LastSequence { get; private set; }

        /// <summary>Komut Tick'i 0 ise (sıra numarası yok) yeni bir numara üretilir.</summary>
        public uint NextSequence() => _nextSequence++;

        /// <summary>Komutu kaydeder. Tamponun eski ucu dolarsa en eski komut düşer. Sıra gerilerse reddedilir.</summary>
        public bool Record(uint sequence, PlayerCommand command)
        {
            if (_count > 0 && !IsNewer(sequence, LastSequence))
                return false;

            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
            }

            var idx = (_start + _count) % _ring.Length;
            _ring[idx] = new Entry { Sequence = sequence, Command = command };
            _count++;
            LastSequence = sequence;
            if (sequence >= _nextSequence)
                _nextSequence = sequence + 1;
            return true;
        }

        public Entry At(int index) => _ring[(_start + index) % _ring.Length];

        /// <summary>Verilen sıra numaralı komutun sonrası tahmini konumu yazar.</summary>
        public bool SetPrediction(uint sequence, Vector3 position)
        {
            for (var i = _count - 1; i >= 0; i--)
            {
                var idx = (_start + i) % _ring.Length;
                if (_ring[idx].Sequence == sequence)
                {
                    _ring[idx].PredictedPosition = position;
                    _ring[idx].HasPrediction = true;
                    return true;
                }

                if (IsNewer(sequence, _ring[idx].Sequence))
                    break;
            }

            return false;
        }

        /// <summary>
        /// Sunucu onayı. Dönen true = tahmin hatalı, istemci sunucu konumuna çekilip
        /// <see cref="At"/> ile kalan komutları yeniden oynatmalı. error: tahmin ile sunucu farkı (m).
        /// </summary>
        public bool Acknowledge(uint ackSequence, Vector3 serverPosition, float tolerance, out float error)
        {
            error = 0f;
            if (_count > 0 && ackSequence != 0 && ackSequence < LastAckedSequence)
                return false; // eski/sıra dışı onay

            var hasEntry = false;
            var predicted = default(Vector3);
            while (_count > 0 && !IsNewer(_ring[_start].Sequence, ackSequence))
            {
                var e = _ring[_start];
                if (e.Sequence == ackSequence && e.HasPrediction)
                {
                    hasEntry = true;
                    predicted = e.PredictedPosition;
                }

                _start = (_start + 1) % _ring.Length;
                _count--;
            }

            if (ackSequence > LastAckedSequence)
                LastAckedSequence = ackSequence;

            if (!hasEntry)
                return false; // karşılaştıracak tahmin yok: düzeltme yapma

            error = Vector3.Distance(predicted, serverPosition);
            return error > tolerance;
        }

        public void Clear()
        {
            _start = 0;
            _count = 0;
        }

        private static bool IsNewer(uint a, uint b) => a != b && (a - b) < 0x80000000u;
    }

    /// <summary>Sunucu: komut sırası doğrulama (tekrar/eski komut reddi) + istemci başına hız sınırı.</summary>
    public sealed class ServerCommandGate
    {
        private readonly float _perSecond;
        private readonly float _burst;
        private float _tokens;
        private float _lastTime = float.NaN;
        private uint _lastSequence;
        private bool _any;

        public ServerCommandGate(float perSecond, float burst)
        {
            _perSecond = Mathf.Max(1f, perSecond);
            _burst = Mathf.Max(1f, burst);
            _tokens = _burst;
        }

        public uint LastSequence => _lastSequence;
        public int RejectedStale { get; private set; }
        public int RejectedRate { get; private set; }

        /// <summary>sequence 0 = sıra yok (yerel/host): yalnızca kabul edilir.</summary>
        public bool Accept(uint sequence, float now)
        {
            if (sequence == 0)
                return true;

            if (_any && (sequence == _lastSequence || (_lastSequence - sequence) < 0x80000000u))
            {
                RejectedStale++;
                return false;
            }

            if (!float.IsNaN(_lastTime))
                _tokens = Mathf.Min(_burst, _tokens + Mathf.Max(0f, now - _lastTime) * _perSecond);
            _lastTime = now;

            if (_tokens < 1f)
            {
                RejectedRate++;
                return false;
            }

            _tokens -= 1f;
            _lastSequence = sequence;
            _any = true;
            return true;
        }
    }
}
