# 🐴 Farm Schedule Builder

A web application for managing shift schedules at a horse farm. Workers submit their availability for upcoming two-week windows, and the system generates balanced schedules across barns and shifts.

## Architecture

```
┌─────────────────┐       ┌──────────────────────────┐
│  React SPA       │       │  Azure Functions API      │
│  (Static Web App)│──────▶│  (.NET 8, isolated)       │
│                  │       │                           │
│  Vite + TS       │       │  /api/availability/{w}    │
│  Auth: SWA OIDC  │       │  /api/workers             │
└─────────────────┘       │  /api/schedule/generate   │
                           └──────────┬───────────────┘
                                      │
                           ┌──────────▼───────────────┐
                           │  Azure Table Storage      │
                           │  (Availability, Workers,  │
                           │   Schedules)              │
                           └──────────────────────────┘
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 19, TypeScript, Vite |
| API | .NET 8, Azure Functions (isolated worker) |
| Storage | Azure Table Storage |
| Hosting | Azure Static Web Apps + Azure Functions |
| Auth | Azure SWA built-in auth (OIDC providers) |
| Infrastructure | Bicep (Azure IaC) |
| CI/CD | GitHub Actions |

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/<org>/farm-schedule-builder.git
   cd farm-schedule-builder
   ```

2. **Install dependencies**

   ```bash
   dotnet restore
   cd src/web && npm install
   ```

3. **Follow the [Development Guide](DEVELOPMENT.md)** for local setup, running tests, and full auth testing.

## Deployment

The project deploys automatically on push to `main` via GitHub Actions:

- **.NET API** → Azure Functions
- **React app** → Azure Static Web Apps
- **Infrastructure** → Bicep templates in `infra/`

See [DEVELOPMENT.md](DEVELOPMENT.md) for required GitHub secrets and setup details.

## Configuring OIDC Providers

Azure Static Web Apps supports custom OIDC authentication providers. Configure them in `src/web/staticwebapp.config.json` and the SWA resource settings:

1. **Microsoft Entra ID (AAD)** — built-in, no extra configuration needed. Users log in via `/.auth/login/aad`.

2. **Custom OIDC provider** — add a custom provider in the Azure portal under your Static Web App → **Settings → Authentication**:
   - Provide the **Client ID**, **Client Secret**, and **OpenID Connect metadata URL** from your identity provider.
   - Choose a provider name (e.g., `google`). Users log in via `/.auth/login/<name>`.

3. **Blocking providers** — to disable a provider, add a route in `staticwebapp.config.json`:
   ```json
   { "route": "/.auth/login/<provider>", "statusCode": 404 }
   ```

For full details, see [Azure SWA Authentication docs](https://learn.microsoft.com/azure/static-web-apps/authentication-custom).

## License

See [LICENSE](LICENSE).
