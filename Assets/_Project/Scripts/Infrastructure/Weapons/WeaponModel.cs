using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// WeaponModelFactory'nin kurduğu silah modelinin kök bileşeni: tutma noktaları (el bileği bağlantıları),
    /// namlu ucu, nişan hattı ve hareketli parçalar (şarjör, sürgü, kapak, pompa, kurma kolu).
    /// Model uzayı: +Z namlu yönü, +Y yukarı, orijin kabzanın üstü (sağ elin tuttuğu nokta). Ölçek gerçek boyut (metre).
    /// El bağlantılarında +Z parmak yönü, +Y elin sırtıdır. Update çalıştırmaz (maliyetsiz).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponModel : MonoBehaviour
    {
        public const string BodyPart = "Body";
        public const string MagazinePart = "Magazine";
        public const string BoltPart = "Bolt";
        public const string SlidePart = "Slide";
        public const string PumpPart = "Pump";
        public const string CoverPart = "Cover";

        public const string MuzzleAnchor = "Muzzle";
        public const string SightAnchor = "Sight";
        public const string FrontSightAnchor = "FrontSight";
        public const string RightHandAnchor = "RightHand";
        public const string LeftHandAnchor = "LeftHand";
        public const string MagazineHandAnchor = "MagazineHand";
        public const string BoltHandAnchor = "BoltHand";
        public const string LoadingPortAnchor = "LoadingPort";
        public const string EjectAnchor = "Eject";
        public const string CoverHandAnchor = "CoverHand";

        private readonly List<Renderer> _renderers = new List<Renderer>(8);
        private Vector3 _magazineHomePosition;
        private Quaternion _magazineHomeRotation = Quaternion.identity;
        private Vector3 _boltHomePosition;
        private Quaternion _boltHomeRotation = Quaternion.identity;
        private Vector3 _slideHomePosition;
        private Vector3 _pumpHomePosition;
        private Quaternion _coverHomeRotation = Quaternion.identity;
        private bool _visible = true;

        public WeaponDefinitionData Definition { get; internal set; }
        public WeaponStyle Style { get; internal set; }
        public bool ForViewmodel { get; internal set; }

        /// <summary>Namlu ucu (+Z atış yönü). Asla null değildir (yoksa kök).</summary>
        public Transform Muzzle { get; internal set; }

        /// <summary>Nişan hattı üzerindeki arka nokta (gez / dürbün göz merceği / nokta nişangâh arka ağzı); +Z nişan yönü.</summary>
        public Transform SightPoint { get; internal set; }

        /// <summary>Arpacık / ön nişan noktası (iron sight: tepe; optikte null). SightLine hizası için.</summary>
        public Transform FrontSightPoint { get; internal set; }

        /// <summary>Ön nişan model-yerel konumu; yoksa null (WeaponViewModel 2 argümanlı EyeOffset'e düşer).</summary>
        public Vector3? FrontSightLocal => FrontSightPoint != null ? transform.InverseTransformPoint(FrontSightPoint.position) : (Vector3?)null;

        /// <summary>Sağ el bileği hedefi (kabza).</summary>
        public Transform RightHandGrip { get; internal set; }

        /// <summary>Sol el bileği hedefi (el kundağı / pompa / tabancada destek eli).</summary>
        public Transform LeftHandGrip { get; internal set; }

        /// <summary>Şarjör değiştirirken sol elin bileği (şarjör parçasının çocuğu). Yoksa null.</summary>
        public Transform MagazineHandGrip { get; internal set; }

        /// <summary>Sürgü kolunu tutan sağ elin bileği (kurma kolu parçasının çocuğu). Yoksa null.</summary>
        public Transform BoltHandGrip { get; internal set; }

        /// <summary>Pompalıda fişek yükleme ağzı. Yoksa null.</summary>
        public Transform LoadingPort { get; internal set; }

        /// <summary>Kovan atma penceresi (+X dışarı). Yoksa null.</summary>
        public Transform EjectPort { get; internal set; }

        /// <summary>Makineli tüfekte besleme kapağını açıp kapatan sol elin bileği (kapak parçasının çocuğu). Yoksa null.</summary>
        public Transform CoverHandGrip { get; internal set; }

        /// <summary>Kovan rengi (pompalıda kırmızı fişek).</summary>
        public bool ShotgunShells => Style == WeaponStyle.Escort || Style == WeaponStyle.EscortMagnum;

        public Transform Body { get; internal set; }
        public Transform Magazine { get; internal set; }
        public Transform Bolt { get; internal set; }
        public Transform Slide { get; internal set; }
        public Transform Pump { get; internal set; }
        public Transform FeedCover { get; internal set; }

        /// <summary>Nişan alırken göz ile SightPoint arası istenen mesafe (m).</summary>
        public float EyeRelief { get; internal set; } = 0.14f;

        /// <summary>Dürbünlü (nişan alınca görünüm gizlenip dürbün kaplaması çizilir).</summary>
        public bool HasScope { get; internal set; }

        /// <summary>Kırmızı nokta / holografik nişangâh (açık optik) var mı?</summary>
        public bool HasOptic { get; internal set; }

        /// <summary>Şarjörün yuvadan çıkış yönü (model uzayı, birim).</summary>
        public Vector3 MagazineEjectDirection { get; internal set; } = Vector3.down;

        /// <summary>Sürgünün geri çekilme mesafesi (m).</summary>
        public float BoltTravel { get; internal set; } = 0.08f;

        /// <summary>Sürgü kolunun kaldırılma açısı (derece, Z ekseni etrafında).</summary>
        public float BoltLiftDegrees { get; internal set; } = 60f;

        /// <summary>Pompanın geri çekilme mesafesi (m).</summary>
        public float PumpTravel { get; internal set; } = 0.085f;

        /// <summary>Tabanca kızağının geri gidiş mesafesi (m).</summary>
        public float SlideTravel { get; internal set; } = 0.026f;

        public IReadOnlyList<Renderer> Renderers => _renderers;

        public bool IsVisible => _visible;

        internal void AddRenderer(Renderer renderer)
        {
            if (renderer != null)
                _renderers.Add(renderer);
        }

        /// <summary>Hareketli parçaların başlangıç pozlarını kaydeder (kurulumdan sonra bir kez).</summary>
        internal void CaptureHomePoses()
        {
            if (Magazine != null)
            {
                _magazineHomePosition = Magazine.localPosition;
                _magazineHomeRotation = Magazine.localRotation;
            }

            if (Bolt != null)
            {
                _boltHomePosition = Bolt.localPosition;
                _boltHomeRotation = Bolt.localRotation;
            }

            if (Slide != null)
                _slideHomePosition = Slide.localPosition;
            if (Pump != null)
                _pumpHomePosition = Pump.localPosition;
            if (FeedCover != null)
                _coverHomeRotation = FeedCover.localRotation;
        }

        public Vector3 MagazineHomePosition => _magazineHomePosition;
        public Quaternion MagazineHomeRotation => _magazineHomeRotation;

        /// <summary>Tüm hareketli parçaları başlangıç pozuna döndürür ve şarjörü görünür yapar.</summary>
        public void ResetParts()
        {
            SetMagazineOffset(Vector3.zero, Quaternion.identity);
            SetBolt(0f, 0f);
            SetSlide(0f);
            SetPump(0f);
            SetCoverOpen(0f);
        }

        /// <summary>Şarjörü yuvasına göre (model uzayında) kaydırır/döndürür.</summary>
        public void SetMagazineOffset(Vector3 offset, Quaternion rotation)
        {
            if (Magazine == null)
                return;

            Magazine.localPosition = _magazineHomePosition + offset;
            Magazine.localRotation = rotation * _magazineHomeRotation;
        }

        /// <summary>Kurma kolu: lift01 = kolun kalkışı, back01 = geri çekilme.</summary>
        public void SetBolt(float lift01, float back01)
        {
            if (Bolt == null)
                return;

            Bolt.localRotation = _boltHomeRotation * Quaternion.Euler(0f, 0f, Mathf.Clamp01(lift01) * BoltLiftDegrees);
            Bolt.localPosition = _boltHomePosition + Vector3.back * (Mathf.Clamp01(back01) * BoltTravel);
        }

        public void SetSlide(float back01)
        {
            if (Slide != null)
                Slide.localPosition = _slideHomePosition + Vector3.back * (Mathf.Clamp01(back01) * SlideTravel);
        }

        public void SetPump(float back01)
        {
            if (Pump != null)
                Pump.localPosition = _pumpHomePosition + Vector3.back * (Mathf.Clamp01(back01) * PumpTravel);
        }

        /// <summary>Makineli tüfek besleme kapağı (0 kapalı, 1 açık ~70°).</summary>
        public void SetCoverOpen(float open01)
        {
            if (FeedCover != null)
                FeedCover.localRotation = _coverHomeRotation * Quaternion.Euler(Mathf.Clamp01(open01) * 70f, 0f, 0f);
        }

        /// <summary>Tüm görüntüleyicileri açar/kapatır (nesne etkin kalır).</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

        /// <summary>Yalnızca şarjör görüntüleyicisini gizler/gösterir.</summary>
        public void SetMagazineVisible(bool visible)
        {
            if (Magazine == null)
                return;

            var r = Magazine.GetComponent<Renderer>();
            if (r != null)
                r.enabled = visible && _visible;
        }
    }
}
