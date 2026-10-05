# Atış Poligonu

Senaryo sayısı: **9** · Öncelik: P1=9

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## RNG-001 — Doğru sahne

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Atış Poligonu düğmesine bas
2. Yüklemeyi bekle

### Beklenen sonuç

TrainingRange açılır; canlı battle royale maçı başlatılmaz.

---

## RNG-002 — Silah rafı kapsamı

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Rafları dolaş
2. Silah adlarını katalogla karşılaştır

### Beklenen sonuç

10 katalog silahı erişilebilir; aynı silahın tekrarı eksik silahı gizlemez.

---

## RNG-003 — Sonsuz yedek

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. PMT-76 şarjörünü boşalt
2. Beş kez doldur

### Beklenen sonuç

Yedek tükenmez; şarjör/doldurma mekaniği çalışmayı sürdürür.

---

## RNG-004 — Mesafe levhaları

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. 25/50/100/200/300 m levhalarına bak
2. Gerçek koordinat uzaklığını ölç

### Beklenen sonuç

Etiketler gerçek hedef uzaklığına uygundur; 300 m hedef alan dışına taşmaz.

---

## RNG-005 — Sabit hedef hasarı

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Sabit hedefi öldür
2. Yeniden doğuş süresini izle

### Beklenen sonuç

Hasar bildirimi doğru; hedef yenilenince tam canla çalışır.

---

## RNG-006 — Hareketli hedef

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Hareketli hedefi bir tam tur gözle
2. Vur

### Beklenen sonuç

Tanımlı iki nokta arasında hareket sürer; hasar hitboxla eşleşir.

---

## RNG-007 — Poligonda zone yok

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. 10 dakika alanda kal

### Beklenen sonuç

Zone daralması ve takım elenmesi poligonu sonlandırmaz.

---

## RNG-008 — Engel parkuru

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Duvar ve kill house içini dolaş

### Beklenen sonuç

Katı nesneler çarpışır; kapılar ve rampalar geçilebilir.

---

## RNG-009 — Poligondan çıkış

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Ana menüden Atış Poligonu; gözlemci kamera ve ekran kaydı.

### Adımlar

1. Esc ile ana menüye dön
2. Harekât başlat

### Beklenen sonuç

Sonsuz mühimmat ve test hedefleri normal maça taşınmaz.

---
