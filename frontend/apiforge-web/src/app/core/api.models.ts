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
  folderName?: string;
  name: string;
  method: string;
  url: string;
  modifiedOn: string;
}

export interface KeyValueItem {
  key: string;
  value?: string;
  enabled: boolean;
  isSecret: boolean;
}

export interface ApiRequestDetail extends ApiRequestSummary {
  workspaceId: string;
  description?: string;
  authType?: string;
  authConfigJson?: string;
  bodyType: string;
  bodyContent?: string;
  timeoutMs: number;
  followRedirects: boolean;
  sslVerification: boolean;
  headers: KeyValueItem[];
  queryParams: KeyValueItem[];
  pathParams: KeyValueItem[];
  versionNumber: number;
  createdOn: string;
}

export interface SaveApiRequestPayload {
  workspaceId: string;
  collectionId: string;
  name: string;
  description?: string;
  method: string;
  url: string;
  authType?: string;
  authConfigJson?: string;
  bodyType: string;
  bodyContent?: string;
  preRequestScript?: string;
  testScript?: string;
  timeoutMs: number;
  followRedirects: boolean;
  sslVerification: boolean;
  headers: KeyValueItem[];
  queryParams: KeyValueItem[];
  pathParams: KeyValueItem[];
  versionNumber: number;
}

export type ImportApiRequestPayload = Omit<SaveApiRequestPayload, 'workspaceId' | 'collectionId' | 'versionNumber'>;
export type ImportApiRequestWithFolderPayload = ImportApiRequestPayload & { folderPath?: string[] };

export interface ImportCollectionPayload {
  name: string;
  description?: string;
  folders?: string[][];
  requests: ImportApiRequestWithFolderPayload[];
}

export interface CollectionImportResult {
  collectionId: string;
  name: string;
  requestCount: number;
}

export interface ApiRequestExport extends ImportApiRequestPayload {
  id: string;
}

export interface CollectionExport {
  formatVersion: string;
  collection: Collection;
  requests: ApiRequestExport[];
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

export interface AuditLog {
  id: string;
  organizationId: string;
  workspaceId?: string;
  actorUserId: string;
  actorName: string;
  actorEmail: string;
  eventType: string;
  entityType: string;
  entityId?: string;
  entityName?: string;
  action: string;
  oldValueJson?: string;
  newValueJson?: string;
  ipAddress?: string;
  userAgent?: string;
  severity: string;
  correlationId: string;
  createdOn: string;
}

export interface CommentModel {
  id: string;
  workspaceId: string;
  entityType: string;
  entityId: string;
  body: string;
  createdBy: string;
  createdByName: string;
  createdOn: string;
}

export interface RequestExample {
  id: string;
  requestId: string;
  name: string;
  statusCode?: number;
  headersJson?: string;
  body?: string;
  createdOn: string;
}

export interface MockServer {
  id: string;
  workspaceId: string;
  collectionId: string;
  collectionName: string;
  name: string;
  slug: string;
  isPublic: boolean;
  apiKeyRequired: boolean;
  delayMs: number;
  routeCount: number;
  logCount: number;
  createdOn: string;
}

export interface MockRoute {
  id: string;
  mockServerId: string;
  method: string;
  path: string;
  requestExampleId?: string;
  exampleName?: string;
}

export interface MockLog {
  id: string;
  mockServerId: string;
  mockRouteId?: string;
  method: string;
  path: string;
  statusCode: number;
  createdOn: string;
}

export interface Monitor {
  id: string;
  workspaceId: string;
  collectionId: string;
  environmentId?: string;
  collectionName: string;
  environmentName?: string;
  name: string;
  scheduleExpression: string;
  isEnabled: boolean;
  lastStatus?: string;
  lastPassedCount?: number;
  lastFailedCount?: number;
  lastLatencyMs?: number;
  lastRunOn?: string;
  createdOn: string;
}

export interface MonitorRun {
  id: string;
  monitorId: string;
  status: string;
  passedCount: number;
  failedCount: number;
  latencyMs?: number;
  createdOn: string;
}

export interface PublishedDoc {
  id: string;
  workspaceId: string;
  collectionId: string;
  collectionName: string;
  slug: string;
  isPublic: boolean;
  brandJson?: string;
  publishedOn?: string;
  createdOn: string;
}

export interface ApiSpec {
  id: string;
  workspaceId: string;
  collectionId?: string;
  name: string;
  format: string;
  validationStatus: string;
  createdOn: string;
}

export interface GovernanceFinding {
  rule: string;
  severity: string;
  message: string;
  location?: string;
}

export interface ApiSpecValidation {
  spec: ApiSpec;
  findings: GovernanceFinding[];
}

export interface AiAssistantConfig {
  id: string;
  organizationId: string;
  provider: string;
  modelName?: string;
  endpointUrl?: string;
  deploymentName?: string;
  isEnabled: boolean;
  createdOn: string;
  modifiedOn?: string;
}

export interface AiAssistantAction {
  action: string;
  providerStatus: string;
  suggestions: string[];
  createdOnUtc: string;
}

export interface AnalyticsPoint {
  label: string;
  value: number;
}

export interface AnalyticsRank {
  label: string;
  value: number;
  metric?: number;
}

export interface AdvancedAnalytics {
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  successRate: number;
  averageLatencyMs: number;
  requestsPerDay: AnalyticsPoint[];
  failuresPerDay: AnalyticsPoint[];
  topUsers: AnalyticsRank[];
  topEndpoints: AnalyticsRank[];
  slowEndpoints: AnalyticsRank[];
}

export interface BillingPlan {
  id: string;
  code: string;
  name: string;
  monthlyPrice: number;
  includedRequests: number;
  includedMembers?: number;
  featuresJson: string;
}

export interface OrganizationSubscription {
  id: string;
  organizationId: string;
  billingPlanId: string;
  planName: string;
  status: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
}

export interface BillingOverview {
  plans: BillingPlan[];
  subscription?: OrganizationSubscription;
  requestsThisPeriod: number;
  members: number;
  workspaces: number;
}

export interface OrganizationSaasSettings {
  organizationId: string;
  productName: string;
  retentionDays: number;
}

export interface ApiKeyModel {
  id: string;
  organizationId: string;
  workspaceId?: string;
  name: string;
  expiresOn?: string;
  lastUsedOn?: string;
  createdOn: string;
}

export interface CreatedApiKey {
  apiKey: ApiKeyModel;
  plainTextKey: string;
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
  statusText: string;
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

export interface RequestRun {
  id: string;
  requestId: string;
  requestName: string;
  method: string;
  url: string;
  actorName: string;
  status: string;
  userId: string;
  statusCode?: number;
  succeeded?: boolean;
  elapsedMs?: number;
  sizeBytes?: number;
  errorMessage?: string;
  bodyPreview?: string;
  startedOn: string;
  completedOn?: string;
  createdOn: string;
}

export interface CollectionRunItem {
  requestId: string;
  name: string;
  method: string;
  url: string;
  succeeded: boolean;
  statusCode?: number;
  elapsedMs?: number;
  errorMessage?: string;
}

export interface CollectionRunResult {
  collectionId: string;
  totalRequests: number;
  passed: number;
  failed: number;
  results: CollectionRunItem[];
}

export interface OrganizationMember {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  avatarUrl?: string;
  status: string;
  roleName: string;
  lastActiveOn?: string;
  createdOn: string;
}

export interface OrganizationRole {
  id: string;
  name: string;
  scope: string;
}

export interface Invitation {
  id: string;
  email: string;
  status: string;
  expiresOn: string;
}
