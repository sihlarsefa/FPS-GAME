using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Yaslanma (lean) pozu kancası: SetLean(-1..+1) (negatif = sola, pozitif = sağa). Ana poz SoldierModel.LateUpdate'te
    /// yazıldığı için (o dosyaya dokunulmadı) gövde omurgasına, ana pozdan SONRA çalışan küçük bir uygulayıcı bileşen
    /// eklenir. ENTEGRASYON: istenirse ana LateUpdate içine taşınabilir (spineYaw yanına roll olarak).
    /// </summary>
    public sealed partial class SoldierModel
    {
        private SoldierLeanApplier _leanApplier;
        private float _leanTarget;

        /// <summary>Hedef yaslanma -1..1.</summary>
        public float LeanTarget => _leanTarget;

        /// <summary>Gövdeyi yana yatırır (köşeden kafa uzatma). 0 verilince uygulayıcı kendini sıfırlar.</summary>
        public void SetLean(float lean)
        {
            _leanTarget = Mathf.Clamp(lean, -1f, 1f);
            if (_leanApplier == null)
            {
                if (Mathf.Abs(_leanTarget) < 0.001f || _spine == null)
                    return;
                _leanApplier = gameObject.GetComponent<SoldierLeanApplier>();
                if (_leanApplier == null)
                    _leanApplier = gameObject.AddComponent<SoldierLeanApplier>();
            }

            _leanApplier.Bind(_spine, _body, _leanTarget);
        }
    }

    /// <summary>SoldierModel pozundan sonra omurgayı/kalçayı yaslanma açısına çevirir.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class SoldierLeanApplier : MonoBehaviour
    {
        public const float MaxRollDegrees = 14f;
        public const float MaxShiftMeters = 0.11f;
        private const float Smooth = 9f;

        private Transform _spine;
        private Transform _body;
        private float _target;
        private float _current;
        private Quaternion _lastSpine;
        private Vector3 _lastBody;
        private bool _hasLast;

        public void Bind(Transform spine, Transform body, float target)
        {
            _spine = spine;
            _body = body;
            _target = target;
        }

        /// <summary>Saf: yumuşatma adımı (test edilebilir).</summary>
        public static float Step(float current, float target, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Smooth * Mathf.Max(0f, dt)));
        }

        private void LateUpdate()
        {
            if (_spine == null || _body == null)
                return;

            _current = Step(_current, _target, Time.deltaTime);
            if (Mathf.Abs(_current) < 0.002f && Mathf.Abs(_target) < 0.001f)
            {
                _hasLast = false;
                return;
            }

            // Ana poz bu karede yazmadıysa (uzak LOD) önceki çarpım birikmesin: kendi yazdığımız değer duruyorsa geri al.
            var spineRot = _spine.localRotation;
            var bodyPos = _body.localPosition;
            if (_hasLast && spineRot == _lastSpine && bodyPos == _lastBody)
            {
                // değişmemiş: önceki uygulamayı geri alıp yeniden uygula
                spineRot = spineRot * Quaternion.Euler(0f, 0f, _prevRoll);
                bodyPos.x -= _prevShift;
            }

            _prevRoll = _current * MaxRollDegrees;
            _prevShift = _current * MaxShiftMeters;
            _spine.localRotation = spineRot * Quaternion.Euler(0f, 0f, -_prevRoll);
            _body.localPosition = new Vector3(bodyPos.x + _prevShift, bodyPos.y, bodyPos.z);
            _lastSpine = _spine.localRotation;
            _lastBody = _body.localPosition;
            _hasLast = true;
        }

        private float _prevRoll;
        private float _prevShift;
    }
}
