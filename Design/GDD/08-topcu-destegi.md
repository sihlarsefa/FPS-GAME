# 8. Topçu Desteği

Kaynak: `ArtilleryService`, `MatchConfig.ArtilleryCooldownSeconds`.

## 8.1 Kim çağırır?

**Telsizci** rolü (slot 4) — anlatı olarak telsiz üzerinden batarya ateşi.  
Teknik çağrı: `ArtilleryService.TryCall(team, caller, target)`.

## 8.2 Parametreler

| Parametre | Değer |
|-----------|-------|
| Bekleme (cooldown) | **150 sn** / tim |
| Mermi sayısı / atış | 8 |
| Yayılma yarıçapı | 18 m |
| İlk gecikme | 6 sn (ıslık öncesi) |
| Mermi hasarı | 120 |
| Patlama yarıçapı | 9 m |
| Mermiler arası | 0.35–0.75 sn |
| Etkin kalma (linger) | son mermiden +1.5 sn |

Hasar kaynağı adı: **Topçu Ateşi** (`DamageSourceIds.Artillery`).

## 8.3 Oynanış döngüsü

1. Telsizci / komutan hedef işaretler (harita veya nişangâh).  
2. Onay → 6 sn uyarı (ıslık: `ArtilleryWhistle`).  
3. 8 mermi yağar; botlar `IsInDangerZone` ile kaçar.  
4. Tim cooldown’a girer.

## 8.4 Tasarım gerekçesi

- **Güçlü ama seyrek:** 150 sn, tek tim başına tek “süper silah” ritmi.  
- **Okunabilir:** Islık + harita işareti → karşı taraf kaçabilir (beceri vs şans dengesi).  
- **Rol kimliği:** Telsizciyi hayatta tutmak stratejik; [18-rol-yetenekleri.md](18-rol-yetenekleri.md).

## 8.5 Kötüye kullanım sınırları

- Dost ateşi kapalıysa müttefik hasarı yok (varsayılan).  
- Zone dışı hedef: geçerli (kaçan düşmanı cezalandırma).  
- Spam yok: cooldown sert.
