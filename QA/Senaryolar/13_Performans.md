# Performans

Senaryo sayısı: **12** · Öncelik: P1=12

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## PERF-001 — 20 savaşan temel ölçüm

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. 2 timle 1080p orta kalitede 5 dakika oyna
2. CPU/GPU frametime kaydet

### Beklenen sonuç

P50/P95/P99 ve 1 yüzde düşük FPS kaydedilir; cihaz hedefi TestPlan ile karşılaştırılır, ölçüm yoksa Geçti yazılmaz.

---

## PERF-002 — 60 savaşan stres

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. 6 timle yoğun çatışmada 5 dakika izle
2. Tahsisleri kaydet

### Beklenen sonuç

Hot path sürekli kare başı GC tahsisi yapmaz; önerilen P95≤16,7 ms hedef sapması raporlanır.

---

## PERF-003 — Topçu VFX yükü

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Altı tim topçu çağrısı başlat
2. Patlama zirvesini ölç

### Beklenen sonuç

VFX havuzu kontrolsüz büyümez; kare sıçramaları ve GPU süresi kanıtla kaydedilir.

---

## PERF-004 — Sis yükü

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Aynı görüşte 10 sis bulutu oluştur
2. GPU zamanını ölç

### Beklenen sonuç

Saydamlık maliyeti raporlanır; bulutlar 25 sn sonrası temizlenir, kalıcı maliyet bırakmaz.

---

## PERF-005 — Mermi havuzu

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. PMT-76 ile kesintisiz yoğun ateş fixture çalıştır
2. Aktif mermi sayısını izle

### Beklenen sonuç

Mermiler yaşam süresi/menzil sonrası temizlenir; sahne nesne sayısı atışla sınırsız büyümez.

---

## PERF-006 — Sahne tekrar bellek

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Menü→maç→menü döngüsünü 10 kez yap
2. Her dönüşte belleği kaydet

### Beklenen sonuç

Kalıcı artış varsa sızıntı adayı açılır; sabit bütçeyle kıyaslanır ve GC sonrası eğilim raporlanır.

---

## PERF-007 — Ses başlangıç maliyeti

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Soğuk başlangıçta ses sentezi süresini ölç

### Beklenen sonuç

Sözleşme hedefi toplam <300 ms; donanım ve süre ölçülerek raporlanır.

---

## PERF-008 — AudioSource sınırı

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Aynı anda çok sayıda atış/patlama oluştur
2. Ses kaynaklarını say

### Beklenen sonuç

3D ses havuzu 32 sınırını aşmaz; bittiğinde kaynaklar yeniden kullanılabilir.

---

## PERF-009 — Mermi izi temizliği

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. 5 dakika duvara ateş et
2. Decal sayısını incele

### Beklenen sonuç

Mermi izi havuzu üst sınır 150; eski izler geri dönüştürülür.

---

## PERF-010 — Yükleme süresi

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Kuzgun Vadisi soğuk/sıcak açılışı üçer kez ölç

### Beklenen sonuç

Yükleme süresi, disk tipi ve NavMesh/runtime üretim yolu ayrı kaydedilir; donmuş görüntü için yükleme durumu görünür.

---

## PERF-011 — Arka plan yükü

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. Windows güncelleme/arka plan iş yükü benzetimi altında maça gir
2. Süreyi ölç

### Beklenen sonuç

Çökme/veri kaybı olmaz; FPS düşüşü temel ölçümden ayrı işaretlenir.

---

## PERF-012 — Uzun oturum

- **Öncelik:** P1
- **Kaynak:** `Docs/CONTRACTS.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Aynı Windows test cihazı, sürücü, güç profili ve build; 60 sn ısınma, 5 dk ölçüm; Profiler/ETW kaydı. Eşikler geçici QA hedefidir.

### Adımlar

1. 60 savaşanla 60 dakika tekrar maç döngüsü yap
2. Bellek/handle sayısını izle

### Beklenen sonuç

Çökme yok; yükselen bellek/handle eğrisi varsa sızıntı incelemesi açılır.

---
