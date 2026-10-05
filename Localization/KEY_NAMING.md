# Anahtar adlandırma standardı

Biçim: `kapsam.alt_kapsam.kimlik` — yalnızca `[a-z0-9_.]`, en az iki segment.

## Önekler

| Önek | Kullanım | Örnek |
|------|----------|-------|
| `brand.` | Marka / sabit ad | `brand.title` |
| `loading.` | Yükleme ekranı | `loading.tip.01` |
| `pause.` | Duraklat menüsü | `pause.btn.resume` |
| `settings.` | Ayarlar | `settings.audio.master` |
| `menu.` | Ana / kurulum menüsü | `menu.difficulty.er` |
| `hud.` | Oyun içi HUD | `hud.ammo.reload` |
| `notify.` | Bildirim / toast | `notify.command.takeover` |
| `match.` | Maç mesajları | `match.msg.zone_shrink` |
| `order.` | Tim emirleri | `order.name.follow` |
| `role.` | Tim rolleri | `role.medic` / `role.medic.abbr` |
| `rank.` | Rütbeler | `rank.yuzbasi` / `rank.yuzbasi.abbr` |
| `item.` | Eşya / kategori | `item.bandage` |
| `weapon.` | Silah / sınıf | `weapon.mpt76` |
| `location.` | Harita lokasyonu | `location.kuzgun_koyu` |
| `team.` | Tim / kill feed | `team.blue` |
| `damage.` | Hasar kaynakları | `damage.frag` |

## Kurallar

1. **Kimlik dili İngilizce** (`reload`, `follow`); görünen metin tabloda.
2. **Kısaltma** için `.abbr` soneki (`rank.tegmen.abbr`).
3. **Numaralı seriler** sıfır dolgulu (`loading.tip.01`).
4. **Placeholder** yalnızca `{0}`, `{1}` veya `{name}` — diller arası aynı küme.
5. **Marka / silah modeli çevrilmez** (HAREKÂT, SAR 9, MPT-76) — tüm dillerde aynı hücre.
6. Yeni anahtar: önce `ASKERI_TERIMLER_SOZLUGU.md`, sonra `strings.csv`, sonra `npm run validate`.
