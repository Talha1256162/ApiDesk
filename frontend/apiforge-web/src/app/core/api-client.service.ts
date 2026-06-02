import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import {
  ActivityEvent,
  ApiRequestDetail,
  ApiRequestSummary,
  ApiResponse,
  ApiResult,
  AuthResponse,
  Collection,
  EnvironmentModel,
  ManagerSummary,
  Organization,
  OrganizationMember,
  PagedResult,
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
}
