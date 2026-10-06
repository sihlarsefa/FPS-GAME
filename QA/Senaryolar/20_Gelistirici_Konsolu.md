# Geliştirici konsolu

Senaryo sayısı: **6** · Öncelik: P0=1, P1=3, P2=1, P3=1

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## DEV-001 — Konsolu açma/kapama

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Menü veya maçta.

### Adımlar

1. Konsol tuşuna bas
2. Tekrar bas

### Beklenen sonuç

Konsol açılır/kapanır; açıkken oyuncu girdisi kilitlenir.

---

## DEV-002 — Komut listesi ve yardım

- **Öncelik:** P2
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Konsol açık.

### Adımlar

1. help yaz
2. Bilinmeyen komut yaz

### Beklenen sonuç

Komut listesi görünür; bilinmeyen komut anlaşılır hata verir, çökme yok.

---

## DEV-003 — Hile komutları yerel modda

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Atış Poligonu.

### Adımlar

1. Silah/can komutu çalıştır

### Beklenen sonuç

Poligonda komutlar etkili olur; sonuç konsola yazılır.

---

## DEV-004 — Çevrimiçi maçta hile engeli

- **Öncelik:** P0
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Online maç, normal oyuncu.

### Adımlar

1. Hile komutu çalıştır

### Beklenen sonuç

Sunucu yetkisiz komutu reddeder; durum değişmez.

---

## DEV-005 — Komut geçmişi

- **Öncelik:** P3
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Konsol açık.

### Adımlar

1. Üç komut yaz
2. Yukarı oka bas

### Beklenen sonuç

Önceki komutlar sırayla gelir.

---

## DEV-006 — Release derlemede konsol

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (konsol)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Release build.

### Adımlar

1. Konsol tuşuna bas

### Beklenen sonuç

Konsol kapalı veya yetki kapısı arkasındadır.

---
