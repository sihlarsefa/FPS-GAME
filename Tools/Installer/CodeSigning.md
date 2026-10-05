# Kod İmzalama Notları (Authenticode)

Windows SmartScreen ve kurumsal AV için kurulum / launcher / oyun EXE'lerinin
Authenticode ile imzalanması gerekir.

## Sertifika

1. **EV Code Signing** (önerilen) veya standart OV Code Signing sertifikası alın
   (DigiCert, Sectigo, GlobalSign vb.).
2. EV genelde USB token / HSM ile gelir; CI'da token oturumu gerekir.
3. Sertifika subject: yayıncı adınız (`HAREKÂT Studios` ile uyumlu tutun).

## signtool

Windows SDK içindeki `signtool.exe`:

```powershell
$cert = "D:\certs\harekat-codesign.pfx"   # veya /sha1 <thumbprint>
$pwd  = $env:HAREKAT_CODESIGN_PASSWORD

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /f $cert /p $pwd `
  "Builds\Installer\HarekatSetup-0.1.0.exe"

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /f $cert /p $pwd `
  "Builds\Windows\HAREKAT.exe" `
  "Builds\Windows\Launcher\Harekat.Launcher.exe"
```

Doğrulama:

```powershell
signtool verify /pa /v "Builds\Installer\HarekatSetup-0.1.0.exe"
```

## Inno Setup SignTool

`Harekat.iss` içinde:

```ini
[Setup]
SignTool=signtool
SignedUninstaller=yes
```

`ISCC` çalıştırmadan önce Inno IDE → Tools → Configure Sign Tools:

```text
Name: signtool
Command: signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $qD:\certs\harekat.pfx$q /p $qPASSWORD$q $f
```

veya `build.ps1 -Sign` ile kurulum sonrası imzalayın.

## CI (GitHub Actions / self-hosted Windows)

- PFX'i **secret** olarak saklayın; asla repoya koymayın.
- Timestamp sunucusu erişilebilir olmalı (ağ kesintisinde imza geçersiz kalabilir).
- SHA-256 digest kullanın (SHA-1 artık kabul edilmez).

## Launcher yama zip

Yama zip'leri Authenticode ile imzalanmaz; bunun yerine backend
`/client/version` → `sha256` alanı ile bütünlük doğrulanır (Launcher PatchService).
Zip içindeki yeni EXE'ler mümkünse yeniden imzalanmış olmalıdır.
