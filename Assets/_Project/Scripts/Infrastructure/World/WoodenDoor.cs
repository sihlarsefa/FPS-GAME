using System;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.World
{
    public enum DoorSound { Open, Close, Slam, Creak, Latch }

    /// <summary>
    /// Menteşeli ahşap kapı: yumuşak aç/kapa, koşarken çarpma, F basılı tutunca aralık (peek) durumu.
    /// Kök = kapı açıklığı merkezi (zemin); kanat, menteşe pivotunda döner. Mermi delinmesi Destructible/DestructionRules'ta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WoodenDoor : MonoBehaviour
    {
        /// <summary>Ses kancası (PlayDoorSound abone). (kapı, ses, dünya konumu)</summary>
        public static event Action<WoodenDoor, DoorSound, Vector3> Sound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookAudio()
        {
            Sound -= PlayDoorSound;
            Sound += PlayDoorSound;
        }

        private static void PlayDoorSound(WoodenDoor door, DoorSound sound, Vector3 pos)
        {
            try
            {
                if (UnityEngine.Application.isBatchMode)
                    return;
                switch (sound)
                {
                    case DoorSound.Open: Project.Infrastructure.Audio.GameAudio.Play(Project.Infrastructure.Audio.SoundId.FootstepWood, pos, 0.7f, 0.7f, 30f); break;
                    case DoorSound.Close: Project.Infrastructure.Audio.GameAudio.Play(Project.Infrastructure.Audio.SoundId.VehicleDoor, pos, 0.6f, 1.15f, 30f); break;
                    case DoorSound.Slam: Project.Infrastructure.Audio.GameAudio.Play(Project.Infrastructure.Audio.SoundId.BulletImpactWood, pos, 0.9f, 0.6f, 45f); break;
                    case DoorSound.Creak: Project.Infrastructure.Audio.GameAudio.Play(Project.Infrastructure.Audio.SoundId.ClothRustle, pos, 0.8f, 0.55f, 25f); break;
                    case DoorSound.Latch: Project.Infrastructure.Audio.GameAudio.Play(Project.Infrastructure.Audio.SoundId.UiClick, pos, 0.5f, 0.8f, 15f); break;
                }
            }
            catch (Exception) { /* ses isteğe bağlı */ }
        }

        private Transform _pivot;
        private Transform _leaf;
        private BoxCollider _zone;
        private NavMeshObstacle _obstacle;
        private bool _hingeRight;
        private float _width = 1.1f;
        private DoorState _state = DoorState.Closed;
        private DoorState _restState = DoorState.Closed;
        private float _sign = 1f;
        private float _angle;
        private float _vel;
        private float _smooth = WoodenDoorMath.SmoothTime;
        private bool _peeking;
        private bool _slamBounce;
        private float _slamCooldown;
        private bool _moving;

        public DoorState State => _state;
        public bool IsPeeking => _peeking;
        public float Angle => _angle;
        public bool IsLocked { get; set; }

        /// <summary>Kapı yoksa (kanat kırıldı) etkileşim yok.</summary>
        public bool IsIntact => _leaf != null;

        public static WoodenDoor FindOn(Collider c) => c == null ? null : c.GetComponentInParent<WoodenDoor>();

        /// <summary>Kapı kurar: kök (açıklık merkezi, zemin), menteşe pivotu, kanat, kol, tetik bölgesi.</summary>
        public static WoodenDoor Build(Transform parent, Vector3 pos, float yaw, float width = 1.1f, float height = 2.1f,
            bool hingeOnRight = false, MaterialId leafMat = MaterialId.Wood)
        {
            var root = StructureKit.CreateGroup(parent, "AhsapKapiMenteseli", pos, Quaternion.Euler(0f, yaw, 0f));
            var door = root.AddComponent<WoodenDoor>();
            door._hingeRight = hingeOnRight;
            door._width = width;

            var pivot = StructureKit.CreateGroup(root.transform, "Mentese", new Vector3(WoodenDoorMath.HingeX(width, hingeOnRight), 0f, 0f), Quaternion.identity);
            var leaf = StructureKit.CreateBox(pivot.transform, "KapiKanat",
                new Vector3(WoodenDoorMath.LeafOffsetX(width, hingeOnRight), height * 0.5f, 0f),
                new Vector3(width - 0.04f, height - 0.03f, 0.05f), Quaternion.identity, leafMat);
            leaf.isStatic = false;
            Destructible.Mark(leaf, Project.Application.Services.DestructibleKind.Wood, MaterialId.Wood, 60f);
            // Kol: menteşenin karşı kenarında
            var knobX = WoodenDoorMath.LeafOffsetX(width, hingeOnRight) * 1.8f;
            var knob = StructureKit.CreateBox(pivot.transform, "Kol", new Vector3(knobX, 1.0f, 0f), new Vector3(0.07f, 0.05f, 0.12f),
                Quaternion.identity, MaterialId.MetalDark, false);
            knob.isStatic = false;
            // Dikmeler: kanadın üst/yan çıtaları (doku okunurluğu)
            StructureKit.CreateBox(pivot.transform, "KapiCita", new Vector3(WoodenDoorMath.LeafOffsetX(width, hingeOnRight), height * 0.5f, 0.03f),
                new Vector3(width - 0.3f, height - 0.35f, 0.02f), Quaternion.identity, MaterialId.WoodDark, false).isStatic = false;

            var zoneGo = StructureKit.CreateGroup(root.transform, "KapiBolge", new Vector3(0f, height * 0.5f, 0f), Quaternion.identity);
            var zone = zoneGo.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(width + 0.2f, height, 1.1f);
            zoneGo.AddComponent<WoodenDoorZone>().Door = door;

            var ob = root.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box;
            ob.center = new Vector3(0f, height * 0.5f, 0f);
            ob.size = new Vector3(width, height, 0.2f);
            ob.carving = true;
            ob.carveOnlyStationary = true;

            door._pivot = pivot.transform;
            door._leaf = leaf.transform;
            door._zone = zone;
            door._obstacle = ob;
            door.enabled = false;
            return door;
        }

        private void Awake()
        {
            if (_pivot == null && transform.childCount > 0)
                _pivot = transform.Find("Mentese");
            if (_leaf == null && _pivot != null)
                _leaf = _pivot.Find("KapiKanat");
            if (_obstacle == null)
                _obstacle = GetComponent<NavMeshObstacle>();
        }

        // ------------------------------------------------------------------ Etkileşim

        /// <summary>F kısa basış: kapalı/aralıksa aç; açıksa kapat. actor = oynayanın dünya konumu.</summary>
        public bool Toggle(Vector3 actorPos)
        {
            if (!IsIntact || IsLocked)
            {
                if (IsLocked)
                    Emit(DoorSound.Latch);
                return false;
            }

            _peeking = false;
            if (_state == DoorState.Open)
                SetRest(DoorState.Closed, DoorSound.Close, WoodenDoorMath.SmoothTime);
            else
            {
                _sign = WoodenDoorMath.OpenSign(transform.InverseTransformPoint(actorPos).z, _hingeRight);
                SetRest(DoorState.Open, DoorSound.Open, WoodenDoorMath.SmoothTime);
            }

            return true;
        }

        /// <summary>F basılı tutma başlangıcı: kapalı kapıyı aralar (bakmak için). Zaten açıksa yok sayar.</summary>
        public bool BeginPeek(Vector3 actorPos)
        {
            if (!IsIntact || IsLocked || _state == DoorState.Open)
                return false;
            if (_state == DoorState.Closed)
                _sign = WoodenDoorMath.OpenSign(transform.InverseTransformPoint(actorPos).z, _hingeRight);
            _peeking = true;
            _restState = DoorState.Closed;
            _state = DoorState.Ajar;
            _smooth = 0.35f;
            Emit(DoorSound.Creak);
            Wake();
            return true;
        }

        /// <summary>F bırakıldı: aralık kapı geri kapanır.</summary>
        public void EndPeek()
        {
            if (!_peeking)
                return;
            _peeking = false;
            SetRest(DoorState.Closed, DoorSound.Close, 0.3f);
        }

        /// <summary>Koşan aktör kapıya girdi: yeterince hızlıysa çarparak açar.</summary>
        public bool TrySlam(Vector3 actorPos, Vector3 actorVelocity)
        {
            if (!IsIntact || IsLocked || _slamCooldown > 0f || (_state == DoorState.Open && !_slamBounce && Mathf.Abs(_angle) > 60f))
                return false;
            var flat = new Vector3(actorVelocity.x, 0f, actorVelocity.z);
            if (!WoodenDoorMath.IsSlam(flat.magnitude))
                return false;
            var local = transform.InverseTransformDirection(flat);
            // Aktör hareket yönünde ilerliyor; kanat ileri tarafa savrulur
            var away = local.z >= 0f ? 1f : -1f;
            _sign = _hingeRight ? away : -away;
            _peeking = false;
            _slamBounce = true;
            _slamCooldown = 0.6f;
            _restState = DoorState.Open;
            _state = DoorState.Open;
            _vel = _sign * 420f;
            _smooth = WoodenDoorMath.SlamSmoothTime;
            _angle = Mathf.Abs(_angle) < 8f ? _sign * 8f : _angle;
            Emit(DoorSound.Slam);
            Wake();
            return true;
        }

        private void SetRest(DoorState rest, DoorSound sound, float smooth)
        {
            _restState = rest;
            _state = rest;
            _smooth = smooth;
            _slamBounce = false;
            Emit(sound);
            Wake();
        }

        private void Wake()
        {
            _moving = true;
            enabled = true;
            if (_obstacle != null)
                _obstacle.enabled = false;
        }

        private void Emit(DoorSound s) => Sound?.Invoke(this, s, transform.position + Vector3.up * 1f);

        // ------------------------------------------------------------------ Simülasyon

        private void Update()
        {
            if (!_moving)
            {
                enabled = false;
                return;
            }

            if (_leaf == null)
            {
                // Kanat kırıldı: kapı serbest kalır
                _moving = false;
                enabled = false;
                if (_obstacle != null)
                    _obstacle.enabled = false;
                return;
            }

            var dt = Mathf.Min(Time.deltaTime, 0.05f);
            _slamCooldown = Mathf.Max(0f, _slamCooldown - dt);
            var target = WoodenDoorMath.TargetAngle(_state, _sign);
            if (_slamBounce)
            {
                // Çarpma: önce aşırı açıya savrul, sonra dinlenme açısına otur
                var over = _sign * WoodenDoorMath.SlamOvershoot;
                if (Mathf.Abs(_angle) < Mathf.Abs(over) - 4f && Mathf.Sign(_vel) == _sign)
                    target = over;
                else
                {
                    _slamBounce = false;
                    _smooth = 0.3f;
                    Emit(DoorSound.Latch);
                }
            }

            _angle = WoodenDoorMath.Step(_angle, target, ref _vel, _smooth, dt);
            _pivot.localRotation = Quaternion.Euler(0f, _angle, 0f);

            if (!_slamBounce && WoodenDoorMath.Settled(_angle, target, _vel))
            {
                _angle = target;
                _pivot.localRotation = Quaternion.Euler(0f, _angle, 0f);
                _moving = false;
                if (_obstacle != null && _state == DoorState.Closed)
                    _obstacle.enabled = true;
                enabled = false;
            }
        }
    }

    /// <summary>Kapı çevresindeki tetik: koşarak geçen karakter kapıyı çarpar (CharacterController/Rigidbody hızından).</summary>
    public sealed class WoodenDoorZone : MonoBehaviour
    {
        public WoodenDoor Door;

        private void OnTriggerEnter(Collider other)
        {
            if (Door == null || other == null)
                return;
            Vector3 v;
            if (other.TryGetComponent<CharacterController>(out var cc))
                v = cc.velocity;
            else if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
                v = other.attachedRigidbody.linearVelocity;
            else
                return;
            Door.TrySlam(other.transform.position, v);
        }
    }
}
