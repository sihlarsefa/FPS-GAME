# Zırh

Senaryo sayısı: **18** · Öncelik: P1=18

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## ARM-001 — Yelek Sv.1: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Yelek, dayanıklılık 200; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. gövde bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 28; dayanıklılık kaybı 12; azalma yüzde 30.

---

## ARM-002 — Yelek Sv.1: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Yelek, dayanıklılık 200; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-003 — Yelek Sv.1: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Yelek, dayanıklılık 200; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 200 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---

## ARM-004 — Yelek Sv.2: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Yelek, dayanıklılık 220; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. gövde bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 24; dayanıklılık kaybı 16; azalma yüzde 40.

---

## ARM-005 — Yelek Sv.2: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Yelek, dayanıklılık 220; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-006 — Yelek Sv.2: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Yelek, dayanıklılık 220; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 220 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---

## ARM-007 — Yelek Sv.3: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Yelek, dayanıklılık 250; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. gövde bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 18; dayanıklılık kaybı 22; azalma yüzde 55.

---

## ARM-008 — Yelek Sv.3: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Yelek, dayanıklılık 250; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-009 — Yelek Sv.3: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Yelek, dayanıklılık 250; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 250 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---

## ARM-010 — Kask Sv.1: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Kask, dayanıklılık 80; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. kafa bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 28; dayanıklılık kaybı 12; azalma yüzde 30.

---

## ARM-011 — Kask Sv.1: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Kask, dayanıklılık 80; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-012 — Kask Sv.1: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.1 Kask, dayanıklılık 80; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 80 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---

## ARM-013 — Kask Sv.2: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Kask, dayanıklılık 150; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. kafa bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 24; dayanıklılık kaybı 16; azalma yüzde 40.

---

## ARM-014 — Kask Sv.2: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Kask, dayanıklılık 150; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-015 — Kask Sv.2: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.2 Kask, dayanıklılık 150; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 150 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---

## ARM-016 — Kask Sv.3: azaltma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Kask, dayanıklılık 230; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. kafa bölgesine 40 ham hasar uygula
2. Can ve dayanıklılık farkını ölç

### Beklenen sonuç

Can kaybı 18; dayanıklılık kaybı 22; azalma yüzde 55.

---

## ARM-017 — Kask Sv.3: kırılmış parça

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Kask, dayanıklılık 230; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Dayanıklılığı 0 yap
2. Aynı bölgeye 40 ham hasar uygula

### Beklenen sonuç

Kırık parça hasarı azaltmaz; can kaybı 40 olur.

---

## ARM-018 — Kask Sv.3: daha sağlam eş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/DamageCalculator.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Test hedefinde yeni Sv.3 Kask, dayanıklılık 230; diğer zırh boş; tek bileşen ham hasar 40 olarak hazırlanmış fixture.

### Adımlar

1. Kuşanılan dayanıklılığı 10 yap
2. Aynı seviye yeni parça al

### Beklenen sonuç

Yeni 230 dayanıklıklı parça kuşanılır; eski 10 dayanıklıklı parça yere düşer.

---
