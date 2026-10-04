# Security

## What NOT to commit

- Password hashes
- `.env` files with secrets
- SSH keys
- Any authentication tokens

## Where to store secrets

- **Phone-side `.env` file** for runtime-only values such as `MONITOR_AUTH_HASH`
- **`.env` file** on the target device (not in Git)
- **Environment variables** passed via `compose.yaml`

Notes:
- Normal deploy no longer depends on external SSH from GitHub-hosted runners.
- In the current self-hosted setup, `MONITOR_AUTH_HASH` is not consumed as a GitHub Actions deploy secret.

## Generating a password hash

```bash
cd src/MobianWebMonitor
dotnet run -- --generate-hash YourSecurePassword
```

Copy the output hash to your `.env` file as `MONITOR_AUTH_HASH`.

## Runtime-only values

These values must ONLY exist in runtime environment, never in source code:

- `Auth:PasswordHash` / `MONITOR_AUTH_HASH`
- Any API tokens

## Authentication

- Password-only login with PBKDF2 (ASP.NET Identity PasswordHasher)
- Cookie-based sessions (7-day sliding expiration)
- Brute-force protection: progressive delays + IP lockout after 5 failed attempts (15 min)
- The dashboard and metrics/history API require authentication; login and static assets are public
- Security headers: CSP, X-Frame-Options DENY, HSTS, no-referrer

The failed-login counters are in memory and reset when the application restarts. Persistent data-protection keys in `/data/protection-keys` let existing authentication cookies survive a normal container replacement.

## Host access and reverse proxy

The container reads host `/proc` and `/sys`, the system D-Bus socket and `/var/run/docker.sock`. The application uses Docker API calls for monitoring, but mounting the socket with `:ro` does not make the API read-only. Access to the socket grants powerful control over the host Docker daemon; treat this container and the Docker-enabled deployment runner as trusted components.

The current forwarded-headers configuration trusts any proxy. Keep the application behind a trusted reverse proxy on `caddy_net`, as the supplied Compose file does, and do not publish port `8082` directly to the internet. Shared Caddy terminates HTTPS and proxies to the monitor; no separate Nginx is required for this application.

## Deployment behavior

Every ordinary push to `main`, including documentation changes, triggers container publishing and then phone deployment. The deploy script pulls the `APP_IMAGE` value in the phone-side `.env`; using `latest` means the running image is not pinned automatically to the workflow source SHA. The script reports Compose status but does not wait for the container healthcheck to pass.
