using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Online.Profile
{
    public enum OfflineQueueState
    {
        Idle = 0,
        Pending = 1,
        Syncing = 2,
        Failed = 3
    }

    /// <summary>
    /// Çevrimdışı oynanan maç sonuçlarını PlayerPrefs kuyruğunda tutar; online olunca gönderilir.
    /// </summary>
    public sealed class OfflineMatchQueue
    {
        public const string PrefsKey = "harekat.online.offlineQueue.v1";
        public const int MaxEntries = 32;

        private readonly List<QueuedMatchEntry> _entries = new List<QueuedMatchEntry>(8);
        private OfflineQueueState _state = OfflineQueueState.Idle;
        private string _lastError;

        public OfflineQueueState State => _state;
        public string LastError => _lastError;
        public int Count => _entries.Count;
        public IReadOnlyList<QueuedMatchEntry> Entries => _entries;

        public event Action StateChanged;

        public void Load()
        {
            _entries.Clear();
            var raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                SetState(_entries.Count > 0 ? OfflineQueueState.Pending : OfflineQueueState.Idle, null);
                return;
            }

            var parts = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length && _entries.Count < MaxEntries; i++)
            {
                if (QueuedMatchEntry.TryParse(parts[i], out var entry))
                    _entries.Add(entry);
            }

            SetState(_entries.Count > 0 ? OfflineQueueState.Pending : OfflineQueueState.Idle, null);
        }

        public void Enqueue(MatchResult result)
        {
            var entry = QueuedMatchEntry.FromResult(result, DateTimeOffset.UtcNow);
            _entries.Add(entry);
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
            Persist();
            SetState(OfflineQueueState.Pending, null);
        }

        public bool TryPeek(out QueuedMatchEntry entry)
        {
            if (_entries.Count == 0)
            {
                entry = default;
                return false;
            }

            entry = _entries[0];
            return true;
        }

        public void Dequeue()
        {
            if (_entries.Count == 0)
                return;
            _entries.RemoveAt(0);
            Persist();
            SetState(_entries.Count > 0 ? OfflineQueueState.Pending : OfflineQueueState.Idle, null);
        }

        public void MarkSyncing() => SetState(OfflineQueueState.Syncing, null);

        public void MarkFailed(string error)
        {
            SetState(_entries.Count > 0 ? OfflineQueueState.Failed : OfflineQueueState.Idle, error);
        }

        public void Clear()
        {
            _entries.Clear();
            Persist();
            SetState(OfflineQueueState.Idle, null);
        }

        private void Persist()
        {
            if (_entries.Count == 0)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
                return;
            }

            var sb = new StringBuilder(256);
            for (var i = 0; i < _entries.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_entries[i].Serialize());
            }

            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }

        private void SetState(OfflineQueueState state, string error)
        {
            _state = state;
            _lastError = error;
            try { StateChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    public readonly struct QueuedMatchEntry
    {
        public readonly bool IsWinner;
        public readonly int Placement;
        public readonly int TotalPlayers;
        public readonly int Kills;
        public readonly int Headshots;
        public readonly float DamageDealt;
        public readonly float SurvivalSeconds;
        public readonly float Accuracy;
        public readonly int TeamPlacement;
        public readonly int TeamCount;
        public readonly long EnqueuedUnix;

        public QueuedMatchEntry(
            bool isWinner, int placement, int totalPlayers, int kills, int headshots,
            float damageDealt, float survivalSeconds, float accuracy,
            int teamPlacement, int teamCount, long enqueuedUnix)
        {
            IsWinner = isWinner;
            Placement = placement;
            TotalPlayers = totalPlayers;
            Kills = kills;
            Headshots = headshots;
            DamageDealt = damageDealt;
            SurvivalSeconds = survivalSeconds;
            Accuracy = accuracy;
            TeamPlacement = teamPlacement;
            TeamCount = teamCount;
            EnqueuedUnix = enqueuedUnix;
        }

        public static QueuedMatchEntry FromResult(MatchResult result, DateTimeOffset when)
        {
            return new QueuedMatchEntry(
                result.IsWinner,
                result.Placement,
                result.TotalPlayers,
                result.Kills,
                result.Headshots,
                result.DamageDealt,
                result.SurvivalSeconds,
                result.Accuracy,
                result.TeamPlacement,
                result.TeamCount,
                when.ToUnixTimeSeconds());
        }

        public MatchResult ToMatchResult()
        {
            return new MatchResult(
                IsWinner, Placement, TotalPlayers, Kills, Headshots,
                DamageDealt, SurvivalSeconds, Accuracy, null,
                TeamPlacement, TeamCount, null, Kills);
        }

        public string Serialize()
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Join("|", new[]
            {
                IsWinner ? "1" : "0",
                Placement.ToString(inv),
                TotalPlayers.ToString(inv),
                Kills.ToString(inv),
                Headshots.ToString(inv),
                DamageDealt.ToString("R", inv),
                SurvivalSeconds.ToString("R", inv),
                Accuracy.ToString("R", inv),
                TeamPlacement.ToString(inv),
                TeamCount.ToString(inv),
                EnqueuedUnix.ToString(inv)
            });
        }

        public static bool TryParse(string line, out QueuedMatchEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(line))
                return false;

            var parts = line.Split('|');
            if (parts.Length < 11)
                return false;

            var inv = CultureInfo.InvariantCulture;
            try
            {
                entry = new QueuedMatchEntry(
                    parts[0] == "1",
                    int.Parse(parts[1], inv),
                    int.Parse(parts[2], inv),
                    int.Parse(parts[3], inv),
                    int.Parse(parts[4], inv),
                    float.Parse(parts[5], inv),
                    float.Parse(parts[6], inv),
                    float.Parse(parts[7], inv),
                    int.Parse(parts[8], inv),
                    int.Parse(parts[9], inv),
                    long.Parse(parts[10], inv));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
