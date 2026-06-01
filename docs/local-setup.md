# Local Setup

## Prerequisites Verified On This Machine

- .NET SDK `10.0.204`
- Node `v24.15.0`
- npm through `npm.cmd` `11.12.1`
- SQL Server Express `SQLEXPRESS`
- Docker was not found on PATH

## Setup

```powershell
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\001_schema.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\002_indexes.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\003_seed_permissions_roles.sql
sqlcmd -S .\SQLEXPRESS -E -b -i database\scripts\004_seed_demo_workspace.sql
dotnet build backend\ApiForge.slnx
cd frontend\apiforge-web
npm.cmd install
npm.cmd run build
```

## Run

Terminal 1:

```powershell
dotnet run --project backend\src\ApiForge.Api\ApiForge.Api.csproj --urls http://localhost:5108
```

Terminal 2:

```powershell
cd frontend\apiforge-web
npm.cmd start -- --host 127.0.0.1 --port 4200
```

Open:

```text
http://127.0.0.1:4200
```
