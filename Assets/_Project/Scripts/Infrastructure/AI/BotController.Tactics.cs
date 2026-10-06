using System;
using System.Collections.Generic;
using Project.Application.AI;
using Project.Application.Combat.Suppression;
using Project.Application.Dialogue;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Dialogue;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Disiplinli asker davranışı (yalnızca otorite): beceri kademesi, baskı (suppression), siperden çık-ateş-et-saklan döngüsü,
    /// sıçramalı ilerleme, sayıca üstünlükte geri çekilme, baskıda kanat, tim telsiz çağrıları.
    /// Saf kurallar <see cref="BotSkill"/>, <see cref="BotSuppression"/>, <see cref="BotCombatRules"/>, <see cref="BotSquadTactics"/>;
    /// burada yalnızca durum ve bağlantı bulunur. Kare başına bellek ayırmaz; pahalı işler (siper araması) beyin zamanlayıcılarına bağlı.
    /// </summary>
    public sealed partial class BotController
    {
        private const float CalloutBotCooldown = 2.2f;
        private const float CalloutTeamCooldown = 1.0f;
        private const float NearbyAllyRadius = 30f;

        private static readonly Dictionary<int, float> TeamCalloutUntil = new(16);

        // beceri / baskı
        private float _skill01 = 0.5f;
        private BotSuppression _suppression;

        // siperden çık-ateş-et-saklan
        private bool _inCoverLatched;
        private bool _coverHidden;
        private CoverPeekPhase _peekPhase;
        private float _peekPhaseEnd;
        private bool _hasCoverSpot;
        private BotTactics.CoverSpot _coverSpot;

        // sıkışma (baskı + temas)
        private float _pinnedSince = -1f;

        // hedef değiştirme / ilk atış
        private Combatant _triggerTarget;
        private float _triggerTargetSeen;
        private float _triggerReadyAt;
        private float _aimAcquiredAt;
        private int _burstShotIndex;

        // yan hareket
        private bool _allowCrawl;

        // telsiz
        private float _nextCalloutTime;
        private float _nextTakingFireCallout;

        /// <summary>Beceri 0 (acemi) .. 1 (seçkin): zorluk + rütbe + bireysel fark.</summary>
        public float Skill01 => _skill01;

        public BotSkillTier SkillTier => BotSkill.TierOf(_skill01);

        /// <summary>Anlık baskı düzeyi 0..1 (ham; beceriyle azalan hali <see cref="EffectiveSuppression"/>).</summary>
        public float SuppressionLevel => _suppression.Level;

        private float EffectiveSuppression => BotSkill.EffectiveSuppression(_suppression.Level, _skill01);

        private float PinnedSeconds => _pinnedSince < 0f ? 0f : Time.time - _pinnedSince;

        /// <summary>Kurulumda: rütbe + zorluk + bireysel farktan beceri ve zorluğa bağlı tepki süresi (Kolay 0.35–0.9 sn, Zor 0.18–0.45 sn).</summary>
        private void ConfigureSkill(MilitaryRank rank)
        {
            _skill01 = BotSkill.Skill01(Profile.Difficulty, rank, Rand());
            Profile.ReactionSeconds = BotSkill.ReactionSeconds(Profile.Difficulty, _skill01, Rand());
        }

        // ------------------------------------------------------------------ her kare

        private void TickSuppression(float dt, float now)
        {
            _suppression.Tick(dt);

            var pending = _perception.ConsumeSuppression();
            if (pending > 0f)
                _suppression.Add(pending);

            var supp = EffectiveSuppression;
            if (supp > 0.5f && now >= _nextTakingFireCallout)
            {
                _nextTakingFireCallout = now + 9f;
                Callout(DialogueCats.TakingFire);
            }

            var hasTarget = _perception.Target != null;
            var sprinting = _wantsMove && _moveSpeed >= MoveSpeed.Run;
            if (hasTarget && supp > 0.3f && !sprinting)
            {
                if (_pinnedSince < 0f)
                    _pinnedSince = now;
            }
            else if (supp < 0.12f || (!hasTarget && now - _perception.LastSeenTime > 4f))
            {
                _pinnedSince = -1f;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookFlashbang()
        {
            // Aynı abonelik tekrarlanmasın.
            FlashbangSystem.BotFlashed -= OnBotFlashed;
            FlashbangSystem.BotFlashed += OnBotFlashed;
        }

        private static void OnBotFlashed(Combatant victim, float seconds, float intensity)
        {
            var bots = AllBots;
            for (var i = 0; i < bots.Count; i++)
                if (bots[i] != null && bots[i].Combatant == victim)
                {
                    bots[i].ReceiveFlash(seconds, intensity);
                    return;
                }
        }

        /// <summary>Flaş bombası: görüş/nişan bozulur — kör süresi ve şiddetle orantılı tam baskı (nişan dağılır, kısa seri, siper arar).</summary>
        public void ReceiveFlash(float seconds, float intensity)
        {
            if (Combatant == null || !Combatant.IsAlive)
                return;
            _suppression.Add(Mathf.Clamp01(intensity) * Mathf.Clamp(0.5f + seconds * 0.12f, 0.5f, 1f));
        }

        /// <summary>Hasar alındı: baskı biriktir.</summary>
        private void AddDamageSuppression()
        {
            _suppression.Add(BotSuppression.DamageImpulse);
        }

        /// <summary>Oyuncu mermisi yakından geçti (BallisticsSystem): kalibreye göre tam baskı. Ölü/başlatılmamış botlarda etkisiz.</summary>
        public void ReceiveNearMiss(float missDistance, WeaponDefinitionData definition)
        {
            if (!_initialized || _dead)
                return;

            var caliber = 1f;
            if (definition != null)
            {
                switch (definition.Category)
                {
                    case WeaponCategory.Lmg:
                    case WeaponCategory.Sniper:
                    case WeaponCategory.Dmr:
                        caliber = 1.4f;
                        break;
                    case WeaponCategory.Pistol:
                    case WeaponCategory.Smg:
                        caliber = 0.7f;
                        break;
                }
            }

            _suppression.Add(BotSuppression.NearMissAmount(missDistance, caliber));
        }

        /// <summary>Atış ışınının yakınından geçtiği düşman botları bastır (isabet olmadan bile yıldırma).</summary>
        private void SuppressAlongShot(Vector3 origin, Vector3 direction, WeaponDefinitionData definition)
        {
            var range = definition != null && definition.Range > 1f ? Mathf.Min(definition.Range, 160f) : 120f;
            var caliber = 1f;
            if (definition != null)
            {
                switch (definition.Category)
                {
                    case WeaponCategory.Lmg:
                    case WeaponCategory.Sniper:
                    case WeaponCategory.Dmr:
                        caliber = 1.4f;
                        break;
                    case WeaponCategory.Pistol:
                    case WeaponCategory.Smg:
                        caliber = 0.7f;
                        break;
                }
            }

            for (var i = 0; i < AllBots.Count; i++)
            {
                var other = AllBots[i];
                if (other == null || ReferenceEquals(other, this) || other._dead || !other._initialized || other._team == _team)
                    continue;

                var to = other.transform.position + Vector3.up * 1.1f - origin;
                var along = Vector3.Dot(to, direction);
                if (along < 2f || along > range)
                    continue;

                var perpendicular = (to - direction * along).magnitude;
                var amount = BotSuppression.NearMissAmount(perpendicular, caliber);
                if (amount > 0f)
                    other._suppression.Add(amount);
            }
        }

        // ------------------------------------------------------------------ nişan / tetik yardımcıları

        /// <summary>Hedefe yerleşme çarpanı (zamanla yakınsar) × baskı.</summary>
        private float AimSettleFactor(float now)
        {
            var settle = BotSkill.AimSettle(now - _aimAcquiredAt, _skill01);
            return settle * BotSkill.SuppressedAimMultiplier(EffectiveSuppression);
        }

        /// <summary>Hedef değişimi/ilk edinme gecikmesi; tetik bu zamandan önce çekilmez.</summary>
        private bool TriggerDelayActive(Combatant target, float now)
        {
            if (!ReferenceEquals(target, _triggerTarget))
            {
                var hadPrevious = _triggerTarget != null && now - _triggerTargetSeen < 2.5f;
                if (hadPrevious)
                {
                    var angle = Mathf.Min(180f, _aimOffYaw);
                    var delay = BotSkill.TargetSwitchDelay(_skill01, angle, !_triggerTarget.IsAlive, Rand());
                    _triggerReadyAt = Mathf.Max(_triggerReadyAt, now + delay);
                }

                _triggerTarget = target;
            }

            _triggerTargetSeen = now;
            return now < _triggerReadyAt;
        }

        /// <summary>Atış başına geri tepme telafisi (kusurlu: rastgele + seri uzadıkça + baskıda bozulur).</summary>
        private float RecoilControlNow()
        {
            return BotSkill.RecoilControl(_skill01, EffectiveSuppression, Rand(), _burstShotIndex);
        }

        // ------------------------------------------------------------------ siper: çık-ateş et-saklan

        /// <summary>Siper noktasında: saklan (çömel, şarjör doldur) → başı çıkar (ayağa kalk / köşeye yaslan) → ateş → saklan.</summary>
        private void RunCoverPeek(float now, WeaponRuntimeService weapon)
        {
            var supp = EffectiveSuppression;
            var health = HealthFraction;
            var position = transform.position;
            var peekCorner = _hasCoverSpot && _coverSpot.High && _coverSpot.HasPeek;

            if (_peekPhaseEnd <= 0f)
            {
                _peekPhase = CoverPeekPhase.Hidden;
                _peekPhaseEnd = now + BotCombatRules.HiddenSeconds(supp, _skill01, Rand()) * 0.5f;
            }

            if (BotCombatRules.PhaseElapsed(now, _peekPhaseEnd))
            {
                if (_peekPhase == CoverPeekPhase.Hidden)
                {
                    if (Rand() < BotCombatRules.PeekWillingness(supp, _skill01, health))
                    {
                        _peekPhase = CoverPeekPhase.Peek;
                        _peekPhaseEnd = now + BotCombatRules.PeekSeconds(supp, _skill01, Rand());
                    }
                    else
                    {
                        _peekPhaseEnd = now + BotCombatRules.HiddenSeconds(supp, _skill01, Rand());
                    }
                }
                else
                {
                    _peekPhase = CoverPeekPhase.Hidden;
                    _peekPhaseEnd = now + BotCombatRules.HiddenSeconds(supp, _skill01, Rand());
                }
            }

            if (_peekPhase == CoverPeekPhase.Peek && weapon != null && (weapon.IsReloading || weapon.CurrentAmmo <= 0))
            {
                // Boş/dolduran silahla başını çıkarma: saklan.
                _peekPhase = CoverPeekPhase.Hidden;
                _peekPhaseEnd = now + 0.8f;
            }

            if (_peekPhase == CoverPeekPhase.Hidden)
            {
                if (Model != null) Model.SetLean(0f);
                _coverHidden = true;
                _desiredStance = supp > 0.7f && !(_hasCoverSpot && _coverSpot.High) ? Stance.Prone : Stance.Crouching;
                if (FlatDistance(position, _tacticPoint) > 0.7f && !_moveFailed)
                    MoveTo(_tacticPoint, MoveSpeed.Walk, 0.4f);
                else
                    StopMoving();

                if (weapon != null && !weapon.IsReloading && weapon.CanReload && weapon.CurrentAmmo < Mathf.CeilToInt(weapon.MagazineSize * 0.5f))
                {
                    var itemUse = Combatant.ItemUse;
                    if (itemUse == null || !itemUse.IsUsing)
                        weapon.TryBeginReload();
                }

                return;
            }

            _coverHidden = false;
            _desiredStance = Stance.Standing;
            if (Model != null)
            {
                // Köşeden kafa uzatma: yaslanma yönü = köşe noktasının bot sağına göre tarafı (yakınsadıkça artar).
                var lean = 0f;
                if (peekCorner)
                {
                    var toCorner = _coverSpot.PeekPoint - position;
                    toCorner.y = 0f;
                    var side = Vector3.Dot(toCorner, transform.right);
                    lean = Mathf.Clamp(side * 2f, -1f, 1f);
                    if (Mathf.Abs(side) < 0.05f)
                        lean = Mathf.Sign(side == 0f ? 1f : side) * 0.8f; // köşede: tam yaslan
                }

                Model.SetLean(lean);
            }

            if (peekCorner && FlatDistance(position, _coverSpot.PeekPoint) > 0.45f && !_moveFailed)
                MoveTo(_coverSpot.PeekPoint, MoveSpeed.Walk, 0.3f);
            else
                StopMoving();
        }

        /// <summary>Siper bulunduğunda sürekli kullanılacak durum sıfırlama.</summary>
        private void BeginCover(in BotTactics.CoverSpot spot)
        {
            _hasCoverSpot = true;
            _coverSpot = spot;
            _inCoverLatched = false;
            _coverHidden = false;
            _peekPhase = CoverPeekPhase.Hidden;
            _peekPhaseEnd = 0f;
        }

        private void ClearCover()
        {
            if (Model != null) Model.SetLean(0f);
            _hasCoverSpot = false;
            _inCoverLatched = false;
            _coverHidden = false;
            _peekPhaseEnd = 0f;
            _allowCrawl = false;
        }

        /// <summary>Tehdit bilgisinden skorlu siper sorgusu; ikinci tehdit (son bilinen / duyulan) varsa gizlilik ikiside aranır.</summary>
        private bool FindCoverSpot(Vector3 origin, Vector3 threatEye, float radius, WeaponRuntimeService weapon, out BotTactics.CoverSpot spot)
        {
            var query = new BotTactics.CoverQuery
            {
                Self = origin,
                Threat = threatEye,
                Radius = radius,
                UseNavMesh = _useNavMesh,
                PreferredRange = weapon != null ? BotTactics.PreferredRange(weapon.Definition) : 30f,
                Suppression = EffectiveSuppression
            };

            var leader = _leader;
            if (leader != null && leader.IsAlive && !ReferenceEquals(leader, Combatant))
            {
                query.HasAllyAnchor = true;
                query.AllyAnchor = leader.transform.position;
            }

            // Duyulan ikinci yön: görüşteki hedefle belirgin farklı bir yönden gelen ses/hasar kaynağı.
            if (_perception.DamageSourceKnown && Time.time - _perception.LastDamagedTime < 4f)
            {
                var src = _perception.DamageSourcePosition + Vector3.up * 1.6f;
                var a = threatEye - query.Self;
                var b = src - query.Self;
                a.y = 0f;
                b.y = 0f;
                if (a.sqrMagnitude > 4f && b.sqrMagnitude > 4f && Vector3.Angle(a, b) > 35f)
                {
                    query.HasSecondary = true;
                    query.Secondary = src;
                }
            }

            return BotTactics.TryFindCoverScored(in query, out spot);
        }

        // ------------------------------------------------------------------ takım arkadaşı gerçekçiliği (G31 kuralları)

        /// <summary>Bastırma siper eşiği: SuppressionState.ShouldSeekCover ile aynı (SuppressionConfig.NpcCoverThreshold).</summary>
        private static readonly float TeammateCoverThreshold = new SuppressionConfig().NpcCoverThreshold;

        private bool _calloutWasMoving;
        private float _calloutLastContact = -99f;
        private float _nextMagCallout;

        /// <summary>Ateş altında mı (SuppressionState.ShouldSeekCover eşdeğeri).</summary>
        private bool ShouldSeekCoverUnderFire => EffectiveSuppression >= TeammateCoverThreshold;

        /// <summary>
        /// Halka adaylarından TeammateCoverScoring ile en iyi siper (BotTactics bulamazsa yedek). Yükseklik, tehdide doğru
        /// ışınla kestirilir (3 kademe); maliyet sınırlı: 8 aday.
        /// </summary>
        private bool TryTeammateCover(Vector3 origin, Vector3 threatEye, out Vector3 point)
        {
            point = origin;
            var cands = new TeammateCoverScoring.Candidate[8];
            var pts = new Vector3[8];
            var n = 0;
            for (var i = 0; i < 8; i++)
            {
                var ang = i * 45f * Mathf.Deg2Rad;
                var raw = origin + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (i % 2 == 0 ? 6f : 11f);
                if (!SampleDestination(raw, 3f, out var p))
                    continue;

                var toThreat = threatEye - p;
                toThreat.y = 0f;
                if (toThreat.sqrMagnitude < 4f)
                    continue;

                var dir = toThreat.normalized;
                var h = 0f;
                var heights = new[] { 0.5f, 1.1f, 1.8f };
                for (var k = 0; k < heights.Length; k++)
                {
                    if (Physics.Raycast(p + Vector3.up * heights[k], dir, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                        h = heights[k] + 0.1f;
                }

                pts[n] = p;
                cands[n++] = new TeammateCoverScoring.Candidate(new Float3(p.x, p.y, p.z), h);
            }

            var self = new Float3(origin.x, origin.y, origin.z);
            var best = TeammateCoverScoring.PickBest(self, new Float3(threatEye.x, threatEye.y, threatEye.z), cands, n);
            if (best < 0)
                return false;

            point = pts[best];
            return true;
        }

        /// <summary>Yaralı dosta yardım kararı (TeammateAid); true ise bu karede siper/örtme kararı verildi.</summary>
        private AidAction DecideTeammateAid(bool enemyKnown)
        {
            var me = Combatant;
            if (me == null || me.IsDowned)
                return AidAction.None;

            var wounded = FindDownedAlly(me);
            if (wounded == null)
                return AidAction.None;

            var p = transform.position;
            var w = wounded.transform.position;
            var enemyNear = enemyKnown && _perception.Target != null && FlatDistance(_perception.Target.transform.position, w) < 25f;
            return TeammateAid.Decide(new Float3(p.x, p.y, p.z), new Float3(w.x, w.y, w.z), true, true,
                ShouldSeekCoverUnderFire, enemyNear, HealthFraction);
        }

        /// <summary>Şarjör ve ilerleme bildirimleri (TeammateCallouts -> mevcut telsiz/diyalog hattı).</summary>
        private void TickTeammateCallouts(float now, WeaponRuntimeService weapon, bool enemyKnown)
        {
            if (enemyKnown)
                _calloutLastContact = now;

            if (weapon != null && now >= _nextMagCallout)
            {
                var kind = TeammateCallouts.MagStatus(weapon.CurrentAmmo, weapon.MagazineSize, 1);
                if (kind != CalloutKind.None)
                {
                    _nextMagCallout = now + 15f;
                    Callout(TeammateCallouts.Category(kind));
                }
            }

            var moving = _moveSpeedSqr() > 1f;
            var progress = TeammateCallouts.Progress(_calloutWasMoving, moving, enemyKnown, now - _calloutLastContact);
            _calloutWasMoving = moving;
            if (progress == CalloutKind.Advancing || progress == CalloutKind.Clear)
                Callout(TeammateCallouts.Category(progress));
        }

        private float _moveSpeedSqr()
        {
            var v = Combatant != null ? Combatant.Velocity : Vector3.zero;
            return v.x * v.x + v.z * v.z;
        }

        // ------------------------------------------------------------------ takım durumu

        /// <summary>Yakın (30 m) canlı tim arkadaşı sayısı (kendisi hariç).</summary>
        private int NearbyAllyCount(float radius = NearbyAllyRadius)
        {
            var sqr = radius * radius;
            var position = transform.position;
            var count = 0;
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || ReferenceEquals(c, Combatant) || !c.IsAlive || c.IsDowned || c.Team != _team)
                    continue;

                if ((c.transform.position - position).sqrMagnitude <= sqr)
                    count++;
            }

            return count;
        }

        // ------------------------------------------------------------------ telsiz çağrıları

        /// <summary>Kategoriden çağrı (DialogueDirector); bot başına ve tim başına bekleme uygulanır. Başarısız olursa sessizce geçer.</summary>
        private void Callout(string category)
        {
            var now = Time.time;
            if (now < _nextCalloutTime || Combatant == null || !Combatant.IsAlive)
                return;

            if (TeamCalloutUntil.TryGetValue(_team, out var until) && now < until)
                return;

            _nextCalloutTime = now + CalloutBotCooldown;
            TeamCalloutUntil[_team] = now + CalloutTeamCooldown;
            try
            {
                DialogueDirector.Say(Combatant, category);
            }
            catch (Exception)
            {
                // diyalog süs özelliğidir
            }
        }

        /// <summary>Yeni düşman teması: yön + mesafe bildirimi (saat yönü cümlesi).</summary>
        private void CalloutContact(Vector3 enemyPosition)
        {
            var now = Time.time;
            if (now < _nextCalloutTime || Combatant == null || !Combatant.IsAlive)
                return;

            if (TeamCalloutUntil.TryGetValue(_team, out var until) && now < until)
                return;

            _nextCalloutTime = now + CalloutBotCooldown;
            TeamCalloutUntil[_team] = now + 3f;
            try
            {
                DialogueDirector.SaySpotted(Combatant, enemyPosition);
            }
            catch (Exception)
            {
                // diyalog süs özelliğidir
            }
        }

        private static void ResetTacticsStatics()
        {
            TeamCalloutUntil.Clear();
        }
    }
}
