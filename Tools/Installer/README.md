# HAREKÂT Windows Kurulum Paketi (Inno Setup)

`Builds/Windows` Unity player çıktısından `HarekatSetup-<sürüm>.exe` üretir.

## Gereksinimler

- [Inno Setup 6+](https://jrsoftware.org/isinfo.php) (Türkçe dil paketi dahil)
- Unity Windows x64 player build → `Builds/Windows/`
- (İsteğe bağlı) Launcher publish → `Builds/Windows/Launcher/`

## Derleme

```powershell
# Varsayılan yollar
.\Tools\Installer\build.ps1

# Özel kaynak / çıktı
.\Tools\Installer\build.ps1 -SourceDir D:\Builds\Windows -Version 0.2.0
```

Manuel:

```text
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Tools\Installer\Harekat.iss /DMyAppVersion=0.1.0
```

Çıktı: `Builds/Installer/HarekatSetup-<sürüm>.exe`

## İçerik

| Özellik | Durum |
|---------|--------|
| Masaüstü / Başlat menüsü kısayolu | Evet |
| Kaldırıcı | Evet (Inno Uninstall) |
| VC++ 2015–2022 x64 kontrolü | Evet (`InitializeSetup`) |
| Kurulum dili TR / EN | Evet |
| Launcher öncelikli başlatma | Varsa `Launcher\Harekat.Launcher.exe` |

## VC++ çalışma zamanı

Kurulum, kayıt defterinde `VC\Runtimes\x64\Installed=1` arar. Yoksa kullanıcıya
https://aka.ms/vs/17/release/vc_redist.x64.exe bağlantısı gösterilir ve kurulum durur.

Üretimde `vc_redist.x64.exe`'yi `[Files]` + `[Run]` ile sessiz kurmak da mümkündür
(`/install /quiet /norestart`); boyut artar (~25 MB).

## Kod imzalama

Ayrıntılar: [CodeSigning.md](CodeSigning.md)
