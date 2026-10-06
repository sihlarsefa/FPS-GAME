using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Presentation.UI.Lobby.Squad
{
    /// <summary>Tim slotundaki oyuncunun saf verisi (Unity nesnesi içermez).</summary>
    public sealed class SquadMember
    {
        public string Id { get; }
        public string Name { get; set; }
        public MilitaryRank Rank { get; set; }
        public bool Ready { get; set; }
        public bool Speaking { get; set; }
        public bool Muted { get; set; }
        public bool IsLeader { get; internal set; }
        public bool IsLocal { get; }

        public SquadMember(string id, string name, MilitaryRank rank, bool isLocal = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Oyuncu kimliği boş olamaz.", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? "ER" : name.Trim();
            Rank = rank;
            IsLocal = isLocal;
        }
    }

    /// <summary>Slot görünüm durumu: boş (davet edilebilir), davet bekleyen, dolu-hazır değil, dolu-hazır.</summary>
    public enum SquadSlotState { Empty = 0, Invited = 1, NotReady = 2, Ready = 3 }

    /// <summary>
    /// 4 slotlu tim (parti) modeli. Yerel oyuncu her zaman slot 0'dadır; lider yoksa en kıdemli üye devralır.
    /// Maç başlatma kuralı: lider dışındaki herkes hazır olmalı, lider zaten başlatan taraftır.
    /// </summary>
    public sealed class SquadRoster
    {
        public const int SlotCount = 4;
        /// <summary>Davetin yanıtsız süreceği saniye.</summary>
        public const float InviteTimeoutSeconds = 30f;

        private readonly SquadMember[] _slots = new SquadMember[SlotCount];
        private readonly float[] _inviteLeft = new float[SlotCount];
        private readonly string[] _inviteName = new string[SlotCount];

        public event Action Changed;

        public SquadMember this[int slot] => InRange(slot) ? _slots[slot] : null;

        public int MemberCount
        {
            get { var n = 0; for (var i = 0; i < SlotCount; i++) if (_slots[i] != null) n++; return n; }
        }

        public int ReadyCount
        {
            get { var n = 0; for (var i = 0; i < SlotCount; i++) if (_slots[i] != null && _slots[i].Ready) n++; return n; }
        }

        public bool IsFull => MemberCount >= SlotCount;

        public SquadMember Leader
        {
            get { for (var i = 0; i < SlotCount; i++) if (_slots[i] != null && _slots[i].IsLeader) return _slots[i]; return null; }
        }

        public SquadSlotState StateOf(int slot)
        {
            if (!InRange(slot)) return SquadSlotState.Empty;
            var m = _slots[slot];
            if (m != null) return m.Ready ? SquadSlotState.Ready : SquadSlotState.NotReady;
            return _inviteLeft[slot] > 0f ? SquadSlotState.Invited : SquadSlotState.Empty;
        }

        public string InvitedName(int slot) => InRange(slot) && _inviteLeft[slot] > 0f ? _inviteName[slot] : null;
        public float InviteRemaining(int slot) => InRange(slot) ? Math.Max(0f, _inviteLeft[slot]) : 0f;

        /// <summary>İlk boş slota ekler; dolu ya da kimlik tekrarıysa false. Yerel oyuncu slot 0'a yerleşir.</summary>
        public bool TryAdd(SquadMember member)
        {
            if (member == null || FindIndex(member.Id) >= 0) return false;
            var target = -1;
            if (member.IsLocal)
            {
                // Yerel oyuncu slot 0'ı alır; oradaki üye varsa boş slota kayar.
                if (_slots[0] != null)
                {
                    for (var i = 1; i < SlotCount; i++) if (_slots[i] == null) { _slots[i] = _slots[0]; _slots[0] = null; break; }
                }
                if (_slots[0] == null) target = 0;
            }
            else
            {
                for (var i = 0; i < SlotCount; i++) if (_slots[i] == null) { target = i; break; }
            }
            if (target < 0) return false;
            _slots[target] = member;
            _inviteLeft[target] = 0f;
            _inviteName[target] = null;
            member.Ready = member.Ready && !member.IsLeader;
            EnsureLeader();
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string id)
        {
            var i = FindIndex(id);
            if (i < 0) return false;
            _slots[i] = null;
            Compact();
            EnsureLeader();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Boş slota davet gönderir (zaman aşımı <see cref="InviteTimeoutSeconds"/>).</summary>
        public bool Invite(int slot, string name)
        {
            if (!InRange(slot) || _slots[slot] != null || _inviteLeft[slot] > 0f) return false;
            _inviteLeft[slot] = InviteTimeoutSeconds;
            _inviteName[slot] = string.IsNullOrWhiteSpace(name) ? "OYUNCU" : name.Trim();
            Changed?.Invoke();
            return true;
        }

        public bool CancelInvite(int slot)
        {
            if (!InRange(slot) || _inviteLeft[slot] <= 0f) return false;
            _inviteLeft[slot] = 0f;
            _inviteName[slot] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Davet sürelerini ilerletir; süresi dolan davetlerin sayısını döner.</summary>
        public int Tick(float dt)
        {
            var expired = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                if (_inviteLeft[i] <= 0f) continue;
                _inviteLeft[i] -= dt;
                if (_inviteLeft[i] <= 0f) { _inviteLeft[i] = 0f; _inviteName[i] = null; expired++; }
            }
            if (expired > 0) Changed?.Invoke();
            return expired;
        }

        /// <summary>Hazır durumunu ayarlar. Lider hazır işaretlenmez (başlatan taraftır).</summary>
        public bool SetReady(string id, bool ready)
        {
            var i = FindIndex(id);
            if (i < 0 || _slots[i].IsLeader) return false;
            if (_slots[i].Ready == ready) return true;
            _slots[i].Ready = ready;
            Changed?.Invoke();
            return true;
        }

        public bool ToggleReady(string id)
        {
            var i = FindIndex(id);
            return i >= 0 && SetReady(id, !_slots[i].Ready);
        }

        public bool SetSpeaking(string id, bool speaking)
        {
            var i = FindIndex(id);
            if (i < 0 || _slots[i].Speaking == speaking) return false;
            _slots[i].Speaking = speaking && !_slots[i].Muted;
            Changed?.Invoke();
            return true;
        }

        public bool SetMuted(string id, bool muted)
        {
            var i = FindIndex(id);
            if (i < 0) return false;
            _slots[i].Muted = muted;
            if (muted) _slots[i].Speaking = false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Lider dışındaki tüm üyeler hazır mı (tek kişi de başlatabilir).</summary>
        public bool CanStart()
        {
            if (MemberCount == 0) return false;
            for (var i = 0; i < SlotCount; i++)
                if (_slots[i] != null && !_slots[i].IsLeader && !_slots[i].Ready) return false;
            return true;
        }

        /// <summary>Başlatmayı engelleyen üyelerin adları (boşsa başlatılabilir).</summary>
        public List<string> WaitingNames()
        {
            var list = new List<string>();
            for (var i = 0; i < SlotCount; i++)
                if (_slots[i] != null && !_slots[i].IsLeader && !_slots[i].Ready) list.Add(_slots[i].Name);
            return list;
        }

        /// <summary>Üst başlık metni: "TİM 3/4 · 2 HAZIR".</summary>
        public string SummaryText()
        {
            var waiting = 0;
            for (var i = 0; i < SlotCount; i++) if (_slots[i] != null && !_slots[i].IsLeader && !_slots[i].Ready) waiting++;
            var s = "TİM " + MemberCount + "/" + SlotCount;
            if (MemberCount > 1) s += waiting == 0 ? "  ·  HAZIR" : "  ·  " + waiting + " BEKLENİYOR";
            return s;
        }

        /// <summary>Lider yoksa en kıdemli üyeyi (eşitlikte ilk slot) lider yapar.</summary>
        private void EnsureLeader()
        {
            var best = -1;
            for (var i = 0; i < SlotCount; i++)
            {
                var m = _slots[i];
                if (m == null) continue;
                if (m.IsLeader) { best = i; break; }
                if (best < 0 || m.Rank > _slots[best].Rank) best = i;
            }
            for (var i = 0; i < SlotCount; i++) if (_slots[i] != null) _slots[i].IsLeader = i == best;
            if (best >= 0) _slots[best].Ready = false;
        }

        /// <summary>Ayrılan üyeden sonra boşlukları kapatır (yerel oyuncu 0'da kalır, davetler yerinde kalır).</summary>
        private void Compact()
        {
            var w = 0;
            for (var r = 0; r < SlotCount; r++)
            {
                if (_slots[r] == null) continue;
                if (w != r) { _slots[w] = _slots[r]; _slots[r] = null; }
                w++;
            }
        }

        private int FindIndex(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (var i = 0; i < SlotCount; i++) if (_slots[i] != null && _slots[i].Id == id) return i;
            return -1;
        }

        private static bool InRange(int slot) => slot >= 0 && slot < SlotCount;
    }
}
