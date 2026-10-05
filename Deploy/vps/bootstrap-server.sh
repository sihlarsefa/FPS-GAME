#!/usr/bin/env bash
set -euo pipefail

# Ubuntu/Debian VPS — Docker + proje kurulumu
# Kullanım: curl ile veya repo klonlandıktan sonra ./bootstrap-server.sh

REPO_URL="${REPO_URL:-https://github.com/sihlarsefa/FPS-GAME.git}"
INSTALL_DIR="${INSTALL_DIR:-/opt/harekat}"

if ! command -v docker >/dev/null 2>&1; then
  echo "[1/5] Docker kuruluyor..."
  curl -fsSL https://get.docker.com | sh
  systemctl enable docker
  systemctl start docker
else
  echo "[1/5] Docker zaten var."
fi

if ! docker compose version >/dev/null 2>&1; then
  apt-get update -qq && apt-get install -y docker-compose-plugin git curl
fi

echo "[2/5] Repo: ${INSTALL_DIR}"
mkdir -p "$(dirname "${INSTALL_DIR}")"
if [ ! -d "${INSTALL_DIR}/.git" ]; then
  git clone "${REPO_URL}" "${INSTALL_DIR}"
else
  git -C "${INSTALL_DIR}" pull --ff-only
fi

echo "[3/5] Web build (statik)"
cd "${INSTALL_DIR}/Web"
if command -v node >/dev/null 2>&1; then
  npm run build
else
  echo "Node yok — Web/dist repo ile gelmeli veya: apt install nodejs npm && npm run build"
fi

echo "[4/5] Ortam dosyası"
cd "${INSTALL_DIR}/Deploy/vps"
if [ ! -f .env ]; then
  cp .env.production.example .env
  echo "UYARI: ${INSTALL_DIR}/Deploy/vps/.env dosyasını düzenleyin (JWT_SECRET, POSTGRES_PASSWORD)"
fi

echo "[5/5] docker compose up"
docker compose --env-file .env up -d --build

echo "Tamam. http://$(curl -s ifconfig.me 2>/dev/null || echo SUNUCU_IP)/"
