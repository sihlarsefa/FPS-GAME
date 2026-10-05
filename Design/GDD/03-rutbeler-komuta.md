# 3. TSK Rütbeleri ve Komuta Zinciri

Kaynak: `MilitaryRank`, `RankCatalog`, `ChainOfCommandService` (sözleşme).

## 3.1 Rütbe listesi (küçükten büyüğe)

| Enum | Tam ad | Kısa | Sınıf | XP eşiği |
|------|--------|------|-------|----------|
| Er | Er | Er | Er/Erbaş | 0 |
| Onbasi | Onbaşı | Onb. | Er/Erbaş | 500 |
| Cavus | Çavuş | Çvş. | Er/Erbaş | 1 200 |
| SozlesmeliEr | Sözleşmeli Er | Sözl.Er | Er/Erbaş | 2 200 |
| UzmanOnbasi | Uzman Onbaşı | Uzm.Onb. | Uzman Erbaş | 3 500 |
| UzmanCavus | Uzman Çavuş | Uzm.Çvş. | Uzman Erbaş | 5 200 |
| AstsubayCavus | Astsubay Çavuş | Astsb.Çvş. | Astsubay | 7 500 |
| AstsubayKidemliCavus | Astsubay Kıdemli Çavuş | Astsb.Kd.Çvş. | Astsubay | 10 000 |
| AstsubayUstcavus | Astsubay Üstçavuş | Astsb.Üçvş. | Astsubay | 13 000 |
| AstsubayKidemliUstcavus | Astsubay Kıdemli Üstçavuş | Astsb.Kd.Üçvş. | Astsubay | 16 500 |
| AstsubayBascavus | Astsubay Başçavuş | Astsb.Bçvş. | Astsubay | 20 500 |
| AstsubayKidemliBascavus | Astsubay Kıdemli Başçavuş | Astsb.Kd.Bçvş. | Astsubay | 25 000 |
| Astegmen | Asteğmen | Asteğmen | Subay | 30 000 |
| Tegmen | Teğmen | Teğmen | Subay | 36 000 |
| Ustegmen | Üsteğmen | Üsteğmen | Subay | 43 000 |
| Yuzbasi | Yüzbaşı | Yzb. | Subay | 51 000 |
| Binbasi | Binbaşı | Bnb. | Subay | 62 000 |
| Yarbay | Yarbay | Yb. | Subay | 75 000 |
| Albay | Albay | Alb. | Subay | 90 000 |

Sınıflar: Er/Erbaş → Uzman Erbaş → Astsubay → Subay.

## 3.2 Maç içi tim rütbe dağılımı

`RankForTeamSlot` (kayıt sırası kıdemle uyumlu):

| Slot | Tipik rütbe |
|------|-------------|
| 0 (komutan) | %60 Yüzbaşı / %40 Üsteğmen |
| 1 | Astsb.Kd.Çvş. veya Astsb.Üçvş. |
| 2–3 | Uzman Çavuş |
| 4–5 | Uzm.Onb. veya Sözl.Er |
| 6–9 | Sözl.Er / Çvş. / Onb. / Er |

Görünen ad: `RankCatalog.FormatName` → örn. `Yzb. Kartal`.

## 3.3 Komuta devri

1. Tim, rütbe kıdemine göre sıralanır (büyük enum = kıdemli).  
2. Aktif komutan ölür veya maç dışı kalır → `CommandTransferredEvent`.  
3. Yeni komutan: hayattaki en kıdemli üye.  
4. AI takipçiler yeni lidere bağlanır.  
5. Oyuncu (komutan) ölürse AI zinciri devam eder; oyuncu izler.

## 3.4 Kariyer rütbesi vs maç içi rütbe

| Tür | Kaynak | Amaç |
|-----|--------|------|
| Kariyer | Toplam XP → `RankForExperience` | Profil, nişan, uzun vadeli ilerleme |
| Maç içi slot | `RankForTeamSlot` | Tatbikat komuta hissi, HUD |

Kariyer XP kazanımı: [12-ilerleme.md](12-ilerleme.md).
