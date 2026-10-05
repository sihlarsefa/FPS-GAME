# HAREKÂT CI/CD — Gerekli Secret'lar ve Ortamlar

Bu doküman `.github/workflows/` altındaki iş akışlarının çalışması için GitHub repository ayarlarında tanımlanması gereken **secret**, **variable** ve **environment** değerlerini listeler.

## Hızlı bakış

| Workflow | Tetikleyici | Ne yapar |
|----------|-------------|----------|
| `backend.yml` | `Backend/**` push/PR | .NET 10 build, test, Docker imajı (GHCR) |
| `web.yml` | `Web/**` push/PR | `npm ci`, lint, build |
| `unity.yml` | Unity proje dosyaları | EditMode test + macOS (`BatchEntry.BuildMac`) + Linux dedicated server |
| `release.yml` | `v*.*.*` tag | Changelog, artefaktlar, GitHub Release |
| `pr-checks.yml` | PR / push | Format, analyzer, CodeQL |
| `deploy.yml` | Manuel / Release sonrası | Staging & production (onaylı) dağıtım |

Dependabot: `.github/dependabot.yml` — NuGet, npm, Docker, GitHub Actions.

---

## 1. Unity lisans secret'ları (zorunlu — Unity CI)

Game-CI (`game-ci/unity-builder`, `game-ci/unity-test-runner`) için repository **Settings → Secrets and variables → Actions** altında:

| Secret | Açıklama | Ne zaman |
|--------|----------|----------|
| `UNITY_LICENSE` | `.ulf` lisans dosyasının **tam içeriği** (Personal / Plus / Pro) | Tercih edilen yöntem |
| `UNITY_EMAIL` | Unity hesabı e-posta | Lisans aktivasyonu / serial ile |
| `UNITY_PASSWORD` | Unity hesabı şifresi | Lisans aktivasyonu / serial ile |
| `UNITY_SERIAL` | Unity Pro/Plus serial numarası | Floating / serial lisansı |

**Önerilen kurulum (Personal):**

1. Yerelde Unity Hub ile bir kez lisansı etkinleştirin.
2. `~/Library/Unity/Unity_lic.ulf` (macOS) veya eşdeğer dosyanın içeriğini `UNITY_LICENSE` secret'ına yapıştırın.
3. Aynı hesabın `UNITY_EMAIL` / `UNITY_PASSWORD` değerlerini de ekleyin (yeniden aktivasyon için).

**Pro/Plus serial:**

- `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL` yeterli olabilir; yine de `UNITY_LICENSE` yedek olarak tutulabilir.

> Unity sürümü workflow'larda `6000.6.0f1` olarak sabitlenmiştir (`ProjectSettings/ProjectVersion.txt` ile uyumlu).

### Build metodları

| Platform | `buildMethod` | Açıklama |
|----------|---------------|----------|
| macOS | `BatchEntry.BuildMac` | İstemci build (editor-setup ajanı) |
| Linux Dedicated Server | `BatchEntry.BuildLinuxDedicatedServer` | Sunucu build |

---

## 2. Container registry

| Secret / izin | Açıklama |
|---------------|----------|
| `GITHUB_TOKEN` | Otomatik sağlanır; `packages: write` ile `ghcr.io/harekat/backend` push |

İmaj etiketleri: `latest` (main), `sha-<kısa>`, `vX.Y.Z` (release).

---

## 3. Dağıtım (Deploy) secret'ları

`deploy.yml` GitHub **Environments** kullanır: `staging` ve `production`.

### Environment: `staging`

| Secret | Açıklama |
|--------|----------|
| `KUBE_CONFIG_STAGING` | Staging kubeconfig dosyasının **base64** içeriği |
| `STAGING_HEALTH_URL` | (Opsiyonel) Health check URL; varsayılan `https://staging.harekat.example.com/health` |

Staging için environment protection rule zorunlu değildir; istenirse reviewer eklenebilir.

### Environment: `production`

| Secret | Açıklama |
|--------|----------|
| `KUBE_CONFIG_PRODUCTION` | Production kubeconfig **base64** |
| `PRODUCTION_HEALTH_URL` | (Opsiyonel) Health URL |

**Onay akışı:**

1. GitHub Environment `production` → **Required reviewers** açın.
2. `workflow_dispatch` ile `environment=production` seçin.
3. `confirm_production` alanına tam olarak `deploy` yazın.
4. Environment onayı + blue-green trafik geçişi uygulanır.

---

## 4. CodeQL

Ek secret gerekmez. `GITHUB_TOKEN` + `security-events: write` yeterlidir.

Yapılandırma: `.github/codeql/codeql-config.yml`

---

## 5. Dependabot

Secret gerekmez. PR'lar Dependabot botu tarafından açılır.

Kapsam:

- `/Backend` — NuGet
- `/Backend/Harekat.Telemetry` — NuGet
- `/Web` — npm
- `/Backend`, `/Deploy/server` — Docker
- `/` — GitHub Actions

---

## 6. Release & changelog

Tag formatı: `vMAJOR.MINOR.PATCH` (ör. `v1.2.0`).

Changelog: Conventional Commits + `git-cliff` (`.github/cliff.toml`).

Örnek commit mesajları:

```
feat(matchmaking): bölge bazlı kuyruk
fix(api): JWT süre aşımı
chore(deps): bump xunit
ci: Unity Library cache anahtarı
```

Manuel tetikleme: Actions → **Release** → Run workflow → tag girin.

---

## 7. Ortam değişkenleri (opsiyonel Variables)

| Variable | Varsayılan | Açıklama |
|----------|------------|----------|
| (yok) | — | Şu an tüm sabitler workflow `env:` bloklarında |

---

## 8. Kontrol listesi

Repository sahibi / devops:

- [ ] `UNITY_LICENSE` (+ `UNITY_EMAIL` / `UNITY_PASSWORD` / gerekirse `UNITY_SERIAL`)
- [ ] Packages izni: workflow'ların GHCR'a yazabilmesi
- [ ] Environment `staging` oluştur; `KUBE_CONFIG_STAGING`
- [ ] Environment `production` oluştur; required reviewers + `KUBE_CONFIG_PRODUCTION`
- [ ] `BatchEntry.BuildMac` / `BatchEntry.BuildLinuxDedicatedServer` editor script'lerinin repoda olduğundan emin ol
- [ ] `Backend/Dockerfile` ve `Web/package.json` hazır olunca ilgili workflow'ları bir kez manuel çalıştır

---

## Workflow diyagramı

```
PR / push
 ├── pr-checks.yml  → format + analyzers + CodeQL
 ├── backend.yml     → build → test → docker
 ├── web.yml         → npm ci → lint → build
 └── unity.yml      → EditMode tests
                      ├── macOS (BatchEntry.BuildMac) [Library cache]
                      └── Linux dedicated server      [Library cache]

tag v*.*.*
 └── release.yml → changelog → artefacts → GitHub Release → (opsiyonel) deploy staging

workflow_dispatch deploy.yml
 ├── staging     → kube apply / set image → health
 └── production  → confirm=deploy + environment approval → blue-green → health
```

## Notlar

- Yalnızca `.github/` klasörü bu görev kapsamında yazılmıştır.
- Unity **Library** klasörü platform bazlı `actions/cache` ile önbelleğe alınır; build sürelerini kısaltır.
- Web veya Backend henüz yokken `pr-checks` ilgili adımları atlar; asıl `web.yml` / `backend.yml` path filtresiyle tetiklenir.
