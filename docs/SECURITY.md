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
- All API and SignalR endpoints require authentication
- Security headers: CSP, X-Frame-Options DENY, HSTS, no-referrer
