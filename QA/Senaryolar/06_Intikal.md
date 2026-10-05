# İntikal

Senaryo sayısı: **13** · Öncelik: P1=13

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## TRN-001 — T-70 tercihinin uygulanması

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Kurulumda Helikopter seç
2. Harekât başlat

### Beklenen sonuç

Oyuncu timi T-70 ile başlar; Kirpi tercihi yanlış uygulanmaz.

---

## TRN-002 — Kirpi tercihinin uygulanması

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Kurulumda Zırhlı Araç seç
2. Harekât başlat

### Beklenen sonuç

Oyuncu timi Kirpi koltuklarında başlar; yere erken düşmez.

---

## TRN-003 — On yolcu koltuğu

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Her iki intikal türünde oyuncu ve 9 botu gözle

### Beklenen sonuç

Tüm tim için benzersiz koltuk/iniş noktası vardır; üst üste karakter doğmaz.

---

## TRN-004 — Koltukta serbest bakış

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. İntikal sürerken fareyle sağa sola bak
2. WASD dene

### Beklenen sonuç

Bakış çalışır; oyuncu koltuğu yürüyerek terk edemez.

---

## TRN-005 — Erken iniş engeli

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Araç hedefe varmadan F bas

### Beklenen sonuç

Uygulanmış intikal kuralı erken inişi engeller; oyuncu boşlukta/kayalık içinde bırakılmaz.

---

## TRN-006 — Elle inme

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Araç Arrived durumuna gelince F bas

### Beklenen sonuç

Yerde güvenli iniş noktasına geçilir; hareket açılır ve DropState Landed olur.

---

## TRN-007 — Otomatik inme

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Varıştan sonra hiçbir tuşa basma
2. 3 saniye izle

### Beklenen sonuç

Otomatik tahliye gerçekleşir; yolcular araçta süresiz kalmaz.

---

## TRN-008 — Sektör ayrılığı

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Sabit seed ile tüm timlerin iniş noktalarını haritada işaretle

### Beklenen sonuç

Timler harita kenarı sektörlerine dağılır; koordinatlar mutlak 400 m altında kalır.

---

## TRN-009 — İntikal zaman aşımı

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Bir test aracının varışını geciktir
2. 90 saniye ilerlet

### Beklenen sonuç

Maç Insertion durumunda sonsuz kalmaz; süre aşımı akışı gözlenir ve kayıt edilir.

---

## TRN-010 — İntikalden maç fazına

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Tüm araçlar varıp tahliye olana dek izle

### Beklenen sonuç

Maç InMatch fazına geçer; sayaç/zone başlangıcı çift tetiklenmez.

---

## TRN-011 — Araçtan güvenli çıkış

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Sürülebilir Kirpiyle duvar yakınına git
2. F ile çık

### Beklenen sonuç

Karakter katı duvar içine veya harita altına doğmaz.

---

## TRN-012 — Dolu sürücü koltuğu

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. İlk karakter Kirpiye girsin
2. İkinci karakterle girmeyi dene

### Beklenen sonuç

Aynı sürücü koltuğuna ikinci sahip atanmaz.

---

## TRN-013 — Araç frenleme

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Kuzgun Vadisi; 4 tim × 10; sabit seed; görüntü/süre kaydı. Araç ve intikal fixture yoksa Engelli.

### Adımlar

1. Düz parkurda hızlan
2. Fren girdisini uygula

### Beklenen sonuç

Hız azalır; çıkış sonrası eski gaz girdisi istemsiz sürmeyi sürdürmez.

---
