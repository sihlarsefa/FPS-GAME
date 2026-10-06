using System;
using System.Collections.Generic;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>Kozmetik tanımı (Design/Progression/cosmetics.json). Yalnızca görsel; oynanışa etkisi yoktur.</summary>
    [Serializable]
    public sealed class CosmeticDefinition
    {
        public string id;
        public string name;
        public string slot;
        public int unlockXp;
        public string unlockMethod;
        public string description;
        public string patternNotes;
        /// <summary>Nadirlik: common/uncommon/rare/epic/legendary (boşsa unlockMethod/XP'den türetilir; bkz. <see cref="CosmeticsService.RarityOf"/>).</summary>
        public string rarity;
        /// <summary>Sezon ödülü bağlantısı: "s{sezon}:{free|premium}:t{kademe}" (boş: sezonla ilgisiz).</summary>
        public string seasonRef;
        /// <summary>Yalnızca bu rolde kuşanılabilir (ör. "sniper"); boş: herkes.</summary>
        public string roleRequired;
    }

    /// <summary>
    /// Kozmetik sahiplik/donanım servisi (saf C#). Varsayılanlar ("default") baştan sahiptir; "career_xp" öğeleri kariyer
    /// tecrübe puanıyla (rütbe terfisiyle birlikte artar) açılır; "season_track"/"season_archive" öğeleri yalnızca
    /// <see cref="Grant"/> ile (sezon ödülü / başarım) açılır. Kalıcılık <see cref="ISettingsStore"/> üzerindedir.
    /// </summary>
    public sealed class CosmeticsService
    {
        public const string OwnedKeyPrefix = "cos.o.";
        public const string EquippedKeyPrefix = "cos.e.";
        public const string MethodDefault = "default";
        public const string MethodCareerXp = "career_xp";

        public const string SlotCamo = "camo";
        public const string SlotBeret = "beret";
        public const string SlotArmband = "armband";
        public const string SlotWeaponSkin = "weapon_skin";
        public const string SlotFacePaint = "face_paint";
        public const string SlotHelmetCover = "helmet_cover";
        public const string SlotGhillie = "ghillie";

        public const string MethodSeasonTrack = "season_track";
        public const string MethodSeasonArchive = "season_archive";

        public const string RarityCommon = "common";
        public const string RarityUncommon = "uncommon";
        public const string RarityRare = "rare";
        public const string RarityEpic = "epic";
        public const string RarityLegendary = "legendary";

        private readonly ISettingsStore _store;
        private readonly List<CosmeticDefinition> _items = new List<CosmeticDefinition>();
        private readonly Dictionary<string, CosmeticDefinition> _byId = new Dictionary<string, CosmeticDefinition>();
        private readonly HashSet<string> _owned = new HashSet<string>();
        private readonly Dictionary<string, string> _equipped = new Dictionary<string, string>();

        /// <summary>Sahiplik veya donanım değiştiğinde tetiklenir.</summary>
        public event Action Changed;

        public CosmeticsService(ISettingsStore store, IEnumerable<CosmeticDefinition> definitions)
        {
            _store = store;
            if (definitions != null)
            {
                foreach (var d in definitions)
                {
                    if (d == null || string.IsNullOrEmpty(d.id) || string.IsNullOrEmpty(d.slot) || _byId.ContainsKey(d.id))
                        continue;
                    _byId[d.id] = d;
                    _items.Add(d);
                }
            }

            Load();
        }

        public IReadOnlyList<CosmeticDefinition> Items => _items;

        public bool TryGet(string id, out CosmeticDefinition def)
        {
            def = null;
            return id != null && _byId.TryGetValue(id, out def);
        }

        public IEnumerable<CosmeticDefinition> InSlot(string slot)
        {
            for (var i = 0; i < _items.Count; i++)
                if (_items[i].slot == slot)
                    yield return _items[i];
        }

        public bool IsOwned(string id) => id != null && _owned.Contains(id);

        /// <summary>Kariyer tecrübesi bu öğeyi açmaya yeter mi (yalnızca "career_xp" yöntemi)?</summary>
        public static bool MeetsXp(CosmeticDefinition def, int careerXp)
        {
            return def != null && def.unlockMethod == MethodCareerXp && careerXp >= def.unlockXp;
        }

        /// <summary>Tecrübe puanına göre yeni açılanları sahiplik listesine ekler; yeni açılanların sayısını döndürür.</summary>
        public int SyncUnlocks(int careerXp)
        {
            var added = 0;
            for (var i = 0; i < _items.Count; i++)
            {
                var d = _items[i];
                if (_owned.Contains(d.id))
                    continue;
                if (d.unlockMethod == MethodDefault || MeetsXp(d, careerXp))
                {
                    AddOwned(d.id);
                    added++;
                }
            }

            if (added > 0)
            {
                _store?.Save();
                Changed?.Invoke();
            }

            return added;
        }

        /// <summary>Nadirlik: tanımda varsa o, yoksa açılış yöntemi/XP'den (UiKitTokens.RarityOf ile aynı eşikler).</summary>
        public static string RarityOf(CosmeticDefinition def)
        {
            if (def == null)
                return RarityCommon;
            if (!string.IsNullOrEmpty(def.rarity))
                return def.rarity;
            if (def.unlockMethod == MethodDefault)
                return RarityCommon;
            if (def.unlockMethod == MethodCareerXp)
                return def.unlockXp < 2000 ? RarityUncommon : def.unlockXp < 6000 ? RarityRare : RarityEpic;
            return RarityLegendary;
        }

        /// <summary>"s1:premium:t14" biçimini çözer; hatalıysa false.</summary>
        public static bool TryParseSeasonRef(string seasonRef, out int season, out string track, out int tier)
        {
            season = 0; track = null; tier = 0;
            if (string.IsNullOrEmpty(seasonRef))
                return false;
            var parts = seasonRef.Split(':');
            if (parts.Length != 3 || parts[0].Length < 2 || parts[0][0] != 's' || parts[2].Length < 2 || parts[2][0] != 't')
                return false;
            if (!int.TryParse(parts[0].Substring(1), out season) || !int.TryParse(parts[2].Substring(1), out tier))
                return false;
            track = parts[1];
            return (track == "free" || track == "premium") && season > 0 && tier > 0;
        }

        /// <summary>Bu rol için kuşanılabilir mi (roleRequired boşsa herkes)?</summary>
        public static bool AllowedForRole(CosmeticDefinition def, string role)
        {
            return def != null && (string.IsNullOrEmpty(def.roleRequired) || def.roleRequired == role);
        }

        /// <summary>Açılış kuralının Türkçe kısa metni (panel etiketi).</summary>
        public static string UnlockRuleText(CosmeticDefinition def)
        {
            if (def == null)
                return "";
            string rule;
            if (def.unlockMethod == MethodDefault) rule = "Başlangıçta açık";
            else if (def.unlockMethod == MethodCareerXp) rule = def.unlockXp + " kariyer XP";
            else if (TryParseSeasonRef(def.seasonRef, out var season, out var track, out var tier))
                rule = "Sezon " + season + (track == "premium" ? " premium" : " ücretsiz") + " kademe " + tier;
            else rule = "Sezon ödülü";
            if (!string.IsNullOrEmpty(def.roleRequired) && def.roleRequired == "sniper")
                rule += " (keskin nişancı)";
            return rule;
        }

        /// <summary>
        /// Sezon kademesi talep edildiğinde: o sezon+hat için kademe ≤ verilen olan ve seasonRef'i bulunan tüm öğeleri açar.
        /// Yeni açılan sayısını döndürür.
        /// </summary>
        public int GrantSeasonTier(int season, string track, int tier)
        {
            var added = 0;
            for (var i = 0; i < _items.Count; i++)
            {
                var d = _items[i];
                if (_owned.Contains(d.id) || !TryParseSeasonRef(d.seasonRef, out var s, out var t, out var k))
                    continue;
                if (s == season && t == track && k <= tier)
                {
                    AddOwned(d.id);
                    added++;
                }
            }

            if (added > 0)
            {
                _store?.Save();
                Changed?.Invoke();
            }

            return added;
        }

        /// <summary>Sezon ödülü/başarım gibi dış kaynaklı açılış. Zaten sahipse false döner.</summary>
        public bool Grant(string id)
        {
            if (!_byId.ContainsKey(id ?? "") || _owned.Contains(id))
                return false;
            AddOwned(id);
            _store?.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sahip olunan öğeyi kuşanır (yuvasında tek öğe).</summary>
        public bool Equip(string id)
        {
            if (!TryGet(id, out var def) || !_owned.Contains(id))
                return false;
            _equipped[def.slot] = id;
            _store?.SetInt(EquippedKeyPrefix + def.slot, Hash(id));
            _store?.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Yuvadaki kuşanılmış öğe kimliği; yoksa varsayılan, o da yoksa null.</summary>
        public string GetEquipped(string slot)
        {
            if (slot != null && _equipped.TryGetValue(slot, out var id) && _owned.Contains(id))
                return id;
            foreach (var d in InSlot(slot))
                if (d.unlockMethod == MethodDefault && _owned.Contains(d.id))
                    return d.id;
            return null;
        }

        public int OwnedCount => _owned.Count;

        private void AddOwned(string id)
        {
            _owned.Add(id);
            _store?.SetInt(OwnedKeyPrefix + id, 1);
        }

        private void Load()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var d = _items[i];
                if (d.unlockMethod == MethodDefault || (_store != null && _store.GetInt(OwnedKeyPrefix + d.id, 0) == 1))
                    _owned.Add(d.id);
            }

            if (_store == null)
                return;
            var slots = new HashSet<string>();
            for (var i = 0; i < _items.Count; i++)
                slots.Add(_items[i].slot);
            foreach (var slot in slots)
            {
                var h = _store.GetInt(EquippedKeyPrefix + slot, 0);
                if (h == 0)
                    continue;
                foreach (var d in InSlot(slot))
                {
                    if (Hash(d.id) == h && _owned.Contains(d.id))
                    {
                        _equipped[slot] = d.id;
                        break;
                    }
                }
            }
        }

        /// <summary>Kararlı (platformdan bağımsız) FNV-1a özeti; 0 "yok" demektir.</summary>
        public static int Hash(string s)
        {
            unchecked
            {
                var h = 2166136261u;
                for (var i = 0; i < s.Length; i++)
                    h = (h ^ s[i]) * 16777619u;
                var r = (int)h;
                return r == 0 ? 1 : r;
            }
        }
    }
}
