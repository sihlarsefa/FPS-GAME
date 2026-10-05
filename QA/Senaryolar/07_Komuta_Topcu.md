# Komuta ve topçu

Senaryo sayısı: **15** · Öncelik: P1=15

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## CMD-001 — Takip emri

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. F1 bas
2. Komutanı botlardan 15 m uzaklaştır

### Beklenen sonuç

Tim üyeleri Follow emriyle komutana yaklaşır; başka timler emir almaz.

---

## CMD-002 — Mevzi emri

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Nişangâhı güvenli noktaya getir
2. F2 bas
3. Komutanı uzaklaştır

### Beklenen sonuç

Mevzi emri kayıt edilir; düşman yokken tim mevzide kalır.

---

## CMD-003 — Taarruz hedefi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. 50 m uzakta zemine nişan al
2. F3 bas

### Beklenen sonuç

Attack emri hedef koordinatını korur; botlar hedefe yönelir.

---

## CMD-004 — Toplan emri

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Botları dağıt
2. F4 bas

### Beklenen sonuç

Regroup emriyle hayattaki takım üyeleri komutana yaklaşır.

---

## CMD-005 — Komutan dışı emir

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Komutansız normal piyade fixture ile F1-F4 bas

### Beklenen sonuç

Yetkisiz oyuncu timin emrini değiştiremez; komuta yetkisi UI ile tutarlıdır.

---

## CMD-006 — Komuta devri

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Komutanı öldür
2. Sağ kalan rütbe sırasını kaydet

### Beklenen sonuç

En yüksek rütbeli sağ kalana komuta geçer; bildirimde doğru isim gösterilir.

---

## CMD-007 — Eş rütbede devir

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Aynı rütbeli iki aday kaydet
2. Komutanı öldür

### Beklenen sonuç

Kayıt sırası eşitlik bozucu olarak kullanılır; rastgele aday seçilmez.

---

## CMD-008 — Son komutan ölümü

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Tek kalan tim üyesini öldür

### Beklenen sonuç

Ölü oyuncuya yeni komuta verilmez; tim elenir.

---

## CMD-009 — Topçu ilk çağrı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Bekleme süresi 0 iken 100 m hedefe V bas

### Beklenen sonuç

Çağrı bir kez kabul edilir; 8 mermi planlanır ve tim bekleme süresi başlar.

---

## CMD-010 — Topçu bekleme süresi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. İlk çağrıdan hemen sonra V bas
2. Sayaç bitince tekrar dene

### Beklenen sonuç

Süre içinde ikinci çağrı reddedilir; süre sonunda tekrar kabul edilir.

---

## CMD-011 — Topçu tim izolasyonu

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Mavi çağrıdan sonra Kırmızı komutanı çağrı yapsın

### Beklenen sonuç

Bir timin bekleme süresi diğer timi kilitlemez.

---

## CMD-012 — Topçu zamanlama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. V çağrısından ilk patlamaya süre tut
2. Tüm patlamaları say

### Beklenen sonuç

İlk patlama yaklaşık 6 sn sonra; toplam 8; ardışık aralıklar 0,35–0,75 sn.

---

## CMD-013 — Topçu saçılma alanı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Tek çağrının tüm patlama koordinatlarını kaydet

### Beklenen sonuç

Her patlama merkezden yatay en çok 18 m uzaklıktadır.

---

## CMD-014 — Harita işaretinden topçu

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. M ile haritada hedef koy
2. Kapat
3. V ile çağır

### Beklenen sonuç

Kullanılan hedef işaret ile örtüşür; kamera bakışı eski hedefi sessizce ezmez.

---

## CMD-015 — Yeni maçta topçu temizliği

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/SquadOrderService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

4 tim offline kontrollü fixture; oyuncu komutan, canlı telsizci; komuta kayıtları ve topçu olayları görülebilir.

### Adımlar

1. Topçu çağırıp maçtan çık
2. Yeni maç başlat

### Beklenen sonuç

Eski patlamalar/yasak süre yeni maçta sürmez; sayaç ve tehlike işareti temizdir.

---
