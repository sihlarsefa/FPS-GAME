using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yerde duran, alınabilir eşya. Görsel: kategoriye göre paylaşılan düşük poligonlu model (WorldItemVisuals) +
    /// soluk vurgu halkası; bakış ışınları için GameLayers.Loot katmanında ~0.8 m tetik kutusu. Zemine (GroundMask)
    /// oturur ve eğime hizalanır. PickupBy: InventoryService.TryPickup → yerine düşenler yanına bırakılır, kısmi
    /// alımda adet azalır, tamamı alınınca havuza döner; LootPickedUpEvent yayınlanır ve alma sesi çalınır.
    /// Etkinken ve alınabilirken LootRegistry'de kayıtlıdır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootPickupComponent : MonoBehaviour, ILootPickup
    {
        /// <summary>Sunucu doğrulaması: savaşan ile eşya arasındaki en büyük alma mesafesi (m).</summary>
        public const float MaxPickupDistance = 6f;

        private const int MaxPooled = 256;
        private const float GroundProbeUp = 0.75f;
        private const float GroundProbeDistance = 500f;
        private const float MaxGroundTilt = 32f;

        /// <summary>
        /// LOD ayıklama eşiği (ekran yüksekliğine oranı). ~0.4 m'lik eşya ≈ 55 m, ~1.1 m'lik silah ≈ 150 m ötede çizilmez
        /// (QualitySettings.lodBias ile ölçeklenir). Binlerce yerdeki eşyanın çizim maliyetini sınırlar.
        /// </summary>
        public const float CullScreenHeight = 0.005f;

        // Sahneye elle yerleştirilen eşyalar için (eski kurulum/antrenman). Spawn ile üretilenlerde kullanılmaz.
        [SerializeField] private string itemId = ItemIds.Bandage;
        [SerializeField] private ItemCategory category = ItemCategory.Medical;
        [SerializeField] private string displayName = "Sargı Bezi";
        [SerializeField] private int quantity = 1;
        [SerializeField] private bool snapToGroundOnAwake = true;

        private static readonly Stack<LootPickupComponent> Pool = new(64);
        private static readonly List<LootItemData> DroppedBuffer = new(8);
        private static Transform _root;
        private static int _nextSpawnId;
        private static int _syncedFrame = -1;

        private LootItemData _item;
        private bool _configured;
        private bool _available;
        private bool _pooled;
        private string _prompt = string.Empty;
        private string _name = string.Empty;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private LODGroup _lodGroup;
        private Transform _display;
        private Vector3 _displayPivot;
        private LootRarity _rarity;
        private float _phase;
        private BoxCollider _trigger;
        private GameObject _prototypeCopy;
        private WorldItemVisual _visual;
        private Vector3 _focusPoint;
        private float _ringRadius = 0.35f;
        private int _spawnId;
        private float _spawnTime;

        // LootRegistry tarafından yönetilir.
        internal int RegistryIndex = -1;
        internal long CellKey;
        internal Vector3 RegisteredPosition;
        internal int RegisteredSpawnId;

        // ------------------------------------------------------------------ Public API

        public LootItemData Item => _item;

        public bool IsAvailable => _available && _configured && _item.IsValid;

        public ItemCategory Category => _item.Category;

        /// <summary>Türkçe görünen ad (ör. "MPT-76", "7.62 Mermi").</summary>
        public string DisplayName => _name;

        /// <summary>Etkileşim metni, ör. "[F] MPT-76 al", "[F] 7.62 Mermi (30) al". Önbelleklidir (her kare güvenli).</summary>
        public string PromptText => IsAvailable ? _prompt : string.Empty;

        /// <summary>Bakış/hedef noktası (modelin ortası, dünya).</summary>
        public Vector3 FocusPoint => _focusPoint;

        /// <summary>Nadirlik (renk kodu; ışın/halka rengi).</summary>
        public LootRarity Rarity => _rarity;

        /// <summary>Dönen/süzülen model kökü (çarpıştırıcı ve kayıt köke bağlı kalır). Dedicated sunucuda null.</summary>
        internal Transform Display => _display;

        /// <summary>Dönüşün merkezi (modelin yerel xz merkezi).</summary>
        internal Vector3 DisplayPivot => _displayPivot;

        internal float AnimPhase => _phase;

        /// <summary>Dönen modelin ağı (alma animasyonu için); yoksa null.</summary>
        internal Mesh DisplayMesh => _filter != null ? _filter.sharedMesh : null;

        internal Material[] DisplayMaterials => _renderer != null ? _renderer.sharedMaterials : null;

        /// <summary>Vurgu halkasının yarıçapı (m).</summary>
        public float RingRadius => _ringRadius;

        /// <summary>
        /// Her Spawn/Configure'da artan benzersiz kimlik (&gt; 0). Havuzdan yeniden kullanılan nesne yeni kimlik alır:
        /// tutulan referansın hâlâ aynı eşyayı gösterdiğini doğrulamak ve ileride ağ eşlemesi (LootRegistry.TryGet) için.
        /// </summary>
        public int SpawnId => _spawnId;

        /// <summary>Eşyanın yere konduğu an (Time.time).</summary>
        public float SpawnTime => _spawnTime;

        /// <summary>Yeni eşya üretir (havuzdan yeniden kullanır), zemine oturtur ve kaydeder. Geçersiz eşyada null.</summary>
        public static LootPickupComponent Spawn(LootItemData item, Vector3 position, float yaw = 0f)
        {
            if (!item.IsValid)
                return null;

            item = Normalize(item);
            var pickup = TakeFromPool();
            if (pickup == null)
            {
                var go = new GameObject("Loot_" + item.ItemId);
                go.SetActive(false);
                go.layer = GameLayers.Loot;
                pickup = go.AddComponent<LootPickupComponent>();
                pickup.snapToGroundOnAwake = false;
            }
            else if (pickup.name.Length != 5 + item.ItemId.Length || !pickup.name.EndsWith(item.ItemId, StringComparison.Ordinal))
            {
                pickup.name = "Loot_" + item.ItemId;
            }

            var root = Root;
            if (root != null && pickup.transform.parent != root)
                pickup.transform.SetParent(root, false);

            pickup.ApplyItem(item);
            EnsurePhysicsSynced();
            pickup.PlaceOnGround(position, yaw);
            pickup._available = true;
            pickup.AssignSpawnId();
            if (!pickup.gameObject.activeSelf)
                pickup.gameObject.SetActive(true);   // OnEnable → kayıt
            else
                LootRegistry.Register(pickup);

            LootFocusDriver.Ensure();
            LootVisualFx.Ensure();
            return pickup;
        }

        /// <summary>Eski API: Spawn(item, position) ile aynıdır.</summary>
        public static LootPickupComponent Create(Vector3 position, LootItemData item) => Spawn(item, position);

        /// <summary>
        /// Savaşanın envanterine almayı dener (yalnızca otorite). Yerine düşen eşyalar (değiştirilen silah/zırh)
        /// yakına bırakılır; kısmi alımda yerdeki adet azalır; tamamı alınırsa eşya kaybolur.
        /// </summary>
        public PickupResult PickupBy(Combatant combatant)
        {
            if (combatant == null || !combatant.IsAlive || combatant.Inventory == null || !IsAvailable)
                return PickupResult.Rejected;

            var reach = MaxPickupDistance + _ringRadius;
            if ((combatant.transform.position - transform.position).sqrMagnitude > reach * reach)
                return PickupResult.Rejected;

            var result = PickupInto(combatant.Inventory, out var taken);
            if (!result.Accepted)
                return result;

            if (GameContext.TryGet<IEventBus>(out var bus))
            {
                try
                {
                    bus.Publish(new LootPickedUpEvent(combatant.Id, taken));
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }

            PlayPickupSound(combatant);
            return result;
        }

        /// <summary>
        /// ILootPickup (eski akış; LootInteractionService olay yayınlar). InventoryService ise tam kurallar uygulanır,
        /// değilse IInventory.TryAddItem ile bir adet alınır.
        /// </summary>
        public bool TryPickup(PlayerId playerId, IInventory inventory)
        {
            if (inventory == null || !IsAvailable)
                return false;

            if (inventory is InventoryService service)
                return PickupInto(service, out _).Accepted;

            if (!GameContext.HasAuthority)
                return false;

            if (!inventory.TryAddItem(_item.ItemId, _item.Category))
                return false;

            Take(1);
            return true;
        }

        /// <summary>Eşyayı yeniden yapılandırır (eski API). Geçersiz eşya → eşya kaldırılır.</summary>
        public void Configure(LootItemData item)
        {
            if (!item.IsValid)
            {
                Despawn();
                return;
            }

            var wasAvailable = IsAvailable;
            ApplyItem(Normalize(item));
            _available = true;
            _pooled = false;
            if (!wasAvailable || _spawnId == 0)
                AssignSpawnId();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            else if (isActiveAndEnabled)
                LootRegistry.Register(this);
        }

        /// <summary>Yerdeki adedi ayarlar (≤0 → eşya kaldırılır).</summary>
        public void SetQuantity(int amount)
        {
            if (amount <= 0)
            {
                Despawn();
                return;
            }

            if (amount == _item.Quantity)
                return;

            _item = _item.WithQuantity(amount);
            RebuildText();
        }

        /// <summary>Eşyayı yerden kaldırır (kayıttan çıkar, havuza döner ya da yok edilir).</summary>
        public void Despawn()
        {
            _available = false;
            LootRegistry.Unregister(this);
            if (this == null || _pooled)
                return;

            if (Pool.Count < MaxPooled && UnityEngine.Application.isPlaying)
            {
                gameObject.SetActive(false);
                _pooled = true;
                Pool.Push(this);
            }
            else
            {
                WorldItemVisuals.SafeDestroy(gameObject);
            }
        }

        /// <summary>Tüm yerdeki eşyaları kaldırır (antrenman sıfırlama vb.).</summary>
        public static void DespawnAll()
        {
            var all = LootRegistry.All;
            for (var i = all.Count - 1; i >= 0; i--)
            {
                var pickup = all[i];
                if (pickup != null)
                    pickup.Despawn();
                else if (i < all.Count)
                    LootRegistry.Unregister(pickup);
            }
        }

        /// <summary>Havuzu boşaltır (havuzdaki nesneler yok edilir).</summary>
        public static void ClearPool()
        {
            while (Pool.Count > 0)
            {
                var pooled = Pool.Pop();
                if (pooled != null)
                    WorldItemVisuals.SafeDestroy(pooled.gameObject);
            }
        }

        // ------------------------------------------------------------------ Unity

        private void Awake()
        {
            gameObject.layer = GameLayers.Loot;
            EnsureComponents();

            if (!_configured)
            {
                // Sahneye elle konmuş eşya: serileştirilmiş alanlardan kur.
                var data = ItemCatalog.Contains(itemId)
                    ? ItemCatalog.CreateLoot(itemId, Mathf.Max(1, quantity))
                    : new LootItemData(itemId, category, displayName, Mathf.Max(1, quantity));
                if (data.IsValid)
                {
                    ApplyItem(Normalize(data));
                    _available = true;
                    AssignSpawnId();
                    if (snapToGroundOnAwake)
                    {
                        EnsurePhysicsSynced();
                        PlaceOnGround(transform.position, transform.eulerAngles.y);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (IsAvailable)
                LootRegistry.Register(this);
        }

        private void OnDisable()
        {
            LootRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            LootRegistry.Unregister(this);
        }

        // ------------------------------------------------------------------ Core

        /// <summary>Envantere alma çekirdeği (olay/ses yok). taken: alınan kısım.</summary>
        private PickupResult PickupInto(InventoryService inventory, out LootItemData taken)
        {
            taken = default;
            if (inventory == null || !IsAvailable || !GameContext.HasAuthority)
                return PickupResult.Rejected;

            var before = _item;
            DroppedBuffer.Clear();
            PickupResult result;
            try
            {
                result = inventory.TryPickup(before, DroppedBuffer);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                DroppedBuffer.Clear();
                return PickupResult.Rejected;
            }

            if (!result.Accepted)
            {
                // Reddedilen alımda envanter değişmemeli; yine de düşen bir şey varsa kaybolmasın.
                SpawnDroppedBuffer(transform.position);
                return result;
            }

            var amount = result.FullyTaken ? before.Quantity : Mathf.Clamp(result.QuantityTaken, 0, before.Quantity);
            taken = before.WithQuantity(Mathf.Max(1, amount));

            // Önce yerine düşenleri bırak, sonra bu eşyayı kaldır: aksi hâlde havuz bu nesneyi hemen düşen eşya için
            // yeniden kullanır ve çağıranın elindeki referans başka bir eşyayı göstermeye başlar.
            SpawnDroppedBuffer(transform.position);

            var wholly = result.FullyTaken || amount >= before.Quantity;
            try
            {
                LootVisualFx.PlayPickup(this, wholly);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            if (wholly)
                Despawn();
            else if (amount > 0)
                Take(amount);

            return result;
        }

        private static void SpawnDroppedBuffer(Vector3 near)
        {
            var count = DroppedBuffer.Count;
            if (count == 0)
                return;

            // Tampon, SpawnDropped sırasında değişmez; yine de kopyalamadan sırayla işle ve sonra temizle.
            for (var i = 0; i < count && i < DroppedBuffer.Count; i++)
            {
                var item = DroppedBuffer[i];
                if (item.IsValid)
                    LootSpawner.SpawnDropped(item, near);
            }

            DroppedBuffer.Clear();
        }

        private void Take(int amount)
        {
            var remaining = _item.Quantity - amount;
            if (remaining <= 0)
                Despawn();
            else
                SetQuantity(remaining);
        }

        private void PlayPickupSound(Combatant combatant)
        {
            if (WorldItemVisuals.IsHeadless)
                return;

            var pitch = 1f + 0.06f * (int)_rarity;   // nadir eşya biraz daha tiz
            try
            {
                if (combatant.IsLocalPlayer)
                    GameAudio.Play2D(SoundId.Pickup, 0.8f, pitch);
                else
                    GameAudio.Play(SoundId.Pickup, transform.position, 0.6f, pitch, 18f);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void ApplyItem(LootItemData item)
        {
            EnsureComponents();
            _item = item;
            _configured = true;

            itemId = item.ItemId;
            category = item.Category;
            displayName = item.DisplayName;
            quantity = item.Quantity;

            var visual = WorldItemVisuals.Get(item);
            if (visual != null && !ReferenceEquals(visual, _visual))
            {
                _visual = visual;
                if (_filter != null)
                    _filter.sharedMesh = visual.Mesh;
                if (_renderer != null)
                {
                    _renderer.sharedMaterials = visual.Materials;
                    _renderer.enabled = visual.Mesh != null;
                }

                if (_prototypeCopy != null)
                {
                    WorldItemVisuals.SafeDestroy(_prototypeCopy);
                    _prototypeCopy = null;
                }

                if (visual.Prototype != null)
                {
                    _prototypeCopy = Instantiate(visual.Prototype, _display != null ? _display : transform, false);
                    _prototypeCopy.name = visual.Prototype.name;
                    GameLayers.SetLayerRecursively(_prototypeCopy, GameLayers.Loot);
                    DisableColliders(_prototypeCopy);
                    _prototypeCopy.SetActive(true);
                }

                RefreshLod();

                var bounds = visual.Bounds;
                var size = new Vector3(
                    Mathf.Max(bounds.size.x, WorldItemVisuals.MinColliderSize),
                    Mathf.Max(bounds.size.y, WorldItemVisuals.MinColliderHeight),
                    Mathf.Max(bounds.size.z, WorldItemVisuals.MinColliderSize));
                _trigger.size = size;
                _trigger.center = new Vector3(bounds.center.x, size.y * 0.5f, bounds.center.z);
                _ringRadius = visual.RingRadius;
                _displayPivot = new Vector3(bounds.center.x, 0f, bounds.center.z);
            }

            _rarity = LootRarityRules.Of(item.ItemId, item.Category);
            _phase = (_nextSpawnId * 0.7371f) % 6.2832f;
            ResetDisplay();

            RebuildText();
            UpdateFocusPoint();
        }

        /// <summary>Zemine (GroundMask) oturtur; eğim ≤ 32° ise yüzeye hizalar.</summary>
        private void PlaceOnGround(Vector3 position, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var origin = position + Vector3.up * GroundProbeUp;
            if (Physics.Raycast(origin, Vector3.down, out var hit, GroundProbeDistance, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                var normal = hit.normal;
                if (Vector3.Angle(normal, Vector3.up) <= MaxGroundTilt)
                    rotation = Quaternion.FromToRotation(Vector3.up, normal) * rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
            UpdateFocusPoint();
            if (RegistryIndex >= 0)
                LootRegistry.Register(this);   // ızgara hücresini güncelle
        }

        /// <summary>Dönen/süzülen modeli dinlenme konumuna alır.</summary>
        internal void ResetDisplay()
        {
            if (_display != null)
                _display.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void UpdateFocusPoint()
        {
            _focusPoint = _trigger != null ? transform.TransformPoint(_trigger.center) : transform.position + Vector3.up * 0.2f;
        }

        private void RebuildText()
        {
            var item = _item;
            var name = !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : ItemCatalog.GetDisplayName(item.ItemId);
            _name = name ?? string.Empty;

            var showCount = item.Quantity > 1 || item.Category == ItemCategory.Ammunition;
            string condition = null;
            if ((item.Category == ItemCategory.Armor || item.Category == ItemCategory.Helmet) && item.Durability >= 0f &&
                ItemCatalog.TryGet(item.ItemId, out var definition) && definition.Durability > 0f)
            {
                var percent = Mathf.Clamp(Mathf.RoundToInt(item.Durability / definition.Durability * 100f), 0, 100);
                if (percent < 100)
                    condition = "%" + percent;
            }

            if (showCount)
                _prompt = condition != null ? $"[F] {_name} ({item.Quantity}) {condition} al" : $"[F] {_name} ({item.Quantity}) al";
            else
                _prompt = condition != null ? $"[F] {_name} ({condition}) al" : $"[F] {_name} al";
        }

        private void EnsureComponents()
        {
            // Dedicated sunucuda (grafik aygıtı yok) çizim bileşeni eklenmez; tetik ve kayıt yeterlidir.
            var headless = WorldItemVisuals.IsHeadless;
            if (_display == null && !headless)
            {
                var child = transform.Find("Display");
                if (child == null)
                {
                    var go = new GameObject("Display") { layer = GameLayers.Loot };
                    child = go.transform;
                    child.SetParent(transform, false);
                }

                _display = child;

                // Eski kurulumda köke konmuş çizici varsa kapat (model artık Display altında çizilir).
                if (TryGetComponent<MeshRenderer>(out var legacy))
                    legacy.enabled = false;
            }

            if (_filter == null && _display != null && !_display.TryGetComponent(out _filter))
                _filter = _display.gameObject.AddComponent<MeshFilter>();

            if (_renderer == null && _display != null && !_display.TryGetComponent(out _renderer))
            {
                _renderer = _display.gameObject.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = ShadowCastingMode.On;
                _renderer.receiveShadows = true;
                _renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            if (_lodGroup == null && !TryGetComponent(out _lodGroup) && _renderer != null)
            {
                _lodGroup = gameObject.AddComponent<LODGroup>();
                _lodGroup.fadeMode = LODFadeMode.None;
            }

            if (_trigger == null)
            {
                if (!TryGetComponent(out _trigger))
                    _trigger = gameObject.AddComponent<BoxCollider>();

                // Eski kurulumdaki ilkel çarpıştırıcılar (silindir vb.) tetik değildi; bakış ışını için tetik olmalı.
                _trigger.isTrigger = true;
                _trigger.size = new Vector3(WorldItemVisuals.MinColliderSize, WorldItemVisuals.MinColliderHeight, WorldItemVisuals.MinColliderSize);
                _trigger.center = new Vector3(0f, WorldItemVisuals.MinColliderHeight * 0.5f, 0f);

                var others = GetComponents<Collider>();
                for (var i = 0; i < others.Length; i++)
                {
                    if (others[i] != _trigger)
                        others[i].enabled = false;
                }
            }
        }

        /// <summary>Uzak eşyaları çizmemek için tek seviyeli LOD (modelin + prototip kopyasının tüm çiziciler).</summary>
        private void RefreshLod()
        {
            if (_lodGroup == null || _renderer == null)
                return;

            Renderer[] renderers;
            if (_prototypeCopy != null)
            {
                var children = _prototypeCopy.GetComponentsInChildren<Renderer>(true);
                renderers = new Renderer[children.Length + 1];
                renderers[0] = _renderer;
                Array.Copy(children, 0, renderers, 1, children.Length);
            }
            else
            {
                renderers = new Renderer[] { _renderer };
            }

            _lodGroup.SetLODs(new[] { new LOD(CullScreenHeight, renderers) });
            _lodGroup.RecalculateBounds();
        }

        private static void DisableColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        private void AssignSpawnId()
        {
            unchecked
            {
                _nextSpawnId++;
                if (_nextSpawnId <= 0)
                    _nextSpawnId = 1;
            }

            _spawnId = _nextSpawnId;
            _spawnTime = UnityEngine.Application.isPlaying ? Time.time : 0f;
        }

        /// <summary>
        /// Aynı karede oluşturulan/taşınan çarpıştırıcıların zemin ışınlarında görünmesi için karede bir kez fizik
        /// dönüşümlerini eşitler (Physics.autoSyncTransforms kapalıyken gerekli).
        /// </summary>
        private static void EnsurePhysicsSynced()
        {
            var frame = Time.frameCount;
            if (frame == _syncedFrame)
                return;

            _syncedFrame = frame;
            Physics.SyncTransforms();
        }

        /// <summary>Eksik adı katalogdan doldurur; adet en az 1.</summary>
        private static LootItemData Normalize(LootItemData item)
        {
            var name = item.DisplayName;
            if (string.IsNullOrEmpty(name))
                name = ItemCatalog.GetDisplayName(item.ItemId);

            var cat = item.Category;
            if (cat == ItemCategory.None && ItemCatalog.TryGet(item.ItemId, out var definition))
                cat = definition.Category;

            if (ReferenceEquals(name, item.DisplayName) && cat == item.Category && item.Quantity >= 1)
                return item;

            return new LootItemData(item.ItemId, cat, name, Mathf.Max(1, item.Quantity), item.LoadedAmmo, item.Durability);
        }

        private static LootPickupComponent TakeFromPool()
        {
            while (Pool.Count > 0)
            {
                var pooled = Pool.Pop();
                if (pooled == null)
                    continue;

                pooled._pooled = false;
                if (!pooled.gameObject.activeSelf)
                    return pooled;
            }

            return null;
        }

        private static Transform Root
        {
            get
            {
                if (_root != null)
                    return _root;

                if (!UnityEngine.Application.isPlaying)
                    return null;

                _root = new GameObject("[Yerdeki Eşyalar]").transform;
                return _root;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pool.Clear();
            DroppedBuffer.Clear();
            _root = null;
            _nextSpawnId = 0;
            _syncedFrame = -1;
        }
    }
}
