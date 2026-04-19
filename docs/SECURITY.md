# Security

## What NOT to commit

- Password hashes
- `.env` files with secrets
- SSH keys
- Any authentication tokens

## Where to store secrets

- **GitHub Secrets** for CI/CD (`MONITOR_AUTH_HASH`, `DEPLOY_SSH_PRIVATE_KEY`, etc.)
- **`.env` file** on the target device (not in Git)
- **Environment variables** passed via `compose.yaml`

## Generating a password hash

```bash
cd src/MobianWebMonitor
dotnet run -- --generate-hash YourSecurePassword
```

Copy the output hash to your `.env` file as `MONITOR_AUTH_HASH`.

## Runtime-only values

These values must ONLY exist in runtime environment, never in source code:

- `Auth:PasswordHash` / `MONITOR_AUTH_HASH`
- `DEPLOY_SSH_PRIVATE_KEY`
- Any API tokens

## Authentication

- Password-only login with PBKDF2 (ASP.NET Identity PasswordHasher)
- Cookie-based sessions (7-day sliding expiration)
- Brute-force protection: progressive delays + IP lockout after 5 failed attempts (15 min)
- All API and SignalR endpoints require authentication
- Security headers: CSP, X-Frame-Options DENY, HSTS, no-referrer
