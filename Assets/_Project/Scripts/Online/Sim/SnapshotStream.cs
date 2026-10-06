using System;

namespace Project.Online.Sim
{
    /// <summary>
    /// Sunucu tarafı: tek (gözlemci, hedef) çifti için delta akışı. İlk gönderim ve her keyframe aralığında
    /// tam durum, aralarda baseline'a göre delta yazar (kayıp paketler keyframe ile toparlanır). Saf mantık.
    /// </summary>
    public sealed class SnapshotChannel
    {
        public const uint DefaultKeyframeTicks = 30;
        private PlayerSnap _baseline;
        private bool _has;
        private uint _lastFullTick;

        /// <summary>Baseline'ı unut (yeniden bağlanma / görünürlük yeniden açıldı): sonraki yazım tam durumdur.</summary>
        public void Reset() { _has = false; }

        /// <summary>Gerekirse tam, değilse delta yazar; tam yazıldıysa true döner.</summary>
        public bool Write(byte[] buf, ref int o, in PlayerSnap current, uint tick, uint keyframeTicks = DefaultKeyframeTicks)
        {
            var full = !_has || tick - _lastFullTick >= keyframeTicks;
            if (full)
            {
                SnapshotDelta.WriteFull(buf, ref o, current);
                _lastFullTick = tick;
            }
            else
                SnapshotDelta.WriteDelta(buf, ref o, _baseline, current);
            _baseline = current;
            _has = true;
            return full;
        }
    }

    /// <summary>İstemci tarafı: bir oyuncunun delta akışını baseline üzerine uygular.</summary>
    public sealed class SnapshotReceiver
    {
        private PlayerSnap _baseline;
        private bool _has;

        public bool HasBaseline => _has;
        public PlayerSnap Last => _baseline;

        public void Reset() { _has = false; }

        /// <summary>
        /// Paketi uygular. allowDeltas=false iken (yeniden eşitleme) yalnızca tam durum kabul edilir.
        /// Bozuk / baseline'sız delta güvenle reddedilir (false).
        /// </summary>
        public bool Apply(byte[] buf, int offset, bool allowDeltas, out bool wasFull)
        {
            wasFull = false;
            if (buf == null || offset < 0 || buf.Length - offset < 5) return false;
            var raw = buf[offset + 4];
            wasFull = (raw & 31) == 31 && (raw & 32) != 0;
            if (!wasFull && !allowDeltas) return false;
            var o = offset;
            if (!SnapshotDelta.TryRead(buf, ref o, _has && !wasFull, _baseline, out var snap)) return false;
            _baseline = snap;
            _has = true;
            return true;
        }
    }
}
