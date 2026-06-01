export interface ApiResult<T> {
  succeeded: boolean;
  message: string;
  data: T;
  errors: { code: string; message: string; field?: string }[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  offset: number;
  count: number;
}

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  avatarUrl?: string;
  timeZone?: string;
}

export interface AuthResponse {
  user: AuthUser;
  organizationId: string;
  workspaceId?: string;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresOnUtc: string;
  refreshTokenExpiresOnUtc: string;
}

export interface Organization {
  id: string;
  name: string;
  slug: string;
  productName?: string;
  createdOn: string;
}

export interface Workspace {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  type: string;
  description?: string;
  createdOn: string;
}

export interface WorkspaceDashboard {
  workspaceId: string;
  totalWorkspaces: number;
  totalCollections: number;
  totalApis: number;
  requestsSentToday: number;
  failedRequestsToday: number;
  activeMembers: number;
  recentActivity: RecentActivity[];
  latestTestRuns: unknown[];
  slowestApis: SlowApi[];
  mostUsedEnvironments: EnvironmentUsage[];
}

export interface RecentActivity {
  createdOn: string;
  actorName: string;
  eventType: string;
  entityName: string;
  status: string;
}

export interface SlowApi {
  requestId: string;
  name: string;
  method: string;
  url: string;
  averageMs: number;
}

export interface EnvironmentUsage {
  environmentId: string;
  name: string;
  runCount: number;
}

export interface Collection {
  id: string;
  workspaceId: string;
  name: string;
  description?: string;
  ownerUserId: string;
  ownerName: string;
  requestCount: number;
  versionNumber: number;
  createdOn: string;
  modifiedOn?: string;
}

export interface ApiRequestSummary {
  id: string;
  collectionId: string;
  folderId?: string;
  name: string;
  method: string;
  url: string;
  modifiedOn: string;
}

export interface EnvironmentModel {
  id: string;
  workspaceId: string;
  name: string;
  isDefault: boolean;
  variableCount: number;
  secretCount: number;
  versionNumber: number;
  createdOn: string;
  modifiedOn?: string;
}

export interface ActivityEvent {
  id: string;
  actorName: string;
  actorEmail: string;
  eventType: string;
  entityType: string;
  entityName?: string;
  status: string;
  severity: string;
  summary?: string;
  createdOn: string;
}

export interface ManagerSummary {
  activeUsersToday: number;
  requestsSentToday: number;
  failedApisToday: number;
  collectionsChangedToday: number;
  environmentsChangedToday: number;
  pendingApprovals: number;
  requestsPerDay: { date: string; value: number }[];
  failedRequestsPerDay: { date: string; value: number }[];
  mostActiveUsers: { userId: string; name: string; count: number }[];
  topFailedEndpoints: { requestId?: string; endpoint: string; failureCount: number }[];
  averageResponseTimeByEndpoint: { requestId?: string; endpoint: string; averageMs: number }[];
}

export interface ApiResponse {
  runId: string;
  statusCode: number;
  succeeded: boolean;
  elapsedMs: number;
  sizeBytes: number;
  headers: Record<string, string[]>;
  cookies: Record<string, string[]>;
  contentType: string;
  body: string;
  bodyPreview: string;
  startedOnUtc: string;
  completedOnUtc: string;
}
