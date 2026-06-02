# API DESK Architecture

## Backend

The backend uses a direct clean architecture:

```text
ApiForge.Api -> ApiForge.Application -> ApiForge.Persistence -> SQL Server
ApiForge.Api -> ApiForge.Infrastructure
ApiForge.Application -> ApiForge.Domain + ApiForge.Shared
```

Controllers only handle HTTP concerns. Services enforce business rules, RBAC, tenant checks, and activity recording. Repositories perform async Dapper database operations using parameterized SQL. Query classes hold reusable SQL where the query is shared or security-sensitive.

## Security

- JWT access token plus refresh token table
- BCrypt password hashing
- Database-backed RBAC
- Permission checks use permission keys, not hardcoded role names
- Sensitive values are masked before activity/run-result storage
- Correlation IDs are attached to requests and activity events

## Frontend

Angular is organized around:

```text
src/app/core      API client, models, auth interceptor
src/app/shared    Monaco editor component
src/app/features  Developer tools services
src/app/app.*     Initial product shell
```

The current UI is intentionally API-backed: login, organization, workspace, dashboard, collections, environments, activity, request sending, and JSON tools all use working code paths rather than static dashboard data.

## Database

The schema is normalized around organizations, workspaces, members, roles, permissions, collections, requests, environments, runs, activity, audit logs, mocks, monitors, docs, governance, and developer tools. Operational tables include `createdOn`, `createdBy`, `modifiedOn`, `modifiedBy`, `isDeleted`, and `versionNumber` where optimistic concurrency is useful.
