import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import {
  ActivityEvent,
  AdvancedAnalytics,
  AiAssistantAction,
  AiAssistantConfig,
  AuditLog,
  ApiRequestDetail,
  ApiRequestSummary,
  ApiResponse,
  ApiResult,
  ApiSpec,
  ApiSpecValidation,
  ApiKeyModel,
  AuthResponse,
  BillingOverview,
  Collection,
  CollectionExport,
  CollectionImportResult,
  CollectionRunResult,
  CommentModel,
  CreatedApiKey,
  EnvironmentModel,
  GovernanceFinding,
  ImportCollectionPayload,
  ManagerSummary,
  MockLog,
  MockRoute,
  MockServer,
  Monitor,
  MonitorRun,
  Organization,
  OrganizationMember,
  OrganizationSaasSettings,
  PublishedDoc,
  PagedResult,
  RequestRun,
  RequestExample,
  SaveApiRequestPayload,
  Workspace,
  WorkspaceDashboard
} from './api.models';

@Injectable({ providedIn: 'root' })
export class ApiClientService {
  private readonly apiBaseUrl = 'http://localhost:5108/api';
  private readonly sessionKey = 'apiforge.session';
  private readonly tokenKey = 'apiforge.accessToken';
  private readonly refreshTokenKey = 'apiforge.refreshToken';
  private readonly authState = signal<AuthResponse | null>(null);

  readonly auth = this.authState.asReadonly();
  readonly isAuthenticated = computed(() => !!this.accessToken);

  constructor(private readonly http: HttpClient) {
    const session = localStorage.getItem(this.sessionKey);
    if (session) {
      try {
        this.authState.set(JSON.parse(session) as AuthResponse);
      } catch {
        this.logout();
      }
    }
  }

  get accessToken(): string | null {
    return this.authState()?.accessToken ?? localStorage.getItem(this.tokenKey);
  }

  get collaborationHubUrl(): string {
    return this.apiBaseUrl.replace('/api', '/hubs/collaboration');
  }

  login(email: string, password: string) {
    return this.http.post<ApiResult<AuthResponse>>(`${this.apiBaseUrl}/auth/login`, { email, password });
  }

  register(payload: { email: string; password: string; fullName: string; organizationName: string; workspaceName?: string }) {
    return this.http.post<ApiResult<AuthResponse>>(`${this.apiBaseUrl}/auth/register`, payload);
  }

  setSession(auth: AuthResponse): void {
    this.authState.set(auth);
    localStorage.setItem(this.sessionKey, JSON.stringify(auth));
    localStorage.setItem(this.tokenKey, auth.accessToken);
    localStorage.setItem(this.refreshTokenKey, auth.refreshToken);
  }

  logout(): void {
    this.authState.set(null);
    localStorage.removeItem(this.sessionKey);
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshTokenKey);
  }

  organizations() {
    return this.http.get<ApiResult<Organization[]>>(`${this.apiBaseUrl}/organizations`);
  }

  workspaces(organizationId: string) {
    const params = new HttpParams().set('organizationId', organizationId).set('count', 100);
    return this.http.get<ApiResult<PagedResult<Workspace>>>(`${this.apiBaseUrl}/workspaces`, { params });
  }

  workspaceDashboard(workspaceId: string) {
    return this.http.get<ApiResult<WorkspaceDashboard>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/dashboard`);
  }

  collections(workspaceId: string) {
    return this.http.get<ApiResult<PagedResult<Collection>>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/collections`, {
      params: new HttpParams().set('count', 100)
    });
  }

  collectionRequests(collectionId: string) {
    return this.http.get<ApiResult<ApiRequestSummary[]>>(`${this.apiBaseUrl}/collections/${collectionId}/requests`);
  }

  requestDetail(requestId: string) {
    return this.http.get<ApiResult<ApiRequestDetail>>(`${this.apiBaseUrl}/requests/${requestId}`);
  }

  createRequest(collectionId: string, payload: SaveApiRequestPayload) {
    return this.http.post<ApiResult<ApiRequestDetail>>(`${this.apiBaseUrl}/collections/${collectionId}/requests`, payload);
  }

  updateRequest(requestId: string, payload: SaveApiRequestPayload) {
    return this.http.put<ApiResult<ApiRequestDetail>>(`${this.apiBaseUrl}/requests/${requestId}`, payload);
  }

  requestHistory(requestId: string, count = 25) {
    return this.http.get<ApiResult<RequestRun[]>>(`${this.apiBaseUrl}/requests/${requestId}/history`, {
      params: new HttpParams().set('count', count)
    });
  }

  exportCollection(collectionId: string) {
    return this.http.get<ApiResult<CollectionExport>>(`${this.apiBaseUrl}/collections/${collectionId}/export`);
  }

  importCollection(workspaceId: string, payload: ImportCollectionPayload) {
    return this.http.post<ApiResult<CollectionImportResult>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/collections/import`, payload);
  }

  runCollection(collectionId: string, environmentId?: string) {
    return this.http.post<ApiResult<CollectionRunResult>>(`${this.apiBaseUrl}/collections/${collectionId}/run`, {
      environmentId: environmentId || null,
      requestIds: null,
      delayMs: 0
    });
  }

  environments(workspaceId: string) {
    return this.http.get<ApiResult<PagedResult<EnvironmentModel>>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/environments`, {
      params: new HttpParams().set('count', 100)
    });
  }

  activity(organizationId: string, workspaceId?: string) {
    let params = new HttpParams().set('organizationId', organizationId).set('count', 50);
    if (workspaceId) {
      params = params.set('workspaceId', workspaceId);
    }
    return this.http.get<ApiResult<PagedResult<ActivityEvent>>>(`${this.apiBaseUrl}/activity`, { params });
  }

  activityFiltered(filter: {
    organizationId: string;
    workspaceId?: string;
    userId?: string;
    eventType?: string;
    status?: string;
    fromUtc?: string;
    toUtc?: string;
    count?: number;
  }) {
    let params = new HttpParams().set('organizationId', filter.organizationId).set('count', filter.count ?? 100);
    if (filter.workspaceId) params = params.set('workspaceId', filter.workspaceId);
    if (filter.userId) params = params.set('userId', filter.userId);
    if (filter.eventType) params = params.set('eventType', filter.eventType);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.fromUtc) params = params.set('fromUtc', filter.fromUtc);
    if (filter.toUtc) params = params.set('toUtc', filter.toUtc);
    return this.http.get<ApiResult<PagedResult<ActivityEvent>>>(`${this.apiBaseUrl}/activity`, { params });
  }

  auditLogs(organizationId: string, workspaceId?: string, userId?: string) {
    let params = new HttpParams().set('organizationId', organizationId).set('count', 100);
    if (workspaceId) params = params.set('workspaceId', workspaceId);
    if (userId) params = params.set('userId', userId);
    return this.http.get<ApiResult<PagedResult<AuditLog>>>(`${this.apiBaseUrl}/activity/audit`, { params });
  }

  exportActivityCsv(organizationId: string, workspaceId?: string, userId?: string) {
    let params = new HttpParams().set('organizationId', organizationId).set('count', 500);
    if (workspaceId) params = params.set('workspaceId', workspaceId);
    if (userId) params = params.set('userId', userId);
    return this.http.get(`${this.apiBaseUrl}/activity/export.csv`, { params, responseType: 'text' });
  }

  exportAuditCsv(organizationId: string, workspaceId?: string, userId?: string) {
    let params = new HttpParams().set('organizationId', organizationId).set('count', 500);
    if (workspaceId) params = params.set('workspaceId', workspaceId);
    if (userId) params = params.set('userId', userId);
    return this.http.get(`${this.apiBaseUrl}/activity/audit/export.csv`, { params, responseType: 'text' });
  }

  managerSummary(workspaceId: string) {
    return this.http.get<ApiResult<ManagerSummary>>(`${this.apiBaseUrl}/activity/manager-summary`, {
      params: new HttpParams().set('workspaceId', workspaceId)
    });
  }

  members(organizationId: string) {
    return this.http.get<ApiResult<PagedResult<OrganizationMember>>>(`${this.apiBaseUrl}/organizations/${organizationId}/members`, {
      params: new HttpParams().set('count', 100)
    });
  }

  sendRequest(requestId: string, environmentId?: string) {
    return this.http.post<ApiResult<ApiResponse>>(`${this.apiBaseUrl}/requests/${requestId}/send`, {
      environmentId: environmentId || null,
      saveHistory: true
    });
  }

  comments(workspaceId: string, entityType: string, entityId: string) {
    return this.http.get<ApiResult<CommentModel[]>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/comments`, {
      params: new HttpParams().set('entityType', entityType).set('entityId', entityId)
    });
  }

  createComment(workspaceId: string, entityType: string, entityId: string, body: string) {
    return this.http.post<ApiResult<CommentModel>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/comments`, { entityType, entityId, body });
  }

  saveResponseExample(requestId: string, payload: { name: string; statusCode?: number; headersJson?: string; body?: string; contentType?: string }) {
    return this.http.post<ApiResult<RequestExample>>(`${this.apiBaseUrl}/requests/${requestId}/examples`, payload);
  }

  mockServers(workspaceId: string) {
    return this.http.get<ApiResult<MockServer[]>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/mock-servers`);
  }

  createMockServer(workspaceId: string, payload: { collectionId: string; name: string; isPublic: boolean; apiKeyRequired: boolean; delayMs: number }) {
    return this.http.post<ApiResult<MockServer>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/mock-servers`, payload);
  }

  mockRoutes(mockServerId: string) {
    return this.http.get<ApiResult<MockRoute[]>>(`${this.apiBaseUrl}/mock-servers/${mockServerId}/routes`);
  }

  mockLogs(mockServerId: string) {
    return this.http.get<ApiResult<MockLog[]>>(`${this.apiBaseUrl}/mock-servers/${mockServerId}/logs`, {
      params: new HttpParams().set('count', 50)
    });
  }

  monitors(workspaceId: string) {
    return this.http.get<ApiResult<Monitor[]>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/monitors`);
  }

  createMonitor(workspaceId: string, payload: { collectionId: string; environmentId?: string; name: string; scheduleExpression: string; isEnabled: boolean }) {
    return this.http.post<ApiResult<Monitor>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/monitors`, payload);
  }

  runMonitor(monitorId: string) {
    return this.http.post<ApiResult<CollectionRunResult>>(`${this.apiBaseUrl}/monitors/${monitorId}/run`, {});
  }

  monitorRuns(monitorId: string) {
    return this.http.get<ApiResult<MonitorRun[]>>(`${this.apiBaseUrl}/monitors/${monitorId}/runs`, {
      params: new HttpParams().set('count', 25)
    });
  }

  publishedDocs(workspaceId: string) {
    return this.http.get<ApiResult<PublishedDoc[]>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/published-docs`);
  }

  publishDocs(workspaceId: string, payload: { collectionId: string; slug: string; isPublic: boolean; password?: string; brandJson?: string }) {
    return this.http.post<ApiResult<PublishedDoc>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/published-docs`, payload);
  }

  unpublishDocs(docId: string) {
    return this.http.delete<ApiResult<unknown>>(`${this.apiBaseUrl}/published-docs/${docId}`);
  }

  apiSpecs(workspaceId: string) {
    return this.http.get<ApiResult<PagedResult<ApiSpec>>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/api-specs`, {
      params: new HttpParams().set('count', 50)
    });
  }

  uploadApiSpec(workspaceId: string, payload: { collectionId?: string; name: string; format: string; content: string }) {
    return this.http.post<ApiResult<ApiSpecValidation>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/api-specs`, payload);
  }

  organizationSettings(organizationId: string) {
    return this.http.get<ApiResult<OrganizationSaasSettings>>(`${this.apiBaseUrl}/organizations/${organizationId}/saas-settings`);
  }

  saveOrganizationSettings(organizationId: string, payload: { productName: string; retentionDays: number }) {
    return this.http.put<ApiResult<OrganizationSaasSettings>>(`${this.apiBaseUrl}/organizations/${organizationId}/saas-settings`, payload);
  }

  aiConfig(organizationId: string) {
    return this.http.get<ApiResult<AiAssistantConfig>>(`${this.apiBaseUrl}/organizations/${organizationId}/ai-config`);
  }

  saveAiConfig(organizationId: string, payload: { provider: string; modelName?: string; endpointUrl?: string; deploymentName?: string; isEnabled: boolean }) {
    return this.http.put<ApiResult<AiAssistantConfig>>(`${this.apiBaseUrl}/organizations/${organizationId}/ai-config`, payload);
  }

  runAiAction(workspaceId: string, payload: { action: string; collectionId?: string; requestId?: string; input?: string }) {
    return this.http.post<ApiResult<AiAssistantAction>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/ai-assistant/actions`, payload);
  }

  advancedAnalytics(workspaceId: string) {
    return this.http.get<ApiResult<AdvancedAnalytics>>(`${this.apiBaseUrl}/workspaces/${workspaceId}/analytics/advanced`);
  }

  billingOverview(organizationId: string) {
    return this.http.get<ApiResult<BillingOverview>>(`${this.apiBaseUrl}/organizations/${organizationId}/billing`);
  }

  apiKeys(organizationId: string) {
    return this.http.get<ApiResult<ApiKeyModel[]>>(`${this.apiBaseUrl}/organizations/${organizationId}/api-keys`);
  }

  createApiKey(organizationId: string, payload: { workspaceId?: string; name: string; expiresOn?: string }) {
    return this.http.post<ApiResult<CreatedApiKey>>(`${this.apiBaseUrl}/organizations/${organizationId}/api-keys`, payload);
  }
}
