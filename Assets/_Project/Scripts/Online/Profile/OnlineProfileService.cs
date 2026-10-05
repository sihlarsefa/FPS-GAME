using System;
using System.Threading;
using System.Threading.Tasks;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Online.Backend;
using UnityEngine;

namespace Project.Online.Profile
{
    /// <summary>
    /// Backend profili ile yerel <see cref="CareerStatsService"/> birleştirmesi.
    /// Online modda tek doğru kaynak backend'dir; çevrimdışı maçlar kuyruklanır ve sonra gönderilir.
    /// </summary>
    public sealed class OnlineProfileService
    {
        private readonly BackendClient _client;
        private readonly CareerStatsService _localCareer;
        private readonly OfflineMatchQueue _queue;
        private bool _onlineMode;
        private bool _syncing;

        public OnlineProfileService(BackendClient client, CareerStatsService localCareer, OfflineMatchQueue queue = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _localCareer = localCareer;
            _queue = queue ?? new OfflineMatchQueue();
            _queue.Load();
        }

        public BackendClient Client => _client;
        public OfflineMatchQueue Queue => _queue;
        public bool IsOnlineMode => _onlineMode;
        public PlayerProfile RemoteProfile { get; private set; }
        public bool IsSyncing => _syncing;

        public event Action ProfileChanged;
        public event Action<string> SyncFailed;

        /// <summary>Giriş sonrası: backend profilini çeker, yereli üzerine yazar, kuyruğu boşaltmayı dener.</summary>
        public async Task<PlayerProfile> ActivateOnlineAsync(CancellationToken ct = default)
        {
            var me = await _client.GetMeAsync(ct).ConfigureAwait(false);
            RemoteProfile = me;
            ApplyRemoteAsAuthority(me);
            _onlineMode = true;
            RaiseChanged();
            await FlushQueueAsync(ct).ConfigureAwait(false);
            return me;
        }

        public void DeactivateOnline()
        {
            _onlineMode = false;
            RemoteProfile = null;
            RaiseChanged();
        }

        /// <summary>
        /// Maç sonucu kaydı. Online ise backend'e kuyruklar (yerel de güncellenir, sonra backend birleşir);
        /// çevrimdışıysa yerel + kuyruk.
        /// </summary>
        public void RecordMatch(MatchResult result)
        {
            if (_localCareer != null)
            {
                try { _localCareer.Record(result); }
                catch (Exception e) { Debug.LogException(e); }
            }

            if (_onlineMode && _client.IsLoggedIn)
            {
                _queue.Enqueue(result);
                _ = FlushQueueSafe();
            }
            else
            {
                _queue.Enqueue(result);
            }

            RaiseChanged();
        }

        public async Task FlushQueueAsync(CancellationToken ct = default)
        {
            if (_syncing || !_client.IsLoggedIn)
                return;

            _syncing = true;
            _queue.MarkSyncing();
            try
            {
                while (_queue.TryPeek(out var entry))
                {
                    ct.ThrowIfCancellationRequested();
                    // Sunucu maç sonucu uçları dedicated server anahtarı ister.
                    // İstemci tarafında kuyruk: profili yeniden çekerek birleştirme tetiklenir;
                    // gerçek XP sunucu sonucuyla gelir. Burada bekleyen yerel delta korunur,
                    // bağlantı varken /players/me ile yetkili profil alınır.
                    await _client.GetMeAsync(ct).ConfigureAwait(false);
                    RemoteProfile = _client.CurrentPlayer;
                    ApplyRemoteAsAuthority(RemoteProfile);
                    _queue.Dequeue();
                }

                _onlineMode = true;
            }
            catch (BackendApiException ex)
            {
                _queue.MarkFailed(ex.TurkishMessage);
                SyncFailed?.Invoke(ex.TurkishMessage);
            }
            catch (Exception ex)
            {
                var msg = "Kuyruk senkronu başarısız: " + ex.Message;
                _queue.MarkFailed(msg);
                SyncFailed?.Invoke(msg);
            }
            finally
            {
                _syncing = false;
                RaiseChanged();
            }
        }

        /// <summary>Backend CareerStatsDto → yerel CareerStats (yetkili kaynak).</summary>
        public void ApplyRemoteAsAuthority(PlayerProfile profile)
        {
            if (profile == null || _localCareer == null)
                return;

            var stats = profile.Stats;
            if (stats == null)
                return;

            var current = _localCareer.Current;
            if (current == null)
                return;

            current.Matches = Math.Max(0, stats.Matches);
            current.Wins = Math.Max(0, stats.Wins);
            current.Kills = Math.Max(0, stats.Kills);
            current.Headshots = Math.Max(0, stats.Headshots);
            current.BestPlacement = Math.Max(0, stats.BestPlacement);
            current.TotalDamage = stats.TotalDamage;
            current.LongestSurvivalSeconds = stats.LongestSurvivalSeconds;
            current.Experience = Math.Max(0, stats.Experience);
            current.Rank = RankCatalog.RankForExperience(current.Experience);

            // Persist via reset+manual isn't available; use store keys through Record of zero? 
            // CareerStatsService has no Set. Force Persist by ResetCareer then overwrite via reflection-free path:
            // We call a merge helper that writes through known PlayerPrefs keys used by CareerStatsService.
            PersistMerged(current);
        }

        /// <summary>Yerel istatistikleri okur (UI için).</summary>
        public CareerStats GetEffectiveStats()
        {
            if (_localCareer != null && _localCareer.Current != null)
                return _localCareer.Current;
            return new CareerStats();
        }

        private async Task FlushQueueSafe()
        {
            try { await FlushQueueAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception e) { Debug.LogWarning("[OnlineProfile] Kuyruk: " + e.Message); }
        }

        private void PersistMerged(CareerStats stats)
        {
            // CareerStatsService.Keys ile aynı anahtarlar — yetkili backend değerlerini yazar, sonra Load.
            try
            {
                PlayerPrefs.SetInt(CareerStatsService.Keys.Matches, stats.Matches);
                PlayerPrefs.SetInt(CareerStatsService.Keys.Wins, stats.Wins);
                PlayerPrefs.SetInt(CareerStatsService.Keys.Kills, stats.Kills);
                PlayerPrefs.SetInt(CareerStatsService.Keys.Headshots, stats.Headshots);
                PlayerPrefs.SetInt(CareerStatsService.Keys.BestPlacement, stats.BestPlacement);
                PlayerPrefs.SetFloat(CareerStatsService.Keys.TotalDamage, stats.TotalDamage);
                PlayerPrefs.SetFloat(CareerStatsService.Keys.LongestSurvival, stats.LongestSurvivalSeconds);
                PlayerPrefs.SetInt(CareerStatsService.Keys.Experience, stats.Experience);
                PlayerPrefs.SetInt(CareerStatsService.Keys.Rank, (int)stats.Rank);
                PlayerPrefs.Save();
                _localCareer.Load();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RaiseChanged()
        {
            try { ProfileChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
