# 1. Vizyon ve Oyun Döngüsü

## 1.1 Vizyon

**HAREKÂT**, oyuncuya bir **tim komutanı** hissi verir: 10 kişilik bir birliği yönetir, TSK tarzı rütbe ve komuta zinciriyle emir verir, Türk yapımı silahlarla dağlık arazide son tim ayakta kalana kadar savaşır.

Oyuncu fantazisi üç katmandadır:

1. **Komuta** — Emirler (Takip, Mevzi, Taarruz, Toplan), topçu çağrısı, intikal seçimi.
2. **Tim** — Roller birbirini tamamlar; tek kahramanlık yerine koordinasyon kazanır.
3. **Tatbikat** — Mavi / Kırmızı kuvvetler; gerçek örgüt adı yok; “harekât tatbikatı” kurgusu.

## 1.2 Tasarım direkleri

| Direk | Açıklama |
|-------|----------|
| Tim ölçeği | 10 kişi; BR’nin klasik 4’lü squad’dan daha “birlik” hissi |
| Komuta zinciri | Rütbe kıdemi; komutan düşünce komuta otomatik devredilir |
| Türk silah ekosistemi | Katalogdaki milli / yerli silah kimlikleri |
| Okunabilir arazi | Kuzgun Vadisi: dere, köprü, köy, karakol, FOB, tepeler |
| Adil ilerleme | Kozmetik dışı güç satışı yok |

## 1.3 Maç döngüsü (çekirdek döngü)

```
Lobide tim / ayar → Intikal seçimi (T-70 | Kirpi)
  → İniş / çıkış → Yağma & mevzi
  → Zone bekleme/daralma döngüsü
  → Çatışma / topçu / emirler
  → Son tim ayakta → Sonuç & XP
  → Kariyer / menü
```

### Fazlar

| Faz | Süre (hedef) | Oyuncu odağı |
|-----|--------------|--------------|
| Ön maç | ~6 sn | Hazırlık, intikal onayı |
| Erken oyun | 0–4 dk | İniş sektörü, temel teçhizat, ilk temas |
| Orta oyun | 4–12 dk | Zone rotasyonu, FOB/karakol baskınları, topçu |
| Geç oyun | 12–25 dk | Dar çember, mevzi savaşları, komuta baskısı |
| Sonuç | anlık | Sıralama, XP, rütbe ilerlemesi |

Varsayılan `MatchConfig`: 4 tim × 10 = 40 savaşçı; harita yarı boyutu 512 m; maç süresi üst sınırı 1500 sn.

## 1.4 Kazanma ve kaybetme

- **Kazanma:** Rakip tüm timler elendiğinde (son ayakta kalan tim).
- **Oyuncu ölümü:** Offline’da oyuncu genelde Tim Komutanı’dır; ölünce komuta kıdem sırasına geçer, AI tim savaşmaya devam eder; oyuncu izleyici / sonuç akışına geçer.
- **Dost ateşi:** Varsayılan kapalı (`FriendlyFire = false`).

## 1.5 Oturum döngüsü (meta)

1. Ana menü → Eğitim / Hızlı maç / Ayarlar  
2. Maç sonucu → XP (`CareerStatsService`)  
3. Rütbe eşiği aşılırsa terfi bildirimi  
4. Kozmetik / profil (gelecek) → yeniden kuyruk  

## 1.6 Başarı ölçütleri (tasarım KPI)

| Ölçüt | Hedef (tasarım) |
|-------|-----------------|
| İlk 10 dk’da “tim hissi” | Emir kullanma oranı > %50 (yeni oyuncu) |
| Ortalama maç süresi | 12–20 dk (erken elenme dahil) |
| Silah çeşitliliği | Üst 3 silah toplam kill < %55 |
| Topçu kullanımı | Maç başına ≥ 1 çağrı (telsizci hayattayken) |
