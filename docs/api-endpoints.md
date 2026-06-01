# API Endpoint Map

## Auth

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

## Organizations

- `GET /api/organizations`
- `POST /api/organizations`
- `GET /api/organizations/{organizationId}/members`
- `POST /api/organizations/{organizationId}/invites`
- `PATCH /api/organizations/{organizationId}/members/{memberId}/status`

## Workspaces

- `GET /api/workspaces?organizationId={organizationId}`
- `POST /api/workspaces`
- `GET /api/workspaces/{workspaceId}/dashboard`
- `PUT /api/workspaces/{workspaceId}`
- `DELETE /api/workspaces/{workspaceId}`

## Collections And Requests

- `GET /api/workspaces/{workspaceId}/collections`
- `POST /api/collections`
- `GET /api/collections/{collectionId}/requests`
- `PUT /api/collections/{collectionId}`
- `DELETE /api/collections/{collectionId}`
- `POST /api/collections/{collectionId}/folders`
- `POST /api/folders/{folderId}/requests`
- `GET /api/requests/{requestId}`
- `PUT /api/requests/{requestId}`
- `POST /api/requests/{requestId}/send`

## Environments

- `GET /api/workspaces/{workspaceId}/environments`
- `POST /api/environments`
- `PUT /api/environments/{environmentId}/variables`

## Activity

- `GET /api/activity`
- `GET /api/activity/manager-summary?workspaceId={workspaceId}`

## Realtime

- `GET /hubs/collaboration`
