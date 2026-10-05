# VPS dağıtım (Docker)

Hedef: **Backend API + Telemetry + Postgres + Redis + Web (nginx)** tek makinede.

## Önkoşul — SSH

Sunucuya (`134.149.201.54`) aşağıdaki **public key** `authorized_keys` içinde olmalı (kullanıcı: genelde `root` veya sağlayıcının verdiği kullanıcı):

```
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAII5EDuxQKw2soAV4ceBexhrSWqoug7ab3LlnO+4Mibk3 f2gomobil-deploy@192
```

Yerel özel anahtar: `~/.ssh/f2gomobil_deploy`

Test:

```bash
ssh -i ~/.ssh/f2gomobil_deploy root@134.149.201.54
```

## İlk kurulum (sunucuda)

```bash
export REPO_URL=https://github.com/sihlarsefa/FPS-GAME.git
export INSTALL_DIR=/opt/harekat
curl -fsSL https://raw.githubusercontent.com/sihlarsefa/FPS-GAME/main/Deploy/vps/bootstrap-server.sh | bash
```

Veya repo klonlandıktan sonra:

```bash
sudo INSTALL_DIR=/opt/harekat ./Deploy/vps/bootstrap-server.sh
```

`.env` dosyasını düzenleyin (`Deploy/vps/.env`):

- `POSTGRES_PASSWORD` — güçlü parola
- `JWT_SECRET` — en az 32 karakter rastgele
- `HTTP_PORT` — varsayılan `80`

```bash
cd /opt/harekat/Deploy/vps
docker compose --env-file .env up -d --build
docker compose ps
```

Kontrol: `http://134.149.201.54/` (portal), `http://134.149.201.54/health`, `http://134.149.201.54/swagger`

## GitHub Actions (CI/CD)

1. `gh auth refresh -h github.com -s workflow` ile **workflow** scope ekleyin; `.github/workflows/` klasörünü `main`'e push edin.
2. Repository → **Settings → Environments** → `vps` oluşturun.
3. Environment secret'ları:

| Secret | Değer |
|--------|--------|
| `VPS_HOST` | `134.149.201.54` |
| `VPS_USER` | SSH kullanıcı adı |
| `VPS_SSH_PRIVATE_KEY` | `f2gomobil_deploy` dosyasının tam içeriği |
| `VPS_INSTALL_DIR` | (opsiyonel) `/opt/harekat` |

`vps-deploy.yml`: `main` push veya manuel **workflow_dispatch** ile sunucuda `git pull` + `docker compose up -d --build` çalıştırır.
