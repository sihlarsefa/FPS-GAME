# Menüler ayarlar kariyer

Senaryo sayısı: **15** · Öncelik: P1=15

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## MENU-001 — Kurulum tim alt sınırı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Tim kaydırıcısını en düşüğe getir
2. Başlat

### Beklenen sonuç

2 tim × 10 = 20 savaşan seçimi gösterilir.

---

## MENU-002 — Kurulum tim üst sınırı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Tim kaydırıcısını en yükseğe getir
2. Başlat

### Beklenen sonuç

6 tim × 10 = 60 savaşan seçimi gösterilir.

---

## MENU-003 — Zorluk eşlemesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Er/Uzman/Komando seçeneklerini sırayla kaydet

### Beklenen sonuç

Seçimler Easy/Normal/Hard karşılığına dönüşür; yeniden açınca korunur.

---

## MENU-004 — FOV alt üst sınırı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. FOV kaydırıcısını 60 sonra 100 yap

### Beklenen sonuç

Görüş açısı 60–100 aralığında; ADS bırakıldığında seçili temele döner.

---

## MENU-005 — Ses ana seviye

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Ana sesi 0 yap
2. Ateş et/menüye dön
3. Yeniden aç

### Beklenen sonuç

Ana seviye tüm ilgili sesleri susturur; geri açınca ses döner.

---

## MENU-006 — Ortam sesi ayrımı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Ortam sesini 0 yap
2. Ateş et

### Beklenen sonuç

Ortam döngüsü kısılır; ana ses açıksa silah sesinin ayrı kontrolü korunur.

---

## MENU-007 — ADS hassasiyeti

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. ADS çarpanını değiştir
2. Aynı fare hareketini ADS/kalçada uygula

### Beklenen sonuç

ADS hassasiyeti çarpanı nişan durumuna uygulanır; genel ayar ezilmez.

---

## MENU-008 — Kalite uygulama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Düşük ve Ultra seç
2. Sahneyi gözle

### Beklenen sonuç

Kalite seçimi uygulanır; sahne pembe materyale dönüşmez veya çökmez.

---

## MENU-009 — Tam ekran geçişi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Tam ekranı aç/kapat
2. Ana menüye dön

### Beklenen sonuç

Pencere/odak düzeni korunur; UI ekran dışında kalmaz.

---

## MENU-010 — FPS ayarı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. FPS göster aç
2. Kapat

### Beklenen sonuç

Sayaç doğru ayara göre görünür/gizlenir; sürekli kaplamaya dönüşmez.

---

## MENU-011 — Ayar kalıcılığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. FOV/ses/oyuncu adı değiştir
2. Oyunu tamamen kapat/aç

### Beklenen sonuç

Kaydedilen değerler geri gelir; geçersiz değerler servis sınırlarına alınır.

---

## MENU-012 — Offline duraklatma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Maçta Esc aç
2. 5 saniye bekle
3. Devam et

### Beklenen sonuç

Offline simülasyon durur; dönünce Time.timeScale doğru normale gelir.

---

## MENU-013 — Menüye çıkış zaman ölçeği

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. Oyun duraklatılmışken Ana Menü seç
2. Yeni maç aç

### Beklenen sonuç

Yeni maç donmuş timeScale=0 durumuyla başlamaz.

---

## MENU-014 — Kariyer XP formülü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. 4 timde 2. sıra, 3 kill, 1 headshot ve galibiyet yok sonuç kaydet

### Beklenen sonuç

XP 3×100+1×25+(4−2)×150=625; kariyer bir kez artar.

---

## MENU-015 — Galibiyet XP

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SettingsService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Temiz yerel test profili; MainMenu sahnesi; mevcut profilin yedeği alınmış; gerçek hesap kullanılmaz.

### Adımlar

1. 4 timde 1. sıra, 0 kill ve galibiyet sonucu kaydet

### Beklenen sonuç

XP (4−1)×150+1000=1450; rütbe eşiği RankCatalog ile eşleşir.

---
