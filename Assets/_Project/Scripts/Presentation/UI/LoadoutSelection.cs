using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Presentation.Bootstrap;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Oyuncunun menüde seçtiği görev rolü, birincil/ikincil silahı ve birincil silah eklentileri (kalıcı: ISettingsStore).
    /// Kimlikler FNV-1a özetiyle saklanır (depo yalnızca sayı tutar). Saf mantık; depo yoksa bellekte tutar.
    /// Maç başında MatchBootstrap.SpawnLocalPlayer <see cref="BuildMatchLoadout"/> ile bu seçimi tüketir.
    /// </summary>
    public sealed class LoadoutSelection
    {
        public const string KeyRole = "loadout.role";
        public const string KeyPrimary = "loadout.primary";
        public const string KeySecondary = "loadout.secondary";
        public const string KeyAttachmentPrefix = "loadout.att.";

        private static LoadoutSelection _shared;

        private readonly ISettingsStore _store;
        private readonly Dictionary<string, int> _memory = new Dictionary<string, int>();

        public LoadoutSelection(ISettingsStore store)
        {
            _store = store;
        }

        /// <summary>Oyun oturumunun deposuna bağlı ortak örnek.</summary>
        public static LoadoutSelection Shared
        {
            get
            {
                if (_shared == null)
                {
                    ISettingsStore store = null;
                    try
                    {
                        GameSession.EnsureInitialized();
                        store = GameSession.Store;
                    }
                    catch (Exception)
                    {
                        // Depo yoksa bellekte çalışır.
                    }

                    _shared = new LoadoutSelection(store);
                }

                return _shared;
            }
        }

        /// <summary>FNV-1a 32 bit (kimlik → saklanabilir sayı; 0 = seçim yok).</summary>
        public static int Hash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            unchecked
            {
                var h = (int)2166136261;
                for (var i = 0; i < text.Length; i++)
                    h = (h ^ text[i]) * 16777619;
                return h == 0 ? 1 : h;
            }
        }

        private int Get(string key, int fallback)
        {
            if (_store != null)
                return _store.GetInt(key, fallback);
            return _memory.TryGetValue(key, out var v) ? v : fallback;
        }

        private void Set(string key, int value)
        {
            if (_store != null)
            {
                _store.SetInt(key, value);
                _store.Save();
            }
            else
            {
                _memory[key] = value;
            }
        }

        /// <summary>Seçili görev rolü (varsayılan Piyade).</summary>
        public TeamRole Role
        {
            get
            {
                var v = Get(KeyRole, (int)TeamRole.Rifleman);
                return Enum.IsDefined(typeof(TeamRole), v) ? (TeamRole)v : TeamRole.Rifleman;
            }
            set => Set(KeyRole, (int)value);
        }

        /// <summary>Menüde (DONANIM) en az bir seçim kaydedildi mi? Kaydedilmediyse maç rol varsayılan teçhizatıyla başlar.</summary>
        public bool HasSavedSelection =>
            Get(KeyRole, -1) != -1 || Get(KeyPrimary, 0) != 0 || Get(KeySecondary, 0) != 0;

        /// <summary>Kayıtlı seçimden maç teçhizatı: rol kiti + seçilen birincil/ikincil + eklentiler (kayıt yoksa null).</summary>
        public Loadout BuildMatchLoadout()
        {
            if (!HasSavedSelection)
                return null;
            var kit = LoadoutCatalog.For(Role);
            kit.PrimaryWeaponId = PrimaryId;
            kit.SidearmId = SecondaryId;
            foreach (var att in AttachmentIds())
                kit.Items.Add(new KeyValuePair<string, int>(att, 1));
            return kit;
        }

        /// <summary>Birincil silah kimliği (seçim yoksa rolün varsayılanı).</summary>
        public string PrimaryId
        {
            get
            {
                var id = Resolve(Get(KeyPrimary, 0), PrimaryCandidates());
                return id ?? LoadoutCatalog.For(Role).PrimaryWeaponId ?? WeaponIds.Mpt76;
            }
            set => Set(KeyPrimary, Hash(value));
        }

        /// <summary>İkincil silah kimliği (seçim yoksa rolün tabancası).</summary>
        public string SecondaryId
        {
            get
            {
                var id = Resolve(Get(KeySecondary, 0), SecondaryCandidates());
                return id ?? LoadoutCatalog.For(Role).SidearmId ?? WeaponIds.Sar9;
            }
            set => Set(KeySecondary, Hash(value));
        }

        /// <summary>Birincil silaha takılı eklenti (yuva başına; yoksa null). Uyumsuz kayıt yok sayılır.</summary>
        public string GetAttachment(AttachmentSlot slot)
        {
            var hash = Get(KeyAttachmentPrefix + (int)slot, 0);
            if (hash == 0)
                return null;
            WeaponDefinitionData weapon;
            if (!WeaponCatalog.TryGet(PrimaryId, out weapon))
                return null;
            var options = CompatibleAttachments(slot, weapon.Category);
            return Resolve(hash, options);
        }

        public void SetAttachment(AttachmentSlot slot, string itemId) => Set(KeyAttachmentPrefix + (int)slot, Hash(itemId));

        /// <summary>Birincil silaha takılı tüm eklentiler.</summary>
        public List<string> AttachmentIds()
        {
            var list = new List<string>(AttachmentCatalog.SlotCount);
            for (var s = 0; s < AttachmentCatalog.SlotCount; s++)
            {
                var id = GetAttachment((AttachmentSlot)s);
                if (!string.IsNullOrEmpty(id))
                    list.Add(id);
            }

            return list;
        }

        private static string Resolve(int hash, IReadOnlyList<string> candidates)
        {
            if (hash == 0 || candidates == null)
                return null;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (Hash(candidates[i]) == hash)
                    return candidates[i];
            }

            return null;
        }

        /// <summary>Birincil silah adayları: tabanca ve yakın dövüş dışındakiler (katalog sırası).</summary>
        public static List<string> PrimaryCandidates()
        {
            var list = new List<string>();
            var all = WeaponCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i].Category;
                if (c != WeaponCategory.Pistol && c != WeaponCategory.Melee && c != WeaponCategory.None)
                    list.Add(all[i].WeaponId);
            }

            return list;
        }

        /// <summary>İkincil silah adayları: tabancalar, hafif makineliler ve pompalılar.</summary>
        public static List<string> SecondaryCandidates()
        {
            var list = new List<string>();
            var all = WeaponCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i].Category;
                if (c == WeaponCategory.Pistol || c == WeaponCategory.Smg || c == WeaponCategory.Shotgun)
                    list.Add(all[i].WeaponId);
            }

            return list;
        }

        /// <summary>Yuvaya uyan eklentiler; ilk öğe her zaman null ("YOK").</summary>
        public static List<string> CompatibleAttachments(AttachmentSlot slot, WeaponCategory category)
        {
            var list = new List<string> { null };
            var all = AttachmentCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].Slot == slot && all[i].IsCompatibleWith(category))
                    list.Add(all[i].ItemId);
            }

            return list;
        }

        /// <summary>Listede bir sonraki/önceki öğe (döngüsel). Geçerli yoksa baştan başlar.</summary>
        public static string Cycle(IReadOnlyList<string> list, string current, int direction)
        {
            if (list == null || list.Count == 0)
                return null;
            var index = -1;
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], current, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return list[direction >= 0 ? 0 : list.Count - 1];
            return list[MainMenuMotion.Wrap(index + (direction >= 0 ? 1 : -1), list.Count)];
        }
    }
}
