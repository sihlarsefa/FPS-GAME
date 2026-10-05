# Unity metin geçiş listesi v2

Otomatik aday envanteri. `mapped` mevcut CSV eşleşmesi; `review` anahtar önerisidir, çeviri değildir. Interpolated ifadeler elle `{0}` vb. argümanlara ayrılmalı. Aynı metnin birden fazla anahtarı varsa bağlama göre seçim yapılır. Bu dosya kaynak kodunu değiştirmez.

| Dosya:satır | Durum | Anahtar / aday | String |
|---|---|---|---|
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:22 | mapped | item.ammo_9mm | 9mm Mermi |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:23 | mapped | item.ammo_556 | 5.56 Mermi |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:24 | mapped | item.ammo_762 | 7.62 Mermi |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:25 | mapped | item.ammo_12 | 12 Kalibre Fişek |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:28 | mapped | item.bandage | Sargı Bezi |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:29 | mapped | item.first_aid | İlk Yardım Çantası |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:30 | mapped | item.medkit | Sıhhiye Çantası |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:33 | mapped | item.energy_drink | Enerji İçeceği |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:34 | mapped | item.painkiller | Ağrı Kesici |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:37 | mapped | item.frag, damage.frag | El Bombası |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:38 | mapped | item.smoke | Sis Bombası |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:41 | mapped | item.vest1 | Çelik Yelek (Sv.1) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:42 | mapped | item.vest2 | Çelik Yelek (Sv.2) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:43 | mapped | item.vest3 | Çelik Yelek (Sv.3) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:46 | mapped | item.helmet1 | Kask (Sv.1) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:47 | mapped | item.helmet2 | Kask (Sv.2) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:48 | mapped | item.helmet3 | Kask (Sv.3) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:51 | mapped | item.backpack1 | Sırt Çantası (Sv.1) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:52 | mapped | item.backpack2 | Sırt Çantası (Sv.2) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:53 | mapped | item.backpack3 | Sırt Çantası (Sv.3) |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:56 | mapped | weapon.sar9 | SAR 9 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:57 | mapped | weapon.tp9 | Canik TP9 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:58 | mapped | weapon.sar109t | SAR 109T |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:59 | mapped | weapon.mpt55 | MPT-55 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:60 | mapped | weapon.mpt76 | MPT-76 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:61 | mapped | weapon.g3a7 | G3A7 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:62 | mapped | weapon.knt76 | KNT-76 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:63 | mapped | weapon.jng90 | JNG-90 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:64 | mapped | weapon.pmt76 | PMT-76 |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:65 | mapped | weapon.escort | Escort |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:163 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:164 | mapped | item.category.ammo | Mühimmat |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:165 | mapped | item.category.armor | Çelik Yelek |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:166 | mapped | item.category.medical | Tıbbi Malzeme |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:167 | mapped | item.category.throwable | Bomba |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:168 | mapped | item.category.attachment | Eklenti |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:169 | mapped | item.category.helmet | Kask |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:170 | mapped | item.category.backpack | Sırt Çantası |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:171 | mapped | item.category.boost | Takviye |
| Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs:172 | mapped | item.category.default | Eşya |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:207 | mapped | role.leader | Tim Komutanı |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:208 | mapped | role.rifleman | Piyade |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:209 | mapped | role.marksman, weapon.category.sniper | Keskin Nişancı |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:210 | mapped | role.mg | Makineli Tüfekçi |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:211 | mapped | role.medic | Sıhhiyeci |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:212 | mapped | role.radioman | Telsizci |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:213 | mapped | role.grenadier | Bombacı |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:214 | mapped | role.default | Asker |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:223 | mapped | role.leader.abbr | KMT |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:224 | mapped | role.rifleman.abbr | PYD |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:225 | mapped | role.marksman.abbr | KN |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:226 | mapped | role.mg.abbr | MAK |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:227 | mapped | role.medic.abbr | SHH |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:228 | mapped | role.radioman.abbr | TEL |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:229 | mapped | role.grenadier.abbr | BMB |
| Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs:230 | mapped | role.default.abbr | AS |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:28 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:29 | mapped | rank.onbasi | Onbaşı |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:30 | mapped | rank.cavus | Çavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:31 | mapped | rank.sozlesmeli_er | Sözleşmeli Er |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:32 | mapped | rank.uzman_onbasi | Uzman Onbaşı |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:33 | mapped | rank.uzman_cavus | Uzman Çavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:34 | mapped | rank.astsubay_cavus | Astsubay Çavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:35 | mapped | rank.astsubay_kidemli_cavus | Astsubay Kıdemli Çavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:36 | mapped | rank.astsubay_ustcavus | Astsubay Üstçavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:37 | mapped | rank.astsubay_kidemli_ustcavus | Astsubay Kıdemli Üstçavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:38 | mapped | rank.astsubay_bascavus | Astsubay Başçavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:39 | mapped | rank.astsubay_kidemli_bascavus | Astsubay Kıdemli Başçavuş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:40 | mapped | rank.astegmen, rank.astegmen.abbr | Asteğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:41 | mapped | rank.tegmen, rank.tegmen.abbr | Teğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:42 | mapped | rank.ustegmen, rank.ustegmen.abbr | Üsteğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:43 | mapped | rank.yuzbasi | Yüzbaşı |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:44 | mapped | rank.binbasi | Binbaşı |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:45 | mapped | rank.yarbay | Yarbay |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:46 | mapped | rank.albay | Albay |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:51 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:52 | mapped | rank.onbasi.abbr | Onb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:53 | mapped | rank.cavus.abbr | Çvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:54 | mapped | rank.sozlesmeli_er.abbr | Sözl.Er |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:55 | mapped | rank.uzman_onbasi.abbr | Uzm.Onb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:56 | mapped | rank.uzman_cavus.abbr | Uzm.Çvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:57 | mapped | rank.astsubay_cavus.abbr | Astsb.Çvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:58 | mapped | rank.astsubay_kidemli_cavus.abbr | Astsb.Kd.Çvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:59 | mapped | rank.astsubay_ustcavus.abbr | Astsb.Üçvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:60 | mapped | rank.astsubay_kidemli_ustcavus.abbr | Astsb.Kd.Üçvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:61 | mapped | rank.astsubay_bascavus.abbr | Astsb.Bçvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:62 | mapped | rank.astsubay_kidemli_bascavus.abbr | Astsb.Kd.Bçvş. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:63 | mapped | rank.astegmen, rank.astegmen.abbr | Asteğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:64 | mapped | rank.tegmen, rank.tegmen.abbr | Teğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:65 | mapped | rank.ustegmen, rank.ustegmen.abbr | Üsteğmen |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:66 | mapped | rank.yuzbasi.abbr | Yzb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:67 | mapped | rank.binbasi.abbr | Bnb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:68 | mapped | rank.yarbay.abbr | Yb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:69 | mapped | rank.albay.abbr | Alb. |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:98 | mapped | rank.category.enlisted | Er/Erbaş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:99 | mapped | rank.category.specialist | Uzman Erbaş |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:100 | mapped | rank.category.nco | Astsubay |
| Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs:101 | mapped | rank.category.officer | Subay |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:14 | mapped | damage.unknown, player.unknown | Bilinmeyen |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:20 | mapped | damage.zone | Harekât Sınırı |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:21 | mapped | damage.fall | Düşme |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:22 | mapped | item.frag, damage.frag | El Bombası |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:23 | mapped | damage.fists | Yumruk |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:24 | mapped | damage.arty | Topçu Ateşi |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:25 | mapped | damage.vehicle | Araç |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:70 | mapped | weapon.category.pistol | Tabanca |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:71 | mapped | weapon.category.smg | Hafif Makineli |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:72 | mapped | weapon.category.ar | Piyade Tüfeği |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:73 | mapped | role.marksman, weapon.category.sniper | Keskin Nişancı |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:74 | mapped | weapon.category.shotgun | Pompalı |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:75 | mapped | weapon.category.melee | Yakın Dövüş |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:76 | mapped | weapon.category.dmr | Nişancı Tüfeği |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:77 | mapped | weapon.category.lmg | Makineli Tüfek |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:78 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:123 | mapped | weapon.sar9 | SAR 9 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:145 | mapped | weapon.tp9 | Canik TP9 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:169 | mapped | weapon.sar109t | SAR 109T |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:194 | mapped | weapon.mpt55 | MPT-55 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:217 | mapped | weapon.mpt76 | MPT-76 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:239 | mapped | weapon.g3a7 | G3A7 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:263 | mapped | weapon.knt76 | KNT-76 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:288 | mapped | weapon.jng90 | JNG-90 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:314 | mapped | weapon.pmt76 | PMT-76 |
| Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs:338 | mapped | weapon.escort | Escort |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:136 | mapped | bot.state.idle | Beklemede |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:137 | mapped | bot.state.loot | Malzeme topluyor |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:138 | mapped | bot.state.roam | Keşifte |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:139 | mapped | bot.state.move_to_zone | Bölgeye intikal |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:140 | mapped | bot.state.engage | Çatışmada |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:141 | mapped | bot.state.investigate | Araştırıyor |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:142 | mapped | bot.state.heal | Tedavi |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:143 | mapped | bot.state.flee | Geri çekiliyor |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:144 | mapped | bot.state.follow | Takipte |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:145 | mapped | bot.state.hold | Mevzide |
| Assets/_Project/Scripts/Application/Services/BotDecisionService.cs:146 | mapped | bot.state.assault | Taarruzda |
| Assets/_Project/Scripts/Application/Services/KillFeedService.cs:172 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Application/Services/KillFeedService.cs:172 | mapped | damage.unknown, player.unknown | Bilinmeyen |
| Assets/_Project/Scripts/Application/Services/KillFeedService.cs:179 | mapped | killfeed.environment | Çevre |
| Assets/_Project/Scripts/Application/Services/KillFeedService.cs:180 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:27 | mapped | team.kartal | Kartal Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:28 | mapped | team.bozkurt | Bozkurt Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:29 | mapped | team.simsek | Şimşek Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:30 | mapped | team.yildirim | Yıldırım Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:31 | mapped | team.kilic | Kılıç Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:32 | mapped | team.kaplan | Kaplan Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:33 | mapped | team.pars | Pars Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:34 | mapped | team.atmaca | Atmaca Timi |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:218 | mapped | team.unknown | Bilinmeyen Tim |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:220 | review | ui.match_service.448fa993 | . Tim |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:343 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Application/Services/MatchService.cs:343 | mapped | damage.unknown, player.unknown | Bilinmeyen |
| Assets/_Project/Scripts/Application/Services/MatchStatsService.cs:312 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Core/Domain/Models/MatchConfig.cs:16 | mapped | location.map | Kuzgun Vadisi |
| Assets/_Project/Scripts/Editor/AssetGeneration.cs:25 | mapped | menu.quality.ultra | Ultra |
| Assets/_Project/Scripts/Editor/BuildTool.cs:13 | review | ui.build_tool.485e4952 | HAREKÂT/Build/Windows İstemci (x64) |
| Assets/_Project/Scripts/Editor/BuildTool.cs:34 | review | ui.build_tool.aaeda3f6 | Windows İstemci |
| Assets/_Project/Scripts/Editor/BuildTool.cs:94 | review | ui.build_tool.544f816a | Build edilecek sahne yok. Önce HAREKÂT/Kurulum çalıştırın. |
| Assets/_Project/Scripts/Editor/BuildTool.cs:105 | review | ui.build_tool.05635042 | [HAREKÂT] Gerekli Unity Hub modülü kurulu değil:  |
| Assets/_Project/Scripts/Editor/BuildTool.cs:106 | review | ui.build_tool.a5466f89 | Unity Hub → Installs → bu editör → Add Modules →  |
| Assets/_Project/Scripts/Editor/BuildTool.cs:107 | review | ui.build_tool.7752a7bc | Bu Mac'te şu an yalnızca Mac Build Support olabilir; Windows build için Windows Build Support (Mono)  |
| Assets/_Project/Scripts/Editor/BuildTool.cs:108 | review | ui.build_tool.995fca60 | ve Dedicated Server için Windows Dedicated Server Build Support gerekir. |
| Assets/_Project/Scripts/Editor/BuildTool.cs:112 | review | ui.build_tool.7f09aee7 | Eksik Build Modülü |
| Assets/_Project/Scripts/Editor/BuildTool.cs:112 | review | ui.build_tool.79c4b04b | Tamam |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:10 | review | ui.project_setup.c067e39e | HAREKÂT/Kurulum/1) Her Şeyi Kur |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:17 | review | ui.project_setup.d2ea65a8 | Sanat kütüphanesi |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:19 | review | ui.project_setup.12cb27d1 | Build Settings sırası |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:34 | review | ui.project_setup.3150d165 | HAREKÂT/Kurulum/5) Sanat Kütüphanesi |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:35 | review | ui.project_setup.d2ea65a8 | Sanat kütüphanesi |
| Assets/_Project/Scripts/Editor/ProjectSetup.cs:93 | mapped | brand.title | HAREKÂT |
| Assets/_Project/Scripts/Editor/SceneBuilder.cs:68 | review | ui.scene_builder.bfa920a5 | [Dünya] |
| Assets/_Project/Scripts/Infrastructure/AI/BotController.cs:114 | review | ui.bot_controller.e6f2abb7 | Şehit |
| Assets/_Project/Scripts/Infrastructure/AI/BotController.cs:114 | review | ui.bot_controller.7c4337d1 | Araçta |
| Assets/_Project/Scripts/Infrastructure/AI/BotController.cs:132 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Infrastructure/Characters/SoldierLook.cs:47 | review | ui.soldier_look.8bc16172 | Dağ |
| Assets/_Project/Scripts/Infrastructure/Characters/SoldierLook.cs:47 | review | ui.soldier_look.c0beecde | Çöl |
| Assets/_Project/Scripts/Infrastructure/Characters/SoldierLook.cs:47 | review | ui.soldier_look.ce2a2b26 | Şehir |
| Assets/_Project/Scripts/Infrastructure/Characters/SoldierModel.cs:1044 | review | ui.soldier_model.8c828336 | silah |
| Assets/_Project/Scripts/Infrastructure/Combat/Combatant.cs:130 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Infrastructure/Config/PlayerMovementConfig.cs:63 | review | ui.player_movement_config.ab5c9193 | (tan(fov/2)/tan(taban/2)) kendisi ölçekler; çağıran taraf (PlayerController) yalnız ayar hassasiyetini ve  |
| Assets/_Project/Scripts/Infrastructure/Config/PlayerMovementConfig.cs:64 | review | ui.player_movement_config.de0af2ae | isteğe bağlı nişan çarpanını SetSensitivity ile verir. Kapalı: ölçekleme tamamen çağırana kalır. |
| Assets/_Project/Scripts/Infrastructure/DI/GameCompositionRoot.cs:150 | review | ui.game_composition_root.db8ea347 | Build(MatchConfig, SettingsService) kullanın. |
| Assets/_Project/Scripts/Infrastructure/Loot/LootFocusDriver.cs:34 | review | ui.loot_focus_driver.d9bad7bd | [Eşya Odak Sürücüsü] |
| Assets/_Project/Scripts/Infrastructure/Loot/LootPickupComponent.cs:42 | mapped | item.bandage | Sargı Bezi |
| Assets/_Project/Scripts/Infrastructure/Loot/LootPickupComponent.cs:656 | review | ui.loot_pickup_component.d3d729c3 | [Yerdeki Eşyalar] |
| Assets/_Project/Scripts/Infrastructure/Loot/WorldItemVisuals.cs:247 | review | ui.world_item_visuals.2b4a9c8a | [Eşya Odak Halkası] |
| Assets/_Project/Scripts/Infrastructure/Player/PlayerHealthComponent.cs:13 | review | ui.player_health_component.d6ecc30d | Project.Infrastructure.Combat.Combatant kullanın. |
| Assets/_Project/Scripts/Infrastructure/Transport/ArmoredCarrier.cs:81 | review | ui.armored_carrier.3b849391 | Kirpi Zırhlı Aracı |
| Assets/_Project/Scripts/Infrastructure/Transport/ArmoredCarrier.cs:109 | review | ui.armored_carrier.3365b6db | Kirpi Zırhlı Aracı - Tim  |
| Assets/_Project/Scripts/Infrastructure/Transport/ArmoredCarrier.cs:443 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Infrastructure/Transport/Helicopter.cs:134 | review | ui.helicopter.5ec24b80 | T-70 Helikopteri - Tim  |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportFactory.cs:32 | review | ui.transport_factory.e82f681a |  kurulamadı; diğer araç türü deneniyor. |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:66 | review | ui.transport_vehicle.153707da | İntikal Aracı |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:117 | review | ui.transport_vehicle.af2899fb | İntikal için hazır |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:118 | review | ui.transport_vehicle.4cdec9bc | İniş bölgesine intikal ediliyor |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:119 | review | ui.transport_vehicle.2082ce51 | İnişe geçiliyor |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:120 | review | ui.transport_vehicle.46530c2a | İniş bölgesine varıldı — araçtan in! |
| Assets/_Project/Scripts/Infrastructure/Transport/TransportVehicle.cs:121 | review | ui.transport_vehicle.9a828209 | Araç bölgeden ayrılıyor |
| Assets/_Project/Scripts/Infrastructure/Vehicles/KirpiModelBuilder.cs:30 | review | ui.kirpi_model_builder.86c5f907 | Kabın |
| Assets/_Project/Scripts/Infrastructure/Vfx/SurfaceClassifier.cs:58 | review | ui.surface_classifier.95af89a4 | çalı |
| Assets/_Project/Scripts/Infrastructure/Vfx/SurfaceClassifier.cs:72 | review | ui.surface_classifier.8c2df231 | ahşap |
| Assets/_Project/Scripts/Infrastructure/Vfx/SurfaceClassifier.cs:74 | review | ui.surface_classifier.068b9cf6 | kütük |
| Assets/_Project/Scripts/Infrastructure/Vfx/SurfaceClassifier.cs:100 | review | ui.surface_classifier.ab96fc9e | çelik |
| Assets/_Project/Scripts/Infrastructure/Vfx/SurfaceClassifier.cs:137 | review | ui.surface_classifier.792a3e0d | çamur |
| Assets/_Project/Scripts/Infrastructure/Weapons/ViewmodelMeshes.cs:219 | review | ui.viewmodel_meshes.f73f9f48 | Görünüm modeli mesh'leri eksik. |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:168 | review | ui.location_builder.57833b70 | Kışla |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:194 | review | ui.location_builder.4656e033 | BarajDuvarı |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:221 | review | ui.location_builder.958891e9 | Ahır |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:223 | review | ui.location_builder.9b9b38d4 | ÇobanKulübesi |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:258 | review | ui.location_builder.f0899852 | Gözetleme |
| Assets/_Project/Scripts/Infrastructure/World/LocationBuilder.cs:269 | review | ui.location_builder.d211b7ea | [YolKenarı] |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:25 | mapped | location.map | Kuzgun Vadisi |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:45 | mapped | location.kuzgun_koyu | Kuzgun Köyü |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:46 | mapped | location.yamac_koyu | Yamaç Köyü |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:47 | mapped | location.sinir_karakolu | Sınır Karakolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:48 | mapped | location.ileri_us | İleri Üs Bölgesi |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:49 | mapped | location.tas_ocagi | Taş Ocağı |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:50 | mapped | location.baraj | Kuzgun Barajı |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:51 | mapped | location.role_tepesi | Röle Tepesi |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:52 | mapped | location.cam_sirti | Çam Sırtı |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:54 | mapped | location.agil | Ağıl |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:55 | mapped | location.yikik_koy | Yıkık Köy |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:56 | mapped | location.goz_kuzey | Kuzey Gözetleme Noktası |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:57 | mapped | location.goz_dogu | Doğu Gözetleme Noktası |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:58 | mapped | location.goz_bati | Batı Gözetleme Noktası |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:59 | mapped | location.goz_guney | Güney Gözetleme Noktası |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:69 | mapped | road.kuzgun_yolu | Kuzgun Köyü Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:73 | mapped | road.yamac_yolu | Yamaç Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:82 | mapped | road.us_yolu | Üs Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:86 | mapped | road.dogu_yolu | Doğu Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:94 | mapped | road.agil_yolu | Ağıl Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:106 | mapped | road.role_yolu | Röle Yolu |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:117 | mapped | road.dogu_patika | Doğu Patika |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:119 | mapped | road.bati_patika | Batı Patika |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:121 | mapped | road.guney_patika | Güney Patika |
| Assets/_Project/Scripts/Infrastructure/World/MapLayoutKuzgunVadisi.cs:233 | review | ui.map_layout_kuzgun_vadisi.2db2f6cf |  Köprüsü |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:12 | review | ui.prop_factory.419b63c4 | KumTorbası |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:26 | review | ui.prop_factory.84899a24 | KumTorbasıHalka |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:65 | review | ui.prop_factory.878cd04e | MühimmatSandığı |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:84 | review | ui.prop_factory.7d62e15c | YanmışAraç |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:87 | review | ui.prop_factory.86c5f907 | Kabın |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:97 | review | ui.prop_factory.7b607da5 | AskeriÇadır |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:114 | review | ui.prop_factory.bd27d739 | KamuflajAğı |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:125 | review | ui.prop_factory.a7281a8b | Çit |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:143 | review | ui.prop_factory.ffc54480 | SamanBalyası |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:175 | review | ui.prop_factory.ebd93f6a | BayrakDireği |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:211 | review | ui.prop_factory.021a51e0 | SokakLambası |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:223 | review | ui.prop_factory.9d0be9aa | Çeşme |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:234 | review | ui.prop_factory.6c5ef30a | Kütük |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:235 | review | ui.prop_factory.6c5ef30a | Kütük |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:242 | review | ui.prop_factory.cdc93dab | OdunYığını |
| Assets/_Project/Scripts/Infrastructure/World/PropFactory.cs:264 | review | ui.prop_factory.fe6a168d | NöbetçiKulübesi |
| Assets/_Project/Scripts/Infrastructure/World/TerrainGenerator.cs:45 | review | ui.terrain_generator.32bc22e4 | [Ağaç Prototipleri] |
| Assets/_Project/Scripts/Infrastructure/World/TrainingRangeBuilder.cs:15 | review | ui.training_range_builder.0c29547b | [Atış Poligonu] |
| Assets/_Project/Scripts/Infrastructure/World/TrainingRangeBuilder.cs:34 | mapped | training.map_name | Atış Poligonu |
| Assets/_Project/Scripts/Infrastructure/World/TrainingRangeBuilder.cs:78 | review | ui.training_range_builder.99cff30b | AtışŞeritleri |
| Assets/_Project/Scripts/Infrastructure/World/TrainingRangeBuilder.cs:85 | review | ui.training_range_builder.f03a1404 | Şerit |
| Assets/_Project/Scripts/Infrastructure/World/WorldBridges.cs:14 | review | ui.world_bridges.bd0703b3 | Köprüler |
| Assets/_Project/Scripts/Infrastructure/World/WorldBridges.cs:57 | review | ui.world_bridges.d3c1f72e | Köprü |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:83 | review | ui.world_generator.489db9f5 | [Dünya]  |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:84 | review | ui.world_generator.ff570142 | Yapılar |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:122 | mapped | location.map | Kuzgun Vadisi |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:163 | review | ui.world_generator.36a1348d | yapılar |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:171 | review | ui.world_generator.e701f6fc | Ağaç temizliği |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:184 | review | ui.world_generator.fe512210 | Dünya noktaları |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:210 | review | ui.world_generator.91d91f5e | , iniş  |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:210 | review | ui.world_generator.2bf12a88 | , doğma  |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:211 | review | ui.world_generator.8b6d4a36 | , araç  |
| Assets/_Project/Scripts/Infrastructure/World/WorldGenerator.cs:211 | review | ui.world_generator.83304b9b | , yapı  |
| Assets/_Project/Scripts/Infrastructure/World/WorldMetadata.cs:18 | mapped | location.map | Kuzgun Vadisi |
| Assets/_Project/Scripts/Infrastructure/World/WorldTypes.cs:176 | mapped | location.map | Kuzgun Vadisi |
| Assets/_Project/Scripts/Infrastructure/World/WorldWater.cs:54 | review | ui.world_water.12636306 | Gölet_ |
| Assets/_Project/Scripts/Presentation/Bootstrap/BootstrapUtility.cs:66 | review | ui.bootstrap_utility.30511d57 | Güneş |
| Assets/_Project/Scripts/Presentation/Bootstrap/BootstrapUtility.cs:178 | review | ui.bootstrap_utility.483028cf | Gözlemci Kamera |
| Assets/_Project/Scripts/Presentation/Bootstrap/GameBootstrap.cs:12 | review | ui.game_bootstrap.b28d6179 | MatchBootstrap / TrainingBootstrap / MainMenuBootstrap kullanın. |
| Assets/_Project/Scripts/Presentation/Bootstrap/GameplayUiController.cs:56 | review | ui.gameplay_ui_controller.f6a22ebf | [Oyun Arayüzü] |
| Assets/_Project/Scripts/Presentation/Bootstrap/MainMenuBootstrap.cs:70 | review | ui.main_menu_bootstrap.a6d9ee5e | [Ana Menü] |
| Assets/_Project/Scripts/Presentation/Bootstrap/MainMenuBootstrap.cs:95 | review | ui.main_menu_bootstrap.9b5b417a | Menü Kamerası |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:190 | review | ui.match_bootstrap.6af113bb | Dünya kurulumu |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:191 | review | ui.match_bootstrap.4f33a3ad | Çatışma sistemleri |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:192 | review | ui.match_bootstrap.527aefb4 | Ganimet ve araçlar |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:193 | review | ui.match_bootstrap.103214d7 | İntikal planı |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:194 | review | ui.match_bootstrap.f98ada5d | Timlerin oluşturulması |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:195 | review | ui.match_bootstrap.016008ab | Arayüz |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:196 | review | ui.match_bootstrap.f1b9efad | Simülasyon döngüsü |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:258 | review | ui.match_bootstrap.bfa920a5 | [Dünya] |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:458 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:763 | mapped | match.msg.kia | ŞEHİT DÜŞTÜN |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:797 | review | ui.match_bootstrap.0bc597c9 | HAREKÂT SONA ERDİ —  |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:799 | mapped | match.msg.ended | HAREKÂT SONA ERDİ |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:883 | review | ui.match_bootstrap.da072bc8 | , süre  |
| Assets/_Project/Scripts/Presentation/Bootstrap/MatchBootstrap.cs:948 | review | ui.match_bootstrap.448fa993 | . Tim |
| Assets/_Project/Scripts/Presentation/Bootstrap/SceneNames.cs:49 | mapped | loading.scene.match | Harekât bölgesi hazırlanıyor — Kuzgun Vadisi |
| Assets/_Project/Scripts/Presentation/Bootstrap/SceneNames.cs:51 | mapped | loading.scene.training | Atış poligonu hazırlanıyor |
| Assets/_Project/Scripts/Presentation/Bootstrap/SceneNames.cs:53 | mapped | loading.scene.menu | Karargâha dönülüyor |
| Assets/_Project/Scripts/Presentation/Bootstrap/SceneNames.cs:55 | mapped | loading.default | Yükleniyor |
| Assets/_Project/Scripts/Presentation/Bootstrap/ServerRuntime.cs:172 | review | ui.server_runtime.358e18ea | kapalı |
| Assets/_Project/Scripts/Presentation/Bootstrap/ServerRuntime.cs:173 | review | ui.server_runtime.8787dc5e | , en çok  |
| Assets/_Project/Scripts/Presentation/Bootstrap/ServerRuntime.cs:173 | review | ui.server_runtime.580e74e9 |  maç |
| Assets/_Project/Scripts/Presentation/Bootstrap/ServerRuntime.cs:235 | review | ui.server_runtime.dd27c0b8 | zırhlı |
| Assets/_Project/Scripts/Presentation/Bootstrap/TestArenaBootstrap.cs:11 | review | ui.test_arena_bootstrap.6644ede8 | TrainingBootstrap kullanın. |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:124 | review | ui.training_bootstrap.0c29547b | [Atış Poligonu] |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:127 | review | ui.training_bootstrap.4f33a3ad | Çatışma sistemleri |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:128 | review | ui.training_bootstrap.4a7f9f41 | Başlangıç noktası |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:129 | review | ui.training_bootstrap.52661e30 | Oyuncu |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:130 | review | ui.training_bootstrap.6270e152 | Silah rafları |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:132 | review | ui.training_bootstrap.016008ab | Arayüz |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:133 | review | ui.training_bootstrap.f1b9efad | Simülasyon döngüsü |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:141 | mapped | training.msg.welcome | ATIŞ POLİGONU — Mermi sınırsız. Raflardan silah alabilirsin. |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:172 | mapped | training.map_name | Atış Poligonu |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:443 | mapped | training.msg.death | VURULDUN — başlangıç noktasında yeniden doğacaksın |
| Assets/_Project/Scripts/Presentation/Bootstrap/TrainingBootstrap.cs:472 | mapped | training.msg.ready | ATIŞ POLİGONU — yeniden hazırsın |
| Assets/_Project/Scripts/Presentation/Player/FirstPersonPlayerPresenter.cs:12 | review | ui.first_person_player_presenter.532c5706 | PlayerController.Create(PlayerSpawnArgs) kullanın. |
| Assets/_Project/Scripts/Presentation/Player/PlayerController.cs:888 | mapped | notify.disembark_arrival | İniş bölgesine varıldı — [F] araçtan in |
| Assets/_Project/Scripts/Presentation/Player/PlayerInteraction.cs:23 | mapped | prompt.disembark | [F] Araçtan in |
| Assets/_Project/Scripts/Presentation/Player/PlayerInteraction.cs:24 | mapped | prompt.disembark | [F] Araçtan in |
| Assets/_Project/Scripts/Presentation/Player/PlayerInteraction.cs:149 | mapped | notify.vehicle_fail | Araca binilemiyor |
| Assets/_Project/Scripts/Presentation/Player/PlayerInteraction.cs:175 | mapped | notify.inventory_partial | Envanterde yer kalmadı (bir kısmı alındı) |
| Assets/_Project/Scripts/Presentation/Player/PlayerInteraction.cs:179 | mapped | notify.inventory_full | Envanterde yer yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:200 | mapped | notify.no_weapon_slot | Bu yuvada silah yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:307 | mapped | notify.no_reserve_ammo | Yedek mermi yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:415 | mapped | notify.no_ammo | Mermi yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:491 | mapped | notify.no_frag | El bombası yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:491 | mapped | notify.no_smoke | Sis bombası yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:553 | mapped | notify.health_full | Can zaten yeterli |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:553 | mapped | notify.no_meds | İlk yardım malzemesi yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:569 | mapped | notify.boost_full | Takviye zaten dolu |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:569 | mapped | notify.no_boost | Takviye eşyası yok |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:657 | mapped | weapon.firemode.auto | Otomatik atış |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:659 | mapped | weapon.firemode.burst3 | Seri atış (3) |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:659 | mapped | weapon.firemode.burst | Seri atış |
| Assets/_Project/Scripts/Presentation/Player/PlayerWeaponHandler.cs:661 | mapped | weapon.firemode.single | Tek atış |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:24 | mapped | notify.arty_out_of_range | Hedef menzil dışında (en fazla 600 m) |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:134 | mapped | notify.need_leader | Emir vermek için tim komutanı olmalısınız |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:141 | mapped | notify.no_order_system | Tim emir sistemi yok |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:154 | review | ui.squad_command_input.597b6526 | Önce haritada hedef işaretleyin |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:154 | mapped | notify.no_assault_target | Taarruz hedefi görülmüyor |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:183 | mapped | order.hold | Emir: Mevzi alın, bekleyin! |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:185 | mapped | order.assault | Emir: Hedefe taarruz! |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:187 | mapped | order.rally | Emir: Toplanın! |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:189 | mapped | order.follow | Emir: Beni takip edin! |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:198 | mapped | order.name.hold | Mevzi Al |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:200 | mapped | order.name.assault | Taarruz |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:202 | mapped | order.name.rally | Toplan |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:204 | mapped | order.name.follow | Takip |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:218 | mapped | notify.no_radioman | Telsizci yok — topçu desteği istenemez |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:225 | mapped | notify.arty_unavailable | Topçu desteği kullanılamıyor |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:252 | review | ui.squad_command_input.597b6526 | Önce haritada hedef işaretleyin |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:272 | mapped | notify.arty_denied | Topçu desteği reddedildi |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:280 | mapped | notify.arty_danger_close | Topçu ateşi istendi — DİKKAT, yakın atış! |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:281 | mapped | notify.arty_inbound | Topçu ateşi istendi — atışlar yolda |
| Assets/_Project/Scripts/Presentation/Player/SquadCommandInput.cs:294 | review | ui.squad_command_input.eb8f7d2c | Topçu hazır değil ( |
| Assets/_Project/Scripts/Presentation/Player/WeaponPresenter.cs:13 | review | ui.weapon_presenter.532c5706 | PlayerController.Create(PlayerSpawnArgs) kullanın. |
| Assets/_Project/Scripts/Presentation/UI/CompassView.cs:29 | mapped | hud.minimap.north | K |
| Assets/_Project/Scripts/Presentation/UI/CompassView.cs:109 | review | ui.compass_view.4b3dc4b3 | İB |
| Assets/_Project/Scripts/Presentation/UI/CompassView.cs:111 | review | ui.compass_view.aeecbdf7 | TOPÇU |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:287 | review | ui.full_map_view.24a69dcb | HAREKÂT HARİTASI |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:379 | review | ui.full_map_view.aeecbdf7 | TOPÇU |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:419 | review | ui.full_map_view.5a8708a7 | Harita verisi yükleniyor… |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:481 | review | ui.full_map_view.78641982 | Bölge |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:482 | review | ui.full_map_view.c2668996 | Harekât alanı |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:483 | review | ui.full_map_view.935687b7 | Güvenli bölgeye |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:484 | review | ui.full_map_view.aac09a64 | Tim |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:485 | review | ui.full_map_view.6fedf2f7 | Tim emri |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:486 | mapped | pause.control.artillery | Topçu desteği |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:487 | review | ui.full_map_view.9359a4f1 | İşaret |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:491 | review | ui.full_map_view.ce7a8c14 | Oyuncu (bakış yönü) |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:492 | review | ui.full_map_view.c214d6d6 | Tim arkadaşı |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:493 | review | ui.full_map_view.1224a907 | Harekât alanı sınırı |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:494 | review | ui.full_map_view.162c500b | Sonraki güvenli bölge |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:495 | review | ui.full_map_view.dd1d461f | İniş bölgesi (LZ) |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:496 | review | ui.full_map_view.18c3c180 | İntikal rotası / aracı |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:497 | review | ui.full_map_view.12f535c8 | Topçu atış hedefi |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:499 | review | ui.full_map_view.1e3a873d | Harita işareti |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:508 | review | ui.full_map_view.16782e45 | Sol tık |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:508 | review | ui.full_map_view.adbb5c77 | İşaret koy (taarruz / topçu hedefi) |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:509 | review | ui.full_map_view.8fec8e58 | Sağ tık |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:509 | review | ui.full_map_view.e1fa8d01 | İşareti kaldır |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:510 | review | ui.full_map_view.99d87117 | Yakınlaştır / uzaklaştır |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:511 | review | ui.full_map_view.3b4a9f18 | Sürükle |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:511 | review | ui.full_map_view.3bdd83cc | Haritayı kaydır |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:512 | review | ui.full_map_view.361fbf05 | Boşluk |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:513 | review | ui.full_map_view.e4ebd50e | Haritayı kapat |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:770 | review | ui.full_map_view.e5aac297 | KİRPİ |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:799 | review | ui.full_map_view.4a3b492b | TOPÇU  |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:799 | review | ui.full_map_view.4269dfde | TOPÇU ATIŞI |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:996 | review | ui.full_map_view.57e998aa | Açık arazi |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1029 | review | ui.full_map_view.3a9e940b | Harekât alanı daralacak   |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1033 | review | ui.full_map_view.ef840baa | Harekât alanı daralıyor   |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1037 | review | ui.full_map_view.7cc7172a | Son güvenli bölge |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1038 | review | ui.full_map_view.774d3760 | Son bölge |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1041 | review | ui.full_map_view.88ec9176 | Harekât alanı henüz belirlenmedi |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1068 | review | ui.full_map_view.2c0645e4 | İçeride |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1103 | review | ui.full_map_view.88d46faf | Atış:  |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1103 | review | ui.full_map_view.1f401a7f | Atış sürüyor |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1117 | review | ui.full_map_view.6d0a7091 | Hazırlanıyor  |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1179 | review | ui.full_map_view.ff7eeb27 | MEVZİDE KAL |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1181 | review | ui.full_map_view.abe9625d | TOPLAN |
| Assets/_Project/Scripts/Presentation/UI/FullMapView.cs:1182 | review | ui.full_map_view.f99f2e64 | BENİ TAKİP ET |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:174 | review | ui.hud_center_info_view.79f23e06 | ŞARJÖR DEĞİŞTİRİLİYOR |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:295 | review | ui.hud_center_info_view.d34adfc3 | [F] İn — araçtan in |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:303 | review | ui.hud_center_info_view.29836fda | Otomatik iniş:  |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:310 | review | ui.hud_center_info_view.c6eff06f | İniş bölgesine varıldı |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:322 | review | ui.hud_center_info_view.49c849f4 | İniş bölgesine  |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:322 | review | ui.hud_center_info_view.307f5530 |   ·  tahmini varış  |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:329 | review | ui.hud_center_info_view.4cdec9bc | İniş bölgesine intikal ediliyor |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:337 | review | ui.hud_center_info_view.ec7a6abc | ARAÇ: KİRPİ |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:338 | mapped | prompt.disembark | [F] Araçtan in |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:356 | review | ui.hud_center_info_view.4cdec9bc | İniş bölgesine intikal ediliyor |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:368 | review | ui.hud_center_info_view.aacf4db6 | İNTİKAL:  |
| Assets/_Project/Scripts/Presentation/UI/HudCenterInfoView.cs:368 | review | ui.hud_center_info_view.e5aac297 | KİRPİ |
| Assets/_Project/Scripts/Presentation/UI/HudContext.cs:359 | mapped | damage.unknown, player.unknown | Bilinmeyen |
| Assets/_Project/Scripts/Presentation/UI/HudContext.cs:385 | mapped | role.default | Asker  |
| Assets/_Project/Scripts/Presentation/UI/HudContext.cs:472 | review | ui.hud_context.448fa993 | . Tim |
| Assets/_Project/Scripts/Presentation/UI/HudContext.cs:503 | mapped | killfeed.environment | Çevre |
| Assets/_Project/Scripts/Presentation/UI/HudContext.cs:503 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:606 | review | ui.hud_controller.a2f773cb | Dost ateşi!  |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:606 | review | ui.hud_controller.216d5039 |  şehit düştü |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:646 | review | ui.hud_controller.8312ebe8 | İNTİKAL BAŞLADI |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:647 | review | ui.hud_controller.b74eb8ea | Tim, harekât bölgesine intikal ediyor |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:650 | review | ui.hud_controller.dabd42e7 | HAREKÂT BAŞLADI |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:651 | review | ui.hud_controller.b09c8165 | Son ayakta kalan tim kazanır — harekât alanının içinde kal |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:667 | review | ui.hud_controller.cbb53139 | Yeni harekât alanı belirlendi —  |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:671 | review | ui.hud_controller.5091ef97 | Harekât alanı daralıyor! |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:675 | review | ui.hud_controller.dbf0a993 | Harekât alanı son sınırına ulaştı |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:687 | review | ui.hud_controller.7c405ac9 | Timde komutayı devralacak kimse kalmadı |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:693 | review | ui.hud_controller.c6170e25 | KOMUTA SİZDE |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:694 | review | ui.hud_controller.e41db6f5 | Komuta size geçti — F1-F4 ile tim emri verin |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:698 | review | ui.hud_controller.2735ceed |  geçti |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:717 | review | ui.hud_controller.da18dc11 | Topçu ateşi istendi |
| Assets/_Project/Scripts/Presentation/UI/HudController.cs:725 | review | ui.hud_controller.92d670b9 | DİKKAT! Düşman topçu ateşi — bölgeden uzaklaş! |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:111 | review | ui.hud_format.66b10eeb | SERİ |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:122 | mapped | role.leader.abbr | KMT |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:124 | mapped | role.mg.abbr | MAK |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:126 | mapped | role.radioman.abbr | TEL |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:127 | mapped | role.grenadier.abbr | BMB |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:128 | review | ui.hud_format.12147081 | PİY |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:137 | mapped | role.leader | Tim Komutanı |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:138 | mapped | role.marksman, weapon.category.sniper | Keskin Nişancı |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:139 | mapped | role.mg | Makineli Tüfekçi |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:140 | mapped | role.medic | Sıhhiyeci |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:141 | mapped | role.radioman | Telsizci |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:142 | mapped | role.grenadier | Bombacı |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:143 | mapped | role.rifleman | Piyade |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:152 | review | ui.hud_format.540bc741 | MEVZİ AL |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:154 | review | ui.hud_format.abe9625d | TOPLAN |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:155 | review | ui.hud_format.adad618a | TAKİP ET |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:164 | review | ui.hud_format.1989541e | Tim emri: Mevzi alın, bulunduğunuz yerde bekleyin |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:165 | review | ui.hud_format.0f24198c | Tim emri: İşaretli noktaya taarruz |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:166 | review | ui.hud_format.af6e448e | Tim emri: Komutanın yanında toplanın |
| Assets/_Project/Scripts/Presentation/UI/HudFormat.cs:167 | review | ui.hud_format.e42a8dbb | Tim emri: Beni takip edin |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:175 | review | ui.hud_status_view.e08589a7 | TİM:  |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:177 | review | ui.hud_status_view.f9aa816d | ÖLDÜRME:  |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:252 | review | ui.hud_status_view.359a59e7 | Harekât alanı daralıyor  |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:256 | review | ui.hud_status_view.005a363e | Harekât alanı  |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:260 | review | ui.hud_status_view.a65b746e | Harekât alanı son sınırında |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:306 | review | ui.hud_status_view.24908a54 | ALAN DIŞINDASIN — güvenli bölgeye  |
| Assets/_Project/Scripts/Presentation/UI/HudStatusView.cs:311 | review | ui.hud_status_view.34f946fc | Sonraki alanın dışındasın —  |
| Assets/_Project/Scripts/Presentation/UI/HudVitalsView.cs:69 | review | ui.hud_vitals_view.48b19b01 | KASK |
| Assets/_Project/Scripts/Presentation/UI/HudVitalsView.cs:73 | review | ui.hud_vitals_view.18387cbe | ÇANTA |
| Assets/_Project/Scripts/Presentation/UI/HudVitalsView.cs:103 | review | ui.hud_vitals_view.e6a0e6d3 | TAKVİYE |
| Assets/_Project/Scripts/Presentation/UI/HudWeaponView.cs:119 | review | ui.hud_weapon_view.a09d3d44 | [R] ŞARJÖR DEĞİŞTİR |
| Assets/_Project/Scripts/Presentation/UI/HudWeaponView.cs:155 | review | ui.hud_weapon_view.60084377 | BOŞ |
| Assets/_Project/Scripts/Presentation/UI/HudWeaponView.cs:324 | review | ui.hud_weapon_view.60084377 | BOŞ |
| Assets/_Project/Scripts/Presentation/UI/HudWeaponView.cs:375 | review | ui.hud_weapon_view.7f4a3f04 | [G] El Bombası  |
| Assets/_Project/Scripts/Presentation/UI/HudWeaponView.cs:392 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:268 | review | ui.inventory_view.48b19b01 | KASK |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:269 | review | ui.inventory_view.18387cbe | ÇANTA |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:291 | review | ui.inventory_view.4d32b4de | ANA SİLAH |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:309 | review | ui.inventory_view.69c7b1cc | KUŞAN |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:357 | review | ui.inventory_view.130bc8c4 | YÜK |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:372 | review | ui.inventory_view.9f3f25e9 | Çanta boş |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:386 | review | ui.inventory_view.a7e75ec2 | İPTAL |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:397 | review | ui.inventory_view.e0e23d1d |  İyileş      |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:397 | mapped | item.category.boost |  Takviye |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:574 | review | ui.inventory_view.28f90f98 | — Boş — |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:576 | review | ui.inventory_view.1b8ddae0 | Tabanca yuvası |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:576 | review | ui.inventory_view.b1423d6c | Ana silah yuvası |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:610 | review | ui.inventory_view.9a871e29 | Şarjör  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:624 | review | ui.inventory_view.a6c6dd41 | ŞARJÖR DEĞİŞİYOR % |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:662 | review | ui.inventory_view.66b10eeb | SERİ |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:700 | review | ui.inventory_view.e5bce8db | Kask yok |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:700 | review | ui.inventory_view.cd83575b | Çanta yok |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:724 | review | ui.inventory_view.ae99fe6b | Dayanıklılık  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:771 | review | ui.inventory_view.9f3f25e9 | Çanta boş |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:800 | review | ui.inventory_view.eb08cd93 | Ağırlık  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:802 | review | ui.inventory_view.77e1920b |  can |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:856 | review | ui.inventory_view.912afe89 | AŞIRI YÜK   |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:876 | review | ui.inventory_view.944b0724 | Sağlık  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:877 | mapped | item.category.boost |      Takviye  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:914 | review | ui.inventory_view.63283950 | Kullanılıyor:  |
| Assets/_Project/Scripts/Presentation/UI/InventoryView.cs:1080 | review | ui.inventory_view.8e2dec47 | Araçtayken eşya bırakılamaz |
| Assets/_Project/Scripts/Presentation/UI/KillFeedView.cs:152 | review | ui.kill_feed_view.7642a05e | Şehit düştü |
| Assets/_Project/Scripts/Presentation/UI/KillFeedView.cs:158 | review | ui.kill_feed_view.020f8926 | Harekât sınırı dışında kaldı |
| Assets/_Project/Scripts/Presentation/UI/KillFeedView.cs:160 | review | ui.kill_feed_view.760a351c | Yüksekten düştü |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreen.cs:40 | mapped | loading.default | Yükleniyor |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreen.cs:52 | mapped | loading.default | Yükleniyor |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:24 | mapped | loading.tip.01 | İpucu: F1 takip, F2 mevzi tut, F3 nişan noktasına taarruz, F4 toplan emirlerini verir. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:25 | mapped | loading.tip.02 | İpucu: Telsizci ya da tim komutanıysan V tuşuyla nişan noktasına topçu atışı isteyebilirsin. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:26 | mapped | loading.tip.03 | İpucu: Harekât alanı daralır — mavi bölgenin dışında kalan asker sürekli hasar alır. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:27 | mapped | loading.tip.04 | İpucu: Komutan şehit düşerse komuta en kıdemli askere geçer; tim harekâta devam eder. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:28 | mapped | loading.tip.05 | İpucu: Q ve E ile siperin arkasından yana eğilerek ateş edebilirsin. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:29 | mapped | loading.tip.06 | İpucu: H ile yaralarını sar, J ile takviye kullan. İyileşirken hareket yavaşlar. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:30 | mapped | loading.tip.07 | İpucu: Kafadan isabetler kask seviyesine göre çok daha ölümcüldür. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:31 | mapped | loading.tip.08 | İpucu: Kirpi zırhlı aracına F ile binebilir, haritada hızla yer değiştirebilirsin. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:32 | mapped | loading.tip.09 | İpucu: M ile tam haritayı aç; işaretlediğin nokta tim emirlerinde hedef olur. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:33 | mapped | loading.tip.10 | İpucu: Z ile yüzüstü yatmak seni uzak mesafeden görünmez kılar. |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:61 | review | ui.loading_screen_view.ae2d0f1f | [Yükleme Ekranı] |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:288 | mapped | brand.title | HAREKÂT |
| Assets/_Project/Scripts/Presentation/UI/LoadingScreenView.cs:345 | mapped | loading.footer | Kuzgun Vadisi · Tim Battle Royale |
| Assets/_Project/Scripts/Presentation/UI/MapMath.cs:95 | mapped | hud.minimap.north | K |
| Assets/_Project/Scripts/Presentation/UI/MatchHudPresenter.cs:10 | review | ui.match_hud_presenter.a584ea46 | HudController.Create(IPlayerHudSource) kullanın. |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:198 | review | ui.menu_backdrop.06de0787 | Dağlar |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:199 | review | ui.menu_backdrop.58008abe | Bitki örtüsü |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:200 | review | ui.menu_backdrop.f90a7ee6 | Kum torbaları |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:204 | review | ui.menu_backdrop.43a01c72 | Kamp ateşi |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:208 | review | ui.menu_backdrop.91ac6caf | İz mermileri |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:209 | review | ui.menu_backdrop.7f93b744 | Parlama ışığı |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:232 | review | ui.menu_backdrop.1fe7828f | MenüKamerası |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdrop.cs:262 | review | ui.menu_backdrop.b9a0be6e | ParlamaIşığı |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:274 | review | ui.menu_backdrop_builder.06de0787 | Dağlar |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:277 | review | ui.menu_backdrop_builder.ab2e1faa | Sırt1 |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:278 | review | ui.menu_backdrop_builder.59ba7405 | Sırt2 |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:279 | review | ui.menu_backdrop_builder.81cffebe | Sırt3 |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:280 | review | ui.menu_backdrop_builder.07b9573e | Sırt4 |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:388 | review | ui.menu_backdrop_builder.0b561241 | KuruAğaç |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:388 | review | ui.menu_backdrop_builder.4e5042c2 | Çam |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:404 | review | ui.menu_backdrop_builder.aa65d01c | Çalı |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:411 | review | ui.menu_backdrop_builder.f7999479 | BüyükKaya |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:461 | review | ui.menu_backdrop_builder.f8eadca5 | KumTorbaları |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:639 | review | ui.menu_backdrop_builder.e10e3d0e | Çadır |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:649 | review | ui.menu_backdrop_builder.ebd93f6a | BayrakDireği |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:690 | review | ui.menu_backdrop_builder.3163ee3d | TimKomutanı |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:692 | review | ui.menu_backdrop_builder.26ff1f3f | MakineliTüfekçi |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:694 | review | ui.menu_backdrop_builder.c18e5e4b | KeskinNişancı |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:696 | review | ui.menu_backdrop_builder.91fdc0dc | Nöbetçi |
| Assets/_Project/Scripts/Presentation/UI/MenuBackdropBuilder.cs:833 | review | ui.menu_backdrop_builder.fd064dd8 | UzakİzMermileri |
| Assets/_Project/Scripts/Presentation/UI/MenuCampfire.cs:33 | review | ui.menu_campfire.1faa67ac | KampAteşi |
| Assets/_Project/Scripts/Presentation/UI/MenuCampfire.cs:57 | review | ui.menu_campfire.3fd8a7db | Taş |
| Assets/_Project/Scripts/Presentation/UI/MenuCampfire.cs:63 | review | ui.menu_campfire.6591fba4 | KorYatağı |
| Assets/_Project/Scripts/Presentation/UI/MenuCampfire.cs:88 | review | ui.menu_campfire.b2be9f20 | AteşIşığı |
| Assets/_Project/Scripts/Presentation/UI/MenuCampfire.cs:157 | review | ui.menu_campfire.b2c344ac | Kıvılcımlar |
| Assets/_Project/Scripts/Presentation/UI/MenuDialog.cs:40 | mapped | pause.dialog.cancel, menu.dialog.cancel | VAZGEÇ |
| Assets/_Project/Scripts/Presentation/UI/MenuDialog.cs:114 | mapped | pause.dialog.cancel, menu.dialog.cancel | VAZGEÇ |
| Assets/_Project/Scripts/Presentation/UI/MenuDialog.cs:116 | mapped | menu.dialog.confirm | ONAYLA |
| Assets/_Project/Scripts/Presentation/UI/MenuFlagCloth.cs:35 | review | ui.menu_flag_cloth.110c07fb | BayrakKumaşı |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:14 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:14 | mapped | menu.difficulty.uzman | Uzman |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:14 | mapped | menu.difficulty.komando | Komando |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:17 | mapped | menu.insertion.heli | Helikopter (T-70) |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:17 | mapped | menu.insertion.apc | Zırhlı Araç (Kirpi) |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:20 | mapped | menu.quality.low | Düşük |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:20 | mapped | menu.quality.medium | Orta |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:20 | mapped | menu.quality.high | Yüksek |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:20 | mapped | menu.quality.ultra | Ultra |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:126 | mapped | menu.difficulty.desc.easy | Düşman timleri geç tepki verir, isabetleri düşüktür. Harekâta yeni başlayanlar için. |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:128 | mapped | menu.difficulty.desc.hard | Hızlı tepki, yüksek isabet ve saldırgan taarruz. Komando timleri hata affetmez. |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:130 | mapped | menu.difficulty.desc.normal | Dengeli tatbikat: eğitimli düşman timleri siper alır, kanattan dolanır. |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:145 | mapped | menu.insertion.desc.apc | Kirpi zırhlı aracıyla karadan intikal. Daha korunaklı ama yavaş; tim indirme noktasında araçtan iner. |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:146 | mapped | menu.insertion.desc.heli | T-70 helikopteriyle hızlı hava intikali. Tim iniş bölgesine helikopterden atlar. |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:154 | review | ui.menu_text.ecfe497f |  Tim =  |
| Assets/_Project/Scripts/Presentation/UI/MenuText.cs:154 | mapped | role.default |  Asker |
| Assets/_Project/Scripts/Presentation/UI/MinimapView.cs:228 | mapped | hud.minimap.north | K |
| Assets/_Project/Scripts/Presentation/UI/MinimapView.cs:596 | review | ui.minimap_view.7bce733e | BÖLGEYE  |
| Assets/_Project/Scripts/Presentation/UI/MinimapView.cs:600 | review | ui.minimap_view.013fe50b | İŞARET  |
| Assets/_Project/Scripts/Presentation/UI/NotificationView.cs:115 | review | ui.notification_view.b68f6ad7 | ETKİSİZ HÂLE GETİRİLDİ |
| Assets/_Project/Scripts/Presentation/UI/NotificationView.cs:196 | review | ui.notification_view.df3e1e92 | KAFADAN — ETKİSİZ HÂLE GETİRİLDİ |
| Assets/_Project/Scripts/Presentation/UI/NotificationView.cs:196 | review | ui.notification_view.b68f6ad7 | ETKİSİZ HÂLE GETİRİLDİ |
| Assets/_Project/Scripts/Presentation/UI/NotificationView.cs:198 | review | ui.notification_view.8d2e0365 | Düşman asker |
| Assets/_Project/Scripts/Presentation/UI/NotificationView.cs:199 | review | ui.notification_view.ca733152 |  ÖLDÜRME |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:120 | review | ui.operation_setup_panel.40887d5c | Harekât emri verildi — tim intikale hazırlanıyor... |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:137 | review | ui.operation_setup_panel.6c9c7cae | Harekât başlatılamadı. Tekrar dene. |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:204 | review | ui.operation_setup_panel.6cc2ec71 | KUZGUN VADİSİ  ·  1024 × 1024 m |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:221 | mapped | settings.btn.back | GERİ |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:223 | review | ui.operation_setup_panel.c8cd3e13 | HAREKÂTA BAŞLA |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:234 | review | ui.operation_setup_panel.29620069 | OYUNCU ADI |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:238 | review | ui.operation_setup_panel.58388f99 | TİM SAYISI |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:268 | review | ui.operation_setup_panel.5ba24c88 | Düşman timleri |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:272 | review | ui.operation_setup_panel.c965aca8 | İNTİKAL |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:273 | review | ui.operation_setup_panel.5ca8e0d9 | İntikal aracı |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:290 | review | ui.operation_setup_panel.ddde0bb7 | HAREKÂT EMRİ |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:302 | review | ui.operation_setup_panel.0c24612b | F1 Takip  ·  F2 Mevzi tut  ·  F3 Taarruz  ·  F4 Toplan  ·  V Topçu |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:306 | review | ui.operation_setup_panel.abe402be | Tim emirleri tuşlarla verilir; harita (M) üzerinde işaretlenen nokta hedef olur. |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:472 | review | ui.operation_setup_panel.3588149c | </b></color>  —  Tim Komutanı   |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:473 | review | ui.operation_setup_panel.72a08a4c |  asker. Sen komutansın,  |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:475 | review | ui.operation_setup_panel.10d079ce | •  Karşı kuvvet:  |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:475 | review | ui.operation_setup_panel.90cfdfa9 |  düşman timi ( |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:477 | review | ui.operation_setup_panel.d849bb7c | •  İntikal:  |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:478 | review | ui.operation_setup_panel.783f6b18 | Kirpi zırhlı araçla kara intikali. |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:480 | review | ui.operation_setup_panel.824ae8d2 | •  Harekât alanı (mavi bölge) aşama aşama daralır; dışında kalan asker hasar alır.  |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:481 | review | ui.operation_setup_panel.9e33ed0b | •  Son ayakta kalan tim harekâtı kazanır.  |
| Assets/_Project/Scripts/Presentation/UI/OperationSetupPanel.cs:482 | review | ui.operation_setup_panel.2c64d096 | •  Komutan şehit düşerse komuta en kıdemli askere geçer ve tim savaşmaya devam eder. |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:26 | mapped | pause.control.move | Hareket |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:27 | mapped | pause.control.sprint | Koş |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:28 | mapped | pause.control.jump | Zıpla |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:29 | mapped | pause.control.crouch | Çömel |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:30 | mapped | pause.control.prone | Yüzüstü yat |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:31 | mapped | pause.control.lean | Yana eğil |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:32 | mapped | pause.control.fire | Ateş |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:33 | review | ui.pause_menu.95354790 | SAĞ TIK |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:33 | mapped | pause.control.aim | Nişan al |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:34 | mapped | pause.control.reload | Şarjör değiştir |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:35 | mapped | pause.control.fire_mode | Atış modu |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:36 | mapped | pause.control.interact | Etkileşim / araca bin |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:37 | mapped | pause.control.weapon_select | Silah seç |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:38 | mapped | pause.control.heal_boost | İyileş / takviye |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:39 | mapped | pause.control.grenade | El bombası / sis |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:40 | mapped | pause.control.inventory | Envanter |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:41 | mapped | pause.control.map | Harita |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:42 | mapped | pause.control.squad_orders | Tim emirleri |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:43 | mapped | pause.control.artillery | Topçu desteği |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:85 | review | ui.pause_menu.1dfba43e | [Duraklatma Menüsü] |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:168 | mapped | pause.title | DURAKLATILDI |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:182 | mapped | pause.btn.resume | DEVAM ET |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:184 | mapped | pause.btn.settings, settings.title | AYARLAR |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:186 | mapped | pause.btn.main_menu | ANA MENÜ |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:189 | mapped | pause.hint.esc | ESC — devam et |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:228 | mapped | pause.subtitle.training | Atış Poligonu — eğitim |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:228 | mapped | pause.subtitle.match | Harekât sürüyor — Kuzgun Vadisi |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:305 | mapped | pause.dialog.training | Atış poligonundan ayrılıp karargâha dönülecek. |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:306 | mapped | pause.dialog.match | Harekâttan çekilirsen bu maçtaki ilerlemen kaydedilmez. Karargâha dönmek istediğine emin misin? |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:307 | mapped | pause.dialog.title | Ana menüye dön |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:307 | mapped | pause.btn.main_menu | ANA MENÜ |
| Assets/_Project/Scripts/Presentation/UI/PauseMenu.cs:307 | mapped | pause.dialog.cancel, menu.dialog.cancel | VAZGEÇ |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:106 | mapped | settings.status.saved | Ayarlar kaydedildi. |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:169 | mapped | settings.status.defaults_loaded | Varsayılan değerler yüklendi — kaydetmek için UYGULA. |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:243 | mapped | pause.btn.settings, settings.title | AYARLAR |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:253 | mapped | settings.mouse_sensitivity | Fare hassasiyeti |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:259 | mapped | settings.ads_multiplier | Nişan (ADS) çarpanı |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:265 | mapped | settings.invert_y | Y eksenini ters çevir |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:276 | mapped | settings.fov | Görüş alanı (FOV) |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:282 | mapped | settings.quality | Grafik kalitesi |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:288 | mapped | settings.fullscreen | Tam ekran |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:293 | mapped | settings.show_fps | FPS göster |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:303 | mapped | settings.master_volume | Ana ses |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:309 | mapped | settings.ambient_volume | Ortam sesi ve müzik |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:326 | mapped | settings.btn.defaults | VARSAYILAN |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:329 | mapped | settings.btn.back | GERİ |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:331 | mapped | settings.btn.apply | UYGULA |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:334 | mapped | settings.status.no_service | Ayar servisi bulunamadı — değişiklikler yalnızca bu oturumda geçerli. |
| Assets/_Project/Scripts/Presentation/UI/SettingsPanel.cs:412 | mapped | settings.status.unsaved | Kaydedilmemiş değişiklikler var. |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:82 | review | ui.squad_panel_view.ec0558d1 | TİM |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:125 | review | ui.squad_panel_view.1e5a1eb3 | ŞEHİT |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:254 | review | ui.squad_panel_view.ec0558d1 | TİM |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:254 | review | ui.squad_panel_view.8be5d965 | TİM —  |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:310 | review | ui.squad_panel_view.4b34980e | EMİR:  |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:370 | review | ui.squad_panel_view.7e6a19df | TOPÇU: ATEŞ YOLDA —  |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:375 | review | ui.squad_panel_view.5a2cd66e | TOPÇU: Kullanılamıyor |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:380 | review | ui.squad_panel_view.395921f3 | TOPÇU:  |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:380 | review | ui.squad_panel_view.d617126c |  sonra hazır |
| Assets/_Project/Scripts/Presentation/UI/SquadPanelView.cs:385 | review | ui.squad_panel_view.c226ae77 | TOPÇU: HAZIR  [V] |
| Assets/_Project/Scripts/Presentation/World/MapBlockoutBuilder.cs:6 | review | ui.map_blockout_builder.e322ba7e | WorldGenerator.Generate kullanın. |
| Assets/_Project/Scripts/Presentation/World/ZoneWallView.cs:57 | review | ui.zone_wall_view.ccb59b7e | [Harekât Alanı Sınırı] |
| Assets/_Project/Scripts/Presentation/World/ZoneWallView.cs:215 | review | ui.zone_wall_view.2f158af8 | Sonraki Bölge Halkası |
| Assets/_Project/Tests/EditMode/BotDecisionTests.cs:283 | review | ui.bot_decision_tests.34234adc | yaklaşırken takibe devam |
| Assets/_Project/Tests/EditMode/BotDecisionTests.cs:356 | mapped | bot.state.engage | Çatışmada |
| Assets/_Project/Tests/EditMode/BotDecisionTests.cs:357 | mapped | bot.state.hold | Mevzide |
| Assets/_Project/Tests/EditMode/BotDecisionTests.cs:358 | mapped | bot.state.assault | Taarruzda |
| Assets/_Project/Tests/EditMode/CombatTests.cs:70 | review | ui.combat_tests.1e4a38f6 | Tim  |
| Assets/_Project/Tests/EditMode/CombatTests.cs:271 | review | ui.combat_tests.e0d7912b | Died olayı uygulanan (kırpılmış) hasarı taşır |
| Assets/_Project/Tests/EditMode/CombatTests.cs:321 | review | ui.combat_tests.9a402d51 | Sınırın üstüne çıkmaz |
| Assets/_Project/Tests/EditMode/CombatTests.cs:345 | review | ui.combat_tests.f87f8a0c | Yeniden doğduktan sonra yeni ölüm yayınlanır |
| Assets/_Project/Tests/EditMode/CombatTests.cs:409 | review | ui.combat_tests.75ef15c5 | Raporlanan hasar kalan canla sınırlı |
| Assets/_Project/Tests/EditMode/CombatTests.cs:494 | review | ui.combat_tests.8ba2bb7d | Kask etkilenmez |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:93 | mapped | item.ammo_9mm | 9mm Mermi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:94 | mapped | item.ammo_12 | 12 Kalibre Fişek |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:95 | mapped | item.bandage | Sargı Bezi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:96 | mapped | item.first_aid | İlk Yardım Çantası |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:97 | mapped | item.medkit | Sıhhiye Çantası |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:98 | mapped | item.vest2 | Çelik Yelek (Sv.2) |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:99 | mapped | item.helmet3 | Kask (Sv.3) |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:100 | mapped | item.backpack1 | Sırt Çantası (Sv.1) |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:250 | review | ui.inventory_tests.78701647 | çanta bırakılınca yük kapasiteyi aşar |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:263 | mapped | item.category.ammo | Mühimmat |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:264 | mapped | item.category.medical | Tıbbi Malzeme |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:265 | mapped | item.category.weapon, weapon.category.default | Silah |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:266 | mapped | item.category.helmet | Kask |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:267 | mapped | item.category.default | Eşya |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:268 | mapped | item.smoke | Sis Bombası |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:269 | mapped | weapon.jng90 | JNG-90 |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:354 | review | ui.inventory_tests.afc969e1 | GiveWeapon kuşanmaz |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:364 | review | ui.inventory_tests.79cd7fe5 | boş yuva seçilemez |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:446 | review | ui.inventory_tests.b3277b94 | mermisiz tüfek yerine dolu tabanca |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:493 | review | ui.inventory_tests.398aba3d | düşük seviye |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:495 | review | ui.inventory_tests.a4edac23 | aynı seviye düşük dayanıklılık |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:499 | review | ui.inventory_tests.db2fe07e | aynı seviye yüksek dayanıklılık |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:505 | review | ui.inventory_tests.081f6dab | yüksek seviye |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:524 | review | ui.inventory_tests.d637b215 | kırık kask korumaz |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:588 | review | ui.inventory_tests.70d0f8ca | aynı yuva değişiklik değil |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:592 | review | ui.inventory_tests.2fb17254 | mermi yokken değişiklik yok |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:599 | review | ui.inventory_tests.7fec3741 | reddedilen alım olay üretmez |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:614 | review | ui.inventory_tests.513e0eae | eski API değiştirmez |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:638 | review | ui.inventory_tests.16237ecf | 75 üstünde yalnızca sıhhiye çantası işe yarar |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:652 | review | ui.inventory_tests.c86d2d41 | az eksikte enerji içeceği |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:669 | review | ui.inventory_tests.d30555d4 | aynı silah |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:670 | review | ui.inventory_tests.3befc1be | boş ikinci yuva |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:674 | review | ui.inventory_tests.877242e0 | daha zayıf silah |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:706 | mapped | role.leader | Tim Komutanı |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:707 | mapped | role.rifleman | Piyade |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:708 | mapped | role.marksman, weapon.category.sniper | Keskin Nişancı |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:709 | mapped | role.mg | Makineli Tüfekçi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:710 | mapped | role.medic | Sıhhiyeci |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:711 | mapped | role.radioman | Telsizci |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:712 | mapped | role.grenadier | Bombacı |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:713 | mapped | role.leader | Tim Komutanı |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:756 | review | ui.inventory_tests.9cc8ba85 |  ağırlık |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:763 | review | ui.inventory_tests.d5573a83 |  mermi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:829 | review | ui.inventory_tests.816a5097 | silah hiç çıkmadı |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:897 | mapped | item.bandage | Sargı Bezi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:898 | mapped | item.ammo_556 | 5.56 Mermi |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:899 | mapped | item.vest2 | Çelik Yelek (Sv.2) |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:900 | review | ui.inventory_tests.51f7cf24 | Eski Zırh |
| Assets/_Project/Tests/EditMode/InventoryTests.cs:923 | review | ui.inventory_tests.d94c6eb9 | zırh tek adet |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:93 | review | ui.item_use_tests.39dff514 | süre bitmeden iyileşme yok |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:94 | review | ui.item_use_tests.dcc969d0 | süre bitmeden tüketim yok |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:114 | review | ui.item_use_tests.645c6e47 | 75'te sargı bezi işe yaramaz |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:131 | review | ui.item_use_tests.bc864bce | elde kalmadı |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:145 | review | ui.item_use_tests.3f890afd | can dolu |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:149 | review | ui.item_use_tests.67dd0314 | bomba kullanılamaz |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:194 | review | ui.item_use_tests.fcaac020 | kullanım yokken iptal olay üretmez |
| Assets/_Project/Tests/EditMode/ItemUseTests.cs:278 | mapped | item.first_aid | İlk Yardım Çantası |
| Assets/_Project/Tests/EditMode/MatchTests.cs:75 | mapped | rank.yuzbasi.abbr | Yzb.  |
| Assets/_Project/Tests/EditMode/MatchTests.cs:75 | mapped | rank.uzman_cavus.abbr | Uzm.Çvş.  |
| Assets/_Project/Tests/EditMode/MatchTests.cs:75 | mapped | role.default | Asker |
| Assets/_Project/Tests/EditMode/MatchTests.cs:115 | review | ui.match_tests.178d90ea | Kayıt yokken yapılandırmadaki tim sayısı |
| Assets/_Project/Tests/EditMode/MatchTests.cs:132 | review | ui.match_tests.ab997a6d | Begin ikinci kez çağrılınca geçiş yok |
| Assets/_Project/Tests/EditMode/MatchTests.cs:143 | review | ui.match_tests.365c70d2 | Maç saati Insertion'dan önce işlemez |
| Assets/_Project/Tests/EditMode/MatchTests.cs:153 | review | ui.match_tests.5070c381 | Insertion yalnızca NotifyDropComplete ile biter |
| Assets/_Project/Tests/EditMode/MatchTests.cs:186 | review | ui.match_tests.c7f9ec26 | Maç saati Insertion'dan beri sayar |
| Assets/_Project/Tests/EditMode/MatchTests.cs:237 | review | ui.match_tests.85fbd247 | Uzm.Çvş. Asker101 |
| Assets/_Project/Tests/EditMode/MatchTests.cs:259 | mapped | team.kartal | Kartal Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:259 | mapped | team.bozkurt | Bozkurt Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:259 | mapped | team.simsek | Şimşek Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:259 | mapped | team.yildirim | Yıldırım Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:260 | mapped | team.kilic | Kılıç Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:260 | mapped | team.kaplan | Kaplan Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:260 | mapped | team.pars | Pars Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:260 | mapped | team.atmaca | Atmaca Timi |
| Assets/_Project/Tests/EditMode/MatchTests.cs:266 | review | ui.match_tests.fc3f5ed2 | 9. Tim |
| Assets/_Project/Tests/EditMode/MatchTests.cs:333 | review | ui.match_tests.3eecde49 | Hayattaki tim için 0 |
| Assets/_Project/Tests/EditMode/MatchTests.cs:351 | review | ui.match_tests.37d31330 | Hayattaki yerel oyuncu kazananı temsil eder |
| Assets/_Project/Tests/EditMode/MatchTests.cs:404 | review | ui.match_tests.18b2852e | Bitişten sonraki ölümler yok sayılır |
| Assets/_Project/Tests/EditMode/MatchTests.cs:406 | review | ui.match_tests.284c2ff8 | Maç saati Ending'de durur |
| Assets/_Project/Tests/EditMode/MatchTests.cs:446 | review | ui.match_tests.64b5a12c | Asb.Kd.Çvş. Mehmet |
| Assets/_Project/Tests/EditMode/MatchTests.cs:450 | review | ui.match_tests.64b5a12c | Asb.Kd.Çvş. Mehmet |
| Assets/_Project/Tests/EditMode/MatchTests.cs:459 | review | ui.match_tests.64b5a12c | Asb.Kd.Çvş. Mehmet |
| Assets/_Project/Tests/EditMode/MatchTests.cs:459 | review | ui.match_tests.b84050d8 | Boş ad mevcut adı silmez |
| Assets/_Project/Tests/EditMode/MatchTests.cs:485 | review | ui.match_tests.59f5c1b0 | Yalnız |
| Assets/_Project/Tests/EditMode/MatchTests.cs:547 | review | ui.match_tests.ed9afcc3 | sunum hatası |
| Assets/_Project/Tests/EditMode/MatchTests.cs:587 | review | ui.match_tests.cde8e868 | Üye listesi ölüleri de içerir |
| Assets/_Project/Tests/EditMode/MatchTests.cs:622 | review | ui.match_tests.cd10d82b | Kazanan timin komutanı temsil eder |
| Assets/_Project/Tests/EditMode/RankTests.cs:13 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Tests/EditMode/RankTests.cs:13 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Tests/EditMode/RankTests.cs:14 | mapped | rank.onbasi | Onbaşı |
| Assets/_Project/Tests/EditMode/RankTests.cs:14 | mapped | rank.onbasi.abbr | Onb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:15 | mapped | rank.cavus | Çavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:15 | mapped | rank.cavus.abbr | Çvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:16 | mapped | rank.sozlesmeli_er | Sözleşmeli Er |
| Assets/_Project/Tests/EditMode/RankTests.cs:16 | mapped | rank.sozlesmeli_er.abbr | Sözl.Er |
| Assets/_Project/Tests/EditMode/RankTests.cs:17 | mapped | rank.uzman_onbasi | Uzman Onbaşı |
| Assets/_Project/Tests/EditMode/RankTests.cs:17 | mapped | rank.uzman_onbasi.abbr | Uzm.Onb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:18 | mapped | rank.uzman_cavus | Uzman Çavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:18 | mapped | rank.uzman_cavus.abbr | Uzm.Çvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:19 | mapped | rank.astsubay_cavus | Astsubay Çavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:19 | mapped | rank.astsubay_cavus.abbr | Astsb.Çvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:20 | mapped | rank.astsubay_kidemli_cavus | Astsubay Kıdemli Çavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:20 | mapped | rank.astsubay_kidemli_cavus.abbr | Astsb.Kd.Çvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:21 | mapped | rank.astsubay_ustcavus | Astsubay Üstçavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:21 | mapped | rank.astsubay_ustcavus.abbr | Astsb.Üçvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:22 | mapped | rank.astsubay_kidemli_ustcavus | Astsubay Kıdemli Üstçavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:22 | mapped | rank.astsubay_kidemli_ustcavus.abbr | Astsb.Kd.Üçvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:23 | mapped | rank.astsubay_bascavus | Astsubay Başçavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:23 | mapped | rank.astsubay_bascavus.abbr | Astsb.Bçvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:24 | mapped | rank.astsubay_kidemli_bascavus | Astsubay Kıdemli Başçavuş |
| Assets/_Project/Tests/EditMode/RankTests.cs:24 | mapped | rank.astsubay_kidemli_bascavus.abbr | Astsb.Kd.Bçvş. |
| Assets/_Project/Tests/EditMode/RankTests.cs:25 | mapped | rank.astegmen, rank.astegmen.abbr | Asteğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:25 | mapped | rank.astegmen, rank.astegmen.abbr | Asteğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:26 | mapped | rank.tegmen, rank.tegmen.abbr | Teğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:26 | mapped | rank.tegmen, rank.tegmen.abbr | Teğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:27 | mapped | rank.ustegmen, rank.ustegmen.abbr | Üsteğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:27 | mapped | rank.ustegmen, rank.ustegmen.abbr | Üsteğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:28 | mapped | rank.yuzbasi | Yüzbaşı |
| Assets/_Project/Tests/EditMode/RankTests.cs:28 | mapped | rank.yuzbasi.abbr | Yzb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:29 | mapped | rank.binbasi | Binbaşı |
| Assets/_Project/Tests/EditMode/RankTests.cs:29 | mapped | rank.binbasi.abbr | Bnb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:30 | mapped | rank.yarbay | Yarbay |
| Assets/_Project/Tests/EditMode/RankTests.cs:30 | mapped | rank.yarbay.abbr | Yb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:31 | mapped | rank.albay | Albay |
| Assets/_Project/Tests/EditMode/RankTests.cs:31 | mapped | rank.albay.abbr | Alb. |
| Assets/_Project/Tests/EditMode/RankTests.cs:38 | mapped | rank.category.enlisted | Er/Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:39 | mapped | rank.category.enlisted | Er/Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:40 | mapped | rank.category.enlisted | Er/Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:41 | mapped | rank.category.enlisted | Er/Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:42 | mapped | rank.category.specialist | Uzman Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:43 | mapped | rank.category.specialist | Uzman Erbaş |
| Assets/_Project/Tests/EditMode/RankTests.cs:44 | mapped | rank.category.nco | Astsubay |
| Assets/_Project/Tests/EditMode/RankTests.cs:45 | mapped | rank.category.nco | Astsubay |
| Assets/_Project/Tests/EditMode/RankTests.cs:46 | mapped | rank.category.officer | Subay |
| Assets/_Project/Tests/EditMode/RankTests.cs:47 | mapped | rank.category.officer | Subay |
| Assets/_Project/Tests/EditMode/RankTests.cs:48 | mapped | rank.category.officer | Subay |
| Assets/_Project/Tests/EditMode/RankTests.cs:76 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er |
| Assets/_Project/Tests/EditMode/RankTests.cs:77 | mapped | rank.albay | Albay |
| Assets/_Project/Tests/EditMode/RankTests.cs:151 | mapped | rank.category.nco | Astsubay |
| Assets/_Project/Tests/EditMode/RankTests.cs:225 | review | ui.rank_tests.151eed92 | Astsb.Kd.Çvş. Bozkurt |
| Assets/_Project/Tests/EditMode/RankTests.cs:226 | review | ui.rank_tests.d694d8a2 | Uzm.Çvş. Atmaca |
| Assets/_Project/Tests/EditMode/RankTests.cs:227 | review | ui.rank_tests.b36f3fd4 | Er Şahin |
| Assets/_Project/Tests/EditMode/RankTests.cs:227 | review | ui.rank_tests.7809d118 | Şahin |
| Assets/_Project/Tests/EditMode/RankTests.cs:228 | mapped | rank.yuzbasi | Yüzbaşı |
| Assets/_Project/Tests/EditMode/RankTests.cs:229 | mapped | rank.tegmen, rank.tegmen.abbr | Teğmen |
| Assets/_Project/Tests/EditMode/RankTests.cs:230 | review | ui.rank_tests.e6d357c9 | Yüzbaşı Kartal |
| Assets/_Project/Tests/EditMode/RankTests.cs:254 | review | ui.rank_tests.7809d118 | Şahin |
| Assets/_Project/Tests/EditMode/RankTests.cs:254 | review | ui.rank_tests.d5c465cf | Yılmaz |
| Assets/_Project/Tests/EditMode/RankTests.cs:254 | review | ui.rank_tests.941815fd | Çelik |
| Assets/_Project/Tests/EditMode/RankTests.cs:254 | review | ui.rank_tests.4d018ee7 | Aydın |
| Assets/_Project/Tests/EditMode/RankTests.cs:254 | review | ui.rank_tests.2a3de1d6 | Öztürk |
| Assets/_Project/Tests/EditMode/RankTests.cs:332 | review | ui.rank_tests.28b59951 | kayıt olay yayınlamamalı |
| Assets/_Project/Tests/EditMode/RankTests.cs:406 | review | ui.rank_tests.4e658af5 | aynı ölüm iki kez sayılmamalı |
| Assets/_Project/Tests/EditMode/RankTests.cs:505 | review | ui.rank_tests.8b1e5cfc | komutan, yardımcı ve uzmanlar slot sırasında olmalı |
| Assets/_Project/Tests/EditMode/RankTests.cs:524 | review | ui.rank_tests.3cac6f34 | tek kişilik timde yardımcı yok |
| Assets/_Project/Tests/EditMode/SimTests.cs:322 | review | ui.sim_tests.157ca986 | paraşüt açılmış olmalı |
| Assets/_Project/Tests/EditMode/SimTests.cs:376 | review | ui.sim_tests.0f6f9db0 | LZ kenardan içeride olmalı |
| Assets/_Project/Tests/EditMode/SimTests.cs:542 | review | ui.sim_tests.984635a2 | gecikmeden önce düşmemeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:642 | review | ui.sim_tests.ac9557d0 | kendi timinin atışı hariç tutulabilmeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:668 | review | ui.sim_tests.2b81bb18 | ilk düşen, önce çağrılan atışın ilk mermisi |
| Assets/_Project/Tests/EditMode/SimTests.cs:669 | review | ui.sim_tests.33f209ca | ikinci mermi (t<6.75) de ilk atıştan |
| Assets/_Project/Tests/EditMode/SimTests.cs:680 | review | ui.sim_tests.5b821bc4 | olay sırası DueImpacts ile aynı olmalı |
| Assets/_Project/Tests/EditMode/SimTests.cs:755 | review | ui.sim_tests.8abd6e76 | zaten temiz tim için sayaç değişmemeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:758 | review | ui.sim_tests.21bfe88a | yeni emir eski sayaçla eşleşmemeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:795 | review | ui.sim_tests.9e034f59 |    Çok Uzun Bir Komutan Adı Burada    |
| Assets/_Project/Tests/EditMode/SimTests.cs:812 | review | ui.sim_tests.b0c98daa | Çok |
| Assets/_Project/Tests/EditMode/SimTests.cs:813 | review | ui.sim_tests.5f42baa0 | girdi değiştirilmemeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:833 | review | ui.sim_tests.94f4c594 | Şahin Öztürk |
| Assets/_Project/Tests/EditMode/SimTests.cs:852 | review | ui.sim_tests.94f4c594 | Şahin Öztürk |
| Assets/_Project/Tests/EditMode/SimTests.cs:900 | review | ui.sim_tests.1e791007 | önceki örnek değişmemeli |
| Assets/_Project/Tests/EditMode/SimTests.cs:944 | review | ui.sim_tests.ea14a0ed | Mavi Tim |
| Assets/_Project/Tests/EditMode/StatsTests.cs:63 | review | ui.stats_tests.1bac4f17 | Hasarın tamamı sayılır |
| Assets/_Project/Tests/EditMode/StatsTests.cs:95 | review | ui.stats_tests.99e04fe7 | Rütbeli görünen ad |
| Assets/_Project/Tests/EditMode/StatsTests.cs:96 | review | ui.stats_tests.03bf8164 | Uzm.Çvş. Asker1 |
| Assets/_Project/Tests/EditMode/StatsTests.cs:111 | review | ui.stats_tests.cd53874c | Türkçe kaynak adı |
| Assets/_Project/Tests/EditMode/StatsTests.cs:154 | mapped | team.kartal | Kartal Timi |
| Assets/_Project/Tests/EditMode/StatsTests.cs:177 | mapped | team.simsek | Şimşek Timi |
| Assets/_Project/Tests/EditMode/StatsTests.cs:184 | mapped | team.bozkurt | Bozkurt Timi |
| Assets/_Project/Tests/EditMode/StatsTests.cs:311 | review | ui.stats_tests.b255c515 | Katalog yoksa kimliğe düşer |
| Assets/_Project/Tests/EditMode/StatsTests.cs:352 | review | ui.stats_tests.85fbd247 | Uzm.Çvş. Asker101 |
| Assets/_Project/Tests/EditMode/StatsTests.cs:353 | review | ui.stats_tests.8fac4e27 | Uzm.Çvş. Asker202 |
| Assets/_Project/Tests/EditMode/StatsTests.cs:367 | mapped | killfeed.environment | Çevre |
| Assets/_Project/Tests/EditMode/StatsTests.cs:406 | review | ui.stats_tests.153a6ce3 | Topçu ateşini çağıran öldüren sayılır |
| Assets/_Project/Tests/EditMode/StatsTests.cs:429 | review | ui.stats_tests.140fe3de | Elle belirlenen tim önceliklidir |
| Assets/_Project/Tests/EditMode/StatsTests.cs:439 | mapped | weapon.mpt76 | MPT-76 |
| Assets/_Project/Tests/EditMode/StatsTests.cs:455 | review | ui.stats_tests.85fcc6fc | Yalnızca maç servisi abone kalır |
| Assets/_Project/Tests/EditMode/StatsTests.cs:467 | mapped | menu.difficulty.er, rank.er, rank.er.abbr | Er  |
| Assets/_Project/Tests/EditMode/StatsTests.cs:471 | review | ui.stats_tests.448fa993 | . Tim |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:184 | mapped | weapon.mpt76 | MPT-76 |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:185 | mapped | damage.zone | Harekât Sınırı |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:186 | mapped | damage.fall | Düşme |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:187 | mapped | item.frag, damage.frag | El Bombası |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:188 | mapped | damage.fists | Yumruk |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:189 | mapped | damage.arty | Topçu Ateşi |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:190 | mapped | damage.vehicle | Araç |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:248 | review | ui.weapon_tests.ca600de9 | Basılı tutmak tek atışta yeni atış üretmemeli |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:261 | review | ui.weapon_tests.000f8e96 | Soğuma bitmeden atış olmaz |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:282 | review | ui.weapon_tests.43e1c33f | Süresi dolan tampon atış üretmemeli |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:344 | review | ui.weapon_tests.0bd7ec24 | Burst aralığı beklenmeli |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:347 | review | ui.weapon_tests.97b0eb18 | Burst tetik bırakılsa da devam eder |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:438 | review | ui.weapon_tests.72d9fa41 | Soğuma sırasında TryFire başarısız olmalı |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:469 | review | ui.weapon_tests.74091ba0 | Zaten değiştiriliyor |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:478 | review | ui.weapon_tests.9df8bc89 | Süre dolmadan mermi eklenmez |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:563 | review | ui.weapon_tests.5f5791e5 | Basılı tutmak değiştirmeyi kesmez |
| Assets/_Project/Tests/EditMode/WeaponTests.cs:615 | review | ui.weapon_tests.67fe445d | Hız 1'e kırpılır |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:59 | review | ui.zone_tests.9c8e5984 | Start'tan önce hasar yok |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:65 | review | ui.zone_tests.cd952bf6 | Tick Start'tan önce ilerletmez |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:82 | review | ui.zone_tests.2337eac0 | Faz 0 hedefi başlangıç çemberinin içinde |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:92 | review | ui.zone_tests.2cbe0ed2 | İkinci Start yok sayılır |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:118 | review | ui.zone_tests.abd55830 | Hedef yarıçap |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:120 | review | ui.zone_tests.1061a4fe | merkez X sınırda |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:121 | review | ui.zone_tests.41fc9cde | merkez Z sınırda |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:133 | review | ui.zone_tests.1be29e56 | daralan çember faz başı çemberinin içinde |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:134 | review | ui.zone_tests.399c7759 | daralan çember hedefi kapsar |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:143 | review | ui.zone_tests.8d95eeee | Son faz yarıçapı 0 |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:170 | review | ui.zone_tests.3f76130f | faz sonu yarıçap |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:181 | review | ui.zone_tests.29dd0f2e | daralan çember faz başının içinde |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:182 | review | ui.zone_tests.399c7759 | daralan çember hedefi kapsar |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:224 | review | ui.zone_tests.e983abcb | Daralma başında yarıçap değişmez |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:262 | review | ui.zone_tests.09912e65 | Faz 0 bekleme süresi |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:263 | review | ui.zone_tests.1da71639 | Faz 0 daralma süresi |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:266 | review | ui.zone_tests.31c910ed | Bitince de son çember dışında hasar sürer |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:347 | review | ui.zone_tests.aca2a208 | Sınır sağlanamasa da yeni çember tamamen içeride |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:403 | review | ui.zone_tests.17513ba3 | Elle daraltmadan sonra hedef içeride |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:406 | review | ui.zone_tests.266f6603 | Daralma sürerken de içeride |
| Assets/_Project/Tests/EditMode/ZoneTests.cs:413 | review | ui.zone_tests.c6ad8324 | Null faz planı → varsayılan plan |
