# İlerleme — Başarımlar, Sezon, Kozmetik

GÖREV C3-6 tohum verisi. Cursor `Backend/Harekat.Domain` kataloglarına aktarır.

## Dosyalar

| Dosya | Amaç | Domain eşlemesi |
|-------|------|-----------------|
| `achievements.json` | 50+ başarım | `AchievementCatalog.Definition(Id, Title, Description, Metric, Target)` |
| `cosmetics.json` | Kozmetik katalog | `CosmeticCatalog.Item(Id, Name, Slot, UnlockXp)` |
| `season1.json` | 10 haftalık Sezon 1 | `Season(Number, Name, StartsAt, EndsAt)` + `Player.SeasonXp` |
| `ekonomi.md` | UZATMA: kazanım / süre analizi | — |

## Sert kurallar

- **Pay-to-win yok.** Kozmetik ve sezon ödülleri yalnızca görünüm.
- Kariyer rütbesi (`Stats.Experience` → `RankCatalog`) **kalıcı**.
- Sezon puanı (`SeasonXp`) arşivde sıfırlanır; rozet `SeasonArchiveEntry.RewardBadge` olarak kalır.
- Backend tohum alanları: başarımlarda `id/title/description/metric/target`; kozmetikte `id/name/slot/unlockXp`; sezonda `number/name/startsAt/endsAt`.

## Metrik sözleşmesi

`Player.AchievementProgress[metric] >= target` → `AchievementService.EvaluateAndUnlock`.

Mevcut backend metrikleri korunur. Yeni metrikler (`jng90_hs_300m`, `artillery_hits`, `command_wins`, silah kill sayaçları vb.) Unity olaylarından (`HitConfirmedEvent`, `ArtilleryStrikeEvent`, `CommandTransferredEvent`, …) MatchResult / telemetry yoluna bağlanacak.

## Slotlar

`camo` · `beret` · `armband` · `weapon_skin` · `victory_pose` · `emblem_frame`

Backend donatım şu an `EquippedCamo` / `EquippedBeret`; diğer slotlar genişletme tohumu.

## Sayılar (C3-6)

Bkz. `Design/CODEX_DURUM.md` FAZ 3 / C3-6.
