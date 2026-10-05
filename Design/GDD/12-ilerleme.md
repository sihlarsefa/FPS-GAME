# 12. İlerleme Sistemi

Kaynak: `CareerStatsService` / `SettingsService` XP sabitleri, `RankCatalog`.

## 12.1 Maç sonu XP

| Kaynak | XP |
|--------|-----|
| Öldürme | 100 |
| Kafa isabeti (bonus) | +25 |
| Geçilen rakip tim | 150 × (TeamCount − placement) |
| Zafer | 1000 |

Örnek: 4 timlik maçta 2. sıra, 5 kill, 1 HS →  
`5×100 + 25 + 150×(4−2) = 500+25+300 = 825` (+galibiyet yok).

## 12.2 Rütbe ilerlemesi

Toplam XP → `RankForExperience` (eşikler [03-rutbeler-komuta.md](03-rutbeler-komuta.md)).  
HUD: mevcut rütbe, sonraki eşiğe ilerleme çubuğu (`ProgressToNextRank`).

## 12.3 Kariyer istatistikleri (hedef)

- Maç / galibiyet / top 2  
- Kill, death, HS oranı  
- En çok kullanılan silah  
- Topçu çağrıları / isabetli destek (gelecek metrik)  
- Favori rol

## 12.4 Sezon (gelecek)

- Sezonluk nişan / kamuflaj (kozmetik)  
- Soft reset yok; rütbe kalıcı, sezon rozeti eklenir  

## 12.5 Eğitim ödülü

Poligon görevleri küçük XP verir (spam önleme: günlük tavan) — [21-ilk-10-dk-tutorial.md](21-ilk-10-dk-tutorial.md).
