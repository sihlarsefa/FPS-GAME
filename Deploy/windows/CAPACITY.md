# HAREKÂT — Kapasite planı (Windows Server)

Varsayımlar (maç başına ~50 oyuncu, dedicated `HAREKAT_Server.exe`):

| Kaynak | Değer |
|--------|--------|
| Maç başına oyuncu | 40–60 (ortalama **50**) |
| Maç başına vCPU | **2** (tick 30 Hz + botlar) |
| Maç başına RAM | **2–3 GB** (ortalama **2.5 GB**) |
| Sunucu tipi (referans) | Windows Server 2022, **16 vCPU / 64 GB RAM** |
| Sunucu başına eşzamanlı maç | ~**6–8** (CPU/RAM headroom %20–25) |
| Sunucu başına eşzamanlı oyuncu | ~**300–400** |
| Port havuzu / sunucu | UDP **7777–7900** (124 port; pratikte CPU önce biter) |
| Backend API (IIS) | Ayrı veya aynı host; 1 API node ~5k CCU API yükü |

## Özet tablo

| Eşzamanlı oyuncu (CCU) | Gerekli maç (≈50/maç) | Oyun sunucusu (Windows) | API / DB önerisi |
|------------------------|----------------------|-------------------------|------------------|
| **1.000** | ~20 | **3–4** × 16 vCPU / 64 GB | 1× API (IIS) + 1× SQL Standard |
| **5.000** | ~100 | **14–18** × 16 vCPU / 64 GB | 2× API (LB) + SQL Always On / güçlü VM |
| **10.000** | ~200 | **28–35** × 16 vCPU / 64 GB | 3× API + SQL cluster + Redis |

## Ölçek notları

- **ServerManager** her Windows oyun hostunda bir Windows Service olarak çalışır; backend’den `pending-allocation` çeker.
- Aynı hostta API + oyun süreçleri mümkün (küçük ölçek / 1k CCU); 5k+ için oyun filolarını API’den ayırın.
- SQL Server: CCU yükseldikçe connection pooling + `indexes.sql` + read replica (liderboard) düşünün.
- Burst için `WarmReadySlots` (ServerManager) ani kuyruk spike’larında boş port bırakır.

## Hızlı formül

```
gerekli_sunucu ≈ ceil( CCU / 50 / maç_per_host )
maç_per_host   ≈ min( floor(vCPU/2), floor(RAM_GB/2.5) ) * 0.8
```

Örnek 16 vCPU / 64 GB: `min(8, 25) * 0.8 ≈ 6.4` → **6 maç/host** → **~300 CCU/host**.
