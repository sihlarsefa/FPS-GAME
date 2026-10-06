# ADR-002 — Prosedürel içerik → hazır varlık → gerçekçi grafik

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06

## Bağlam

İlk günden oynanabilir dünya lazım; sanat ekibi yok.

## Karar

1. Prosedürel arazi / yapı / splat (URP) ile oynanabilir haritalar.
2. `ContentOverrides` ile ücretsiz PBR / ses / model eşlemesi.
3. İleride isteğe bağlı HDRP / filmik cila (zorunlu değil).

## Sonuçlar

- `Generated/` mesh’ler SetupAll ile üretilir
- ThirdParty + lisans tablosu; Git LFS büyük dosyalar için
