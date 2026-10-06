using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Asker ayrıntı katmanı (prosedürel): çene/ağız, balaklava/şemagh, medik arması, telsizci anteni, taşıyıcı cepleri,
    /// gece görüş cihazı ve çanta eklentileri. Kemik/vuruş kutusu yerleşimine dokunmaz; yalnızca görsel parçalar ekler.
    /// </summary>
    public sealed partial class SoldierModel
    {
        private readonly Transform[] _antennas = new Transform[4];

        private TeamRole CurrentRole => _owner != null ? _owner.Role : TeamRole.Rifleman;

        /// <summary>Mat siyah ince parçalar: kulaklık, mikrofon, anten, bere bandı/arması, tabanca (piyano siyahı değil).</summary>
        private static Material BlackTrim => MaterialLibrary.Lit(SoldierLook.BeretTrim, 0.05f);

        /// <summary>Bere sol önündeki siyah yuvarlak arma (ASKER_REFERANSI).</summary>
        private void AddBeretBadge(Variant v)
        {
            v.Objects.Add(Part("BeretBadge", Head, CharacterMeshes.Cylinder("beretBadgeRound", -0.005f, 0.005f, 0.017f, 0.017f, 12, true), BlackTrim,
                new Vector3(-0.052f, 0.158f, 0.088f), Quaternion.Euler(80f, -20f, 0f), false));
        }

        /// <summary>Hafif sakal gölgesi: yüzün alt kısmında ten renginden bir ton koyu, mat kabuk (yalnızca yüz örtüsü yokken).</summary>
        private void BuildStubble()
        {
            var tone = _look.Skin * 0.8f;
            tone.a = 1f;
            var mat = CharacterMaterials.Skin(tone) ?? MaterialLibrary.Lit(tone, 0.05f);
            Part("Stubble", Head, CharacterMeshes.Ellipsoid("stubbleShade", new Vector3(0f, 0.09f, 0f), new Vector3(0.0835f, 0.1205f, 0.1035f), 12, 4, -90f, -30f),
                mat, Vector3.zero, false);
        }

        /// <summary>İnce siyah telsiz kulaklık (iki kulak kapağı + baş yayı) ve boyun mikrofonu teli.</summary>
        private void BuildHeadset()
        {
            var black = BlackTrim;
            var cup = CharacterMeshes.Cylinder("headsetCup", -0.009f, 0.009f, 0.028f, 0.026f, 10, true);
            for (var side = -1; side <= 1; side += 2)
            {
                Part("HeadsetCup", Head, cup, black, new Vector3(side * 0.088f, 0.088f, -0.004f), Quaternion.Euler(0f, 0f, 90f), false);
            }

            // Sol kapaktan boyna inen ince tel + boğaz mikrofonu.
            Part("MicWire", Head, CharacterMeshes.Box("micWire", Vector3.zero, new Vector3(0.004f, 0.11f, 0.004f)), black,
                new Vector3(-0.078f, 0.025f, 0.004f), Quaternion.Euler(0f, 0f, -8f), false);
            Part("MicCapsule", Head, CharacterMeshes.Ellipsoid("micCapsule", Vector3.zero, new Vector3(0.014f, 0.01f, 0.008f), 8, 4), black,
                new Vector3(-0.03f, -0.025f, 0.066f), false);
        }

        /// <summary>Sağ uyluğa sarkık tabanca kılıfı (drop-leg) + kemerde küçük cepler.</summary>
        private void BuildHolsterAndBeltPouches()
        {
            var m = _mat;
            var black = BlackTrim;
            var hip = _rightHip;
            Part("HolsterDrop", _body, CharacterMeshes.RoundedBox("holsterDropPlate", Vector3.zero, new Vector3(0.03f, 0.06f, 0.07f)), m.GearDark,
                new Vector3(0.1f, 0.0f, 0.03f), false);
            Part("HolsterBody", hip, CharacterMeshes.RoundedBox("holsterBody", Vector3.zero, new Vector3(0.046f, 0.17f, 0.072f)), m.GearDark,
                new Vector3(0.093f, -0.225f, 0.045f), false);
            // Tabanca: namlu/sürgü kılıfın üstünden görünür, kabza geride.
            Part("HolsterPistolSlide", hip, CharacterMeshes.Box("holsterSlide", Vector3.zero, new Vector3(0.022f, 0.03f, 0.075f)), black,
                new Vector3(0.093f, -0.128f, 0.05f), false);
            Part("HolsterPistolGrip", hip, CharacterMeshes.Box("holsterGrip", Vector3.zero, new Vector3(0.022f, 0.06f, 0.026f)), black,
                new Vector3(0.093f, -0.145f, 0.0f), Quaternion.Euler(-14f, 0f, 0f), false);
            // Bacak kayışları (dış yüz).
            Part("HolsterStrapTop", hip, CharacterMeshes.Box("holsterStrap", Vector3.zero, new Vector3(0.052f, 0.014f, 0.1f)), m.GearDark,
                new Vector3(0.09f, -0.18f, 0.03f), false);
            Part("HolsterStrapLow", hip, CharacterMeshes.Box("holsterStrap", Vector3.zero, new Vector3(0.052f, 0.014f, 0.1f)), m.GearDark,
                new Vector3(0.09f, -0.31f, 0.03f), false);

            // Kemer önünde iki küçük cep.
            var pouch = CharacterMeshes.RoundedBox("beltSmallPouch", Vector3.zero, new Vector3(0.05f, 0.07f, 0.04f));
            Part("BeltPouchFrontL", _body, pouch, m.GearDark, new Vector3(-0.14f, 0.02f, 0.118f), false);
            Part("BeltPouchFrontL2", _body, pouch, m.GearDark, new Vector3(-0.085f, 0.02f, 0.122f), false);
        }

        private void BuildFaceDetail()
        {
            var m = _mat;
            BuildHeadset();
            BuildHolsterAndBeltPouches();
            if (_look.FaceCover == FaceCoverKind.None)
                BuildStubble();
            // Facial volume is sculpted into the continuous head surface.
            Part("LipLine", Head, CharacterMeshes.Ellipsoid("lipLineSculpt", Vector3.zero,
                new Vector3(0.022f, 0.0017f, 0.002f), 16, 4), m.Hair, new Vector3(0f, 0.039f, 0.092f), false);

            switch (_look.FaceCover)
            {
                case FaceCoverKind.Balaclava:
                    // Alt yüzü ve boynu saran balaklava; gözler ve burun üstü açık.
                    Part("Balaclava", Head, CharacterMeshes.Ellipsoid("balaclava2", new Vector3(0f, 0.09f, 0f), new Vector3(0.083f, 0.12f, 0.103f), 10, 3, -90f, -16f),
                        m.Cloth, Vector3.zero, false);
                    Part("BalaclavaNeck", _neck, CharacterMeshes.Cylinder("balaclavaNeck2", -0.03f, 0.05f, 0.066f, 0.06f, 10, false), m.Cloth,
                        Vector3.zero, false);
                    break;

                case FaceCoverKind.Shemagh:
                    // Şemagh: yüzün alt yarısına sarılı + boyun halkası + omuza düşen uç.
                    Part("ShemaghFace", Head, CharacterMeshes.Ellipsoid("shemaghFace2", new Vector3(0f, 0.09f, 0f), new Vector3(0.087f, 0.123f, 0.107f), 10, 3, -90f, -12f),
                        m.Cloth, Vector3.zero, false);
                    Part("ShemaghNeck", _neck, CharacterMeshes.Cylinder("shemaghNeck2", -0.045f, 0.05f, 0.088f, 0.076f, 10, false), m.Cloth,
                        Vector3.zero, false);
                    Part("ShemaghTail", Chest, CharacterMeshes.Box("shemaghTail", Vector3.zero, new Vector3(0.07f, 0.12f, 0.02f)), m.Cloth,
                        new Vector3(0.07f, 0.23f, 0.118f), Quaternion.Euler(0f, 0f, -12f), false);
                    break;
            }

            BuildRoleDetail();
            BuildChestEquipmentDetail();
        }

        /// <summary>
        /// Göğüs ekipman gerçekçiliği: isim bandı, künye zinciri + künye, telsizcide yelek telsiz cebi ve kısa anten.
        /// Aynı malzeme + aynı kemik ebeveyni olan parçalar (Name/Dog/Radio önekleri) SoldierMeshCombiner ile birleşir.
        /// </summary>
        private void BuildChestEquipmentDetail()
        {
            var m = _mat;
            // Sağ göğüs isim bandı (üniforma/yelek üstü, ince koyu şerit + açık kenar çizgisi).
            Part("NameTape", Chest, CharacterMeshes.Box("nameTape", Vector3.zero, new Vector3(0.1f, 0.026f, 0.006f)), m.GearDark,
                new Vector3(0.075f, 0.285f, 0.126f), false);
            Part("NameTapeEdge", Chest, CharacterMeshes.Box("nameTapeEdge", Vector3.zero, new Vector3(0.1f, 0.003f, 0.007f)), m.Gear,
                new Vector3(0.075f, 0.2975f, 0.1265f), false);

            // Künye zinciri: boyundan V şeklinde inen iki ince halka sırası + metal künye.
            var link = CharacterMeshes.Box("dogChain", Vector3.zero, new Vector3(0.003f, 0.085f, 0.003f));
            Part("DogChainL", Chest, link, m.Metal, new Vector3(-0.022f, 0.33f, 0.112f), Quaternion.Euler(-8f, 0f, -14f), false);
            Part("DogChainR", Chest, link, m.Metal, new Vector3(0.022f, 0.33f, 0.112f), Quaternion.Euler(-8f, 0f, 14f), false);
            Part("DogTag", Chest, CharacterMeshes.RoundedBox("dogTag", Vector3.zero, new Vector3(0.022f, 0.036f, 0.003f)), m.Metal,
                new Vector3(0f, 0.282f, 0.121f), Quaternion.Euler(-8f, 0f, 0f), false);

            if (CurrentRole == TeamRole.Radioman)
            {
                // Yelek omzunda el telsizi cebi + kısa sabit anten (sırt anteni ayrıca sallanır).
                Part("RadioPouch", Chest, CharacterMeshes.RoundedBox("radioPouch", Vector3.zero, new Vector3(0.05f, 0.11f, 0.04f)), m.GearDark,
                    new Vector3(-0.17f, 0.2f, 0.1f), false);
                Part("RadioStubAntenna", Chest, CharacterMeshes.Cylinder("radioStubAntenna", 0f, 0.16f, 0.005f, 0.0025f, 4, false), BlackTrim,
                    new Vector3(-0.17f, 0.255f, 0.1f), Quaternion.Euler(-6f, 0f, 4f), false);
            }
        }

        /// <summary>
        /// Kask kamuflaj kılıfı ağı + lastik bant (yedek mühimmat/yaprak sıkıştırma halkaları).
        /// Adlar Helmet ile başlar: birleştirmeden muaf (kask devrilebilir).
        /// ENTEGRASYON: SoldierModel.cs EnsureHelmet içinde level >= 2 için AddHelmetCoverDetail(v) çağrılmalı.
        /// </summary>
        private void AddHelmetCoverDetail(Variant v)
        {
            var m = _mat;
            v.Objects.Add(Part("HelmetElastic", Head, CharacterMeshes.Cylinder("helmetElastic", 0.118f, 0.128f, 0.1075f, 0.1035f, 12, false, 1.2f),
                m.GearDark, new Vector3(0f, 0f, -0.01f), false));
            var loop = CharacterMeshes.Box("helmetLoop", Vector3.zero, new Vector3(0.012f, 0.012f, 0.006f));
            for (var i = 0; i < 4; i++)
            {
                var a = (-50f + i * 33f) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 0.1055f, 0.137f, Mathf.Cos(a) * 0.1305f - 0.01f);
                v.Objects.Add(Part("HelmetLoop" + i, Head, loop, m.Gloves, pos, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), false));
            }
        }

        /// <summary>Role özgü görsel işaretler: sıhhiyeci kırmızı haç armaları.</summary>
        private void BuildRoleDetail()
        {
            if (CurrentRole != TeamRole.Medic)
                return;

            var m = _mat;
            for (var i = 0; i < 2; i++)
            {
                var shoulder = i == 0 ? _leftShoulder : _rightShoulder;
                var side = i == 0 ? -1f : 1f;
                Part("MedicPatch", shoulder, CharacterMeshes.Box("medicPatch", Vector3.zero, new Vector3(0.008f, 0.062f, 0.062f)), m.White,
                    new Vector3(side * 0.0515f, -0.215f, 0f), false);
                Part("MedicCrossV", shoulder, CharacterMeshes.Box("medicCrossV", Vector3.zero, new Vector3(0.009f, 0.044f, 0.012f)), m.Red,
                    new Vector3(side * 0.0525f, -0.215f, 0f), false);
                Part("MedicCrossH", shoulder, CharacterMeshes.Box("medicCrossH", Vector3.zero, new Vector3(0.009f, 0.012f, 0.044f)), m.Red,
                    new Vector3(side * 0.0525f, -0.215f, 0f), false);
            }
        }

        /// <summary>Plaka taşıyıcı yan cepleri + bel faydalı cebi.</summary>
        private void AddCarrierPouches(Variant v)
        {
            var m = _mat;
            var side = CharacterMeshes.RoundedBox("sidePouchRounded", Vector3.zero, new Vector3(0.05f, 0.09f, 0.09f));
            v.Objects.Add(Part("SidePouchL", Chest, side, m.GearDark, new Vector3(-0.228f, 0.01f, 0.01f), false));
            v.Objects.Add(Part("SidePouchR", Chest, side, m.GearDark, new Vector3(0.228f, 0.01f, 0.01f), false));
            v.Objects.Add(Part("UtilityPouch", _spine, CharacterMeshes.RoundedBox("utilityPouchRounded", Vector3.zero, new Vector3(0.12f, 0.07f, 0.04f)), m.GearDark,
                new Vector3(0.05f, 0.06f, 0.128f), false));
            v.Objects.Add(Part("CarrierBadge", Chest, CharacterMeshes.Box("carrierBadge", Vector3.zero, new Vector3(0.1f, 0.03f, 0.008f)), m.Camo,
                new Vector3(0f, 0.255f, 0.152f), false));
        }

        /// <summary>Kaska bağlı gece görüş cihazı (yukarı kalkık konumda): gövde, iki tüp ve karşı ağırlık.</summary>
        private void AddNvg(Variant v)
        {
            var m = _mat;
            v.Objects.Add(Part("NvgBody", Head, CharacterMeshes.Box("nvgBody", Vector3.zero, new Vector3(0.1f, 0.045f, 0.05f)), m.GearDark,
                new Vector3(0f, 0.2f, 0.1f), Quaternion.Euler(-70f, 0f, 0f), false));
            var tube = CharacterMeshes.Cylinder("nvgTube", -0.03f, 0.03f, 0.016f, 0.016f, 6, true);
            v.Objects.Add(Part("NvgTubeL", Head, tube, m.NvgLens, new Vector3(-0.025f, 0.228f, 0.124f), Quaternion.Euler(20f, 0f, 0f), false));
            v.Objects.Add(Part("NvgTubeR", Head, tube, m.NvgLens, new Vector3(0.025f, 0.228f, 0.124f), Quaternion.Euler(20f, 0f, 0f), false));
            v.Objects.Add(Part("NvgBattery", Head, CharacterMeshes.Box("nvgBattery", Vector3.zero, new Vector3(0.06f, 0.05f, 0.035f)), m.GearDark,
                new Vector3(0f, 0.14f, -0.135f), false));
        }

        /// <summary>Çanta varyantı eklentileri: matara, rol eşyaları (telsiz + uzun anten, medik çantası haçı).</summary>
        private void AddBackpackDetail(Variant v, int level)
        {
            var m = _mat;
            var root = _backpackRoot;
            var backZ = SoldierDetailRules.BackpackBackZ(level);

            if (level >= 1)
            {
                var bottle = CharacterMeshes.Cylinder("bottle", -0.1f, 0.1f, 0.036f, 0.034f, 8, true);
                v.Objects.Add(Part("Bottle", root, bottle, m.GearDark, new Vector3(-0.145f - level * 0.015f, 0.02f, backZ + 0.07f), false));
            }

            if (level >= 3)
            {
                v.Objects.Add(Part("MatRoll", root, CharacterMeshes.Cylinder("matRoll", -0.15f, 0.15f, 0.05f, 0.05f, 8, true), m.GearDark,
                    new Vector3(0f, -0.15f, backZ + 0.03f), Quaternion.Euler(0f, 0f, 90f), false));
            }

            var role = CurrentRole;
            if (role == TeamRole.Radioman)
            {
                v.Objects.Add(Part("RadioUnit", root, CharacterMeshes.Box("radioUnit", Vector3.zero, new Vector3(0.14f, 0.2f, 0.05f)), m.GearDark,
                    new Vector3(0f, 0.12f, backZ - 0.025f), false));
                v.Objects.Add(Part("RadioKnob", root, CharacterMeshes.Cylinder("radioKnob", 0f, 0.02f, 0.014f, 0.014f, 6, true), m.Metal,
                    new Vector3(0.04f, 0.2f, backZ - 0.055f), Quaternion.Euler(-90f, 0f, 0f), false));

                var pivot = Bone("AntennaPivot", root, new Vector3(0.05f, 0.21f, backZ - 0.03f));
                _antennas[level] = pivot;
                var whip = Part("WhipAntenna", pivot, CharacterMeshes.Cylinder("whipAntenna", 0f, 0.42f, 0.006f, 0.0025f, 4, false), BlackTrim,
                    Vector3.zero, false);
                whip.transform.SetParent(pivot, false);
                v.Objects.Add(pivot.gameObject);
            }
            else if (role == TeamRole.Medic)
            {
                v.Objects.Add(Part("MedicBagWhite", root, CharacterMeshes.Box("medicBagWhite", Vector3.zero, new Vector3(0.11f, 0.11f, 0.008f)), m.White,
                    new Vector3(0f, 0.12f, backZ - 0.004f), false));
                v.Objects.Add(Part("MedicBagCrossV", root, CharacterMeshes.Box("medicBagCrossV", Vector3.zero, new Vector3(0.026f, 0.085f, 0.008f)), m.Red,
                    new Vector3(0f, 0.12f, backZ - 0.009f), false));
                v.Objects.Add(Part("MedicBagCrossH", root, CharacterMeshes.Box("medicBagCrossH", Vector3.zero, new Vector3(0.085f, 0.026f, 0.008f)), m.Red,
                    new Vector3(0f, 0.12f, backZ - 0.009f), false));
            }
        }

        /// <summary>Küçük, koyu, tamamen mat göz: parlak beyaz + gökyüzü yansıması mavi parıltı yapıyordu; emisyon yok.</summary>
        private static Material EyeMaterial => MaterialLibrary.Lit(new Color(0.24f, 0.22f, 0.2f), 0.02f);

        private void BuildEyes()
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var position = new Vector3(side * 0.032f, 0.105f, 0.082f);
                Part("Eyelid", Head, CharacterMeshes.Ellipsoid("eyelidSculpt", Vector3.zero,
                    new Vector3(0.017f, 0.0075f, 0.0065f), 16, 6), _mat.Skin, position, false);
                Part("Eye", Head, CharacterMeshes.Ellipsoid("eyeSculpt", Vector3.zero,
                    new Vector3(0.0095f, 0.0028f, 0.003f), 16, 6), EyeMaterial,
                    position + new Vector3(0f, 0f, 0.005f), false);
                Part("Iris", Head, CharacterMeshes.Ellipsoid("irisSculpt", Vector3.zero,
                    new Vector3(0.0038f, 0.0035f, 0.0012f), 12, 6), _mat.Hair,
                    position + new Vector3(0f, 0f, 0.008f), false);
                Part("Brow", Head, CharacterMeshes.Ellipsoid("browSculpt", Vector3.zero,
                    new Vector3(0.026f, 0.0048f, 0.0032f), 16, 4), _mat.Hair,
                    new Vector3(side * 0.032f, 0.124f, 0.090f), Quaternion.Euler(0f, side * 12f, side * -6f), false);
            }
        }

        private void AddCarrierWebbing(Variant variant, bool heavy)
        {
            var z = heavy ? 0.158f : 0.153f;
            // Nokta ızgarası (MOLLE) yerine düz telsiz/idari cep + kapak.
            variant.Objects.Add(Part("AdminPouch", Chest, CharacterMeshes.RoundedBox("adminPouchRounded", Vector3.zero, new Vector3(0.15f, 0.075f, 0.022f)),
                _mat.GearDark, new Vector3(0f, 0.215f, z + 0.004f), false));
            variant.Objects.Add(Part("AdminFlap", Chest, CharacterMeshes.Box("adminFlap", Vector3.zero, new Vector3(0.154f, 0.02f, 0.026f)),
                _mat.Gear, new Vector3(0f, 0.245f, z + 0.005f), false));
            variant.Objects.Add(Part("CarrierHandle", Chest, CharacterMeshes.RoundedBox("carrierHandleSculpt", Vector3.zero,
                new Vector3(0.115f, 0.018f, 0.022f)), _mat.GearDark, new Vector3(0f, 0.285f, -0.162f), false));
            // MOLLE şerit hatları: admin cebi ile şarjör cepleri arası ve şarjör cepleri altında yatay koyu kayış sırası.
            var molle = CharacterMaterials.Solid(CharacterMaterialKind.Cordura, _look.Gear * 0.62f) ?? _mat.GearDark;
            var strap = CharacterMeshes.Box("molleStrap", Vector3.zero, new Vector3(0.2f, 0.007f, 0.006f));
            var rows = new[] { 0.142f, 0.156f, 0.17f, -0.005f, -0.02f };
            for (var i = 0; i < rows.Length; i++)
                variant.Objects.Add(Part("MolleStrap", Chest, strap, molle, new Vector3(0f, rows[i], z - 0.004f), false));
            for (var side = -1; side <= 1; side += 2)
            {
                variant.Objects.Add(Part("ShoulderBuckle", Chest, CharacterMeshes.RoundedBox("carrierBuckleSculpt", Vector3.zero,
                    new Vector3(0.047f, 0.033f, 0.012f)), BlackTrim, new Vector3(side * 0.115f, 0.259f, 0.130f), false));
            }
        }

        private void UpdateAntennaMotion(float tilt, float roll)
        {
            for (var i = 0; i < _antennas.Length; i++)
            {
                if (_antennas[i] != null)
                    _antennas[i].localRotation = Quaternion.Euler(tilt, 0f, roll);
            }
        }
    }
}
