# HUD ve harita

Senaryo sayısı: **20** · Öncelik: P1=20

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## HUD-001 — Can göstergesi

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. 30 hasar al
2. 10 iyileş
3. Göstergeyi kontrol et

### Beklenen sonuç

Can sırasıyla 70 ve 80; bar ve sayı aynı kaynağı gösterir.

---

## HUD-002 — Zırh dayanıklılığı

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Yeleğe hasar al
2. Kaskı değiştir

### Beklenen sonuç

Seviye/dayanıklılık doğru parçaya yansır; eski değer kalmaz.

---

## HUD-003 — Şarjör ve yedek

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Tek ateş et
2. R ile doldur

### Beklenen sonuç

Şarjör azalır ve yedekten tamamlanır; göstergeler birbirine karışmaz.

---

## HUD-004 — Ateş modu etiketi

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. B ile mod değiştir

### Beklenen sonuç

Single/Burst/Auto Türkçe TEK/SERİ/OTO karşılığıyla eşleşir.

---

## HUD-005 — Pusula yönleri

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Kuzey/doğu/güney/batıya dön

### Beklenen sonuç

K/D/G/B etiketleri ve derece eşleşir; minimap kuzeyi sabittir.

---

## HUD-006 — İsabet sahibi

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Yerel oyuncu ve bot ayrı ayrı vurulsun/vursun

### Beklenen sonuç

Yerel atış isabetinde gösterge çıkar; başkasının atışı yerel isabet göstergesi üretmez.

---

## HUD-007 — Hasar yönü

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Dört yönden tek tek hasar uygula

### Beklenen sonuç

Gösterge gerçek kaynak yönünü işaret eder; kamera döndüğünde doğru dönüşür.

---

## HUD-008 — Öldürme akışı

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Dost ve rakip öldürmelerini tetikle

### Beklenen sonuç

İsim/silah/kafa isabeti doğru; dost ayrımı yalnız renk dışında metin/ikonla anlaşılır.

---

## HUD-009 — On kişilik tim paneli

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. 10 üyeli timde farklı canlar ve bir ölü oluştur

### Beklenen sonuç

10 ayrı satır; rütbe/rol/can doğru; ölü üye açıkça işaretli.

---

## HUD-010 — Dost dünya işareti

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Dostu 100 m sonra 301 m uzağa taşı

### Beklenen sonuç

İşaret yakın dostta görünür, 300 m üstü görünmez; rakipte dost işareti yoktur.

---

## HUD-011 — Komuta bildirimi

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Komutan ölümünü tetikle

### Beklenen sonuç

Bildirim doğru yeni komutan adı/rütbesiyle bir kez çıkar.

---

## HUD-012 — Kullanım ilerleme

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. H ile iyileş
2. Ateş ederek iptal et

### Beklenen sonuç

İlerleme gerçek süreyi izler; iptalde gösterge kaybolur.

---

## HUD-013 — Dürbün örtüsü

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. JNG-90 ile nişan al
2. Bırak
3. SAR 9 ile nişan al

### Beklenen sonuç

Dürbün retikülü yalnız dürbünlü durumda; eski siyah örtü tabancada kalmaz.

---

## HUD-014 — Zone uyarısı

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Sınır dışına çık
2. Güvenli alana dön

### Beklenen sonuç

Dış alan renk/uyarısı aktif duruma bağlı; güvenli alanda temizlenir.

---

## HUD-015 — Mini harita konumu

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Harita köşelerine git
2. Oyuncu ve müttefik işaretlerini izle

### Beklenen sonuç

Dünya koordinatları doğru UV yönüne dönüşür; yatay/dikey aynalanma olmaz.

---

## HUD-016 — Tam harita imleci

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. M aç
2. Bir nokta işaretle
3. M kapat

### Beklenen sonuç

Açıkken imleç serbest; kapatınca oyun bakışı geri; işaret dünya noktasını tutar.

---

## HUD-017 — Harita pencere çakışması

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. M aç
2. Tab aç
3. Esc ile geri dön

### Beklenen sonuç

Pencere önceliği tutarlı; gizli pencere tıklama almaz; imleç kilidi kaybolmaz.

---

## HUD-018 — Konum adları

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Tüm isimli lokasyonları haritada kontrol et

### Beklenen sonuç

WorldMetadata isimleri gösterilir; Türkçe İ/ı/Ş/ğ bozulmaz.

---

## HUD-019 — Mini harita veri yokluğu

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. MinimapTexture olmayan test fixture aç

### Beklenen sonuç

Gri yedek gösterilir; hata yağmuru veya NullReference oluşmaz.

---

## HUD-020 — HUD yeniden giriş

- **Öncelik:** P1
- **Kaynak:** `Docs/MODUL_SPESIFIKASYONLARI.md`
- **Dayanak:** Sözleşme
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows 1920×1080 ve Türkçe; oyuncu +9 takım üyesi; olayları kontrollü tetikleyen test sahnesi.

### Adımlar

1. Maçtan menüye dönüp üç kez başlat
2. Bir isabet olayı üret

### Beklenen sonuç

HUD tek olay için tek bildirim verir; eski abonelikler birikmez.

---
