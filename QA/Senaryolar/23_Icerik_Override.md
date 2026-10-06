# İçerik override

Senaryo sayısı: **5** · Öncelik: P1=2, P2=3

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## OVR-001 — Varsayılan içerik

- **Öncelik:** P1
- **Kaynak:** `Assets/ThirdParty/README.md`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Override yok.

### Adımlar

1. Oyunu başlat

### Beklenen sonuç

Yer tutucu/varsayılan varlıklar yüklenir; eksik varlık uyarısı çökertmez.

---

## OVR-002 — Model override

- **Öncelik:** P2
- **Kaynak:** `Assets/ThirdParty/README.md`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Geçerli model ThirdParty'ye konmuş.

### Adımlar

1. Oyunu başlat
2. Silahı gözle

### Beklenen sonuç

Override model kullanılır; ölçek/pivot doğru.

---

## OVR-003 — Ses override

- **Öncelik:** P2
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Audio/GameAudio.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Geçerli ses dosyası konmuş.

### Adımlar

1. Silahla ateş et

### Beklenen sonuç

Override ses çalınır; ses seviyesi ayarlara uyar.

---

## OVR-004 — Bozuk override

- **Öncelik:** P1
- **Kaynak:** `Assets/ThirdParty/README.md`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Geçersiz/bozuk dosya konmuş.

### Adımlar

1. Oyunu başlat

### Beklenen sonuç

Varsayılana düşülür, uyarı günlüğe yazılır, oyun çökmez.

---

## OVR-005 — Malzeme override

- **Öncelik:** P2
- **Kaynak:** `Assets/_Project/Scripts/Infrastructure/Rendering/MaterialLibrary.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Malzeme dosyası konmuş.

### Adımlar

1. Sahneyi aç

### Beklenen sonuç

Malzeme uygulanır; shader eksikse yedek malzeme kullanılır.

---
