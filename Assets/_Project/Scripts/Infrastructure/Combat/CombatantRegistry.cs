using System;
using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Sahnedeki tüm savaşanların (oyuncu + botlar + antrenman hedefleri) kaydı.
    /// Combatant.Initialize kaydeder, OnDestroy siler. Ölen savaşan yok edilene kadar listede KALIR
    /// (tim paneli, izleyici modu ve öldüren bilgisi için) — hedef seçerken <see cref="Combatant.IsAlive"/> ve
    /// <see cref="Combatant.IsTargetable"/> kontrol edin. <see cref="AliveCount"/> yalnızca canlıları sayar.
    /// Liste sırası kayıt sırasıdır; döngülerde indeks kullanın (foreach/LINQ yerine) — tahsis yapmaz.
    /// </summary>
    public static class CombatantRegistry
    {
        private static readonly List<Combatant> Items = new(64);
        private static readonly Dictionary<int, Combatant> ById = new(64);
        private static Combatant _localPlayer;

        /// <summary>Kayıt değiştiğinde (ekleme/çıkarma/temizleme) artar; önbellek geçersizleştirme için.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<Combatant> All => Items;

        public static int Count => Items.Count;

        /// <summary>Yerel oyuncunun savaşanı (yoksa null). IsLocalPlayer olan ilk kayıt otomatik atanır.</summary>
        public static Combatant LocalPlayer
        {
            get => _localPlayer != null ? _localPlayer : null;
            set => _localPlayer = value;
        }

        public static int AliveCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Items.Count; i++)
                {
                    var c = Items[i];
                    if (c != null && c.IsAlive)
                        count++;
                }

                return count;
            }
        }

        public static event Action<Combatant> Registered;
        public static event Action<Combatant> Unregistered;

        public static void Register(Combatant combatant)
        {
            if (combatant == null)
                return;

            if (!Items.Contains(combatant))
                Items.Add(combatant);

            if (combatant.Id.IsValid)
            {
                // Aynı kimlikle eski bir kayıt varsa (yeniden doğma / yeniden başlatma) yenisi geçerlidir.
                if (ById.TryGetValue(combatant.Id.Value, out var previous) && previous != null && previous != combatant)
                    Items.Remove(previous);

                ById[combatant.Id.Value] = combatant;
            }

            if (combatant.IsLocalPlayer)
                _localPlayer = combatant;

            Version++;
            Registered?.Invoke(combatant);
        }

        public static void Unregister(Combatant combatant)
        {
            if (ReferenceEquals(combatant, null))
                return;

            var removed = Items.Remove(combatant);

            // Kimliği değişmiş olabilir; sözlükte bu örneğe işaret eden girdiyi bul.
            var key = int.MinValue;
            foreach (var pair in ById)
            {
                if (ReferenceEquals(pair.Value, combatant))
                {
                    key = pair.Key;
                    break;
                }
            }

            if (key != int.MinValue)
            {
                ById.Remove(key);
                removed = true;
            }

            if (ReferenceEquals(_localPlayer, combatant))
                _localPlayer = null;

            if (!removed)
                return;

            Version++;
            Unregistered?.Invoke(combatant);
        }

        public static bool TryGet(PlayerId id, out Combatant combatant)
        {
            if (id.IsValid && ById.TryGetValue(id.Value, out combatant) && combatant != null)
                return true;

            combatant = null;
            return false;
        }

        /// <summary>Kimliğe göre savaşan (yoksa null).</summary>
        public static Combatant Get(PlayerId id) => TryGet(id, out var c) ? c : null;

        public static void Clear()
        {
            Items.Clear();
            ById.Clear();
            _localPlayer = null;
            Version++;
        }

        /// <summary>Timin tüm üyelerini (ölüler dahil) çıktı listesine ekler. Listeyi temizlemez.</summary>
        public static void GetTeam(int team, List<Combatant> output)
        {
            if (output == null)
                return;

            for (var i = 0; i < Items.Count; i++)
            {
                var c = Items[i];
                if (c != null && c.Team == team)
                    output.Add(c);
            }
        }

        /// <summary>Timin canlı üyelerini çıktı listesine ekler. Listeyi temizlemez.</summary>
        public static void GetAliveTeam(int team, List<Combatant> output)
        {
            if (output == null)
                return;

            for (var i = 0; i < Items.Count; i++)
            {
                var c = Items[i];
                if (c != null && c.Team == team && c.IsAlive)
                    output.Add(c);
            }
        }

        public static int CountAliveInTeam(int team)
        {
            var count = 0;
            for (var i = 0; i < Items.Count; i++)
            {
                var c = Items[i];
                if (c != null && c.Team == team && c.IsAlive)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Konuma en yakın canlı ve hedef alınabilir savaşan. <paramref name="excludeTeam"/> ≥ 0 ise o timi atlar
        /// (düşman arama); <paramref name="onlyTeam"/> ≥ 0 ise yalnızca o tim (dost arama).
        /// </summary>
        public static Combatant FindNearest(Vector3 position, float maxDistance, int excludeTeam = -1, int onlyTeam = -1,
            Combatant ignore = null)
        {
            Combatant best = null;
            var bestSqr = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
            for (var i = 0; i < Items.Count; i++)
            {
                var c = Items[i];
                if (c == null || c == ignore || !c.IsAlive || !c.IsTargetable)
                    continue;

                if (excludeTeam >= 0 && c.Team == excludeTeam)
                    continue;

                if (onlyTeam >= 0 && c.Team != onlyTeam)
                    continue;

                var sqr = (c.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }

            return best;
        }

        /// <summary>Yok edilmiş (Unity null) girdileri temizler. Normalde gerekmez; OnDestroy kaydı siler.</summary>
        public static void Prune()
        {
            var changed = false;
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i] == null)
                {
                    Items.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                _tmpKeys.Clear();
                foreach (var pair in ById)
                {
                    if (pair.Value == null)
                        _tmpKeys.Add(pair.Key);
                }

                for (var i = 0; i < _tmpKeys.Count; i++)
                    ById.Remove(_tmpKeys[i]);

                Version++;
            }
        }

        private static readonly List<int> _tmpKeys = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Items.Clear();
            ById.Clear();
            _localPlayer = null;
            Registered = null;
            Unregistered = null;
            Version = 0;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene) => Prune();
    }
}
