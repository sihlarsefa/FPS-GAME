# HAREKÂT — Modül Spesifikasyonları (25 modül)

Her modülün ayrıntılı görev tanımı. Ortak kurallar ve API sözleşmeleri [CONTRACTS.md](CONTRACTS.md) dosyasında, sahiplik ve anlık durum [DURUM.md](DURUM.md) dosyasında.
Yarım kalan bir modülü tamamlayacak her ajan (Claude, Cursor, Codex) bu dosyayı kaynak olarak kullanır.

## app-weapons  — _TAMAMLANDI (Claude)_

```text
Application weapons & damage. Own: Application/Catalogs/WeaponCatalog.cs, WeaponIds.cs (read-only ids already set),
Application/Services/DamageCalculator.cs, WeaponRuntimeService.cs, CombatService.cs, HealthService.cs, BasicWeaponService.cs (legacy, keep compiling),
Tests: Assets/_Project/Tests/EditMode/WeaponTests.cs, CombatTests.cs.
- WeaponCatalog: 10 Turkish weapons with full WeaponDefinitionData (DisplayName e.g. "MPT-76", AmmoType, damage, mag, fire interval,
  reload, MuzzleVelocity, recoil, Hip/Ads spread, bloom, AdsZoom, HasScope (JNG-90 6x, KNT-76 3x), FireModes, falloff, EquipSeconds,
  IsBoltAction for JNG-90 (interval ~1.4s), Escort PelletCount 9). Balance: SAR9 dmg 28 mag 15; TP9 dmg 26 mag 18; SAR109T 22 dmg 30 mag 800rpm;
  MPT-55 26 dmg 30 mag 750rpm; MPT-76 36 dmg 20 mag 600rpm; G3A7 38 dmg 20 mag 550rpm; KNT-76 52 dmg 10 mag semi; JNG-90 90 dmg 5 mag head x2.5;
  PMT-76 34 dmg 100 mag 650rpm reload 6s; Escort 20x9 pellets mag 7. GetDisplayName also maps DamageSourceIds (Zone "Harekât Sınırı", Fall "Düşme",
  FragGrenade "El Bombası", Fists "Yumruk", Artillery "Topçu Ateşi", Vehicle "Araç").
- WeaponRuntimeService: TryTrigger handles Single (needs pressed edge), Burst (BurstCount shots at FireInterval), Auto (held); fire cooldown;
  bolt action; ammo; bloom grows BloomPerShot (max MaxBloom) and decays ~4 deg/s; GetSpreadAngle = (aim?Ads:Hip) * stance(crouch .8, prone .6)
  * move(1+1.5*speed) * (grounded?1:2.5) + bloom (aim halves bloom); reload: TryBeginReload requires reserve>0 or AmmoSource null (infinite);
  publishes WeaponReloadStartedEvent(OwnerId,...), on finish takes ammo via AmmoSource.TakeAmmo and publishes WeaponReloadedEvent; CancelReload;
  CycleFireMode cycles Definition.FireModes; TryFire(out DamageInfo) for IWeapon compat; SetLoadedAmmo clamps.
- DamageCalculator per CONTRACTS; ComputeFallDamage: 0 below 12 m/s then (v-12)*7.5.
- CombatService: friendly fire blocked when !friendlyFire && teams.AreAllies(attacker, victim) && attacker!=victim. Target lookup via
  IDamageableRegistry; armor via IArmored.Armor.GetArmorFor(part) using DamageCalculator; apply DamageInfo with BodyPart + source pos;
  publish HitConfirmedEvent (IsKill when target died). Explosion: vest at 50% effectiveness, helmet ignored, part=Torso.
- HealthService: ApplyDamage clamps, publishes PlayerDamagedEvent full ctor; on death publishes PlayerDiedEvent(victim, attacker, weaponId, headshot)
  exactly once, raises Damaged/Died. HealCapped, ResetToFull.
- Write thorough EditMode tests (fire modes, reload with reserve, spread, armor, friendly fire, death once).
```

## app-items  — _TAMAMLANDI (Claude)_

```text
Application items/inventory. Own: Application/Catalogs/ItemCatalog.cs, ItemIds.cs (ids set), LoadoutCatalog.cs,
Application/Services/InventoryService.cs, LootSpawnService.cs, LootInteractionService.cs (keep compiling), ItemUseService.cs, BoostService.cs,
Tests: Tests/EditMode/InventoryTests.cs, ItemUseTests.cs.
- ItemCatalog: Turkish names ("9mm Mermi","5.56 Mermi","7.62 Mermi","12 Kalibre Fişek","Sargı Bezi"(+10 cap75 4s),"İlk Yardım Çantası"(heal to 75, 6s),
  "Sıhhiye Çantası"(to 100, 8s),"Enerji İçeceği"(boost 40, 4s),"Ağrı Kesici"(boost 60, 6s),"El Bombası","Sis Bombası","Çelik Yelek (Sv.1/2/3)"
  (durability 200/220/250, reduction .30/.40/.55),"Kask (Sv.1/2/3)"(80/150/230, .30/.40/.55),"Sırt Çantası (Sv.1/2/3)"(capacity +150/+200/+250)),
  weights (9mm .375, 5.56 .5, 7.62 .7, 12g 1.25, bandage 2, firstaid 10, medkit 20, energy 4, painkiller 10, frag 12, smoke 14), PickupQuantity
  (ammo 30, 12g 10, bandage 5). Every WeaponCatalog weapon (use WeaponIds constants; WeaponCatalog may still be a skeleton — build item list from
  WeaponIds + names without calling WeaponCatalog at static init; or lazily) appears as Weapon-category item. Stable order for network indices.
- InventoryService exactly per skeleton doc (rules there). Changed event on every mutation. Weapons are WeaponRuntimeService instances created with
  the eventBus, OwnerId set, AmmoSource = this (or null when InfiniteAmmo). WantsItem heuristics for bots.
- LoadoutCatalog: per TeamRole loadouts with Turkish role names ("Tim Komutanı","Piyade","Keskin Nişancı","Makineli Tüfekçi","Sıhhiyeci","Telsizci","Bombacı"):
  Leader MPT-76+SAR9, vest2 helmet2 bag2; Marksman JNG-90 + SAR109T? (secondary allowed) ; MachineGunner PMT-76; Medic MPT-55 + 4 firstaid + 2 medkit;
  Radioman MPT-55; Grenadier G3A7 + 4 frag; Rifleman MPT-76 or MPT-55; everyone ammo (~150 rounds main), 3 bandages, 1 frag, 1 smoke, vest/helmet level 1-2.
  RoleForSlot: 0 Leader,1 Marksman,2 MachineGunner,3 Medic,4 Radioman,5 Grenadier,6-9 Rifleman.
- LootSpawnService: tiers Low/Medium/High/Military weighted tables (Military: more 7.62 rifles, KNT-76, JNG-90, PMT-76, level 2-3 armor);
  SpawnChance Low .45 Medium .6 High .75 Military .85; weapon → +2 ammo stacks.
- ItemUseService/BoostService per skeleton docs (boost decays 0.6/s; heal per sec: >0:1, >40:2, >80:3 ... reasonable; speed bonus >60: 1.04).
- Thorough EditMode tests.
```

## app-match  — _TAMAMLANDI (Claude)_

```text
Application match flow. Own: Application/Services/MatchService.cs, ZoneService.cs, MatchStatsService.cs, KillFeedService.cs,
DamageableRegistry.cs, Tests/EditMode/MatchTests.cs, ZoneTests.cs, StatsTests.cs.
- MatchService per skeleton doc, team-aware: RegisterCombatant(id,name,isLocal,team,role) stores display name (caller passes rank-formatted name),
  team names: 0 "Kartal Timi",1 "Bozkurt Timi",2 "Şimşek Timi",3 "Yıldırım Timi",4 "Kılıç Timi",5 "Kaplan Timi",6 "Pars Timi",7 "Atmaca Timi".
  Subscribes PlayerDiedEvent in ctor (unsubscribe in Dispose); ignores unknown/duplicate deaths; individual placement and team placement;
  ends when AliveTeamCount<=1 (publish MatchEndedEvent(winnerId, winnerTeam), transition Ending); MatchPhaseChangedEvent on every transition;
  Begin(): Lobby→PreMatch; Tick: PreMatch→Insertion after PreMatchDurationSeconds; NotifyDropComplete: Insertion→InMatch. MatchElapsedSeconds counts
  from Insertion start. Also ICombatantDirectory, ITeamRelations, GetTeamMembers.
- ZoneService per skeleton doc (seeded IRandom, new circle fully inside current and center within map bounds*0.8, linear interpolation while
  shrinking, ZoneStageChangedEvent, DistanceToSafeZone, damage only after Start and outside).
- MatchStatsService: team kills, survival, killer name, accuracy; BuildResult uses IMatchService + ITeamRelations (cast directory) for team fields.
- KillFeedService: entries with ally flags relative to local player's team (directory as ITeamRelations if available), weapon display via
  WeaponCatalog.GetDisplayName wrapped in try/catch fallback to id.
- DamageableRegistry: add IReadOnlyCollection<IDamageable> All (keep interface).
- Thorough EditMode tests (team elimination order, winner, zone containment over all phases, stats).
```

## app-sim  — _TAMAMLANDI (Claude)_

```text
Application simulation & command. Own: Application/Catalogs/RankCatalog.cs, NameRoster.cs; Application/Services/ChainOfCommandService.cs,
SquadOrderService.cs (contains ArtilleryService, InsertionPlanner, TeamInsertion, ArtilleryImpact), PlaneRouteService.cs, SkydiveSimulator.cs,
BotDecisionService.cs, BotDifficultyProfile.cs, SettingsService.cs (SettingsService+CareerStatsService), SeededRandom.cs, SimulationClock.cs,
Tests/EditMode/SimTests.cs, RankTests.cs, BotDecisionTests.cs.
- RankCatalog: Turkish rank names + abbreviations (Er, Onb., Çvş., Sözl.Er, Uzm.Onb., Uzm.Çvş., Astsb.Çvş., Astsb.Kd.Çvş., Astsb.Üçvş., Astsb.Kd.Üçvş.,
  Astsb.Bçvş., Astsb.Kd.Bçvş., Asteğmen, Teğmen, Üsteğmen, Yzb., Bnb., Yb., Alb.), categories ("Er/Erbaş","Uzman Erbaş","Astsubay","Subay"),
  XP thresholds (career progression Er→Yüzbaşı+), RankForTeamSlot (0 → Yüzbaşı or Üsteğmen; 1 → Astsubay Kıdemli Çavuş/Üstçavuş; 2-3 Uzman Çavuş;
  4-5 Uzman Onbaşı/Sözleşmeli Er; 6-9 Sözleşmeli Er/Er/Onbaşı/Çavuş), FormatName "Yzb. Kartal".
- ChainOfCommandService: ordering by rank desc then registration order; transfers on PlayerDiedEvent; CommandTransferredEvent.
- SquadOrderService, ArtilleryService (cooldown per team, DueImpacts scheduling with random spread, ArtilleryStrikeEvent on call and per impact),
  InsertionPlanner (teams spread on map edge sectors ~ evenly by angle; LZ 160-300 m inward from edge, kept inside |x|,|z| < 400; player team uses
  given method, others random/alternating), PlaneRouteService (route for helicopters: Start=edge point at altitude, End=LZ), SkydiveSimulator per doc.
- BotDecisionService per doc + squad: if IsSquadMember && HasSquadOrder: Hold → Hold (unless enemy visible → Engage), Attack → Assault toward target,
  Follow/Regroup → Follow when DistanceToLeader > 8 (else Idle/Roam near). Enemy visible always wins (armed). Zone escape beats orders when outside zone.
- BotDifficultyProfile.For: Easy (aim err 6°, reaction .9s), Normal (3.5°, .55s), Hard (1.8°, .3s), view 160/220/280 m, etc.
- SettingsService keys + clamping, Changed event; CareerStatsService XP = kills*100 + headshots*25 + max(0,(TeamCount - teamPlacement))*150 + win 1000,
  updates Rank via RankCatalog. NameRoster: 80+ Turkish callsigns/surnames (Kartal, Bozkurt, Atmaca, Şahin, Yılmaz, Demir, Çelik, Kaya, Aydın, Öztürk, ...).
- Thorough EditMode tests.
```

## infra-rendering  — _TAMAMLANDI (Claude)_

```text
Infrastructure/Rendering (MaterialId.cs exists, fixed). Own all of Infrastructure/Rendering/*: MaterialLibrary.cs, MaterialSpec.cs,
GameArtLibrary.cs, ProceduralTextures.cs, CameraRig.cs, PostProcessing.cs (+ any new file there).
- MaterialLibrary: spec table for EVERY MaterialId (color, smoothness, metallic, transparency/unlit/emissive/additive, texture key such as DigitalCamo,
  TurkishFlag, Noise); Get() uses GameArtLibrary.Load() materials when present else creates from spec (cache). URP Lit properties: _BaseColor,
  _BaseMap, _Smoothness, _Metallic, _EmissionColor+_EMISSION, transparent: _Surface=1, _Blend=0, _SrcBlend/_DstBlend, _ZWrite=0, keyword
  _SURFACE_TYPE_TRANSPARENT, renderQueue 3000, _Cull=0 for double sided (verify names in URP shader sources under BuiltInPackages).
  Particles: "Universal Render Pipeline/Particles/Unlit" with soft circle texture, additive vs alpha blend.
- ProceduralTextures: SoftCircle, Circle, Ring, Triangle, WhitePixel, TurkishFlag (red #E30A17 with correct white crescent & star geometry),
  DigitalCamo(4 colors pixel pattern), Noise, ToSprite. Cache all.
- CameraRig: world camera (far clip 1500, URP renderPostProcessing true, AA FXAA/SMAA) + overlay viewmodel camera on Viewmodel layer
  (near .01, fov 60 fixed, cameraStack add, renderType Overlay), world camera culls Viewmodel layer; AudioListener on world camera. Handles the case
  where URP is not active (fallback: viewmodel camera with higher depth + clear depth).
- PostProcessing: global Volume with runtime VolumeProfile (Tonemapping ACES, Bloom, ColorAdjustments slightly desaturated military grade,
  Vignette), Menu look warmer; ApplyQuality(level) sets QualitySettings level if available + shadow distance.
- Also a RenderSettingsUtil (fog color/density, ambient, procedural skybox material) used by bootstraps: static void ApplyOutdoorAtmosphere(bool menu).
```

## infra-audio  — _Claude — devam ediyor_

```text
Infrastructure/Audio (SoundId.cs exists, fixed). Own Infrastructure/Audio/* : GameAudio.cs, ProceduralAudioSynth.cs, AudioPool.cs, FootstepEmitter.cs (+ new files).
- Synthesize EVERY SoundId with AudioClip.Create (44.1k mono) at Initialize (fast, < 300ms total; cache): gunshots per class (noise burst + low thump
  + crack; sniper long boom with echo tail; MG punchy), mechanical clicks for reload/bolt, footsteps (filtered noise, slight variations via pitch),
  hit marker tick, kill confirm two-tone, headshot ding, flesh/helmet hits, impacts, whiz, explosion (long lowpassed rumble), artillery whistle
  (descending tone), helicopter rotor loop (periodic thumps ~5 Hz + turbine whine), vehicle diesel loop, wind loop, ambience loop (wind + distant birds),
  distant battle (far rumbles/pops), menu music loop (dark military drone/pad with slow drum, ~16 s seamless loop), radio beep/chatter (bandpassed noise
  bursts), heartbeat loop, UI clicks.
- GameAudio API per CONTRACTS §3.2; pooled 3D AudioSources (32) with logarithmic rolloff, spatialBlend 1, doppler 0; 2D sources; PlayGunshot uses
  weapon category; distant shots get AudioLowPassFilter falloff; volumes from MasterVolume/AmbientVolume (AudioListener.volume for master).
  DontDestroyOnLoad host "[GameAudio]". Must not throw if no AudioListener exists.
- FootstepEmitter MonoBehaviour: plays Footstep by distance travelled (walk 2.2 m, sprint 2.8 m, crouch quieter, none in air); usable by player and bots.
```

## infra-vfx  — _TAMAMLANDI (Claude)_

```text
Infrastructure/Vfx (SurfaceKind.cs exists). Own Infrastructure/Vfx/*: GameVfx.cs + internal pools/effect builders.
- GameVfx API per CONTRACTS §3.3. Build ParticleSystem templates in code (main/emission bursts/shape/colorOverLifetime/sizeOverLifetime, renderer
  material from MaterialLibrary.ParticleAdditive/ParticleAlpha); pool instances; DontDestroyOnLoad host "[GameVfx]".
  Muzzle flash (additive quads + short point light, max 3 lights), tracers (pooled LineRenderers, additive), impacts per surface (dust/sparks/
  wood chips/water splash), bullet hole decals (pooled quads offset 0.01 along normal, max 150, parent to hit transform if non-static is fine),
  blood (red puffs), explosion (fireball + smoke + debris + flash light + dust), smoke cloud (long-lived large alpha particles; duration param),
  dust (landing / rotor wash). Classify: terrain→Dirt, renderer material name/MaterialId hints (Metal, Wood, Concrete...), water y.
- Everything must work if Initialize was not called (lazy init) and with no camera.
```

## infra-combat  — _TAMAMLANDI (Claude)_

```text
Infrastructure/Combat + Zone. Own: Infrastructure/Combat/* (Combatant.cs skeleton exists, Hitbox.cs, CombatantRegistry.cs, BallisticsSystem.cs,
ExplosionSystem.cs, ThrowableProjectile.cs, SmokeVolume.cs, MeleeAttack.cs, ArtilleryExecutor.cs, UnityHitScanner.cs existing) and
Infrastructure/Zone/ZoneDamageController.cs (rewrite: namespace Project.Infrastructure.Zone; keep a compiling Initialize).
- Combatant per CONTRACTS §3.4: Initialize creates HealthService/InventoryService/BoostService/ItemUseService, registers in IDamageableRegistry and
  CombatantRegistry, hooks Health.Damaged/Died; Update ticks ItemUse and Boost (authority only); on death: IsTargetable false, unregister,
  Inventory.DropAll → LootSpawner.DropAround (Project.Infrastructure.Loot) when DropLootOnDeath, raise Died. GetAimPosition uses Hitbox
  children/EyePoint/AimPoint. Rank property settable.
- BallisticsSystem: struct array projectiles (no GameObjects), gravity, per-frame segment RaycastNonAlloc on GameLayers.BulletMask with
  QueryTriggerInteraction.Collide, sort by distance, skip shooter's hitboxes and (if !friendly fire, via MatchConfig/ITeamRelations from GameContext)
  allies, ignore triggers that aren't Hitbox (loot/vehicle triggers) — non-trigger Vehicle colliders stop bullets (metal impact).
  Hit Hitbox → CombatService.ApplyBulletHit(distance travelled) + GameVfx.Blood (+ GameAudio HitFlesh/HitHelmet near local player);
  world → GameVfx.Impact(Classify). Tracers via GameVfx.Tracer per frame segment (every bullet for MG/AR is fine, keep cheap). Bullet whiz when
  passing within 3 m of local player (not own). FireWeapon: pellets, ApplySpread, publish WeaponFiredEvent(origin), GameAudio.PlayGunshot,
  GameVfx.MuzzleFlash at visualMuzzle when shooter not local. Max lifetime 3 s / range.
- ExplosionSystem: LOS-checked damage to CombatantRegistry members in radius via CombatService.ApplyExplosion (attacker), VFX, audio, ExplosionEvent,
  camera shake for local player (FirstPersonCameraController via CombatantRegistry.LocalPlayer GetComponentInChildren, intensity by distance).
- ThrowableProjectile: rigidbody sphere (layer Projectile), frag fuse 4 s radius 9 max 135 dmg, bounce sound; smoke spawns SmokeVolume + GameVfx.SmokeCloud 25 s.
- SmokeVolume: static list of spheres with expiry; segment-sphere test.
- MeleeAttack: SphereCast/raycast from eye 2.2 m against Hitbox → CombatService.ApplyMeleeHit, Punch sound.
- ArtilleryExecutor: MonoBehaviour polls ArtilleryService.DueImpacts each frame (authority), plays ArtilleryWhistle ~1.2 s before impact
  (schedule), then ExplosionSystem.Explode(pos, ShellRadius, ShellDamage, caller, DamageSourceIds.Artillery); clears list.
- ZoneDamageController: every 1 s applies CombatService.ApplyEnvironmentalDamage(zone dps) to alive targetable landed combatants outside zone; plays
  ZoneDamage 2D sound for local player.
- UnityHitScanner: fix to use Hitbox (owner id) instead of IDamageable/"Head" tag.
```

## infra-player  — _Claude — devam ediyor_

```text
Infrastructure/Player motor+camera. Own: Infrastructure/Player/CharacterControllerMotor.cs, FirstPersonCameraController.cs,
PlayerHealthComponent.cs (legacy: keep compiling, mark Obsolete), RandomSpawnPointService.cs, Infrastructure/Config/PlayerMovementConfig.cs
(add fields; keep existing ones & names — the .asset exists), WeaponConfig.cs (keep compiling).
- CharacterControllerMotor per CONTRACTS §3.5 + IPlayerMotor: walk 4.6, sprint 7.2 (forward only), crouch 2.4, prone 1.1 m/s; stand 1.8 m, crouch 1.2,
  prone 0.6 height with smooth transition and ceiling check before standing; C toggles crouch, Ctrl holds, Z toggles prone; jump (not prone/crouch);
  air control; slope/step; lean Q/E (-1..1 smooth, disabled while sprinting); SpeedMultiplier; Landed(impactSpeed) event; ControlEnabled false
  (in vehicle) skips movement; Teleport disables/enables CharacterController; MoveRaw; Velocity; EyeHeight from stance (1.62/1.05/0.35).
  Must work when Configure not yet called (use defaults via ScriptableObject.CreateInstance<PlayerMovementConfig>()).
- FirstPersonCameraController per §3.5 + IFirstPersonCamera: pitch clamp, sensitivity, InvertY, recoil offset that recovers (spring), lean roll
  (±12°) and offset (±0.35 m), eye height smoothing, head bob, FOV smoothing to BaseFieldOfView/zoom (fov = 2*atan(tan(base/2)/zoom)), Shake
  (perlin), AimOrigin/AimForward (include recoil). Works with CameraRig (Rendering module) — if rig missing, uses Camera on child.
```

## infra-weapon-visuals  — _Claude — devam ediyor_

```text
Infrastructure/Weapons. Own: Infrastructure/Weapons/WeaponModelFactory.cs, WeaponViewModel.cs (rewrite; keep legacy
ApplyRecoil + CreateDefault(Transform, WeaponConfig) compiling), + new files there.
- WeaponModelFactory.Build: distinct low-poly models for each Turkish weapon from primitives/generated boxes (MPT-76: long rifle, tan/black
  polymer furniture, rail optic; MPT-55 shorter; G3A7 classic with wooden/green furniture; KNT-76 with scope; JNG-90 long bolt rifle with big
  scope + bipod; PMT-76 MG with box mag + bipod; SAR109T compact SMG; SAR9/TP9 pistols; Escort pump shotgun), MaterialLibrary materials
  (GunMetal/GunPolymer/GunTan/GunWood), colliders removed, shadows off for viewmodel, muzzle transform at barrel tip. Scale ~real size (m).
- WeaponViewModel per §3.6: arms (sleeves CamoWoodland + gloves/skin) holding weapon; fists mode; raise/lower on equip (EquipSeconds);
  ADS pose (sight aligned to camera center, scope weapons hide when HasScope && fully aimed via SetHidden by caller); recoil kick (position +
  rotation, spring back); reload animation (mag down/up, bolt for JNG-90 after each shot); melee punch, throw, use (bandage/drink) poses; sway
  from look delta, bob from speed, sprint pose; layer = given layer recursively.
```

## infra-characters  — _Claude — devam ediyor_

```text
Infrastructure/Characters + training dummy. Own: Infrastructure/Characters/* (SoldierLook.cs, SoldierModel.cs, new files),
Infrastructure/Player/DamageableTarget.cs (rewrite per §3.7).
- SoldierModel: low-poly soldier (~1.8 m) from primitives: helmet (TSK style) or maroon beret for leader, digital camo uniform (ProceduralTextures.
  DigitalCamo via MaterialLibrary or colored materials), plate carrier vest (level visuals), backpack, team armband color, boots, gloves; joints
  (hips, knees, shoulders, elbows) for procedural walk/run cycle, crouch/prone poses, aim pitch on upper body/arms, weapon held via
  WeaponModelFactory.Build at WeaponSocket, death fall (rotate root to ground over 0.6 s, disable hitboxes), seated pose for transport.
  createHitboxes: Hitbox.CreateSphere head, Box torso, capsules arms/legs (BodyPart) on layer GameLayers.Hitbox; visual renderers on visualLayer.
  Remove primitive colliders from visual parts. SetVisible toggles renderers.
- SoldierLook.ForTeam: camo palette per team (0 Woodland greens, 1 Mountain grays, 2 Desert tans, 3 Urban, then cycle) + armband color.
- DamageableTarget: training dummies as Combatants (team 99, IsBot, DropLootOnDeath=false) with simple target-board/mannequin model + hitboxes,
  respawn (Health.ResetToFull) after RespawnSeconds, moving variant patrols between points.
```

## infra-ai  — _Claude — devam ediyor_

```text
Infrastructure/AI. Own: Infrastructure/AI/* (BotController.cs, BotSpawnArgs.cs, BotPerception.cs, BotSquad... any new files).
- BotController.Create(args): builds GameObject: CapsuleCollider (layer Bot), NavMeshAgent (radius .35, height 1.8, speed 3.5-6, angularSpeed 540,
  autoBraking), Combatant.Initialize(args...; rank via RankCatalog.RankForTeamSlot), SoldierModel.Build(look ForTeam, hitboxes), FootstepEmitter,
  loadout from LoadoutCatalog.For(role) applied to inventory (InventoryService.GiveWeapon/GiveItem + armor via TryPickup of ItemCatalog loot),
  registers in MatchService (RegisterCombatant with RankCatalog.FormatName) and ChainOfCommandService.
- Insertion: if args.Transport: parent to seat (SoldierModel.SetSeated), agent disabled; on Transport.Arrived/ReleasePassengers move to
  GetDisembarkPoint(seat), NavMesh.SamplePosition + Warp, enable agent, HasLanded; ground spawn otherwise.
- Perception (staggered every .2-.3 s): enemies only (ITeamRelations / Combatant.Team), within ViewDistance & FOV (always within 15 m), LOS raycast
  GameLayers.LineOfSightMask + SmokeVolume.BlocksLineOfSight; hearing via WeaponFiredEvent within HearingDistance → last known position;
  damage → turn to source. Ally awareness.
- Decision via BotDecisionService.Decide(BotSenses) every .5 s; squad: leader = ChainOfCommandService.GetCommander(team) Combatant; followers
  hold wedge formation slots behind leader; SquadOrderService orders (player team) — Hold at position, Assault to target, Follow, Regroup.
  AI team commanders choose objectives (loot locations / zone / enemy contact) and occasionally call ArtilleryService on enemy positions
  (Radioman/Leader alive, cooldown).
- Combat: aim with error (profile, distance, movement), turn speed, reaction delay, burst fire using WeaponRuntimeService.TryTrigger and
  BallisticsSystem.FireWeapon (origin eye), reload, strafe/crouch, seek cover (move toward nearest structure collider edge / rock) when hurt,
  grenades at enemies behind cover (ThrowableProjectile) occasionally, punch when unarmed & close.
- Heal (ItemUse.TryBeginBestHeal), zone movement (NavMesh.SamplePosition inside NextZone), looting LootRegistry items it WantsItem
  (LootPickupComponent.PickupBy), roam. SoldierModel locomotion/aim updated every frame; Combatant.Velocity set.
- Performance: 60 bots; no allocations per frame; NavMesh path requests throttled. Dead: disable agent, collider; keep body.
```

## infra-transport  — _Claude — devam ediyor_

```text
Infrastructure/Transport (TransportVehicle.cs skeleton exists — implement/extend). Own Infrastructure/Transport/*: Helicopter.cs,
ArmoredCarrier.cs, TransportFactory.cs, TransportVehicle.cs, + new files.
- Helicopter.Create(TeamInsertion plan, altitude): low-poly T-70 (Black Hawk-like) from primitives/MeshFactory-like generated meshes (fuselage,
  cockpit glass, tail boom, main + tail rotors spinning, side doors open, skids, HeliOlive), 10 seats in cabin facing outward, PassengerViewPoint
  per seat (door view). Flight: from plan.Start at altitude toward LZ with banking, decelerate, descend to hover ~1 m above ground at LZ
  (WorldMetadata.SampleGroundHeight / raycast GroundMask), touch down, Arrived; after ReleasePassengers wait 4 s then lift off and fly away,
  Departed, destroy after 40 s. Rotor loop audio (GameAudio.StartLoop HelicopterRotor), GameVfx.Dust rotor wash near ground.
- ArmoredCarrier.Create(plan): low-poly BMC Kirpi MRAP (V-hull, big wheels, armored windows, turret ring with MG, VehicleOlive/Tan), 10 seats
  (rear compartment, side benches) with view points at windows; drives from plan.Start to LZ on terrain (follow ground height, simple steering,
  ~12 m/s, slows at end), rear door opens, Arrived; departs after release. Engine loop audio, dust.
- TransportFactory.Create by plan.Method. Robust if WorldMetadata missing (use raycast / y=0).
```

## infra-vehicle-drive  — _CURSOR (Faz 2)_

```text
Infrastructure/Vehicles. Own Infrastructure/Vehicles/*: DrivableVehicle.cs, VehicleRegistry.cs, + new files.
- DrivableVehicle.Spawn: Kirpi model (reuse your own builder — do NOT depend on Transport module internals), Rigidbody (mass 14000, low COM),
  BoxCollider body on layer GameLayers.Vehicle, 4 raycast-suspension wheels (spring/damper, lateral grip, drive force, brake, max ~85 km/h,
  stable on slopes, anti-roll), visual wheels spin/steer, engine audio pitch by speed, dust. TryEnter(Combatant): driver seat (driver hidden or
  seated), Exit: place beside vehicle on ground. SetInput from player. Health (bullets stop on body), explodes at 0 health (ExplosionSystem).
  Vehicles roadkill: damage combatants hit above 25 km/h via CombatService.ApplyEnvironmentalDamage? (use DamageSourceIds.Vehicle).
- VehicleRegistry static list + FindNearest. Must be fully self-contained so failures don't affect others.
```

## infra-world-terrain  — _Claude — devam ediyor_

```text
Infrastructure/World terrain side. Own: Infrastructure/World/WorldTypes.cs (MapLayout.CreateKuzgunVadisi — keep types; you may add),
WorldMetadata.cs, WorldGenerator.cs, WorldGenerationOptions (in WorldGenerator.cs), TerrainGenerator.cs, MinimapTextureGenerator.cs, NavMeshBaker.cs,
MeshFactory.cs, TreeFactory/RockFactory helpers (new files).
- Kuzgun Vadisi (1024 m, x,z∈[-512,512]): mountain ridges along edges (natural boundary up to ~150 m), a central valley with a river (RiverSpec,
  carved channel, water plane at WaterLevel) crossing N→S with 2-3 bridges points left flat for structures owner, rolling hills, rocky slopes.
  Locations (names Turkish, kinds from LocationKind): "Kuzgun Köyü" (Village, center-ish), "Yamaç Köyü" (Village), "Sınır Karakolu" (Karakol, north
  ridge), "İleri Üs Bölgesi" (ForwardBase, Military tier), "Taş Ocağı" (Quarry), "Kuzgun Barajı" (Dam on river south), "Röle Tepesi" (RelayHill,
  highest peak), "Çam Sırtı" (Forest), "Ağıl" (Farm), "Yıkık Köy" (Ruins), 3-4 small "Gözetleme Noktası" outposts. Roads: asphalt main road through
  valley + dirt roads to all locations. Flatten areas for locations (smooth blend), carve roads (≤ 15% grade where possible).
- TerrainGenerator: TerrainData heightmap 513, size (1024, MaxHeight, 1024), position (-512,0,-512); fBm + ridges + river carve + location flatten;
  terrain layers (grass, dry grass, dirt, rock on slopes > 30°, gravel road, mud near river, snow on peaks above ~135 m) with procedural tileable
  textures (create TerrainLayer objects + Texture2D; editor will persist them), alphamap 512; trees as TerrainData.treePrototypes from prefab
  GameObjects (pine + oak + dead tree + bush, built via MeshFactory; prefab root MeshFilter/MeshRenderer w/ 2 materials + CapsuleCollider) —
  WorldGenerator must expose the tree prototype GameObjects so the editor can save them as prefabs (e.g. WorldGenerator.LastTreePrototypes)
  — ~3500 trees concentrated in forests/ridges, none on roads/locations/river. Terrain material: MaterialLibrary/ URP "Universal Render Pipeline/Terrain/Lit".
- WorldGenerator.Generate: terrain → LocationBuilder.BuildAll (structures owner) → water planes → WorldMetadata (locations, loot points, spawns,
  vehicle spawns) → minimap → NavMesh bake (NavMeshBaker using UnityEngine.AI.NavMeshBuilder.CollectSources on GameLayers.WorldMask physics
  colliders + terrain + tree capsule sources (NavMeshBuildSource Capsule) — agent radius .35 height 1.8 slope 40 step .45, voxel ~0.18) when
  options.BakeNavMesh. NavMeshBaker.EnsureLoaded adds data once (NavMesh.AddNavMeshData) — WorldMetadata.Awake calls it.
- MinimapTextureGenerator: stylized military map (hillshade + contour lines every 20 m, layer colors, river/water blue, roads, building footprints
  dark, grid lines every 100 m with labels-friendly contrast). WorldMetadata API per §3.11 (Instance set in Awake/OnEnable, cleared OnDestroy).
```

## infra-world-buildings  — _Claude — devam ediyor_

```text
Infrastructure/World buildings. Own ONLY: Infrastructure/World/BuildingGenerator.cs, StructureKit.cs (shared low-level
helpers: wall-with-openings builder, slab, ramp stairs, box/cylinder parts with MaterialLibrary materials, static marking). Types BuildingSpec/
BuildingResult/BuildingStyle in WorldTypes.cs (terrain owner) — read only.
- BuildingGenerator.Build(spec, parent): wall segments with real door (1.4×2.3 m) and window openings, floors/slabs, foundations down 2 m into terrain,
  ramp stairs (≤ 33°) to upper floors and flat roofs where accessible, interior partitions with doorways, a few cover props inside, loot points per
  floor (BuildingResult.LootPoints, Bounds). Styles: Anatolian VillageHouse (stone walls StoneDark/Plaster, flat earthen roof with parapet or tile
  roof), TwoStoryHouse, Mosque (cubic hall + dome hemisphere + slender minaret with balcony), Shop, Barracks, Karakol (fortified 2-storey building),
  WatchTower (accessible platform via ramp, sandbagged top), Hangar, Warehouse, FactoryHall, Barn, Bunker (concrete, firing slits), RadarStation,
  Shed, DamControl, ShepherdHut. Ruined flag → broken wall heights, missing roof, rubble. All Default layer, colliders, MaterialLibrary.Get(MaterialId).
  Deterministic from spec.Seed. Keep object count reasonable (merge simple boxes where possible).
```

## infra-world-locations  — _CURSOR (Faz 2)_

```text
Infrastructure/World props & locations. Own ONLY: Infrastructure/World/PropFactory.cs, LocationBuilder.cs, TrainingRangeBuilder.cs
(+ new files like LocationLayouts.cs). Use BuildingGenerator (infra-world-buildings) via BuildingSpec — do not edit it.
- PropFactory: Sandbags (wall/ring), Hesco barrier rows, Container, AmmoCrate ("Mühimmat Sandığı"), Barrel, burnt car Wreck, military Tent, CamoNet,
  Fence, HayBale, Rock, Hedgehog, FlagPole (Turkish flag quad with ProceduralTextures.TurkishFlag; optional gentle wave script), Helipad (H marking),
  Antenna mast, StreetLamp, Well (çeşme), Tree stump, Woodpile, Concrete barriers, Guard booth. Signature (Transform parent, Vector3 pos, float yaw,
  System.Random rng) → GameObject. Colliders, Default layer, MaterialLibrary materials.
- LocationBuilder.BuildAll(layout, terrain, parent, seed, lootOut, structuresOut, vehiclesOut): per LocationSpec build a coherent layout on terrain
  (y = terrain.SampleHeight + terrain.transform.position.y): villages (10-16 houses + mosque + streets + çeşme), Sınır Karakolu (Karakol building,
  perimeter wall + gate + watch towers + flag), İleri Üs (Hesco perimeter, tents, containers, helipad, ammo crates, vehicle spawns), Taş Ocağı
  (terraced cuts, rocks, machinery wrecks, shed), Baraj (concrete dam wall across river + DamControl), Röle Tepesi (antennas, bunker, RadarStation),
  Ağıl farm (barn, fences, hay), Yıkık Köy ruins, outposts (sandbag ring + tent + flag). Output loot points (tier from spec; Military at FOB/Karakol),
  structure bounds, vehicle spawns (FOB, villages). Also small scattered props along roads (barriers, wrecks).
- TrainingRangeBuilder.Build(parent): standalone flat 300×300 m "Atış Poligonu" (own flat terrain or large ground plane with collider): shooting
  lanes with distance boards (25/50/100/200/300 m) and target stands, weapon racks (loot points for every weapon), kill house (2 buildings),
  obstacle walls, Turkish flags, spawn point; returns WorldMetadata (LootPoints, GroundSpawnPoints, Locations "Atış Poligonu", MapHalfSize 150).
```

## infra-loot  — _Claude — devam ediyor_

```text
Infrastructure/Loot. Own: Infrastructure/Loot/LootPickupComponent.cs (rewrite per §3.12), LootRegistry.cs, LootSpawner.cs,
UnityLootProximityQuery.cs (fix: use LootRegistry), + new files (WorldItemVisuals.cs).
- World item visuals by category: weapons via WeaponModelFactory.Build (world scale, layer Loot), ammo boxes (olive with caliber color stripe),
  medical (white box red crescent — Kızılay style crescent, not cross), boost cans, grenades, vest/helmet/backpack shapes; trigger collider on
  layer GameLayers.Loot (size ~0.8 m) for look raycasts; subtle highlight; resting on ground (raycast GroundMask down).
- PickupBy(Combatant): InventoryService.TryPickup, spawns returned dropped items nearby (SpawnDropped), reduces quantity on partial take, destroys
  when fully taken, publishes LootPickedUpEvent via GameContext IEventBus, GameAudio Pickup.
- PromptText Turkish: "[F] MPT-76 al", "[F] 7.62 Mermi (30) al".
- LootRegistry: list + spatial queries (FindNearest, FindLookTarget via Physics.Raycast on InteractMask then fallback angle cone).
- LootSpawner.SpawnWorldLoot (roll groups per point with SpawnChance, cluster group items around point), DropAround (ring layout), SpawnDropped.
  Also "Mühimmat Sandığı" crates (static props) are handled by structures; you only spawn items.
```

## pres-player  — _Claude — devam ediyor_

```text
Presentation/Player. Own: Presentation/Player/* (PlayerController.cs, PlayerSpawnArgs.cs, PlayerWeaponHandler.cs, PlayerInteraction.cs,
SquadCommandInput.cs, IPlayerHudSource.cs (exists — keep), FirstPersonPlayerPresenter.cs + WeaponPresenter.cs (legacy: keep compiling, Obsolete)).
- PlayerController.Create(args): GameObject "Player" (layer Player): CharacterController, CharacterControllerMotor (Configure with
  Resources/ScriptableObject default config), UnityInputReader, camera pivot + CameraRig (Rendering) + FirstPersonCameraController, WeaponViewModel
  (layer Viewmodel), Combatant (local, team, role Leader, rank from career CareerStatsService rank or Yüzbaşı), hitboxes (Hitbox.Create* on player:
  head/torso/limbs following stance height), FootstepEmitter, loadout via LoadoutCatalog (same approach as bots), registers in MatchService
  (RankCatalog.FormatName) and ChainOfCommandService, CombatantRegistry.LocalPlayer.
- Frame loop: build PlayerCommand (PlayerCommand.From) from input; submit locally (authority) → motor, camera look, weapon handling (equip slots
  1-4/wheel/X holster, ADS + scope zoom from definition AdsZoom/HasScope, TryTrigger → BallisticsSystem.FireWeapon from camera AimOrigin/AimForward
  with GetSpreadAngle; recoil → camera.AddRecoil + viewmodel.OnFire; reload; fire mode; melee when unarmed), interaction F (LootRegistry.FindLookTarget
  → PickupBy; DrivableVehicle enter/exit via VehicleRegistry; disembark from transport), heal H/J via Combatant.ItemUse (viewmodel PlayUse,
  cancel on fire/switch/jump), grenades G/T (ThrowableProjectile.Throw from camera, cooldown, inventory count), squad orders F1-F4
  (SquadOrderService.Issue with aim point raycast) only when commander, artillery V (ArtilleryService.TryCall at aim point up to 600 m; Radioman alive
  in team or player is commander) — Keyboard.current for F1-F4/V.
- Transport: while in TransportVehicle: parented to seat, camera at PassengerViewPoint, motor ControlEnabled false, look allowed; on Arrived →
  press F or auto after 3 s to disembark → teleport to GetDisembarkPoint, enable control, Combatant.DropState Landed.
- Driving: when in DrivableVehicle, WASD → SetInput, camera at DriverViewPoint, F exits.
- Fall damage on motor Landed via CombatService.ApplyEnvironmentalDamage(DamageSourceIds.Fall). Death: disable input, camera falls to ground
  (death cam), Died event. ApplySettings (sensitivity, FOV, invert). Implement IPlayerHudSource fully.
```

## pres-ui-kit  — _TAMAMLANDI (Claude)_

```text
Presentation UI kit (DONE — reference only). Owns Presentation/UI/UiTheme.cs, UiSprites.cs, UiFactory.cs, UiWidgets.cs: UGUI factory (CreateCanvas, Panel, Label, Button, Slider, Toggle, Image, RawImage, ProgressBar, layout helpers, EnsureEventSystem with InputSystemUIInputModule), theme colors (accent #E30A17), procedural sprites, widgets (UiProgressBar, UiFader, UiOptionSelector).
```

## pres-hud  — _Claude — devam ediyor_

```text
Presentation HUD. Own: Presentation/UI/HudController.cs, CompassView.cs,
CrosshairView.cs, KillFeedView.cs, SquadPanelView.cs, AllyMarkersView.cs, DamageIndicatorView.cs, ScopeOverlayView.cs, NotificationView.cs,
MatchHudPresenter.cs (legacy: keep compiling, Obsolete) + new HUD files.
- Use UiFactory/UiTheme/UiSprites/UiWidgets from pres-ui-kit (DONE; do not edit those files).
- HudController.Create(player): health bar (+boost bar, armor/helmet level+durability), ammo (mag/reserve, ∞ when -1, fire mode "TEK/SERİ/OTO"),
  weapon slots, compass (K/KD/D/GD/G/GB/B/KB + degrees + ally/LZ markers), crosshair (spread-based, hidden when scoped/aiming with sight),
  hit marker (HitConfirmedEvent where attacker local: white, kill red, headshot), damage direction indicators (PlayerDamagedEvent victim local),
  kill feed (KillFeedService, ally green/enemy red), top-right "TİM: x | HAYATTA: y | ÖLDÜRME: z", zone timer ("Harekât alanı daralıyor 01:20"),
  squad panel (10 members with rank-formatted names, role, health bars, dead marked — CombatantRegistry.GetTeam), overhead ally markers (world→screen
  blue diamonds + names within 300 m), center notifications (phase changes, CommandTransferredEvent "Komuta Astsb.Kd.Çvş. X'e geçti",
  ArtilleryStrikeEvent "Topçu ateşi istendi", squad orders), interaction prompt, heal progress ring, insertion info ("İntikal: T-70 — [F] İn"),
  scope overlay (black vignette circle + mil-dot reticle) when IsScoped, low-health red vignette, outside-zone blue tint, FPS counter if setting.
  Subscribe/unsubscribe events safely (GameContext).
```

## pres-map-inventory  — _Claude — devam ediyor_

```text
Presentation map & inventory. Own: Presentation/UI/MinimapView.cs, FullMapView.cs, InventoryView.cs, MapMath.cs (+ new files there).
Use UiFactory/UiTheme/UiSprites (pres-hud) — if missing members, code against CONTRACTS and report.
- MinimapView: bottom-right square RawImage of WorldMetadata.MinimapTexture (fallback gray), north-up, zoomed ~250 m view centered on player,
  player arrow rotated by yaw, allies (blue dots, CombatantRegistry same team), zone circles (current blue edge, next white) via ring sprites
  scaled, LZ marker, artillery target marker, map grid label, RectMask2D.
- FullMapView (M): full-screen map with legend, location names (WorldMetadata.Locations), grid coordinates (A-J/1-10 per 100 m), zones, allies,
  player, transport routes, click to place marker (event PointMarked → also used for squad Attack order / artillery target by pres-player),
  cursor unlocked while open (restore after).
- InventoryView (Tab): equipment (3 weapon slots with ammo, vest/helmet/backpack with durability), backpack list with counts/weights and capacity
  bar, buttons to drop (spawns via LootSpawner.SpawnDropped) and use meds; cursor unlocked while open; Turkish labels.
```

## pres-menus  — _Claude — devam ediyor_

```text
Presentation menus. Own: Presentation/UI/MainMenuController.cs, MenuBackdrop.cs, SettingsPanel.cs, PauseMenu.cs, EndScreen.cs,
LoadingScreen.cs, CareerPanel.cs (+ new menu files). Use UiFactory/UiTheme (pres-hud) and GameSession/SceneNames (pres-bootstrap) per CONTRACTS.
- MainMenuController (added by MainMenuBootstrap): title "HAREKÂT" + subtitle "Tim Battle Royale — Kuzgun Vadisi", buttons: "HAREKÂTA KATIL" (opens
  setup panel: tim sayısı 2-6 slider "4 Tim = 40 Asker", zorluk Er/Uzman/Komando → Easy/Normal/Hard, intikal Helikopter/Zırhlı Araç, oyuncu adı),
  "ATIŞ POLİGONU", "KARİYER" (rank insignia text, XP bar to next rank, stats), "AYARLAR", "ÇIKIŞ" (Application.Quit / editor stop). Menu music
  (GameAudio.SetAmbience MenuMusic). Cursor unlocked.
- MenuBackdrop: 3D diorama built in code: dusk mountains (simple terrain mesh or big rocks), sandbag wall, a Kirpi-like block vehicle, 3-4 soldiers
  (SoldierModel) in idle poses with weapons, Turkish flag on pole waving, campfire/ember particles, fog, slowly drifting camera (CameraRig/Camera),
  occasional distant flash + DistantBattle sound.
- SettingsPanel: sensitivity, ADS multiplier, FOV 60-100, master/ambient volume, invert Y, quality (Düşük/Orta/Yüksek/Ultra), fullscreen, FPS
  göster; Apply via SettingsService.Apply + immediate effects (GameAudio volumes, PostProcessing.ApplyQuality, Screen.fullScreen).
- PauseMenu (Esc): Time.timeScale 0 while open, "DEVAM ET", "AYARLAR", "ANA MENÜ"; cursor handling.
- EndScreen: victory "ZAFER! KARTAL TİMİ HAREKÂTI KAZANDI" / defeat "ŞEHİT DÜŞTÜN" or "TİMİN ELENDİ", team placement #x/N, kills, damage,
  headshots, accuracy, survival time, XP gained and rank progress; buttons "TEKRAR" and "ANA MENÜ".
- LoadingScreen static overlay (DontDestroyOnLoad canvas).
```

## pres-bootstrap  — _Claude — devam ediyor_

```text
Presentation flow & composition. Own: Presentation/Bootstrap/* (GameSession.cs, SceneNames.cs, MatchBootstrap.cs, TrainingBootstrap.cs,
MainMenuBootstrap.cs, GameLoopPresenter.cs (rewrite), GameBootstrap.cs + TestArenaBootstrap.cs (legacy: keep compiling, Obsolete)),
Infrastructure/DI/GameCompositionRoot.cs (rewrite: Build(MatchConfig, SettingsService) registering everything in CONTRACTS §2 incl.
ChainOfCommandService), Presentation/World/ZoneWallView.cs, Presentation/World/MapBlockoutBuilder.cs (legacy keep), Presentation/Debugging/ZoneVisualizer.cs.
- GameSession per CONTRACTS §4 (PlayerPrefsSettingsStore, survive scene loads, CreateMatchConfig from settings: TeamCount, difficulty, insertion,
  random seed).
- MatchBootstrap (KuzgunVadisi scene, may be the only object besides generated world): Awake: GameSession.EnsureInitialized, GameLayers.
  ConfigureCollisionMatrix, composition root → GameContext.Set, GameAudio/GameVfx Initialize, PostProcessing + RenderSettingsUtil atmosphere,
  WorldMetadata (if missing: WorldGenerator.Generate runtime fallback with BakeNavMesh), BallisticsSystem.Create, ZoneDamageController.Create,
  ArtilleryExecutor.Create, LootSpawner.SpawnWorldLoot, a few DrivableVehicle.Spawn at WorldMetadata.VehicleSpawns, InsertionPlanner.Plan →
  TransportFactory.Create per team, PlayerController.Create (team 0 slot 0, seat 0) + BotController.Create for 9 teammates (seats) and other teams,
  names NameRoster + RankCatalog, HudController/MinimapView/FullMapView/InventoryView/PauseMenu, ZoneWallView. Start: match.Begin(); on Insertion
  phase start transports Begin() + zone.Start(); when all transports arrived & released (or 90 s) → match.NotifyDropComplete().
  GameLoopPresenter ticks SimulationClock → GameTickCoordinator (match, zone, artillery) per tick. On MatchEndedEvent or local death (show death
  then end screen after 4 s; offer spectate later) → MatchStatsService.BuildResult → CareerStatsService.Record → EndScreen (restart reloads scene,
  menu loads MainMenu). Cursor lock management with pause/map/inventory. OnDestroy: dispose container, GameContext.Clear, CombatantRegistry/
  LootRegistry/SmokeVolume clear, Time.timeScale = 1.
- TrainingBootstrap (TrainingRange scene): same infrastructure but no zone/teams fighting: player on ground with all weapons + infinite ammo
  (InventoryService.InfiniteAmmo), DamageableTarget dummies at lane targets and moving targets, LootSpawner for racks, HUD; Esc menu.
- MainMenuBootstrap: GameSession init, GameAudio init, MainMenuController + MenuBackdrop.
- ZoneWallView: tall cylinder (MeshFactory.ZoneWall or own) with ZoneWall material following IZoneService.CurrentZone, + next zone ground ring.
```

## editor-setup  — _CURSOR (Faz 2)_

```text
Editor tooling. Own: Editor/* (SceneSetupMenu.cs rewrite → keep class compiling; new ProjectSetup.cs, AssetGeneration.cs,
SceneBuilder.cs, BuildTool.cs, BatchEntry.cs).
- Menu "HAREKÂT/Kurulum/1) Her Şeyi Kur" (and individual steps) + batch entry BatchEntry.SetupAll (for -executeMethod):
  1) TagManager layers from GameLayers.CustomLayerNames (SerializedObject on ProjectSettings/TagManager.asset), tag "Head".
  2) PlayerSettings: company "FPSGameStudio", product "HAREKÂT", activeInputHandler new (via PlayerSettings or SerializedObject), linear color
     space, default fullscreen, run in background, API compatibility .NET Standard.
  3) URP: create Assets/_Project/Settings/Rendering/URP_{Low,Medium,High,Ultra}.asset + renderer data (UniversalRendererData with
     postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset")),
     UniversalRenderPipelineAsset.Create(rendererData) → set renderScale/shadowDistance/msaa/HDR/soft shadows per tier, GraphicsSettings.
     defaultRenderPipeline, QualitySettings levels names + renderPipeline per level (SerializedObject on QualitySettings if needed).
  4) Art library: create material assets for every MaterialId via MaterialLibrary.CreateFromSpec into Assets/_Project/Art/Materials, textures as PNG
     assets (ProceduralTextures), GameArtLibrary asset at Assets/_Project/Resources/GameArtLibrary.asset; default PlayerMovementConfig in Resources.
  5) Scenes: Assets/_Project/Scenes/MainMenu.unity (MainMenuBootstrap), KuzgunVadisi.unity (WorldGenerator.Generate with persisted TerrainData asset,
     terrain layers/textures saved as assets, tree prototype prefabs saved via PrefabUtility, NavMeshData saved as asset and assigned to
     WorldMetadata, minimap PNG asset, static flags on world geometry, + MatchBootstrap object, directional light, skybox), TrainingRange.unity
     (TrainingRangeBuilder + TrainingBootstrap). Use SceneNames.PathOf.
  6) EditorBuildSettings scenes in order MainMenu, KuzgunVadisi, TrainingRange.
- BuildTool: "HAREKÂT/Build/macOS" (Builds/macOS/HAREKAT.app) and "HAREKÂT/Build/Windows İstemci" + "HAREKÂT/Build/Windows Dedicated Server" (StandaloneWindows64 + StandaloneBuildSubtarget.Server — sunucu altyapısı Windows Server, Linux YOK; ayrıntı Docs/CURSOR_FAZ2.md F2-1,
  if module missing log warning). BatchEntry.BuildMac for CI.
- Everything idempotent (re-run safe), uses AssetDatabase.StartAssetEditing/StopAssetEditing appropriately, logs Turkish progress, catches errors per step.
```
