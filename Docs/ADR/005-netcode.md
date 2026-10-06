# ADR-005 — Netcode for GameObjects

- **Durum:** Kabul edildi (paket etkinleştirme F4-2)
- **Tarih:** 2026-10-06

## Bağlam

Özel protokol maliyeti yüksek; Unity ekosistemi ve transport hazır.

## Karar

`com.unity.netcode.gameobjects` + Unity Transport. Kod `HAREKAT_NETCODE` / versionDefines ile kapılı; paket eklenince açılır.

## Sonuçlar

- Host + Client doğrulama F4-2 / F5-7
- Interest management ~60 oyuncu
