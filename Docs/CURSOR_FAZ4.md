# Cursor (Opus) — FAZ 4 Görev Listesi

**Durum (2026-10-05 23:55):**
- F4-1 devam: SetupAll ✅ (4 sahne), macOS build ✅ (~90 sn ayakta), EditMode 520/521.
- Blok: Claude dalga3 `Infrastructure/AI/BotController.Vehicle.cs` (`using Project.Core.Domain` / BotState); Windows Build Support yok.
- F4-3 ✅ `Deploy/windows/STAGING_PROVA.md`.
- Claude **entegrasyon tamam** (23:45); dalga 3 hâlâ yazılıyor — listedeki alanlara dokunma.

**Claude şu an paralel çalışıyor (dalga 3, 8 ajan).** Bu dosyalara **dokunma**; hata görürsen `Docs/DURUM.md`'ye not düş:
- `Infrastructure/AI/*`
- `Scripts/Online/**`
- `Infrastructure/Vehicles/*`
- `Infrastructure/Rendering/Atmosphere*`
- `Presentation/UI/CommandWheel*` ve `Ping*`
- `Infrastructure/Audio/Radio*`
- `Presentation/Bootstrap/Skirmish*`
- Silah eklentileri: `AttachmentCatalog`, `AttachmentStats`
- `Presentation/Tutorial/*`

Başlatma cümlesi: *"Docs/CURSOR_FAZ4.md içindeki GÖREV F4-X'i baştan sona uygula."*
F4-1 önce başlamalı. F4-3 ve F4-5 ondan bağımsız, paralel yürüyebilir.

## GÖREV F4-1 — Gerçek Unity Doğrulaması (eski F2-4 + F2-5) · EN ÖNCELİKLİ
Talimatlar: `Docs/CURSOR_FAZ2.md` → F2-4 ve F2-5 bölümleri.
1. **Kurulum:** Unity **6.6 (6000.6.4f1)**; modüller: Mac + **Windows Build Support (Mono)** + **Windows Dedicated Server**. `ProjectVersion.txt` 6000.6.4f1'e güncellenebilir.
2. **Batch derleme:** `-batchmode -quit` ile derle, `Logs/import.log` içinde **0 `error CS`**. Stub ile gerçek URP API'si arasındaki farkları düzelt.
3. **Kurulum betiği:** `Project.EditorTools.BatchEntry.SetupAll`. Şu sahneler oluşmalı ve tekrar açıldığında bozulmamalı:
   - MainMenu
   - KuzgunVadisi
   - AyazGecidi
   - TrainingRange
   - Kalıcı varlıklar `Assets/_Project/Generated/<Sahne>/` altında olmalı.
4. **Testler:**
   - EditMode: 429+ test.
   - PlayMode: `Tests/PlayMode` (`zsh Tools/UnityVerify/run_playmode.sh`).
   - `Logs/screens/*.png` ekran görüntüleri; kullanıcıya göster.
5. **Build:** Windows istemci, Windows Dedicated Server ve macOS build'leri. macOS build'ini 2 dakika çalıştırıp çökmediğini doğrula.
6. **Bulunan hataları düzelt.** Claude'un dalga 3 alanlarındaysa `DURUM.md`'ye not düş. Her turun özetini `Docs/FAZ2_DURUM.md`'ye "FAZ 4" başlığıyla yaz.

## GÖREV F4-2 — Netcode'u Etkinleştir (F4-1'den sonra)
`FAZ3_KANCALAR.md`'de Netcode için bekleyen maddeler:
- `Packages/manifest.json`'a `com.unity.netcode.gameobjects` (2.13.2) ve `com.unity.transport` ekle.
- NetworkManager prefab'ı ve sahne kurulumu: SetupAll'a ekle.
- Oyuncu prefab'ında `NetworkObject`, `NetworkPlayer` ve `NetworkTransform`.
- ConnectionApproval, en fazla 60 oyuncu.
- **Doğrulama:** **Host + 1 Client** (editör + build) ile intikal, ateş etme, hasar ve maç sonu senkronu.
- **Lag compensation testi:** yapay 150 ms gecikme ile isabetler doğru kaydediliyor mu?
- Bulgularını `DURUM.md`'ye yaz.

## GÖREV F4-3 — Windows Server Hazırlık (Staging) Provası · `Deploy/windows/*`, `Backend/`
Kullanıcının Windows Server'ında uçtan uca prova. Sunucuya erişim yoksa `Deploy/windows/STAGING_PROVA.md` adlı adım adım bir kontrol listesi yaz ve PowerShell betiklerini uçtan uca **kuru çalıştırma (dry-run)** moduyla test edilebilir hale getir.
1. **MSSQL:** veritabanı oluşturma, migration'lar, yedekleme job'ları.
2. **IIS:** Backend (HTTPS), Web, Wiki.
3. **`Harekat.ServerManager` Windows servisi + Windows Dedicated Server build'i:**
   - ServerManager maçı atasın, `HAREKAT_Server.exe` başlasın, maç bitince sonuç backend'e gitsin.
4. **Yük testi** (`Tools/LoadTest`): 1.000 sanal oyuncu ile eşleştirme ve sonuç gönderimi.
5. Sonuç raporu ve kapasite tablosunun güncellenmesi.

## GÖREV F4-4 — Unity CI (Windows)
- `.github/workflows/unity.yml`'i **self-hosted Windows runner** ile çalışır hale getir (lisans secret'ları).
- Adımlar: EditMode testleri + Windows istemci ve Dedicated Server build'leri + artefaktlar.
- `Tools/UnityVerify/verify.sh`'yi macOS veya Linux'a bağlı olmayan bir PR kontrolü olarak da ekle. Windows'ta Git Bash ya da PowerShell eşdeğeri `verify.ps1` yaz.

## GÖREV F4-5 — Hazır Varlık Paketlerini Getir · `Assets/ThirdParty/*`
Kaynak: `Design/Assets/*.csv` (Codex C3-1 listesi). Önce **ücretsiz ve lisansı uygun** olanları al.
- **Poly Haven / ambientCG:** PBR dokular (zemin, kaya, beton, taş duvar, kiremit, metal) → `MaterialId` eşlemeleri.
- **Sonniss GDC:** paketten seçilen silah, patlama ve ortam sesleri → `SoundId` eşlemeleri. Dosya boyutuna dikkat.
- **Mixamo:** kullanıcının Adobe hesabıyla; mümkün değilse indirme listesi ve adımları.
- **Eşleme ve kontrol:**
  - Eşlemeleri "HAREKÂT/İçerik/Varlık Eşleyici" ile `ContentOverrides.asset`'e yaz.
  - Lisans tablosunu `Assets/ThirdParty/README.md`'ye işle.
  - Oyunu açıp yeni dokuların ve seslerin geldiğini ekran görüntüsüyle doğrula.
- Toplam boyutu raporla. Repo şişmesin diye büyük dosyalar için **Git LFS** öner ve `.gitattributes` hazırla.

## GÖREV F4-6 — Sürüm 0.1 "Kapalı Test" Paketi
F4-1 ve F4-2 bitince:
- Windows istemci için kurulum dosyası (Inno Setup, `Tools/Installer`).
- Launcher ile staging sunucusuna bağlantı.
- Yama notu (`Web/content/patchnotes/0.1.0.md`).
- QA regresyon listesinden (`QA/RegresyonListesi.md`) 60 senaryonun koşulması ve sonuçların `QA/dashboard` CSV'sine işlenmesi.
