using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// Üçte bir kuralı çözücüsü. Kamera konumu ve özneye göre, öznenin ekranda istenen NDC noktasına (örn. x = -1/3 sol üçte bir çizgisi,
    /// y = +1/3 üst çizgi) düşmesi için bakış hedefini bulur. Kahraman karakter genelde bir üçte bir çizgisine, gözleri üst çizgiye
    /// yerleştirilir; boş yarı, bakış yönünde (look room) bırakılır.
    /// </summary>
    public static class LobbyThirdsFraming
    {
        public const float LeftThird = -1f / 3f;
        public const float RightThird = 1f / 3f;
        public const float UpperThird = 1f / 3f;

        public static float HorizontalFov(float verticalFovDeg, float aspect)
        {
            var half = Mathf.Tan(Mathf.Deg2Rad * verticalFovDeg * 0.5f) * aspect;
            return 2f * Mathf.Atan(half) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Bakış hedefi: özneye kamera→özne yönünde aynı uzaklıkta bir nokta. NDC +x sağ, +y yukarı. Özne ekranda (ndcX, ndcY)'e oturur.
        /// </summary>
        public static Vector3 SolveTarget(Vector3 camera, Vector3 subject, float verticalFovDeg, float aspect, float ndcX, float ndcY)
        {
            var toSubject = subject - camera;
            var dist = toSubject.magnitude;
            if (dist < 1e-3f) return camera + Vector3.forward;
            aspect = Mathf.Max(0.1f, aspect);

            var tanV = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(verticalFovDeg, 1f, 170f) * 0.5f);
            var tanH = tanV * aspect;

            // Özne yönü (yaw, pitch); kamerayı özneye göre ters sapma açısı kadar çeviririz.
            var dir = toSubject / dist;
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            var yawOffset = Mathf.Atan(ndcX * tanH) * Mathf.Rad2Deg;
            var pitchOffset = Mathf.Atan(ndcY * tanV) * Mathf.Rad2Deg;

            // Özne sağda (ndcX > 0) olacaksa kamera özneden SOLA bakar: yaw eksilir. Özne yukarıdaysa kamera aşağı bakar: pitch artar.
            var rot = Quaternion.Euler(pitch + pitchOffset, yaw - yawOffset, 0f);
            return camera + rot * Vector3.forward * dist;
        }

        /// <summary>Verilen kamera durumunda öznenin NDC konumu (doğrulama ve testler için). Arkada kalırsa false.</summary>
        public static bool ProjectToNdc(Vector3 camera, Vector3 target, float verticalFovDeg, float aspect, Vector3 subject, out Vector2 ndc)
        {
            ndc = Vector2.zero;
            var fwd = target - camera;
            if (fwd.sqrMagnitude < 1e-6f) return false;

            var rot = Quaternion.Inverse(Quaternion.LookRotation(fwd, Vector3.up));
            var local = rot * (subject - camera);
            if (local.z <= 1e-3f) return false;

            var tanV = Mathf.Tan(Mathf.Deg2Rad * verticalFovDeg * 0.5f);
            ndc = new Vector2(local.x / (local.z * tanV * aspect), local.y / (local.z * tanV));
            return true;
        }

        /// <summary>Öznenin üçte bir çizgisine uzaklığı ([0, ~1] NDC); en yakın dikey çizgiye göre.</summary>
        public static float DistanceToThirdLine(float ndcX) => Mathf.Min(Mathf.Abs(ndcX - LeftThird), Mathf.Abs(ndcX - RightThird));
    }
}
