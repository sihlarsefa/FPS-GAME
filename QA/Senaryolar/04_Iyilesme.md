# İyileşme

Senaryo sayısı: **22** · Öncelik: P1=22

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## HEAL-001 — Sargı Bezi: tam kullanım

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sargı Bezi; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 50 yap
2. Envanterden kullan
3. 4 sn bekle

### Beklenen sonuç

Can 60 olur; eşya sayısı bir azalır; kullanım çubuğu kapanır.

---

## HEAL-002 — Sargı Bezi: iyileşme tavanı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sargı Bezi; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 75 yap
2. Kullanmayı dene

### Beklenen sonuç

Kullanım başlamaz; can ve eşya sayısı değişmez.

---

## HEAL-003 — Sargı Bezi: ateşle iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sargı Bezi; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 50 yap
2. Kullan
3. Süre dolmadan ateş et

### Beklenen sonuç

Kullanım iptal olur; eşya harcanmaz, iyileşme uygulanmaz.

---

## HEAL-004 — Sargı Bezi: silah değişiminde iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sargı Bezi; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 50 yap
2. Kullan
3. İkinci silahı seç

### Beklenen sonuç

Kullanım/animasyon kapanır; eşya tüketilmez.

---

## HEAL-005 — Sargı Bezi: kullanımda hız

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sargı Bezi; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 50 yap
2. Kullanırken düz yürü
3. Kullanımdan sonra yürü

### Beklenen sonuç

Kullanımda hız çarpanı 0,5; bitiş/iptal sonrası 1,0 olur.

---

## HEAL-006 — İlk Yardım Çantası: tam kullanım

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet İlk Yardım Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Envanterden kullan
3. 6 sn bekle

### Beklenen sonuç

Can 75 olur; eşya sayısı bir azalır; kullanım çubuğu kapanır.

---

## HEAL-007 — İlk Yardım Çantası: iyileşme tavanı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet İlk Yardım Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 75 yap
2. Kullanmayı dene

### Beklenen sonuç

Kullanım başlamaz; can ve eşya sayısı değişmez.

---

## HEAL-008 — İlk Yardım Çantası: ateşle iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet İlk Yardım Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullan
3. Süre dolmadan ateş et

### Beklenen sonuç

Kullanım iptal olur; eşya harcanmaz, iyileşme uygulanmaz.

---

## HEAL-009 — İlk Yardım Çantası: silah değişiminde iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet İlk Yardım Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullan
3. İkinci silahı seç

### Beklenen sonuç

Kullanım/animasyon kapanır; eşya tüketilmez.

---

## HEAL-010 — İlk Yardım Çantası: kullanımda hız

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet İlk Yardım Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullanırken düz yürü
3. Kullanımdan sonra yürü

### Beklenen sonuç

Kullanımda hız çarpanı 0,5; bitiş/iptal sonrası 1,0 olur.

---

## HEAL-011 — Sıhhiye Çantası: tam kullanım

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sıhhiye Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Envanterden kullan
3. 8 sn bekle

### Beklenen sonuç

Can 100 olur; eşya sayısı bir azalır; kullanım çubuğu kapanır.

---

## HEAL-012 — Sıhhiye Çantası: iyileşme tavanı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sıhhiye Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 100 yap
2. Kullanmayı dene

### Beklenen sonuç

Kullanım başlamaz; can ve eşya sayısı değişmez.

---

## HEAL-013 — Sıhhiye Çantası: ateşle iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sıhhiye Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullan
3. Süre dolmadan ateş et

### Beklenen sonuç

Kullanım iptal olur; eşya harcanmaz, iyileşme uygulanmaz.

---

## HEAL-014 — Sıhhiye Çantası: silah değişiminde iptal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sıhhiye Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullan
3. İkinci silahı seç

### Beklenen sonuç

Kullanım/animasyon kapanır; eşya tüketilmez.

---

## HEAL-015 — Sıhhiye Çantası: kullanımda hız

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ItemUseService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

100 azami can; envanterde 2 adet Sıhhiye Çantası; boost 0; can test düzeneği erişilebilir.

### Adımlar

1. Canı 20 yap
2. Kullanırken düz yürü
3. Kullanımdan sonra yürü

### Beklenen sonuç

Kullanımda hız çarpanı 0,5; bitiş/iptal sonrası 1,0 olur.

---

## HEAL-016 — Enerji içeceği

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Boost 0 iken enerji içeceğini kullan
2. 4 saniye bekle

### Beklenen sonuç

40 boost eklenir; zamanla azalma varsa bitiş tick sırası kaydedilir; eşya bir azalır.

---

## HEAL-017 — Ağrı kesici

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Boost 0 iken ağrı kesici kullan
2. 6 saniye bekle

### Beklenen sonuç

60 boost eklenir; can anlık tam dolmaz.

---

## HEAL-018 — Boost tavanı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Boost 100 iken takviye kullanmayı dene

### Beklenen sonuç

Kullanım başlamaz; eşya boşa harcanmaz.

---

## HEAL-019 — Boost sönümü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Boost 60 yap
2. 10 saniye eşya kullanmadan bekle

### Beklenen sonuç

Boost yaklaşık 6 azalır; 0 altına inmez.

---

## HEAL-020 — Ölüm sırasında iyileşme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Sargı kullan
2. Süre dolmadan öldürücü hasar al

### Beklenen sonuç

Ölü karakter iyileşmez; kullanım sonlandırılır.

---

## HEAL-021 — Zıplamayla kullanım iptali

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. Yaralıyken H bas
2. Kullanım sırasında Space bas

### Beklenen sonuç

Kullanım iptal olur ve tüketim gerçekleşmez.

---

## HEAL-022 — Yaralanma tek ölüm bildirimi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/BoostService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test sahnesi; 100 azami can, yaralı karakter, enerji içeceği/ağrı kesici; boost sayacı okunabilir.

### Adımlar

1. 1 can hedefe aynı karede iki öldürücü isabet uygula

### Beklenen sonuç

Ölüm, skor ve yağma tek kez işlenir; öldürme sayısı çift artmaz.

---
