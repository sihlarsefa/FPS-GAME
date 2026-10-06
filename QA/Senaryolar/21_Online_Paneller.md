# Online paneller

Senaryo sayısı: **6** · Öncelik: P0=2, P1=3, P3=1

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## PNL-001 — Giriş/hesap paneli

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Ana menü, backend açık.

### Adımlar

1. Giriş panelini aç
2. Geçerli hesapla giriş yap

### Beklenen sonuç

Giriş başarılı; oyuncu adı ve rütbe görünür.

---

## PNL-002 — Giriş hatası

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Backend açık.

### Adımlar

1. Yanlış parola gir

### Beklenen sonuç

Anlaşılır hata mesajı; tekrar denenebilir; parola düz metin görünmez.

---

## PNL-003 — Sunucu listesi

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Giriş yapılmış.

### Adımlar

1. Sunucu paneline gir
2. Yenile

### Beklenen sonuç

Sunucular ad, oyuncu sayısı ve gecikmeyle listelenir; boş liste mesajı vardır.

---

## PNL-004 — Sunucuya bağlanma

- **Öncelik:** P0
- **Kaynak:** `Assets/_Project/Scripts/Presentation/Bootstrap/GameSession.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Çalışan sunucu.

### Adımlar

1. Sunucu seç
2. Bağlan

### Beklenen sonuç

Lobiye geçilir; hata durumunda menüye dönülür.

---

## PNL-005 — Backend kapalıyken çevrimdışı

- **Öncelik:** P0
- **Kaynak:** `Assets/_Project/Scripts/Presentation/Bootstrap/GameSession.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Backend durdurulmuş.

### Adımlar

1. Oyunu aç
2. Atış Poligonu'nu başlat

### Beklenen sonuç

Oyun çökmez; online panelleri 'bağlanılamadı' gösterir; offline mod çalışır.

---

## PNL-006 — Steam kancası

- **Öncelik:** P3
- **Kaynak:** `Assets/_Project/Scripts/Presentation/UI/MainMenuController.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Steam yok.

### Adımlar

1. Menüde Steam öğelerine bak

### Beklenen sonuç

Steam yoksa öğeler pasif/gizli; hata günlüğü yok.

---
