# Sürülebilir Kirpi

Senaryo sayısı: **7** · Öncelik: P1=5, P2=2

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## KRP-001 — Kirpi'ye binme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Kirpi yakınında, ayakta.

### Adımlar

1. Kirpi'ye yaklaş
2. Bin tuşuna bas

### Beklenen sonuç

Sürücü koltuğuna geçilir; kamera araç kamerasına döner; HUD araç göstergesi açılır.

---

## KRP-002 — Gaz, fren ve direksiyon

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Kirpi'de sürücü.

### Adımlar

1. W ile hızlan
2. S ile fren
3. A/D ile dön

### Beklenen sonuç

Araç girdiyle tutarlı hızlanır, durur ve döner; ani takla yok.

---

## KRP-003 — Araçtan inme

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Kirpi sürülüyor.

### Adımlar

1. Dur
2. İn tuşuna bas

### Beklenen sonuç

Oyuncu araç yanında güvenli noktada belirir; motor kilidi kalkar (bkz. MOV-023).

---

## KRP-004 — Yolcu koltukları

- **Öncelik:** P2
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; İki oyuncu veya bot ile.

### Adımlar

1. Sürücü bin
2. İkinci oyuncu bin

### Beklenen sonuç

Yolcular boş koltuklara oturur; dolu koltuk reddedilir.

---

## KRP-005 — Araç hasarı ve imha

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Kirpi, silah veya bomba ile.

### Adımlar

1. Araca ateş et/bomba at
2. Sağlık sıfırlanana kadar devam

### Beklenen sonuç

Hasar kademeleri görünür; imhada içindekiler hasar alır veya atılır; enkaz kalır.

---

## KRP-006 — Kirpi ile intikal

- **Öncelik:** P1
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Maç, intikal aşaması.

### Adımlar

1. Kirpi ile intikal et
2. Bölgeye in

### Beklenen sonuç

Kirpi intikali tamamlanır; oyuncular indikten sonra sürülebilir araç olarak kalır.

---

## KRP-007 — Eğimde ve engelde sürüş

- **Öncelik:** P2
- **Kaynak:** `Assets/_Project/Scripts (araç/Kirpi)`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Arazi sahnesi.

### Adımlar

1. Rampaya çık
2. Alçak engele çarp

### Beklenen sonuç

Araç tırmanır; takılma halinde geri alınabilir; yere gömülme yok.

---
