# Envanter

Senaryo sayısı: **14** · Öncelik: P1=14

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## INV-001 — İlk ana silahı alma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Yerdeki MPT-76 üzerine bak
2. F bas
3. Tab aç

### Beklenen sonuç

Boş ana yuvaya alınır ve otomatik kuşanılır; yerdeki silah tek kez kaybolur.

---

## INV-002 — İkinci ana yuva

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. MPT-76 varken G3A7 al
2. Yuvaları kontrol et

### Beklenen sonuç

İkinci ana yuva dolar; ilk silah korunur.

---

## INV-003 — Dolu ana yuva değiştirme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. İki ana yuva doluyken birini seç
2. KNT-76 al

### Beklenen sonuç

Seçili ana silah yerde düşer; yeni silah seçili yuvada olur.

---

## INV-004 — Tabanca aktifken ana silah alma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. İki ana yuvayı doldur
2. Tabancayı seç
3. MPT-55 al

### Beklenen sonuç

Ana yuva 0 değiştirilir; tabanca yuvası korunur.

---

## INV-005 — Tabanca yuvası

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. SAR 9 al
2. Canik TP9 al

### Beklenen sonuç

Tek tabanca yuvası 2 kullanılır; eski tabanca yere düşer.

---

## INV-006 — Düşen silahın mermisi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. MPT-76 şarjörünü 7 mermiye indir
2. Silahı bırak
3. Yeniden al

### Beklenen sonuç

Yerdeki ve geri alınan silahta 7 mermi korunur; tam şarjöre bedelsiz dönmez.

---

## INV-007 — Boş yuva seçimi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Tek silahla boş yuvanın numara tuşuna bas

### Beklenen sonuç

Boş yuva geçerli silah gibi kuşanılmaz; mevcut silah bozulmaz.

---

## INV-008 — Tekerlek yuva döngüsü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Bir ana silah ve tabancayla tekerleği iki yönde çevir

### Beklenen sonuç

Yalnız dolu yuvalar arasında döner; sınırda takılmaz.

---

## INV-009 — Silahsız yumruk

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. X ile silahı indir
2. 2 m içindeki hedefe LMB bas

### Beklenen sonuç

Yumruk uygulanır; silah mermisi harcanmaz.

---

## INV-010 — Mühimmat kısmi alma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Kapasitede 5 ağırlık boşluk bırak
2. 30 adet 5.56 yığınını al

### Beklenen sonuç

Her mermi 0,5 ağırlık olduğundan 10 alınır; yerde 20 kalır.

---

## INV-011 — Kapasite doluyken alma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Yükü tam kapasiteye getir
2. Bir bandaj almayı dene

### Beklenen sonuç

Alım reddedilir; yerde eşya ve mevcut envanter değişmez.

---

## INV-012 — Kuşanılan ekipman ağırlığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Boş envanterde kask/yelek/silah kuşan
2. Ağırlık sayacını izle

### Beklenen sonuç

Kuşanılan teçhizat yığın ağırlığına eklenmez; yelek kapasiteye 50 ekler.

---

## INV-013 — Çanta kaybında fazla yük

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Sv.3 çantayla 200 ağırlık taşı
2. Çantayı bırak
3. Yeni mühimmat almayı dene

### Beklenen sonuç

Mevcut eşyalar sessizce silinmez; fazla yük gösterilir ve yeni yığın alınmaz.

---

## INV-014 — Ölüm yağması

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/InventoryService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Offline test sahnesi; boş envanter, yerde bilinen eşya yığınları; başlangıç kapasitesi 60; doğrudan fixture yoksa Engelli.

### Adımlar

1. Mermi ve iki silahla öl
2. İkinci gözlemciyle düşen eşyaları say

### Beklenen sonuç

DropLootOnDeath açıkken envanter bir kez düşer; aynı ölümde çoğalma olmaz.

---
