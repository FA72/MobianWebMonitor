# Mobian Web Monitor

A lightweight, dark-themed, mobile-first web dashboard for monitoring a Mobian (Linux) device. Built for OnePlus 6 running Mobian with Docker, but portable to other ARM64 Linux devices.

## Features

- **Real-time metrics** via SignalR (1-second updates for CPU, RAM, Battery)
- **Battery monitoring** with charge limiter status, temperature, current, voltage, health
- **CPU/RAM charts** with Chart.js (5m / 15m / 1h / 6h / 24h range)
- **Docker container list** with status, CPU, and memory usage
- **Systemd service status** for battery-limiter via D-Bus
- **SQLite history** with daily file rotation and auto-cleanup
- **Password-only auth** with PBKDF2 hashing and brute-force protection
- **Dark theme** optimized for mobile screens
- **Non-root Docker** container with resource limits

## Quick Start (Local Dev)

```bash
# Clone
git clone <repo-url>
cd MobianWebMonitor

# Generate a password hash
cd src/MobianWebMonitor
dotnet run -- --generate-hash YourPassword

# Set the hash in appsettings.json under Auth:PasswordHash
# Then run
dotnet run
```

Note: CPU/RAM/Battery collectors read from `/proc` and `/sys`, which only work on Linux. On Windows/macOS, metrics will show N/A.

## Production Deployment

1. Set up GitHub Secrets (see below)
2. Push to `main` → CI builds and pushes `linux/arm64` image to GHCR
3. On the device, copy `deploy/phone/` files
4. Create `.env` from `.env.example`
5. Run `bash update.sh` or trigger the Deploy workflow

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | SSH host (e.g. `192.168.1.130`) |
| `DEPLOY_USER` | SSH user (e.g. `sshadmin`) |
| `DEPLOY_SSH_PRIVATE_KEY` | SSH private key for deploy |
| `MONITOR_AUTH_HASH` | PBKDF2 password hash |

### Device Setup

```bash
# Find Docker group GID
getent group docker | cut -d: -f3

# Create deploy directory
mkdir -p ~/mobian-web-monitor/deploy/phone/volumes/history

# Copy deploy files and create .env
```

## Architecture

- **ASP.NET Core 10** Blazor Server + SignalR
- **MetricsAggregator** — thread-safe snapshot holder
- **FastMetricsService** (1s) — CPU, RAM, Battery → SignalR push + SQLite write
- **SlowMetricsService** (5-30s) — Disk, Docker, systemd service
- **HistoryStorage** — SQLite WAL mode, daily file rotation
- **HardwareProfileFactory** — auto-detects OnePlus 6 or falls back to generic Linux

## Limitations

- Single-user password auth (no multi-user, no sessions management)
- No network metrics in current version
- Docker stats require socket access (host Docker group GID)
- D-Bus access required for systemd service status

---

# Mobian Web Monitor (RU)

Легковесный тёмный дашборд для мониторинга устройства на Mobian (Linux). Создан для OnePlus 6 с Docker, но портируем на другие ARM64 Linux устройства.

## Возможности

- **Метрики в реальном времени** через SignalR (обновление каждую секунду для CPU, RAM, Battery)
- **Мониторинг батареи** со статусом лимитера заряда, температурой, током, напряжением, здоровьем
- **Графики CPU/RAM** на Chart.js (5м / 15м / 1ч / 6ч / 24ч)
- **Список Docker контейнеров** со статусом, CPU и использованием памяти
- **Статус systemd сервиса** battery-limiter через D-Bus
- **История в SQLite** с ежедневной ротацией файлов и автоочисткой
- **Авторизация по паролю** с PBKDF2 хешированием и защитой от перебора
- **Тёмная тема** оптимизирована для мобильных экранов

## Быстрый старт

```bash
git clone <repo-url>
cd MobianWebMonitor/src/MobianWebMonitor

# Сгенерировать хеш пароля
dotnet run -- --generate-hash ВашПароль

# Установить хеш в appsettings.json -> Auth:PasswordHash
dotnet run
```

## Деплой

См. английскую секцию выше. Файлы деплоя в `deploy/phone/`.
