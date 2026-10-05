# Bölge ve maç

Senaryo sayısı: **12** · Öncelik: P1=12

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## ZON-001 — Başlangıçta zone hasarı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Zone başlamadan sınır dışına çık
2. 3 saniye bekle

### Beklenen sonuç

Start öncesi zone hasarı uygulanmaz.

---

## ZON-002 — Güvenli alan hasarsızlığı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Zone başladıktan sonra merkezde 5 saniye bekle

### Beklenen sonuç

Zone kaynaklı can kaybı olmaz.

---

## ZON-003 — Sınır dışı periyot

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Aktif fazda dışarıda 3 tam hasar periyodu bekle

### Beklenen sonuç

Saniyelik hasar mevcut fazın DPS değeriyle uyumludur; kare hızına göre çarpılmaz.

---

## ZON-004 — İçeri dönüş

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Hasar alırken güvenli alana gir
2. İki periyot bekle

### Beklenen sonuç

Sonraki dış alan hasarı durur; dış alan uyarısı kapanır.

---

## ZON-005 — Yeni çember kapsaması

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Tüm fazların merkez/radius değerlerini kaydet

### Beklenen sonuç

Yeni merkez uzaklığı + yeni radius önceki radius değerini aşmaz.

---

## ZON-006 — Doğrusal daralma

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Daralma başlangıç/orta/bitiş radiusunu kaydet

### Beklenen sonuç

Orta zamanda radius başlangıç ve bitiş ortalamasına yakın; sıçrama olmaz.

---

## ZON-007 — Harita sınırı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. 20 farklı seed ile son merkezleri kaydet

### Beklenen sonuç

Merkez koordinatları harita halfsize×0,8 sınırı içinde kalır.

---

## ZON-008 — İntikal yolcusu muafiyeti

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Zone dışında hâlâ intikal koltuğunda olan hedefi izle

### Beklenen sonuç

Henüz Landed olmayan yolcuya yaya zone hasarı uygulanmaz.

---

## ZON-009 — Son tim galibiyeti

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Diğer timlerin son üyelerini sırayla ele

### Beklenen sonuç

Bir tim kaldığında yalnız bir MatchEnded olayı ve doğru kazanan görünür.

---

## ZON-010 — Tim değil birey sayımı

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Mavi timde 1, Kırmızı timde 2 oyuncu bırak

### Beklenen sonuç

2 canlı tim varken maç bitmez; oyuncu sayısıyla tim sayısı karıştırılmaz.

---

## ZON-011 — Çift ölüm bildirimi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Aynı ölü oyuncu için ikinci test ölüm olayı gönder

### Beklenen sonuç

Canlı tim/oyuncu sayısı ikinci kez düşmez; yerleşim bozulmaz.

---

## ZON-012 — Yeni maç zone sıfırlama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Application/Services/ZoneService.cs`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Sabit seed 4×10 Kuzgun Vadisi; zone fazlarını hızlandırabilen izole test düzeneği.

### Adımlar

1. Son fazda menüye dön
2. Yeni maç başlat

### Beklenen sonuç

Önceki radius/süre/hasar yeni maça taşınmaz.

---
