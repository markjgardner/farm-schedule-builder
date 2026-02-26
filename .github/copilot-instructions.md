# Copilot Instructions

## Branching and PRs

All development work must be done in a separate branch (not `main`). When work is complete, push the branch and open a pull request. Squash-merge is the preferred merge strategy.

## Build, Test, and Lint

```bash
# .NET — build and test from repo root
dotnet build FarmScheduler.sln
dotnet test FarmScheduler.sln

# Run a single .NET test by fully-qualified name
dotnet test FarmScheduler.sln --filter "FullyQualifiedName~SchedulingServiceTests.GenerateSchedule_FillsAllSlots"

# Run a single .NET test class
dotnet test FarmScheduler.sln --filter "FullyQualifiedName~AdminFunctionsTests"

# Frontend — run from src/web
cd src/web
npm run build        # tsc + vite build
npx vitest --run     # single test run
npm run lint         # eslint

# Run a single frontend test file
cd src/web && npx vitest --run src/components/__tests__/AdminPage.test.tsx
```

## Architecture

This is a horse farm shift-scheduling app. Two barns (Windhover, York) × two shifts (Morning, Evening) × 14 days = 56 slots per schedule window. Workers submit availability; a biweekly timer generates schedules.

**Backend:** .NET 8 Azure Functions (isolated worker model) in `src/FarmScheduler.Functions/`. The Function App is standalone (not SWA-managed) because it needs timer triggers and Service Bus bindings. Azure Static Web Apps links to it as a backend, proxying `/api/*` requests and forwarding the `x-ms-client-principal` auth header.

**Frontend:** React 19 + TypeScript + Vite SPA in `src/web/`. Hosted on Azure Static Web Apps (Standard tier, required for custom OIDC providers). Authentication is handled entirely by SWA Easy Auth — the frontend never touches tokens directly.

**Data:** Azure Table Storage (Workers table, Availability table). No SQL database. The Functions app storage account is reused for Table Storage to minimize cost.

**Schedule output:** Generated schedules are published to an Azure Service Bus topic (`schedule-generated`) for downstream processing.

**Infrastructure:** Bicep modules in `infra/`. Provisioned by CI/CD, not manually. Uses managed identity for all service-to-service auth — no connection strings in app settings.

## Key Conventions

### Authentication flow
SWA Easy Auth handles login (Microsoft, Google, Facebook OIDC). The `x-ms-client-principal` header (Base64-encoded JSON) is parsed by `AuthHelper.ParseClientPrincipal()` which returns a `ClientPrincipalInfo` record containing userId, userDetails, and userRoles. All Azure Functions use `AuthorizationLevel.Anonymous` — auth is enforced by checking this header, not by function keys.

### Admin authorization
Admin is app-level, not SWA-role-based. The `Worker.IsAdmin` boolean in Table Storage controls access. `AdminFunctions.RequireAdminAsync()` looks up the caller's worker record and checks `IsAdmin`. The frontend detects admin status by probing `GET /api/admin/workers` — a 200 means admin, anything else means not.

### Repository pattern
Repositories implement interfaces (`IWorkerRepository`, `IAvailabilityRepository`) and are named `XxxTableRepository` to signal Table Storage backing. Each uses a constant `PartitionKey` (e.g., `"worker"`) with the entity ID as `RowKey`. Entity-to-model mapping is done via private static `MapToXxx()` methods. All methods are async.

### Function organization
One function class per domain area (WorkerFunctions, AvailabilityFunctions, AdminFunctions, ScheduleGeneratorFunction). Constructor-injected dependencies. Return `IActionResult` (OkObjectResult, UnauthorizedResult, BadRequestObjectResult). Static `JsonSerializerOptions` as class fields.

### Frontend patterns
- `api.ts`: Generic `apiFetch<T>()` wrapper auto-redirects to `/.auth/login/aad` on 401
- `useAuth.ts`: Hook fetches `/.auth/me`, auto-registers worker on first login (fire-and-forget POST)
- Components receive auth state as props; admin tab conditionally rendered

### Testing
- .NET: xUnit + Moq + FluentAssertions. Test classes mirror source structure. Auth is mocked via `CreateRequest()` helper that builds `DefaultHttpContext` with a Base64 `x-ms-client-principal` header.
- Frontend: Vitest + React Testing Library + jsdom. Tests in `__tests__/` directories adjacent to components.

### Scheduling algorithm
Greedy constraint-satisfaction in `SchedulingService`. Processes most-constrained slots first (fewest eligible workers). Scoring weights: fairness ×10, clustering +5 (same-day shifts), barn consistency +2. Accepts an optional `Random` seed for deterministic test output. Unfilled slots get `WorkerId=""`, `WorkerName="UNFILLED"`.

### Bicep / Infrastructure
Modular: `main.bicep` orchestrates `storage`, `function-app`, `service-bus`, `key-vault`, `app-insights`, `static-web-app` modules. RBAC role assignments are separate module calls. Resource naming: `${baseName}-${environmentName}-${uniqueString(resourceGroup().id)}`. All outputs needed by CI/CD must be surfaced in `main.bicep` outputs.

### CI/CD pitfalls
- `actions/upload-artifact@v4` excludes hidden files by default — must set `include-hidden-files: true` for the `.azurefunctions` directory required by Flex Consumption
- Flex Consumption Function Apps require `az functionapp deployment source config-zip` (not `az functionapp deploy` which returns HTTP 415)
- SWA deployment token is retrieved via `az staticwebapp secrets list` — requires the correct resource name from Bicep outputs
