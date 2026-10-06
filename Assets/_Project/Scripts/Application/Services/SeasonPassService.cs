using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Sezon kartı ödül satırı (Resources/Progression/season1_pass.json).</summary>
    [Serializable]
    public sealed class SeasonRewardEntry
    {
        public int tier;
        public string cosmeticId;
    }

    /// <summary>Sezon kartı tanımı: 1–maxTier kademe, kademe başı tecrübe, ücretsiz + premium (yakında) ödül hatları.</summary>
    [Serializable]
    public sealed class SeasonPassDefinition
    {
        public int season = 1;
        public string name;
        public int maxTier = 50;
        public int xpPerTier = 600;
        public List<SeasonRewardEntry> free = new List<SeasonRewardEntry>();
        public List<SeasonRewardEntry> premium = new List<SeasonRewardEntry>();
    }

    /// <summary>
    /// Sezon kartı (saf C#, yalnızca kozmetik). Maç sonucundan sezon tecrübesi kazanılır; kademe = tecrübe / kademe başı tecrübe.
    /// Ücretsiz hat ödülleri talep edilince <see cref="CosmeticsService.Grant"/> ile verilir. Premium hat yalnızca yer tutucudur
    /// ("yakında"): gerçek para akışı yoktur ve talep edilemez. Kalıcılık <see cref="ISettingsStore"/> üzerindedir.
    /// </summary>
    public sealed class SeasonPassService
    {
        public const string XpKey = "season.xp";
        public const string ClaimedKeyPrefix = "season.c.";
        public const int MaxXpPerMatch = 1200;

        private readonly ISettingsStore _store;
        private readonly CosmeticsService _cosmetics;
        private readonly SeasonPassDefinition _def;
        private readonly HashSet<int> _claimed = new HashSet<int>();
        private int _xp;

        public event Action Changed;

        public SeasonPassService(ISettingsStore store, CosmeticsService cosmetics, SeasonPassDefinition definition)
        {
            _store = store;
            _cosmetics = cosmetics;
            _def = definition ?? new SeasonPassDefinition();
            if (_def.maxTier < 1) _def.maxTier = 1;
            if (_def.xpPerTier < 1) _def.xpPerTier = 1;
            _def.free = _def.free ?? new List<SeasonRewardEntry>();
            _def.premium = _def.premium ?? new List<SeasonRewardEntry>();
            Load();
        }

        public SeasonPassDefinition Definition => _def;
        public int MaxTier => _def.maxTier;
        public int Xp => _xp;
        public int MaxXp => _def.maxTier * _def.xpPerTier;

        /// <summary>Premium hat henüz satışta değil.</summary>
        public static bool PremiumAvailable => false;

        /// <summary>Maç sonucundan kazanılan sezon tecrübesi: katılım + öldürme + isabet + sağ kalma + sıralama primi.</summary>
        public static int XpForMatch(MatchResult r)
        {
            var xp = 100;
            xp += Math.Max(0, r.Kills) * 25;
            xp += Math.Max(0, r.Headshots) * 5;
            xp += (int)(Math.Max(0f, r.SurvivalSeconds) / 60f) * 5;
            if (r.IsWinner || r.Placement == 1) xp += 300;
            else if (r.Placement > 0 && r.Placement <= 3) xp += 150;
            else if (r.Placement > 0 && r.Placement <= 10) xp += 50;
            return Math.Min(xp, MaxXpPerMatch);
        }

        /// <summary>Verilen toplam tecrübenin kademesi (0 = henüz kademe yok).</summary>
        public int TierForXp(int xp) => Math.Min(_def.maxTier, Math.Max(0, xp) / _def.xpPerTier);

        public int CurrentTier => TierForXp(_xp);

        /// <summary>Mevcut kademe içindeki ilerleme 0–1 (azami kademede 1).</summary>
        public float TierProgress => CurrentTier >= _def.maxTier ? 1f : (_xp % _def.xpPerTier) / (float)_def.xpPerTier;

        public int XpIntoTier => CurrentTier >= _def.maxTier ? _def.xpPerTier : _xp % _def.xpPerTier;

        public int AddXp(int amount)
        {
            if (amount <= 0) return 0;
            var before = _xp;
            _xp = Math.Min(MaxXp, _xp + amount);
            var gained = _xp - before;
            if (gained > 0)
            {
                _store?.SetInt(XpKey, _xp);
                _store?.Save();
                Changed?.Invoke();
            }
            return gained;
        }

        public int AddMatch(MatchResult r) => AddXp(XpForMatch(r));

        public SeasonRewardEntry FreeAt(int tier)
        {
            for (var i = 0; i < _def.free.Count; i++)
                if (_def.free[i] != null && _def.free[i].tier == tier) return _def.free[i];
            return null;
        }

        public SeasonRewardEntry PremiumAt(int tier)
        {
            for (var i = 0; i < _def.premium.Count; i++)
                if (_def.premium[i] != null && _def.premium[i].tier == tier) return _def.premium[i];
            return null;
        }

        public bool IsClaimed(int tier) => _claimed.Contains(tier);

        public bool CanClaim(int tier)
        {
            var e = FreeAt(tier);
            return e != null && tier >= 1 && tier <= CurrentTier && !_claimed.Contains(tier);
        }

        public int ClaimableCount()
        {
            var n = 0;
            for (var i = 0; i < _def.free.Count; i++)
                if (_def.free[i] != null && CanClaim(_def.free[i].tier)) n++;
            return n;
        }

        /// <summary>Ücretsiz hat ödülünü talep eder ve kozmetiği envantere verir.</summary>
        public bool Claim(int tier)
        {
            if (!CanClaim(tier)) return false;
            var e = FreeAt(tier);
            _claimed.Add(tier);
            _store?.SetInt(ClaimedKeyPrefix + tier, 1);
            _cosmetics?.Grant(e.cosmeticId); // zaten sahipse false; talep yine de sayılır
            _store?.Save();
            Changed?.Invoke();
            return true;
        }

        public int ClaimAll()
        {
            var n = 0;
            for (var i = 0; i < _def.free.Count; i++)
                if (_def.free[i] != null && Claim(_def.free[i].tier)) n++;
            return n;
        }

        /// <summary>Premium hattı talep edilemez (yakında).</summary>
        public bool ClaimPremium(int tier) => false;

        private void Load()
        {
            if (_store == null) return;
            _xp = Math.Max(0, Math.Min(MaxXp, _store.GetInt(XpKey, 0)));
            for (var i = 0; i < _def.free.Count; i++)
            {
                var e = _def.free[i];
                if (e != null && _store.GetInt(ClaimedKeyPrefix + e.tier, 0) == 1)
                    _claimed.Add(e.tier);
            }
        }
    }
}
