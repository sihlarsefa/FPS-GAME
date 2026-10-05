# HAREKÂT Launcher (.NET 10 WinForms)

Windows launcher: haber akışı, sürüm/yama, **OYNA** + JWT token aktarımı.

## Özellikler

| Özellik | Açıklama |
|---------|----------|
| Haberler | `GET /news?lang=` |
| Sürüm | `GET /client/version?channel=` |
| Yama | Zip indir → **SHA-256** doğrula → kurulum dizinine aç |
| OYNA | `HAREKAT.exe` + `-token <jwt>` ve `HAREKAT_ACCESS_TOKEN` |
| Giriş | `POST /auth/login` (isteğe bağlı; token yoksa çevrimdışı oynanabilir) |

## Gereksinimler

- Windows 10/11 x64
- .NET 10 SDK (geliştirme)
- Çalışma zamanı: `net10.0-windows` self-contained veya shared framework

> macOS/Linux'ta bu proje derlenmez (`UseWindowsForms` + `net10.0-windows`).

## Yapılandırma

`appsettings.json`:

```json
{
  "ApiBaseUrl": "https://api.harekat.example",
  "Channel": "stable",
  "Language": "tr",
  "GameExecutable": "HAREKAT.exe",
  "GameRelativeDir": "..",
  "GameArgs": ""
}
```

Kurulumda launcher `InstallerDir\Launcher\` altına konur; oyun bir üst dizindedir (`GameRelativeDir: ".."`).

Yerel sürüm dosyası: `{InstallDir}/version.txt`

## Derleme / yayın

```powershell
cd Tools\Launcher
dotnet restore
dotnet build -c Release

dotnet publish src\Harekat.Launcher\Harekat.Launcher.csproj `
  -c Release -r win-x64 --self-contained true `
  -o ..\..\Builds\Windows\Launcher
```

Inno Setup bu çıktıyı `Builds\Windows\Launcher\` olarak paketler.

## Token aktarımı

OYNA tıklandığında (giriş yapıldıysa):

1. Komut satırı: `HAREKAT.exe … -token "<accessToken>"`
2. Ortam: `HAREKAT_ACCESS_TOKEN=<accessToken>`

Unity tarafı (F3-1 Online) bu argümanı/`Environment.GetEnvironmentVariable` ile okur.

## Admin içerik

Haber ve sürüm düzenleme backend Admin API:

- `POST/PUT/DELETE /admin/news` (rol: Admin)
- `PUT /admin/client/version` (rol: Admin)

Örnek sürüm yayını:

```http
PUT /admin/client/version
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "version": "0.1.1",
  "patchUrl": "https://cdn.harekat.example/patches/0.1.1.zip",
  "sha256": "<64-hex>",
  "patchSizeBytes": 123456789,
  "releaseNotes": "Denge yaması",
  "mandatory": false,
  "channel": "stable"
}
```
