# Development Guide

## Prerequisites

- [Node.js 20](https://nodejs.org/) (LTS)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (Azure Storage emulator)

Install Azurite globally if you haven't already:

```bash
npm install -g azurite
```

## Local Development Setup

### 1. Start Azurite (storage emulator)

```bash
azurite --silent
```

### 2. Start the Azure Functions API

```bash
cd src/FarmScheduler.Functions
func start
```

The API will be available at `http://localhost:7071`.

### 3. Start the React frontend

```bash
cd src/web
npm install
npm run dev
```

The frontend will be available at `http://localhost:5173`.

### Full Auth Testing with SWA CLI

Authentication via `/.auth/*` routes only works through Azure Static Web Apps. For local auth testing, use the SWA CLI:

```bash
npm install -g @azure/static-web-apps-cli
swa start --api-devserver-url http://localhost:7071
```

This proxies both the frontend and API through the SWA emulator (default `http://localhost:4280`) and provides a mock authentication flow.

## Running Tests

### .NET tests

```bash
dotnet test
```

### Frontend tests

```bash
cd src/web
npm test          # watch mode
npx vitest --run  # single run
```

### Linting

```bash
cd src/web
npm run lint
```

## Project Structure

```
├── src/
│   ├── FarmScheduler.Core/       # Shared domain models and logic
│   ├── FarmScheduler.Functions/  # Azure Functions API
│   └── web/                      # React frontend (Vite + TypeScript)
├── tests/                        # .NET test projects
├── infra/                        # Bicep infrastructure-as-code
└── .github/workflows/            # CI/CD pipeline
```

## Deployment

Deployment is automated via the GitHub Actions workflow in `.github/workflows/build-deploy.yml`. Pushes to `main` trigger the full pipeline:

1. **Build & Test (.NET)** — restores, builds, tests, and publishes the Functions app
2. **Build & Test (Web)** — installs, builds, tests, and uploads the React app
3. **Deploy to Azure** — provisions infrastructure via Bicep, deploys the Functions app and Static Web App

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_CLIENT_ID` | Service principal client ID (OIDC federated credentials) |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Target resource group |
| `AZURE_FUNCTIONAPP_NAME` | Azure Function App name |
| `AZURE_STATIC_WEB_APP_TOKEN` | SWA deployment token |
