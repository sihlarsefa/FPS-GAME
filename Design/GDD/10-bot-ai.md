# 10. Botlar ve Yapay Zekâ

Kaynak: `BotDecisionService`, `BotSenses`, `SquadOrder`.

## 10.1 Tasarım hedefi

Botlar “kusursuz nişancı” değil; **okunabilir askeri davranış** sergiler: emre uyar, bölgeden kaçar, yağmalar, iyileşir. Performans: ~60 savaşçı, kademeli tick.

## 10.2 Karar önceliği (bireysel)

1. **Engage** — Düşman görünür + silahlı (veya <4 m yumruk).  
2. Silahsız + uzak düşman → **Flee** veya **Loot**.  
3. **MoveToZone** — Bölge dışı (emirlerden önce).  
4. **Heal** — Can < %60, iyileştirme var, 4 sn düşman yok (histerezis %85’e kadar).  
5. **Investigate** — Silah sesi / son bilinen düşman (<10 sn bellek).  
6. **Loot** — İhtiyaç + bilinen yağma.  
7. Sonraki zone dışıysa **MoveToZone**.  
8. Aksi halde **Roam**.

## 10.3 Tim (squad) kuralları

Takipçi (`IsSquadMember`, lider ≠ kendisi):

| Koşul | Durum |
|-------|--------|
| Görünen silahlı düşman | Engage (mutlak) |
| Bölge dışı | MoveToZone |
| Emir: Mevzi | Hold |
| Emir: Taarruz | Assault |
| Emir: Follow/Regroup | Lidere >8 m → Follow; <4 m Idle; Follow iken ≤10 m yağma alınabilir |
| Emir yok | 30 m tasma; tasma içinde ses araştır / ≤20 m yağma |

## 10.4 Silah tercihi

Bot yağmada `WeaponTier` 1→4 yükseltir (tabanca → 7.62 platform). Rol loadout’u başlangıç avantajı verir.

## 10.5 Zorluk (`BotDifficulty`)

Normal varsayılan. Profil etkileri (tasarım): tepki süresi, isabet sapması, heal agresyonu, ses duyma yarıçapı. Kolayda tasma gevşer; Zorda investigate süresi uzar.

## 10.6 Topçu kaçınma

Aktif topçu bölgesinde botlar danger zone’dan çıkar — oyuncuya “atıstan kaçılabilir” okunurluğu sağlar.
