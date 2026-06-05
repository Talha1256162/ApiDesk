# API DESK

API DESK is a premium API collaboration platform for software houses, QA teams, backend teams, and engineering managers.

The product name is configurable in the UI and stored as organization-level configuration in the database design.

## Stack

- Backend: ASP.NET Core Web API on .NET 10
- Database: SQL Server
- Data access: Dapper only
- Frontend: Angular 21
- Realtime foundation: SignalR
- API docs: Swagger/OpenAPI
- Architecture: Controller -> Service -> Repository -> Query/SQL

## Local Database

The current local setup targets:

```text
Server=.\SQLEXPRESS
Database=ApiForgePro
```

Run scripts in order:

```powershell
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\001_schema.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\002_indexes.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\003_seed_permissions_roles.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\004_seed_demo_workspace.sql
```

Seeded admin:

```text
Email: admin@apiforge.local
Password: Admin@12345
```

## Run Backend

```powershell
dotnet build backend\ApiForge.slnx
dotnet run --project backend\src\ApiForge.Api\ApiForge.Api.csproj --urls http://localhost:5108
```

Swagger:

```text
http://localhost:5108/swagger
```

## Run Frontend

PowerShell execution policy blocks `npm.ps1` on this machine, so use `npm.cmd`:

```powershell
cd frontend\apiforge-web
npm.cmd install
npm.cmd start -- --host 127.0.0.1 --port 4200
```

App:

```text
http://127.0.0.1:4200
```

## Backend Integration Tests

The backend integration suite lives in:

```text
backend\tests\ApiForge.IntegrationTests
```

It uses `WebApplicationFactory<Program>` plus a local SQL Server test database. Testcontainers are intentionally not required for this repo because the local development environment already targets SQL Server Express.

Default test database:

```text
Server=.\SQLEXPRESS
Database=ApiForgePro_IntegrationTests
```

Optional overrides:

```powershell
$env:APIDESK_TEST_SQL_MASTER="Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
$env:APIDESK_TEST_SQL="Server=.\SQLEXPRESS;Database=ApiForgePro_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Run:

```powershell
dotnet test backend\ApiForge.slnx -c Release -v minimal
```

The test fixture drops and recreates the integration database, then applies all scripts in `database\scripts` in filename order. Do not point `APIDESK_TEST_SQL` at a real production or development database.

Covered integration areas:

- Auth login, invalid login, refresh, logout, and anonymous protected-route rejection
- Workspace and organization RBAC isolation
- Owner protection and invalid role-scope rejection
- Environment tenant isolation, manage permission enforcement, and secret masking
- Request runner auth modes, body modes, missing variables, invalid protocols, and SSRF blocking
- Mock server API-key enforcement
- Documentation public/private/password access rules
- Collection import/export/re-import folder and request preservation

## Production Configuration

Do not deploy with the local `appsettings.json` JWT signing key. In `Production`, the API now fails startup if `Jwt:SigningKey` is missing or still contains local placeholder text.

Set these environment variables or hosting configuration values:

```text
ConnectionStrings__ApiForge=<production SQL Server connection string>
Jwt__Issuer=ApiForge
Jwt__Audience=ApiForge
Jwt__SigningKey=<strong random signing key, not LOCAL_DEV/CHANGE_ME>
Jwt__AccessTokenMinutes=60
Jwt__RefreshTokenDays=14
Cors__AllowedOrigins__0=https://your-frontend-domain
RequestRunner__AllowPrivateNetworkTargets=false
Swagger__Enabled=false
BuildInfo__FrontendCommit=<git commit>
BuildInfo__BackendCommit=<git commit>
BuildInfo__DeploymentTimestampUtc=<utc timestamp>
```

Optional AI provider configuration:

```text
AI_PROVIDER=OpenAI
AI_API_KEY=<provider key>
AI_BASE_URL=<provider endpoint>
AI_MODEL=<model name>
AI_TIMEOUT_SECONDS=30
```

## Implemented Phase 1 Foundation

- JWT login/register/refresh/logout structure
- Organization creation and membership
- Database-backed roles and granular permissions
- Workspace APIs and dashboard metrics
- Collections, folders, request metadata, request versioning
- Environment and variable storage with secret masking
- Backend request runner with variable resolution
- Request run history tables and result persistence
- Activity feed and manager summary APIs
- SignalR collaboration hub foundation
- Angular SaaS shell with real API login/dashboard/activity/API client flows
- Built-in JSON tools and developer utilities module

## Verification Snapshot

Verified locally:

- Backend solution builds with 0 errors
- Frontend build succeeds
- SQL Server schema and seed scripts applied
- Login succeeds using seeded admin
- Dashboard API returns one collection and one API
- Backend request runner executes the seeded request and returns HTTP 200
- Angular login/dashboard/API client/JSON tools verified through headless Edge
