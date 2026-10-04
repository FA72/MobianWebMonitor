# Mobian Web Monitor

Веб-монитор для телефона на Mobian, который работает домашним сервером. Написан на ASP.NET Core 10 и Blazor Server, запускается в Docker. Основное устройство — OnePlus 6 (ARM64); для других Linux-устройств предусмотрен общий аппаратный профиль.

Дашборд показывает нагрузку, память, аккумулятор и Docker-контейнеры в браузере. Сам монитор **не управляет зарядкой**: он читает показатели аккумулятора и состояние отдельного сервиса [battery-limiter](https://github.com/FA72/battery-limiter).

## Что есть в интерфейсе

Четыре вкладки и общий блок аккумулятора:

| Вкладка | Что показывает |
| --- | --- |
| Обзор | Нагрузку CPU по ядрам, RAM и swap, диск, состояние `battery-limiter.service` |
| CPU | Текущую нагрузку и график для всех ядер |
| RAM | Использованную, кешированную и свободную память, график |
| Docker | Все контейнеры, включая остановленные: состояние, образ, время работы, CPU и RAM |

В блоке аккумулятора — процент заряда, температура, ток, напряжение, состояние батареи и признак внешнего питания. Интерфейс тёмный, адаптирован для телефона, поддерживает русский и английский языки по настройкам браузера. Отдельной вкладки процессов в текущей версии нет.

Для графиков доступны интервалы 5 и 15 минут, 1, 6 и 24 часа. Если свежие CPU/RAM/показатели аккумулятора не поступали более пяти секунд, появляется предупреждение об устаревших данных. При таймауте Docker статистика CPU/RAM может показываться из кеша с отметкой в интерфейсе.

## Как собираются данные

| Источник | Интервал |
| --- | --- |
| CPU из `/proc/stat`, RAM из `/proc/meminfo`, аккумулятор из `/sys/class/power_supply` | 1 секунда |
| Состояние `battery-limiter.service` через системный D-Bus (`busctl`) | 10 секунд |
| Docker API через `/var/run/docker.sock` | 15 секунд |
| Диск через `DriveInfo` | 30 секунд |
| Запись CPU/RAM, заряда и температуры батареи в SQLite | 15 секунд |

Фоновые службы передают последний снимок в `MetricsAggregator`. Страница читает его раз в секунду, а Blazor Server передаёт обновления интерфейса по своему SignalR-соединению. Исторические данные для Chart.js загружаются через HTTP API.

SQLite работает в WAL-режиме, с отдельным файлом на каждый день UTC. Раз в час удаляется история старше вчерашнего дня; сохраняются сегодняшний и вчерашний файлы, чтобы график за последние 24 часа переживал смену суток. В Docker история лежит в `deploy/phone/volumes/history`, ключи защиты cookie — в `volumes/protection-keys`. Эти каталоги сохраняются при обновлении контейнера.

У OnePlus 6 данные берутся из `bq27411-0` и `pmi8998-charger`. Если этих устройств нет, включается общий Linux-профиль. Датчики, которые ядро не предоставляет, отображаются как недоступные. Подробнее о переносе: [docs/PORTING.md](docs/PORTING.md).

## Запуск для разработки

Нужны .NET SDK 10 и Linux для реальных метрик. На Windows или macOS можно собрать приложение, но Linux-датчики будут недоступны. Хранилища имеют фиксированные пути `/data/history` и `/data/protection-keys`, поэтому перед первым запуском на Linux создайте их для своего пользователя.

```bash
git clone https://github.com/FA72/MobianWebMonitor.git
cd MobianWebMonitor

dotnet build src/MobianWebMonitor/MobianWebMonitor.csproj
dotnet run --project src/MobianWebMonitor -- --generate-hash '<password>'

sudo install -d -o "$(id -u)" -g "$(id -g)" /data/history /data/protection-keys
Auth__PasswordHash='<generated-hash>' dotnet run --project src/MobianWebMonitor
```

Откройте `http://localhost:8082`. Хеш передаётся через переменную окружения; в репозитории `Auth:PasswordHash` оставлен пустым. Сам пароль и полученный хеш не нужно добавлять в Git.

## Развёртывание на телефоне

В [deploy/phone](deploy/phone) лежат Compose, пример `.env` и скрипт обновления. Контейнер `mobian-web-monitor-app` работает от UID/GID `1000:1000`, ограничен 256 МБ RAM и 0,5 CPU. Порты приложения на хост не публикуются: HTTP на `8082` доступен внутри внешней Docker-сети `caddy_net`.

Перед первым деплоем нужны:

1. Docker Engine с Compose и существующая сеть `caddy_net`.
2. Общий Caddy с маршрутом к `mobian-web-monitor-app:8082`.
3. Self-hosted GitHub Actions runner на телефоне с метками `self-hosted`, `Linux`, `ARM64`, `mobian-monitor-phone`; его пользователь должен иметь доступ к Docker.
4. Каталог `$HOME/mobian-web-monitor/deploy/phone` у пользователя runner и локальный `.env`, созданный по [.env.example](deploy/phone/.env.example).

| Переменная `.env` | Значение |
| --- | --- |
| `APP_IMAGE` | Образ GHCR, например `ghcr.io/fa72/mobianwebmonitor:latest` |
| `MONITOR_AUTH_HASH` | Хеш, полученный через `--generate-hash` |
| `DOCKER_GID` | GID группы Docker на хосте: `getent group docker \| cut -d: -f3` |

Сохраните `.env` только на телефоне и ограничьте права чтения, например `chmod 600 .env`. Runtime-конфигурация и каталоги `volumes/` исключены из Git и не заменяются workflow.

### CI/CD

1. Push в `main` запускает [Publish Container](.github/workflows/container-publish.yml) на GitHub-hosted runner `ubuntu-latest`. Buildx собирает `linux/arm64` образ из [Dockerfile](Dockerfile) и отправляет его в GHCR с тегами короткого SHA и `latest`.
2. После успешной сборки ветки `main` запускается [Deploy Phone](.github/workflows/deploy.yml). Эта задача выполняется на self-hosted runner внутри телефона, поэтому входящий SSH-доступ из GitHub не нужен.
3. Runner получает исходники того коммита, который собрал `Publish Container`, и копирует `deploy/phone` в `$HOME/mobian-web-monitor`. Локальные `.env` и `volumes/` исключены из копирования.
4. Для чтения GHCR workflow использует свой `GITHUB_TOKEN`. Скрипт [update.sh](deploy/phone/update.sh) выполняет `docker compose pull`, `docker compose up -d` и очистку образов без тегов (`docker image prune -f`).

Деплой скачивает **`APP_IMAGE` из телефонного `.env`**. Если там указан `latest`, контейнер обновляется по этому тегу; checkout исходников по SHA сам по себе не фиксирует тег образа. `update.sh` выводит состояние Compose, но не ожидает результата healthcheck. Проверка контейнера отдельно запрашивает `/login` и резервный Blazor-скрипт `/js/blazor.server.js`.

Оба workflow можно запустить вручную. Фильтра по изменённым путям нет: обычный push в `main`, в том числе с документацией, запускает сборку и последующий деплой.

### Caddy и Nginx

Публичный вход вынесен в отдельный репозиторий [CaddyConfigurator](https://github.com/FA72/CaddyConfigurator). Его [10-monitor.caddy](https://github.com/FA72/CaddyConfigurator/blob/main/deploy/phone/sites/10-monitor.caddy) задаёт `monitor.fa72.ru` и `reverse_proxy {$MONITOR_UPSTREAM}`. В общем Compose значение upstream по умолчанию — `mobian-web-monitor-app:8082`.

```text
Браузер → HTTPS / shared-caddy → HTTP / mobian-web-monitor-app:8082
                                      общая сеть caddy_net
```

Caddy завершает TLS, получает и обновляет сертификаты и проксирует HTTP/WebSocket к Blazor. Собственный Caddy в Compose монитора не запускается. Nginx для монитора тоже не нужен: Nginx в общем стеке обслуживает другие маршруты, а этот дашборд доступен через Caddy напрямую.

## Авторизация и границы доступа

Вход по одному паролю, хешированному ASP.NET Identity PasswordHasher (PBKDF2). Сессия хранится в cookie с продлеваемым сроком семь дней. После неудачных попыток входа задержка увеличивается; после пяти ошибок адрес блокируется на 15 минут. Счётчики находятся в памяти приложения и сбрасываются при его перезапуске. Дашборд и API требуют авторизации.

`/proc` и `/sys` подключены для чтения метрик хоста, системный D-Bus — для чтения состояния сервиса. Docker socket подключён с `:ro`, однако это **не делает Docker API доступным только для чтения**: доступ к сокету даёт широкие права на Docker хоста. Монитор использует API для наблюдения, но контейнеру и его runner следует доверять.

Приложение принимает forwarded-заголовки от любого прокси. Поэтому контейнер рассчитан на доступ через доверенный reverse proxy в `caddy_net`, без прямой публикации его порта в интернет. Дополнительные сведения: [docs/SECURITY.md](docs/SECURITY.md).

## Ограничения

- Нет управления зарядкой, контейнерами или systemd-сервисами из веб-интерфейса.
- Нет мониторинга сети и отдельных процессов.
- Диск определяется в файловом пространстве самого приложения: в контейнере это не перечень всех разделов хоста.
- Данные аккумулятора зависят от драйверов и аппаратного профиля; общий Linux-профиль требует проверки на конкретном устройстве.
- Графики используют Chart.js с CDN jsDelivr; без доступа к нему числовые показатели доступны, графики могут не загрузиться.
- Один пароль, без отдельных пользователей и управления сессиями.

## Структура репозитория

- [src/MobianWebMonitor](src/MobianWebMonitor) — Blazor-компоненты, API, сборщики метрик и SQLite.
- [deploy/phone](deploy/phone) — Compose и обновление контейнера.
- [.github/workflows](.github/workflows) — сборка ARM64-образа и деплой на телефон.
- [docs/PORTING.md](docs/PORTING.md), [docs/SECURITY.md](docs/SECURITY.md) — перенос на другие устройства и модель доступа.

## English overview

Mobian Web Monitor is an ASP.NET Core 10 / Blazor Server dashboard for a Linux phone used as a home server. The primary target is OnePlus 6 running Mobian (ARM64), with a generic Linux hardware profile for other devices.

- Four tabs: **Overview**, **CPU**, **RAM**, **Docker**, plus a persistent battery panel. Russian and English UI follow the browser language.
- CPU/RAM/battery are collected every second; systemd status every 10 seconds, Docker every 15 seconds, disk every 30 seconds. Blazor delivers UI updates over its SignalR connection.
- CPU/RAM/battery history is written to SQLite every 15 seconds. Daily UTC databases retain today and yesterday for the 24-hour charts.
- The monitor observes battery data and the separate [battery-limiter](https://github.com/FA72/battery-limiter) service. It does not control charging or containers.
- Pushes to `main` build a `linux/arm64` image on a GitHub-hosted runner and deploy through a phone-side self-hosted runner. Deployment preserves `.env` and `volumes/`, and pulls the phone's `APP_IMAGE` setting (usually `latest`); it is not automatically pinned to the source SHA.
- Shared [CaddyConfigurator](https://github.com/FA72/CaddyConfigurator) provides HTTPS and proxies directly to `mobian-web-monitor-app:8082` over `caddy_net`. The monitor does not need Nginx or its own Caddy.
- Password authentication uses PBKDF2, seven-day sliding cookies and an in-memory IP lockout. The container has host `/proc`, `/sys`, D-Bus and Docker socket access; a read-only socket mount does not restrict Docker API capabilities.

Build with `dotnet build src/MobianWebMonitor/MobianWebMonitor.csproj`. See the deployment section above, [runtime example](deploy/phone/.env.example), [porting notes](docs/PORTING.md) and [security notes](docs/SECURITY.md). There is no process or network monitoring in the current version.
