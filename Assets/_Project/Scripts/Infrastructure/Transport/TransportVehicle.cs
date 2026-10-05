using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// İntikal aracı tabanı (T-70 helikopteri / Kirpi). Begin() ile başlangıçtan iniş noktasına gider, varınca Arrived,
    /// yolcular inince (ReleasePassengers) bir süre sonra ayrılır ve Departed. Yolcular koltuk Transform'larına bağlanır.
    /// <para>
    /// Koltuk sözleşmesi: <see cref="GetSeat"/> yolcunun KÖK noktasıdır (araç zemini, kalçanın tam altı); ileri yönü
    /// yolcunun baktığı yöndür. Oturan model (SoldierModel.SetSeated) kalçayı kökten ~0.45 m yukarıda tutmalıdır.
    /// Yolcu nesnesi koltuğa parent edilmelidir (SetParent(seat, false)). <see cref="PassengerViewPoint"/> oturan askerin
    /// göz noktasıdır (helikopterde kapıdan, Kirpi'de pencereden dışarı bakar); FPP kamera buna bağlanabilir.
    /// </para>
    /// <para>
    /// Boşaltma: Arrived sonrası yolcular <see cref="GetDisembarkPoint"/> noktasına iner. ReleasePassengers çağrılmazsa
    /// araç, varışta koltuklarda yolcu varken hepsi koltuktan ayrıldığında ya da <see cref="AutoReleaseSeconds"/>
    /// dolduğunda kendisi bırakır. Ayrılırken koltukta kalan (parent'lı) yolcular indirme noktasına bırakılır
    /// (<see cref="PassengerEjected"/>) — araç yok edilirken hiçbir yolcu onunla birlikte yok olmaz.
    /// </para>
    /// </summary>
    public abstract class TransportVehicle : MonoBehaviour
    {
        public const int DefaultSeatCount = 10;

        private static readonly List<TransportVehicle> ActiveVehicles = new List<TransportVehicle>(16);

        private float _arrivalTime;
        private int _occupiedAtArrival;
        private float _emptySince = -1f;
        private float _occupancyTimer;
        private bool _releaseRequested;
        private float _releaseTime;
        private bool _destroyScheduled;
        private float _destroyAt;
        private bool _passengersEjected;

        protected Transform[] Seats = Array.Empty<Transform>();
        protected Transform[] ViewPoints = Array.Empty<Transform>();
        protected Vector3[] DisembarkLocal = Array.Empty<Vector3>();
        protected float[] DisembarkYawLocal = Array.Empty<float>();
        protected AudioSource LoopSource;

        public InsertionMethod Method { get; protected set; }
        public int Team { get; protected set; }
        public int SeatCount { get; protected set; } = 10;
        public bool HasArrived { get; protected set; }
        public bool IsUnloading { get; protected set; }
        public bool IsDeparting { get; protected set; }
        public Vector3 LandingZone { get; protected set; }

        public event Action Arrived;
        public event Action Departed;

        /// <summary>Koltukta kalmış bir yolcu araç ayrılırken indirme noktasına bırakıldı (yolcu, koltuk indeksi).</summary>
        public event Action<Transform, int> PassengerEjected;

        /// <summary>Etkin tüm intikal araçları (mini harita / HUD için). Tahsis yapmadan okunabilir.</summary>
        public static IReadOnlyList<TransportVehicle> All => ActiveVehicles;

        /// <summary>Türkçe görünen ad ("T-70 Helikopteri", "Kirpi Zırhlı Aracı").</summary>
        public virtual string DisplayName => "İntikal Aracı";

        /// <summary>Aracın şimdiki durumu (HUD metni için <see cref="StatusText"/>).</summary>
        public TransportPhase Phase { get; protected set; } = TransportPhase.Waiting;

        /// <summary>Begin() çağrıldı mı?</summary>
        public bool HasBegun { get; protected set; }

        /// <summary>Plandaki (InsertionPlanner) iniş noktası; <see cref="LandingZone"/> buna yakın seçilen gerçek iniş yeridir.</summary>
        public Vector3 PlannedLandingZone { get; protected set; }

        /// <summary>Başlangıç noktası (dünya).</summary>
        public Vector3 StartPosition { get; protected set; }

        /// <summary>Anlık dünya hızı (m/s).</summary>
        public Vector3 Velocity { get; protected set; }

        /// <summary>Yolcular inebilir mi (varıldı, henüz ayrılmıyor)?</summary>
        public bool CanDisembark => HasArrived && !IsDeparting;

        /// <summary>Varıştan sonra kimse ReleasePassengers çağırmazsa otomatik bırakma süresi (sn). 0 = kapalı.</summary>
        public float AutoReleaseSeconds { get; set; } = 60f;

        /// <summary>Ayrıldıktan sonra yok edilme süresi (sn).</summary>
        public float DestroyDelaySeconds { get; set; } = 40f;

        /// <summary>LZ'ye yatay mesafe (m).</summary>
        public float DistanceToLandingZone
        {
            get
            {
                var p = transform.position;
                var dx = LandingZone.x - p.x;
                var dz = LandingZone.z - p.z;
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
        }

        /// <summary>Tahmini varış süresi (sn); varıldıysa 0.</summary>
        public virtual float EstimatedSecondsToArrival => HasArrived ? 0f : DistanceToLandingZone / 10f;

        /// <summary>Durumun Türkçe açıklaması (sabit dizgiler — her karede okunabilir, tahsis yok).</summary>
        public virtual string StatusText
        {
            get
            {
                switch (Phase)
                {
                    case TransportPhase.Waiting: return "İntikal için hazır";
                    case TransportPhase.EnRoute: return "İniş bölgesine intikal ediliyor";
                    case TransportPhase.Landing: return "İnişe geçiliyor";
                    case TransportPhase.Unloading: return "İniş bölgesine varıldı — araçtan in!";
                    case TransportPhase.Departing: return "Araç bölgeden ayrılıyor";
                    default: return string.Empty;
                }
            }
        }

        public abstract Transform GetSeat(int index);
        public abstract Transform PassengerViewPoint(int index);
        public abstract Vector3 GetDisembarkPoint(int index);
        public abstract void Begin();
        public abstract void ReleasePassengers();

        protected void RaiseArrived() => Arrived?.Invoke();
        protected void RaiseDeparted() => Departed?.Invoke();

        // ------------------------------------------------------------------ public helpers

        /// <summary>Koltuk indeksini [0, SeatCount) aralığına sarar (fazla asker olursa koltuklar paylaşılır).</summary>
        public int NormalizeSeatIndex(int index)
        {
            var count = Seats != null && Seats.Length > 0 ? Seats.Length : Mathf.Max(1, SeatCount);
            index %= count;
            return index < 0 ? index + count : index;
        }

        /// <summary>Koltukta parent'lı bir yolcu var mı?</summary>
        public bool IsSeatOccupied(int index)
        {
            if (Seats == null || Seats.Length == 0)
                return false;

            var seat = Seats[NormalizeSeatIndex(index)];
            return seat != null && seat.childCount > 0;
        }

        /// <summary>Parent'lı yolcusu olan koltuk sayısı.</summary>
        public int OccupiedSeatCount
        {
            get
            {
                var count = 0;
                if (Seats == null)
                    return 0;

                for (var i = 0; i < Seats.Length; i++)
                {
                    if (Seats[i] != null && Seats[i].childCount > 0)
                        count++;
                }

                return count;
            }
        }

        /// <summary>İndirme noktasında askerin bakacağı yön (Y açısı, derece).</summary>
        public virtual float GetDisembarkYaw(int index)
        {
            if (DisembarkYawLocal == null || DisembarkYawLocal.Length == 0)
                return transform.eulerAngles.y;

            return transform.eulerAngles.y + DisembarkYawLocal[NormalizeSeatIndex(index)];
        }

        // ------------------------------------------------------------------ lifecycle

        protected virtual void Awake()
        {
            if (!ActiveVehicles.Contains(this))
                ActiveVehicles.Add(this);
        }

        protected virtual void OnDestroy()
        {
            ActiveVehicles.Remove(this);
            StopLoopAudio();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveVehicles.Clear();
        }

        // ------------------------------------------------------------------ shared seat / disembark helpers

        protected Transform SeatAt(int index)
        {
            if (Seats == null || Seats.Length == 0)
                return transform;

            var seat = Seats[NormalizeSeatIndex(index)];
            return seat != null ? seat : transform;
        }

        protected Transform ViewPointAt(int index)
        {
            if (ViewPoints == null || ViewPoints.Length == 0)
                return SeatAt(index);

            var view = ViewPoints[NormalizeSeatIndex(index)];
            return view != null ? view : SeatAt(index);
        }

        /// <summary>
        /// Yerel indirme noktasını (yalnızca araç yönüyle döndürülmüş) zemine oturtur; nokta doluysa dışa doğru kaydırarak dener.
        /// </summary>
        protected Vector3 ResolveDisembarkPoint(int index, Vector3 outwardLocal)
        {
            var local = DisembarkLocal != null && DisembarkLocal.Length > 0 ? DisembarkLocal[NormalizeSeatIndex(index)] : Vector3.zero;
            var yawRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var origin = transform.position;
            var outward = yawRotation * outwardLocal;
            if (outward.sqrMagnitude > 1e-6f)
                outward.Normalize();

            var first = Vector3.zero;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var flat = origin + yawRotation * local + outward * (attempt * 1.6f);
                var groundY = TransportGround.SurfaceHeight(flat);
                var terrainY = TransportGround.TerrainHeight(flat);

                // Çatı/ağaç tepesine bırakma: yüzey araziden çok yüksekse arazi seviyesini al.
                var y = groundY > terrainY + 2.5f ? terrainY : groundY;
                var candidate = new Vector3(flat.x, y + 0.05f, flat.z);
                if (attempt == 0)
                    first = candidate;

                if (TransportGround.IsStandable(candidate))
                    return candidate;
            }

            return first;
        }

        /// <summary>Seyahat başladığında çağrılır (ortak bayraklar).</summary>
        protected void MarkBegun()
        {
            HasBegun = true;
            Phase = TransportPhase.EnRoute;
        }

        /// <summary>Varışta çağrılır: bayraklar, Arrived olayı, boşaltma izlemesi.</summary>
        protected void MarkArrived()
        {
            if (HasArrived)
                return;

            HasArrived = true;
            IsUnloading = true;
            Phase = TransportPhase.Unloading;
            _arrivalTime = Time.time;
            _occupiedAtArrival = OccupiedSeatCount;
            _emptySince = -1f;
            Velocity = Vector3.zero;

            try
            {
                RaiseArrived();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (_releaseRequested)
                _releaseTime = Time.time;
        }

        /// <summary>ReleasePassengers ortak kısmı. Varıştan önce çağrılırsa varışta hemen geri sayım başlar.</summary>
        protected void RequestRelease()
        {
            if (_releaseRequested || IsDeparting)
                return;

            _releaseRequested = true;
            _releaseTime = Time.time;
        }

        protected bool ReleaseRequested => _releaseRequested;

        /// <summary>Bırakma isteğinden (ve varıştan) bu yana geçen süre; istek yoksa -1.</summary>
        protected float SecondsSinceRelease => _releaseRequested && HasArrived ? Time.time - Mathf.Max(_releaseTime, _arrivalTime) : -1f;

        /// <summary>Boşaltma sırasında her kare çağrılır: otomatik bırakma (herkes indiyse / zaman aşımı).</summary>
        protected void TickUnloading(float deltaTime)
        {
            if (!HasArrived || IsDeparting || _releaseRequested)
                return;

            _occupancyTimer -= deltaTime;
            if (_occupancyTimer > 0f)
                return;

            _occupancyTimer = 0.25f;
            var now = Time.time;
            if (_occupiedAtArrival > 0)
            {
                if (OccupiedSeatCount == 0)
                {
                    if (_emptySince < 0f)
                        _emptySince = now;
                    else if (now - _emptySince >= 1.5f)
                        RequestRelease();
                }
                else
                {
                    _emptySince = -1f;
                }
            }

            if (!_releaseRequested && AutoReleaseSeconds > 0f && now - _arrivalTime >= AutoReleaseSeconds)
                RequestRelease();
        }

        /// <summary>Ayrılış başlangıcı: koltukta kalanları bırak, bayraklar, Departed, yok etme zamanlayıcısı.</summary>
        protected void MarkDeparting()
        {
            if (IsDeparting)
                return;

            EjectRemainingPassengers();
            IsDeparting = true;
            IsUnloading = false;
            Phase = TransportPhase.Departing;
            _destroyScheduled = true;
            _destroyAt = Time.time + Mathf.Max(1f, DestroyDelaySeconds);

            try
            {
                RaiseDeparted();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>Ayrılıştan sonra her kare çağrılır; süre dolunca yolcuları güvenle bırakıp aracı yok eder.</summary>
        protected void TickDestroyTimer()
        {
            if (!_destroyScheduled || Time.time < _destroyAt)
                return;

            _destroyScheduled = false;
            _passengersEjected = false;
            EjectRemainingPassengers();
            Destroy(gameObject);
        }

        /// <summary>Koltuklara parent'lı kalmış yolcuları indirme noktalarına bırakır (CharacterController / NavMeshAgent güvenli).</summary>
        protected void EjectRemainingPassengers()
        {
            if (_passengersEjected || Seats == null)
                return;

            _passengersEjected = true;
            for (var i = 0; i < Seats.Length; i++)
            {
                var seat = Seats[i];
                if (seat == null)
                    continue;

                for (var c = seat.childCount - 1; c >= 0; c--)
                {
                    var passenger = seat.GetChild(c);
                    if (passenger == null)
                        continue;

                    var point = GetDisembarkPoint(i);
                    var yaw = GetDisembarkYaw(i);
                    passenger.SetParent(null, true);

                    var controller = passenger.GetComponent<CharacterController>();
                    var controllerWasEnabled = controller != null && controller.enabled;
                    if (controllerWasEnabled)
                        controller.enabled = false;

                    passenger.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));

                    if (controllerWasEnabled)
                        controller.enabled = true;

                    var agent = passenger.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.enabled && agent.isActiveAndEnabled)
                        agent.Warp(point);

                    try
                    {
                        PassengerEjected?.Invoke(passenger, i);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ audio

        private bool _loopRequested;
        private SoundId _loopId;
        private float _loopVolume;
        private float _loopMaxDistance;
        private float _loopRetryTimer;

        /// <summary>Döngü sesi ister; GameAudio henüz hazır değilse hazır olunca başlatılır.</summary>
        protected void RequestLoopAudio(SoundId id, float volume, float maxDistance)
        {
            _loopRequested = true;
            _loopId = id;
            _loopVolume = volume;
            _loopMaxDistance = maxDistance;
            _loopRetryTimer = 0f;
            TryStartLoop();
        }

        /// <summary>Döngü sesini (gerekirse başlatıp) pitch ile günceller.</summary>
        protected void UpdateLoopAudio(float pitch, float deltaTime)
        {
            if (LoopSource == null)
            {
                LoopSource = null;
                if (!_loopRequested)
                    return;

                _loopRetryTimer -= deltaTime;
                if (_loopRetryTimer > 0f)
                    return;

                _loopRetryTimer = 1f;
                TryStartLoop();
                if (LoopSource == null)
                    return;
            }

            LoopSource.pitch = pitch;
        }

        private void TryStartLoop()
        {
            if (!_loopRequested || LoopSource != null || !GameAudio.IsInitialized)
                return;

            try
            {
                LoopSource = GameAudio.StartLoop(_loopId, transform, _loopVolume, true, _loopMaxDistance);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _loopRequested = false;
                LoopSource = null;
            }
        }

        protected void StopLoopAudio()
        {
            _loopRequested = false;
            if (LoopSource == null)
            {
                LoopSource = null;
                return;
            }

            try
            {
                GameAudio.StopLoop(LoopSource);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            LoopSource = null;
        }

        // ------------------------------------------------------------------ build helpers

        /// <summary>Koltuk ve görüş noktası nesnelerini oluşturur (yerel konum/yaw dizilerinden).</summary>
        protected void CreateSeats(Vector3[] seatPositions, float[] seatYaws, Vector3[] viewPositions, Vector3[] viewEulers)
        {
            var count = seatPositions != null ? seatPositions.Length : 0;
            SeatCount = Mathf.Max(1, count);
            Seats = new Transform[count];
            ViewPoints = new Transform[count];

            var seatRoot = new GameObject("Koltuklar").transform;
            seatRoot.SetParent(transform, false);
            var viewRoot = new GameObject("GorusNoktalari").transform;
            viewRoot.SetParent(transform, false);

            for (var i = 0; i < count; i++)
            {
                var seat = new GameObject("Koltuk_" + i).transform;
                seat.SetParent(seatRoot, false);
                seat.localPosition = seatPositions[i];
                seat.localRotation = Quaternion.Euler(0f, seatYaws != null && i < seatYaws.Length ? seatYaws[i] : 0f, 0f);
                Seats[i] = seat;

                var view = new GameObject("Gorus_" + i).transform;
                view.SetParent(viewRoot, false);
                view.localPosition = viewPositions != null && i < viewPositions.Length ? viewPositions[i] : seatPositions[i] + Vector3.up * 1.1f;
                view.localRotation = Quaternion.Euler(viewEulers != null && i < viewEulers.Length ? viewEulers[i] : Vector3.zero);
                ViewPoints[i] = view;
            }
        }

        /// <summary>Paylaşımlı mesh ile görsel parça oluşturur (çarpıştırıcısız).</summary>
        internal static MeshRenderer CreateVisual(string name, Transform parent, CachedVehicleMesh mesh, Material[] slotMaterials, bool castShadows)
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Vehicle;
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = TransportMeshBuilder.ResolveMaterials(mesh.Slots, slotMaterials);
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        /// <summary>Görünmez kutu çarpıştırıcısı (mermi/hareket engeli için; Vehicle katmanı).</summary>
        internal static BoxCollider CreateBoxCollider(Transform parent, Vector3 center, Vector3 size, string name = "Carpisma")
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Vehicle;
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        /// <summary>Kinematik gövde (çarpıştırıcılar bunun altında; yolcular bunun DIŞINDA — bileşik çarpıştırıcıya karışmaz).</summary>
        internal static Transform CreateKinematicBody(Transform parent)
        {
            var go = new GameObject("Govde");
            go.layer = GameLayers.Vehicle;
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            return go.transform;
        }
    }

    /// <summary>İntikal aracının evresi.</summary>
    public enum TransportPhase
    {
        Waiting = 0,    // Begin bekleniyor
        EnRoute = 1,    // intikal
        Landing = 2,    // iniş / yavaşlama / kapı açılıyor
        Unloading = 3,  // varıldı, yolcular iniyor
        Departing = 4   // ayrılıyor
    }
}
