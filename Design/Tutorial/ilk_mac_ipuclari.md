# İlk Maç Rehberliği — Bağlamsal İpuçları

Poligon bitince (veya atlanırsa) ilk **3 maçta** gösterilir. Her ipucu bir kez; oyuncu kapatırsa aynı maçta tekrarlanmaz. Kalıcılık: `career.firstMatchTipsSeen[]`.

## Gösterim kuralları

1. HUD orta-alt banner, max **6 sn**; Esc / tık ile kapat.
2. Savaş baskısı yüksekken (yakın hasar, ADS, zone shrink) ertele — kuyrukta tut.
3. Aynı anda en fazla **1** ipucu.
4. Maç 1–3 dışında gösterme (Komando parkuru hariç — orada kapalı).
5. Telemetri: `first_match_tip_shown`, `first_match_tip_dismissed`, `first_zone_damage`, `session_d1_return`.

---

## Maç 1 — İniş ve yağma

| id | Tetikleyici | Metin TR | Metin EN |
|----|-------------|----------|----------|
| `m1_insert` | `MatchPhaseChangedEvent` → İntikal/Yağma | İniş sonrası hemen silah ve mermi topla. Envanter: TAB. | After landing, grab a gun and ammo. Inventory: TAB. |
| `m1_loot` | `LootPickedUpEvent` (ilk) | Yağma alındı. Ağırlık kapasitesini aşma; gereksiz eşyayı bırak. | Loot secured. Watch weight capacity; drop what you don’t need. |
| `m1_follow` | İniş + 45 sn, emir yok | Timine F1 Takip ver; yalnız kalma. | Issue F1 Follow so your squad stays with you. |
| `m1_heal` | `PlayerDamagedEvent` RemainingHealth &lt; 70 | Yaralandın. H ile sargı — 75 HP tavanı. | You’re hurt. Bandage with H — caps at 75 HP. |
| `m1_zone` | `ZoneStageChangedEvent` (ilk shrink/warning) | Beyaz çember güvenli alan. Dışarıda can yanar. | White circle is the safe zone. Outside deals damage. |

## Maç 2 — Komuta ve temas

| id | Tetikleyici | Metin TR | Metin EN |
|----|-------------|----------|----------|
| `m2_orders` | Temas sesi / ilk düşman görüşü, emir yok | Temas yakın. F2 Mevzi veya F3 Taarruz ver. | Contact nearby. Issue F2 Hold or F3 Attack. |
| `m2_smoke` | Açık alanda hasar + sis envanterde | Açık alan! T ile sis at, geçişi ört. | Open ground! Throw smoke (T) to cover the cross. |
| `m2_firemode` | 80 m+ mesafede Auto ateş | Uzun menzilde B ile Tekli moda geç. | At long range, switch to Single with B. |
| `m2_command` | `CommandTransferredEvent` (oyuncu yeni komutan) | Komuta sende. Tim seni dinliyor — F1–F4. | You have command. Your squad listens — F1–F4. |
| `m2_artillery` | Telsizci hayatta + 2. zone fazı | V ile topçu çağırabilirsin (CD ~150 sn). | Call artillery with V (≈150 s cooldown). |

## Maç 3 — Sağkalım ve araç

| id | Tetikleyici | Metin TR | Metin EN |
|----|-------------|----------|----------|
| `m3_medkit` | RemainingHealth &lt; 40 + medkit var | Kritik! Sıhhiye çantası 100 HP’ye çıkar. | Critical! Medkit restores up to 100 HP. |
| `m3_kirpi` | `DrivableVehicle` 30 m içinde, sürücü yok | Yakında Kirpi var. F ile bin; timi taşı. | Kirpi nearby. Enter with F; ferry your squad. |
| `m3_regroup` | Tim üyesi &gt;80 m dağınık | Tim dağıldı. F4 Toplan — işaret koy. | Squad scattered. F4 Regroup — place a marker. |
| `m3_final` | Son 2 tim / son zone | Son çember. Sis + mevzi; acele etme. | Final circle. Use smoke and hold; don’t rush. |
| `m3_done` | `MatchEndedEvent` (ilk 3 maç özeti) | Eğitim ipuçları bitti. Poligonda Komando parkuru dene. | Match tips done. Try the Commando course on the range. |

---

## Tetikleyici → Core/Events eşlemesi

| Tetikleyici özeti | Olay / sinyal |
|-------------------|---------------|
| Faz değişimi | `MatchPhaseChangedEvent` |
| Yağma | `LootPickedUpEvent` |
| Hasar / can | `PlayerDamagedEvent` |
| Zone | `ZoneStageChangedEvent` |
| Komuta devri | `CommandTransferredEvent` |
| Emir verildi | `SquadOrderIssuedEvent` (ipucu bastırma) |
| Topçu | `ArtilleryStrikeEvent` |
| Maç sonu | `MatchEndedEvent` |
| Araç yakın | sunum: `VehicleRegistry.FindNearest` |
| Temas algısı | sunum: bot algı / ses katmanı |

## Bastırma (dismiss / already know)

Aşağıdakilerden biri olursa ilgili ipucu bir daha gösterilmez:

- Oyuncu zaten ilgili eylemi yaptı (`SquadOrderIssuedEvent`, `ItemUsedEvent`, `ArtilleryStrikeEvent`…).
- `career.matchesPlayed >= 3`.
- Ayar: «Eğitim ipuçlarını kapat».

## Ölçüm (GDD 21.6 ile uyumlu)

`tutorial_step_completed`, `first_order_issued`, `first_zone_damage`, `first_artillery_call`, `session_d1_return`, artı `first_match_tip_*`.
