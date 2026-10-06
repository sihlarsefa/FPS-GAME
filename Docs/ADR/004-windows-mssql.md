# ADR-004 — Windows Server + MSSQL

- **Durum:** Kabul edildi
- **Tarih:** 2026-10-06

## Bağlam

Kurumsal host (APP2025): IIS zaten var; Linux yok.

## Karar

- Oyun backend: **ASP.NET + MSSQL** IIS’te
- Web/Wiki: düz HTML IIS siteleri
- Redis yalnızca ihtiyaç duyulan kuyruk/önbellek için (mevcut host kısıtlarına uy)

## Sonuçlar

- `Deploy/windows/*` betikleri; diğer IIS uygulamalarına dokunulmaz
- EF: boş DB’de `EnsureCreated`, migration geçmişi varsa `Migrate`
