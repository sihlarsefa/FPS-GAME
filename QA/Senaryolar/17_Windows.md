# Windows matrisi

Senaryo sayısı: **10** · Öncelik: P1=10

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## WIN-001 — DPI yüzde 100

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. 1080p yüzde 100 DPI ile menü/HUD/anketi aç

### Beklenen sonuç

Metin kırpılmaz; tıklama alanları görünür konumla eşleşir.

---

## WIN-002 — DPI yüzde 150

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. 1440p yüzde 150 DPI ayarla
2. Uygulamayı yeniden aç

### Beklenen sonuç

Menü ve HUD ekran sınırları içinde; imleç kayması olmaz.

---

## WIN-003 — DPI yüzde 200

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. 4K yüzde 200 DPI ile ayarlar ve envanteri aç

### Beklenen sonuç

Kritik butonlar erişilir; yazılar oransız küçülmez/kırpılmaz.

---

## WIN-004 — Karma DPI ekran

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. Yüzde 100 ve yüzde 150 iki monitör arasında pencere taşı

### Beklenen sonuç

Boyut/odak düzelir; fare koordinatları farklı ölçekte sapmaz.

---

## WIN-005 — Monitör çıkarma

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. İkinci ekranda pencereli oyunu aç
2. Ekranı çıkar

### Beklenen sonuç

Pencere erişilebilir birincil ekrana döner; siyah ekran kilidi olmaz.

---

## WIN-006 — NVIDIA sürücü ölçümü

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. QA aday cihazında kaydedilmiş NVIDIA sürümüyle 60 savaşan ölç

### Beklenen sonuç

GPU/sürücü tam sürümü ve frametime eklenir; destek iddiası ancak gerçek koşumdan sonra yapılır.

---

## WIN-007 — AMD sürücü ölçümü

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. QA aday cihazında kaydedilmiş AMD sürümüyle aynı seed ölç

### Beklenen sonuç

Aynı kalite/çözünürlükte artefakt, çökme ve frametime kaydı alınır.

---

## WIN-008 — Intel sürücü ölçümü

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. QA aday Intel GPU cihazında düşük kalite ölç

### Beklenen sonuç

Başlatma/render hatası ve performans kaydedilir; destek garantisi varsayılmaz.

---

## WIN-009 — Uyku dönüşü

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. Menüdeyken Windows uykuya al
2. Uyandır
3. Test maçına gir

### Beklenen sonuç

Ses/giriş/görüntü düzelir; çevrim içi oturum gerekiyorsa açık yeniden bağlantı akışı sunulur.

---

## WIN-010 — Standart kullanıcı kurulumu

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

TestPlan cihaz kaydında Windows sürümü/build, GPU/sürücü, DPI, ekran ve güç profili doldurulur; kombinasyonlar ayrı koşul kimliği alır.

### Adımlar

1. Yönetici olmayan test kullanıcısıyla dağıtımı aç
2. Ayar kaydet

### Beklenen sonuç

Gerekli kurulum izinleri açık belirtilir; günlük oynanışta gereksiz yükseltilmiş yetki beklenmez.

---
