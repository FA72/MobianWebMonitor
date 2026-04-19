#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

echo "Pulling latest image..."
docker compose pull

echo "Restarting..."
docker compose up -d

echo "Cleaning old images..."
docker image prune -f

echo "Done."
docker compose ps
