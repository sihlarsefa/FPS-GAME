# Erişilebilirlik

Senaryo sayısı: **8** · Öncelik: P1=8

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## ACC-001 — Klavye menü sırası

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Fareyi bırak
2. Tab/Shift+Tab ile ana menüyü dolaş
3. Enter kullan

### Beklenen sonuç

Erişilebilir menüde tüm eylemler ulaşılabilir; odak görünür ve mantıklıdır.

---

## ACC-002 — Odak tuzağı

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Ayarlar aç
2. Son alandan Tab bas
3. Esc ile kapat

### Beklenen sonuç

Odak aktif pencerede yönetilir; kapanınca açan kontrole döner; sonsuz tuzak olmaz.

---

## ACC-003 — Yalnız renk ayrımı

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Dost/düşman, hasar ve hazır durumlarını gri tonla incele

### Beklenen sonuç

Anlam metin/ikon/şekille de aktarılır; kritik ayrım yalnız kırmızı/yeşile bağlı değildir.

---

## ACC-004 — Büyütmede taşma

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Web/anketi yüzde 200 yakınlaştır
2. 320 CSS px genişlikte kullan

### Beklenen sonuç

Alanlar okunur; içerik kaybı ve zorunlu yatay kaydırma yoktur.

---

## ACC-005 — Anket etiketleri

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Anketi ekran okuyucuyla form modunda gez

### Beklenen sonuç

Her alanın adı/ölçeği ve hata mesajı duyurulur; zorunluluk anlaşılır.

---

## ACC-006 — Hareket hassasiyeti

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. İşletim sisteminde azaltılmış hareket aç
2. UI animasyonlarını izle

### Beklenen sonuç

Desteklenen web yüzeylerinde gereksiz animasyon azaltılır; oyun kamera seçeneği eksikse öneri açılır.

---

## ACC-007 — Anket hata kurtarma

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Zorunlu alanları boş gönder
2. İlk hatayı düzelt

### Beklenen sonuç

Gönderim engellenir; ilk hata odağa gelir; doldurulmuş diğer alanlar kaybolmaz.

---

## ACC-008 — Ses olmadan kritik olay

- **Öncelik:** P1
- **Kaynak:** `Docs/CODEX_FAZ2.md`
- **Dayanak:** QA kabul hedefi
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci + portal/anket; klavye, ekran okuyucu, renk simülasyonu; test edilen ürün/sürüm açık kaydedilir.

### Adımlar

1. Oyunu sessize al
2. Zone ve komuta devrini tetikle

### Beklenen sonuç

Kritik olaylar görsel/metinsel olarak da fark edilir; yalnız sesle bilgi verilmez.

---
