using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Nişangâh türü (göz mesafesi ve hat hesabı için).</summary>
    public enum SightKind
    {
        Iron,
        RedDot,
        Holo,
        Scope
    }

    /// <summary>
    /// Saf matematik: ADS'de arka gez (veya kırmızı nokta kasası) ile arpacık (veya nokta) ekran merkezine TAM oturacak
    /// göz-silah ofseti. WeaponViewModel: pos = SightAlignment.EyeOffset(sightLocal, frontLocal, eyeRelief) (konum),
    /// rot = SightAlignment.SightLineRotation(sightLocal, frontLocal) (ADS hedef dönüşü).
    /// </summary>
    public static class SightAlignment
    {
        /// <summary>Türe göre varsayılan göz mesafesi (m): gez gözden uzak/kırmızı nokta daha yakın değil, optik yakın.</summary>
        public static float DefaultEyeRelief(SightKind kind)
        {
            switch (kind)
            {
                case SightKind.RedDot: return 0.10f;
                case SightKind.Holo: return 0.11f;
                case SightKind.Scope: return 0.08f;
                default: return 0.14f;
            }
        }

        /// <summary>
        /// Silah (model) uzayında arka nişan noktası <paramref name="rearLocal"/> göz eksenine (x=0,y=0,z=eyeRelief) gelecek ofset.
        /// Silah modelinin yerel konumu olarak kullanılır (ViewModel kökü göz/kamera uzayında).
        /// </summary>
        public static Vector3 EyeOffset(Vector3 rearLocal, float eyeRelief)
        {
            return new Vector3(-rearLocal.x, -rearLocal.y, eyeRelief - rearLocal.z);
        }

        /// <summary>Arka nişan + arpacık hattı verilirse: hat, rotasyon düzeltmesinden sonra kamera eksenine paralel olur.</summary>
        public static Vector3 EyeOffset(Vector3 rearLocal, Vector3 frontLocal, float eyeRelief)
        {
            var rot = SightLineRotation(rearLocal, frontLocal);
            var rotatedRear = rot * rearLocal;
            return new Vector3(-rotatedRear.x, -rotatedRear.y, eyeRelief - rotatedRear.z);
        }

        /// <summary>
        /// Gez→arpacık doğrusunu +Z (bakış) eksenine hizalayan küçük dönüş. Hat zaten düzse identity; aşırı açıda (&gt;6 derece)
        /// sınırlanır (model hatası silahı eğmesin).
        /// </summary>
        public static Quaternion SightLineRotation(Vector3 rearLocal, Vector3 frontLocal)
        {
            var dir = frontLocal - rearLocal;
            if (dir.sqrMagnitude < 1e-8f)
                return Quaternion.identity;
            var rot = Quaternion.FromToRotation(dir.normalized, Vector3.forward);
            rot.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
                angle -= 360f;
            if (Mathf.Abs(angle) > 6f)
                rot = Quaternion.AngleAxis(Mathf.Sign(angle) * 6f, axis);
            return rot;
        }

        /// <summary>Gez→arpacık hattı +Z'ye 3 dereceden az eğimliyse demir nişan hattıdır (optik kasası değil).</summary>
        public static bool IsIronLine(Vector3 rearLocal, Vector3 frontLocal)
        {
            var d = frontLocal - rearLocal;
            return d.z > 0.05f && Vector3.Angle(d, Vector3.forward) < 3f;
        }

        /// <summary>Hattın (arka+ön) göz eksenine göre açısal hatası (miliradyan), pozisyon+rotasyon uygulandıktan sonra.</summary>
        public static float AxisErrorMilliradians(Vector3 pos, Quaternion rot, Vector3 rearLocal, Vector3 frontLocal)
        {
            var r = pos + rot * rearLocal;
            var f = pos + rot * frontLocal;
            var d = f - r;
            if (d.z < 1e-6f) return float.MaxValue;
            // Gözden (0,0,0) arka ve ön noktalara bakış yönleri arasındaki açı: ikisi de eksende ise 0.
            var offAxis = Mathf.Max(Mathf.Sqrt(r.x * r.x + r.y * r.y) / Mathf.Max(0.01f, r.z), Mathf.Sqrt(f.x * f.x + f.y * f.y) / Mathf.Max(0.01f, f.z));
            return offAxis * 1000f;
        }

        /// <summary>Arka nişanın ekran merkezinden kalan yanal/dikey hatası (m); 0 = tam oturuyor. Test/teşhis için.</summary>
        public static Vector2 ResidualError(Vector3 weaponLocalPos, Quaternion weaponLocalRot, Vector3 rearLocal)
        {
            var p = weaponLocalPos + weaponLocalRot * rearLocal;
            return new Vector2(p.x, p.y);
        }

        /// <summary>Optik (kırmızı nokta/holo/dürbün) hizalama çözümü: konum, dönüş ve kalan hata.</summary>
        public readonly struct OpticSolution
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float ResidualMilliradians;
            public readonly bool Clamped;

            public OpticSolution(Vector3 position, Quaternion rotation, float residualMrad, bool clamped)
            {
                Position = position;
                Rotation = rotation;
                ResidualMilliradians = residualMrad;
                Clamped = clamped;
            }
        }

        /// <summary>
        /// Optik hizası: pencere/lens merkezi göz eksenine (0,0,eyeRelief), optik eksen (boreLocal, model uzayı) +Z'ye gelir.
        /// Model optiği hafif eğik monte edilmişse dönüş en çok <paramref name="maxTiltDegrees"/> ile düzeltilir; kalan eğim
        /// ResidualMilliradians'ta raporlanır (kolimatör noktası bu kadar kayar). Eski yol rotasyonu hiç uygulamıyordu.
        /// </summary>
        public static OpticSolution SolveOptic(Vector3 windowCenterLocal, Vector3 boreLocal, float eyeRelief, float maxTiltDegrees = 3f)
        {
            var rot = Quaternion.identity;
            var clamped = false;
            var residual = 0f;
            if (boreLocal.sqrMagnitude > 1e-8f)
            {
                var bore = boreLocal.normalized;
                var full = Quaternion.FromToRotation(bore, Vector3.forward);
                var ang = Quaternion.Angle(Quaternion.identity, full);
                var limit = Mathf.Max(0f, maxTiltDegrees);
                if (ang > limit && ang > 1e-4f)
                {
                    rot = Quaternion.Slerp(Quaternion.identity, full, limit / ang);
                    clamped = true;
                    residual = (ang - limit) * Mathf.Deg2Rad * 1000f;
                }
                else
                {
                    rot = full;
                }
            }

            var rw = rot * windowCenterLocal;
            var pos = new Vector3(-rw.x, -rw.y, eyeRelief - rw.z);
            return new OpticSolution(pos, rot, residual, clamped);
        }

        /// <summary>
        /// Ayarlanmış poz (hip→ADS, aim 0..1) için gez noktasının ekran merkezine uzaklığı (m, x/y); aim=1'de
        /// <paramref name="swayOffset"/> * <paramref name="swayDamping"/> kalan salınım dahil. Teşhis/test için.
        /// </summary>
        public static Vector2 SightResidualWithSway(Vector3 adsPos, Quaternion adsRot, Vector3 rearLocal, Vector3 swayOffset, float swayDamping)
        {
            var p = adsPos + adsRot * rearLocal + swayOffset * Mathf.Clamp01(swayDamping);
            return new Vector2(p.x, p.y);
        }
    }
}
