# HAREKÂT — Yeni bilgisayara taşıma

Tarih: 2026-10-06. Bu dal (`tasima-2026-10-06`) projenin o anki tam halidir: kod, sahneler, üretilmiş varlıklar,
ThirdParty (Poly Haven CC0) modelleri/dokuları/HDRI'ler ve ses dosyaları (büyük ikililer Git LFS'te).
Durum: 7 derleme biriminin hepsi 0 hata, 2239 EditMode testi geçiyor.

## 1. Kurulacaklar

| Araç | Sürüm | Not |
|---|---|---|
| Git + Git LFS | güncel | Klonlamadan ÖNCE `git lfs install` (yoksa modeller/sesler boş işaretçi gelir) |
| Unity Hub + Unity Editor | **6000.6.4f1** (macOS: Apple Silicon) | Modüller: hedef platform (Mac/Windows Build Support, Windows Dedicated Server) |
| .NET SDK | 10.x | `Tools/UnityVerify/verify.sh` (Unity'siz derleme + test) için |
| Blender | 5.2 LTS | İsteğe bağlı (model işleme) |

## 2. Klonlama

```
git lfs install
git clone -b tasima-2026-10-06 https://github.com/sihlarsefa/FPS-GAME.git
cd FPS-GAME
git lfs pull
```

Kontrol: `git lfs ls-files | wc -l` sıfırdan büyük olmalı; `Assets/ThirdParty/HDRI/*.hdr` dosyaları birkaç MB olmalı (130 bayt ise LFS inmemiştir → `git lfs pull`).

## 3. İlk açılış

1. Unity Hub → Add → klasörü seç → 6000.6.4f1 ile aç. İlk içe aktarma uzun sürer (Library/ yeniden üretilir; depoda yok).
2. Menü `HAREKÂT` araçları veya batch: `Unity -batchmode -quit -projectPath . -executeMethod Project.EditorTools.BatchEntry.SetupAll`
   (URP kademeleri, ses mikseri vb. yeniden üretir; GraphicsSettings'te "BatchRendererGroup Variants = Keep All" zorunlu — SetupAll bunu zorlar).
3. macOS build: `Unity -batchmode -quit -projectPath . -executeMethod Project.EditorTools.BatchEntry.BuildMac -logFile Logs/build_mac.log`
4. Otomatik 4K ekran görüntüsü: `zsh Tools/UnityVerify/oto_ekran.sh --menu "$PWD/Logs/otoekran/menu"` (çıktı yolu MUTLAK olmalı).

## 4. Unity'siz doğrulama

```
zsh Tools/UnityVerify/setup_deps.sh        # bir kez
zsh Tools/UnityVerify/verify.sh /tmp/out --tests --player
```
`verify.sh` içindeki Unity yolu (`U=/Applications/Unity/Hub/Editor/6000.6.0f1/...`) makineye göre düzeltilmeli.

## 5. Depoda OLMAYANLAR (bilerek)

- `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/` — Unity yeniden üretir / build yeniden alınır.
- `.env` dosyaları — sunucu sırları; `Deploy/vps/.env.production.example` şablonundan yeniden oluşturun.
- `Tools/UnityVerify/.deps/` — `setup_deps.sh` yeniden kurar.
- Claude Code hafızası (`~/.claude/projects/...`) makineye özeldir; bağlam için `Docs/DURUM.md`, `Docs/SIRADAKI_PLAN.md`,
  `Docs/UCUNCU_PARTI_PLAN.md` okunur.

## 6. Kritik notlar

- **Görünmeyen asker kök nedeni**: GPU Resident Drawer açıkken `BatchRendererGroup Variants` "Keep All" olmazsa build'de
  URP/Lit nesneleri çizilmez (editörde görünür). Ayar `ProjectSettings/GraphicsSettings.asset` → `m_BrgStripping: 2`.
- Depo HERKESE AÇIK: satın alınan Asset Store paketleri depoya girmeden önce depo private yapılmalı (EULA §2.2.1.1(d)).
- Bu dal main'e birleştirilince `windows-deploy.yml` Backend/Web/Wiki değişiklikleri yüzünden canlı Windows sunucusuna
  otomatik dağıtım yapar; birleştirmeyi dağıtıma hazır olunca yapın.
- Yarım kalan iş: G33 dalgası (lobi askerleri, vitrin, renk birleştirme, POI ışıkları, mikser lowpass, silah ince detay,
  aksesuar kataloğu, asker tepkileri, iç mekan, yol detayı, asker ekipmanı, cephe hattı) oturum kapanınca yarıda kaldı;
  kod bu haliyle derleniyor ve testler geçiyor, kalan kısımlar yeniden çalıştırılmalı.
