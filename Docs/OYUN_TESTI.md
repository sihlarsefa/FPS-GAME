# HAREKÂT — Gerçek Oynanış Kontrol Listesi

> FAZ 5 / F5-2. Her maddeyi editör Play veya `Builds/macOS/HAREKAT.app` ile dene.
> Durum: `çalışıyor` / `hatalı` / `eksik` — ekran görüntüsü `Logs/screens/` veya elle ekle.

**Ortam:** Unity 6000.6.4f1 · macOS build · Windows Server staging (online)

## 5 dakikalık ilk oyun rehberi

1. `open Builds/macOS/HAREKAT.app` (veya editörde MainMenu Play).
2. **ATIŞ POLİGONU — SERBEST** → WASD hareket, fare bakış, sol tık ateş, R şarjör, sağ tık nişan.
3. Esc → ana menü. **HAREKÂT KUR** → harita Kuzgun, Gündüz, Açık hava → Başla.
4. İntikalde F ile in (veya otomatik). F1 takip, V topçu (hedef menzilde), U İHA, Y komut çarkı.
5. Ölürsen killcam → izleyici (Q/E hedef). Maç bitince Kariyer / Ana menü.

## Kontrol listesi

| # | Özellik | Durum | Ekran | Not |
|---|---------|-------|-------|-----|
| 1 | MainMenu yüklenir, Canvas var |  |  | PlayMode: MainMenuLoadTests |
| 2 | Harekât kurulumu — 3 harita (Kuzgun / Ayaz / Mavi Liman) |  |  | |
| 3 | Gün saati + hava seçimi |  |  | |
| 4 | İntikal T-70 / helikopter + F iniş |  |  | |
| 5 | Silahlar (MPT-76, JNG-90, SAR9…) ateş / şarjör / dürbün |  |  | |
| 6 | Silah eklentileri (loot + otomatik takma) |  |  | |
| 7 | El bombası / sis |  |  | |
| 8 | Topçu (V) |  |  | |
| 9 | İHA (U) |  |  | |
| 10 | Tim emirleri F1–F4 |  |  | PlayMode: Squad F1 |
| 11 | Komut çarkı (Y) + ping (orta tık) |  |  | |
| 12 | Yaralı (DBNO) + canlandırma (F) |  |  | |
| 13 | Kirpi sürüş / taret / tim taşıma |  |  | |
| 14 | Envanter + harita + işaret |  |  | |
| 15 | Skor tablosu (CapsLock) |  |  | |
| 16 | Telsiz altyazıları |  |  | |
| 17 | Başarım bildirimi |  |  | |
| 18 | Killcam + izleyici |  |  | |
| 19 | Maç sonu + kariyer / rütbe |  |  | |
| 20 | Ayarlar: tuş, gamepad, dil, grafik |  |  | |
| 21 | Kozmetik paneli |  |  | |
| 22 | Çatışma (hızlı maç) |  |  | |
| 23 | Poligon eğitimli (tutorial) |  |  | |
| 24 | Online paneller (backend ayaktayken) |  |  | Staging |

## Otomatik kanıt (F4-1 / F5-1)

- EditMode: Unity batch `546+` geçti (F4-1).
- PlayMode duman: `zsh Tools/UnityVerify/run_playmode.sh` (Soak hariç).
- Soak: `zsh Tools/UnityVerify/run_soak.sh` → `Logs/soak/RAPOR.md`.

## Bulunan hatalar

| Tarih | Madde | Dosya / öneri | Sahip |
|-------|-------|---------------|-------|
|  |  |  | Cursor / Claude |
