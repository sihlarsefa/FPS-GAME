# ADR-001 — Sunucu otoritesi

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06

## Bağlam

60 oyuncuya kadar FPP BR; istemci öngörüsü ile hile riski yüksek.

## Karar

Hasar, ölüm, maç sonucu ve envanter mutasyonu **sunucu otoritesinde**. İstemci girdi/RPC gönderir; sunucu doğrular. Dedicated Server build + ServerManager.

## Sonuçlar

- Netcode for GameObjects + ConnectionApproval
- Lag compensation (kısa geçmiş) isabet için
- Botlar sunucu-only (`ServerBotGate`)
