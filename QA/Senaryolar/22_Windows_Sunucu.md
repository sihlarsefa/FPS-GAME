# Windows sunucu

Senaryo sayısı: **6** · Öncelik: P0=2, P1=3, P2=1

Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.

## SRV-001 — Sunucu servisini başlatma

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.ServerManager/Program.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Windows Server, Backend/Harekat.ServerManager.

### Adımlar

1. Servisi/konsolu başlat

### Beklenen sonuç

Yönetici açılır, port dinler, günlük yazar.

---

## SRV-002 — Dedicated maç örneği

- **Öncelik:** P0
- **Kaynak:** `Backend/Harekat.ServerManager/Program.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; ServerManager çalışıyor.

### Adımlar

1. Maç örneği oluştur

### Beklenen sonuç

Dedicated sunucu işlemi başlar ve listede görünür.

---

## SRV-003 — Örnek kapatma

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.ServerManager/Program.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Çalışan örnek.

### Adımlar

1. Örneği durdur

### Beklenen sonuç

İşlem temiz kapanır, port serbest kalır, oyuncular menüye döner.

---

## SRV-004 — Çökme sonrası toparlama

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.ServerManager/Program.cs`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Çalışan örnek.

### Adımlar

1. Dedicated işlemi öldür

### Beklenen sonuç

Yönetici kaybı algılar; liste güncellenir; yeni örnek açılabilir.

---

## SRV-005 — appsettings yapılandırması

- **Öncelik:** P2
- **Kaynak:** `Backend/Harekat.ServerManager/appsettings.json`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; appsettings.json değiştirilmiş.

### Adımlar

1. Portu değiştir
2. Yeniden başlat

### Beklenen sonuç

Yeni ayar uygulanır; geçersiz değerde açık hata ile durur.

---

## SRV-006 — Güvenlik duvarı ve dış erişim

- **Öncelik:** P1
- **Kaynak:** `Backend/Harekat.ServerManager`
- **Dayanak:** FAZ3 görev C3-8
- **Durum:** Çalıştırılmadı

### Ön koşul

Windows istemci, Unity Editör veya yerel derleme; Ayrı makineden istemci.

### Adımlar

1. Sunucuya LAN/İnternetten bağlan

### Beklenen sonuç

Gerekli portlar açıkken bağlanılır; kapalıyken zaman aşımı mesajı.

---
