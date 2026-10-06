using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>Tek bir lobi çekimi: konum, bakış hedefi, FOV ve optik/ton değerleri. Saf veri (Unity nesnesi içermez).</summary>
    public struct LobbyShot
    {
        public Vector3 Position;
        public Vector3 Target;
        public float FieldOfView;

        /// <summary>Odak mesafesi (m). Kahramanın kameraya uzaklığına oturur.</summary>
        public float FocusDistance;

        /// <summary>Bulanıklık miktarı 0..1 (0 = DoF kapalı, 1 = en güçlü bokeh).</summary>
        public float Blur;

        /// <summary>Pozlama kayması (EV). Ayarlar çekimi karartılır.</summary>
        public float ExposureEv;

        /// <summary>El kamerası şiddeti çarpanı (1 = varsayılan).</summary>
        public float ShakeScale;

        /// <summary>Ufuk eğimi (derece, Dutch angle). Çok küçük tutulur.</summary>
        public float RollDegrees;

        public LobbyShot(Vector3 position, Vector3 target, float fov, float focus, float blur, float exposureEv, float shakeScale, float roll)
        {
            Position = position;
            Target = target;
            FieldOfView = fov;
            FocusDistance = focus;
            Blur = blur;
            ExposureEv = exposureEv;
            ShakeScale = shakeScale;
            RollDegrees = roll;
        }

        /// <summary>Hedefe göre odak mesafesi hesaplayan kısayol (odak = kamera-hedef uzaklığı).</summary>
        public static LobbyShot Focused(Vector3 position, Vector3 target, float fov, float blur, float exposureEv, float shakeScale, float roll)
        {
            var d = Vector3.Distance(position, target);
            return new LobbyShot(position, target, fov, d, blur, exposureEv, shakeScale, roll);
        }

        public float Distance => Vector3.Distance(Position, Target);
    }
}
