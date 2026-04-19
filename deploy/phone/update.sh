#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

# Ensure volume directories exist with correct ownership (container runs as uid 1000)
mkdir -p volumes/history
if [ "$(stat -c '%u' volumes/history 2>/dev/null)" != "1000" ]; then
  chown 1000:1000 volumes/history 2>/dev/null || chmod 777 volumes/history
fi

mkdir -p volumes/protection-keys
if [ "$(stat -c '%u' volumes/protection-keys 2>/dev/null)" != "1000" ]; then
  chown 1000:1000 volumes/protection-keys 2>/dev/null || chmod 777 volumes/protection-keys
fi

echo "Pulling latest image..."
docker compose pull

echo "Restarting..."
docker compose up -d

echo "Cleaning old images..."
docker image prune -f

echo "Done."
docker compose ps
