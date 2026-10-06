# Telsiz replikleri v2 - ses kapsama durumu

Tarih: 2026-10-06. Kaynak: `Design/Audio/telsiz_replikleri_v2.csv` (590 id). Format: Ogg Vorbis mono 22.05 kHz (`Assets/_Project/Resources/Audio/Voice/v2/<ses>/<stres>/<id>.ogg`).

## Kapsama

- Üretilen sesler: `dfki_er`, `dfki_kalin`, `dfki_genc` (3 ses x 3 stres x 590 id = 5310 klip).
- `yelda`: v2 CSV için üretilmedi (bilinçli atlandı).
- Her id, her üç sesin her üç stresinde (sakin/catisma/panik) mevcuttur; CSV'deki `stress` sütunu yalnızca replik niyetidir, klip her stres için üretildi.
- Kapsanmayan id: 0.

## Kategori başına id sayısı (3 dfki sesi ile kapsanır)

| kategori | id | kapsayan ses |
|---|---|---|
| ack_er | 13 | dfki_er, dfki_kalin, dfki_genc |
| ack_nco | 9 | dfki_er, dfki_kalin, dfki_genc |
| ack_negative | 5 | dfki_er, dfki_kalin, dfki_genc |
| ack_peer | 12 | dfki_er, dfki_kalin, dfki_genc |
| ack_superior | 9 | dfki_er, dfki_kalin, dfki_genc |
| advance | 15 | dfki_er, dfki_kalin, dfki_genc |
| ammo_request | 8 | dfki_er, dfki_kalin, dfki_genc |
| artillery_warn | 4 | dfki_er, dfki_kalin, dfki_genc |
| bleeding | 6 | dfki_er, dfki_kalin, dfki_genc |
| breath | 4 | dfki_er, dfki_kalin, dfki_genc |
| calm_down | 5 | dfki_er, dfki_kalin, dfki_genc |
| clear | 9 | dfki_er, dfki_kalin, dfki_genc |
| clear_room | 5 | dfki_er, dfki_kalin, dfki_genc |
| contact_full | 9 | dfki_er, dfki_kalin, dfki_genc |
| contact_lost | 4 | dfki_er, dfki_kalin, dfki_genc |
| cover_me | 12 | dfki_er, dfki_kalin, dfki_genc |
| covering | 9 | dfki_er, dfki_kalin, dfki_genc |
| critical | 4 | dfki_er, dfki_kalin, dfki_genc |
| defeat | 2 | dfki_er, dfki_kalin, dfki_genc |
| distance_full | 16 | dfki_er, dfki_kalin, dfki_genc |
| empty_dry | 2 | dfki_er, dfki_kalin, dfki_genc |
| enemy_down | 14 | dfki_er, dfki_kalin, dfki_genc |
| enemy_down_hs | 5 | dfki_er, dfki_kalin, dfki_genc |
| enemy_down_multi | 5 | dfki_er, dfki_kalin, dfki_genc |
| enemy_flank_warn | 5 | dfki_er, dfki_kalin, dfki_genc |
| enemy_reload | 2 | dfki_er, dfki_kalin, dfki_genc |
| er_asks | 5 | dfki_er, dfki_kalin, dfki_genc |
| fall_back | 9 | dfki_er, dfki_kalin, dfki_genc |
| flank | 8 | dfki_er, dfki_kalin, dfki_genc |
| flash | 2 | dfki_er, dfki_kalin, dfki_genc |
| frag_clear | 2 | dfki_er, dfki_kalin, dfki_genc |
| friendly_fire | 5 | dfki_er, dfki_kalin, dfki_genc |
| grenade_incoming | 11 | dfki_er, dfki_kalin, dfki_genc |
| grenade_out | 2 | dfki_er, dfki_kalin, dfki_genc |
| grenade_throw | 12 | dfki_er, dfki_kalin, dfki_genc |
| heal_done | 2 | dfki_er, dfki_kalin, dfki_genc |
| heli_inc | 2 | dfki_er, dfki_kalin, dfki_genc |
| hit_self | 12 | dfki_er, dfki_kalin, dfki_genc |
| holding | 5 | dfki_er, dfki_kalin, dfki_genc |
| jam | 2 | dfki_er, dfki_kalin, dfki_genc |
| last_enemy | 2 | dfki_er, dfki_kalin, dfki_genc |
| leader_command_take | 4 | dfki_er, dfki_kalin, dfki_genc |
| leader_orders | 10 | dfki_er, dfki_kalin, dfki_genc |
| leader_praise | 3 | dfki_er, dfki_kalin, dfki_genc |
| loot_found | 3 | dfki_er, dfki_kalin, dfki_genc |
| low_health | 4 | dfki_er, dfki_kalin, dfki_genc |
| mag_empty | 13 | dfki_er, dfki_kalin, dfki_genc |
| man_down | 10 | dfki_er, dfki_kalin, dfki_genc |
| medic | 10 | dfki_er, dfki_kalin, dfki_genc |
| moving_up | 2 | dfki_er, dfki_kalin, dfki_genc |
| order_hold | 3 | dfki_er, dfki_kalin, dfki_genc |
| order_move | 3 | dfki_er, dfki_kalin, dfki_genc |
| part_clock | 24 | dfki_er, dfki_kalin, dfki_genc |
| part_num | 20 | dfki_er, dfki_kalin, dfki_genc |
| part_open | 18 | dfki_er, dfki_kalin, dfki_genc |
| part_tail | 10 | dfki_er, dfki_kalin, dfki_genc |
| part_target | 19 | dfki_er, dfki_kalin, dfki_genc |
| part_unit | 2 | dfki_er, dfki_kalin, dfki_genc |
| praise | 3 | dfki_er, dfki_kalin, dfki_genc |
| quiet | 3 | dfki_er, dfki_kalin, dfki_genc |
| radio_check | 6 | dfki_er, dfki_kalin, dfki_genc |
| relieved | 3 | dfki_er, dfki_kalin, dfki_genc |
| reload | 13 | dfki_er, dfki_kalin, dfki_genc |
| reload_last | 8 | dfki_er, dfki_kalin, dfki_genc |
| report_status | 8 | dfki_er, dfki_kalin, dfki_genc |
| report_to_leader | 11 | dfki_er, dfki_kalin, dfki_genc |
| request_support | 5 | dfki_er, dfki_kalin, dfki_genc |
| revive_thanks | 5 | dfki_er, dfki_kalin, dfki_genc |
| reviving | 5 | dfki_er, dfki_kalin, dfki_genc |
| self_heal | 5 | dfki_er, dfki_kalin, dfki_genc |
| sitrep | 3 | dfki_er, dfki_kalin, dfki_genc |
| smoke | 11 | dfki_er, dfki_kalin, dfki_genc |
| smoke_used | 4 | dfki_er, dfki_kalin, dfki_genc |
| sniper_warn | 4 | dfki_er, dfki_kalin, dfki_genc |
| squad_kill_ack | 6 | dfki_er, dfki_kalin, dfki_genc |
| squad_lost | 2 | dfki_er, dfki_kalin, dfki_genc |
| suppress | 8 | dfki_er, dfki_kalin, dfki_genc |
| taking_fire | 10 | dfki_er, dfki_kalin, dfki_genc |
| thanks | 3 | dfki_er, dfki_kalin, dfki_genc |
| vehicle_inc | 2 | dfki_er, dfki_kalin, dfki_genc |
| victory | 5 | dfki_er, dfki_kalin, dfki_genc |
| watch | 4 | dfki_er, dfki_kalin, dfki_genc |
| wounded | 15 | dfki_er, dfki_kalin, dfki_genc |
| zone_move | 6 | dfki_er, dfki_kalin, dfki_genc |

## Id listesi

Tüm id'ler ve metinleri `Voice/v2/voice_manifest_v2.json` içinde (`codec: ogg` girişleri). Lisans: dfki CC BY-NC-SA 4.0, yalnızca yer tutucu (bkz. `Docs/SES_TTS_NOTU.md`).
