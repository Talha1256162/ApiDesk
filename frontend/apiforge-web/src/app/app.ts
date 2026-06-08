import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ApiClientService } from './core/api-client.service';
import {
  ActivityEvent,
  AdvancedAnalytics,
  AiAssistantAction,
  AiAssistantConfig,
  AiProviderStatus,
  AuditLog,
  ApiRequestDetail,
  ApiRequestSummary,
  ApiResponse,
  ApiSpec,
  ApiSpecValidation,
  ApiKeyModel,
  BillingOverview,
  BillingPlan,
  BuildInfo,
  CommentModel,
  Collection,
  CollectionRunResult,
  EnvironmentModel,
  ImportApiRequestPayload,
  ImportApiRequestWithFolderPayload,
  ImportCollectionPayload,
  Invitation,
  KeyValueItem,
  ManagerSummary,
  MockLog,
  MockRoute,
  MockServer,
  Monitor,
  MonitorRun,
  Organization,
  OrganizationMember,
  OrganizationRole,
  OrganizationSaasSettings,
  PublishedDoc,
  RequestRun,
  SaveApiRequestPayload,
  Workspace,
  WorkspaceDashboard
} from './core/api.models';
import { DeveloperToolsService, JsonDiff, JsonStats } from './features/developer-tools/developer-tools.service';
import { MonacoEditorComponent } from './shared/monaco-editor.component';
import { BadgeComponent } from './shared/ui/badge.component';
import { EmptyStateComponent } from './shared/ui/empty-state.component';
import { JsonTreeComponent } from './shared/ui/json-tree.component';
import { PremiumSelectComponent, PremiumSelectOption } from './shared/ui/premium-select.component';
import { SkeletonComponent } from './shared/ui/skeleton.component';
import { StatCardComponent } from './shared/ui/stat-card.component';
import { ToastComponent } from './shared/ui/toast.component';

type ViewKey =
  | 'dashboard'
  | 'workspaces'
  | 'collections'
  | 'api-client'
  | 'environments'
  | 'mock-servers'
  | 'monitors'
  | 'documentation'
  | 'governance'
  | 'ai-assistant'
  | 'analytics'
  | 'billing'
  | 'json-tools'
  | 'dev-tools'
  | 'activity'
  | 'reports'
  | 'team'
  | 'settings';

type ToastState = {
  title: string;
  message: string;
  tone: 'default' | 'success' | 'danger';
};

type NavItem = { key: ViewKey; label: string; hint: string; icon: string };
type NavSection = { title: string; items: NavItem[] };
type RequestConfigTab = 'Params' | 'Auth' | 'Headers' | 'Body' | 'Tests' | 'Settings';
type ResponseTab = 'Body' | 'Headers' | 'Cookies' | 'Timeline' | 'History';
type ResponseViewMode = 'pretty' | 'raw' | 'tree';
type EditableKeyValue = KeyValueItem & { id: string };
type EditableKeyValueKind = 'headers' | 'query' | 'path';
type RequestTreeGroup = { key: string; label: string; requests: ApiRequestSummary[] };
type PublicView = 'landing' | 'login' | 'pricing';
type ImportTargetMode = 'workspace' | 'newWorkspace' | 'mergeCollection';

type ImportPreview = {
  fileName: string;
  collectionName: string;
  folderCount: number;
  requestCount: number;
  environmentVariableCount: number;
  authTypes: string[];
  variables: string[];
  scriptsDetected: number;
  unsupportedItems: string[];
  payload?: ImportCollectionPayload;
  environmentPayload?: {
    name: string;
    variables: { key: string; value?: string; scope: string; isSecret: boolean; enabled: boolean }[];
  };
};

type ImportSuccess = {
  kind: 'collection' | 'environment';
  title: string;
  message: string;
  collectionId?: string;
  environmentId?: string;
  requestCount?: number;
  variableCount?: number;
};

type GeneratedCollectionPreview = {
  providerStatus: string;
  collectionName: string;
  folders: string[][];
  variables: string[];
  tests: string[];
  mockExamples: string[];
  payload: ImportCollectionPayload;
};

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorComponent,
    BadgeComponent,
    EmptyStateComponent,
    JsonTreeComponent,
    PremiumSelectComponent,
    SkeletonComponent,
    StatCardComponent,
    ToastComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  readonly productName = signal('API DESK');
  readonly activeView = signal<ViewKey>('dashboard');
  readonly shellLoading = signal(false);
  readonly pageLoading = signal(false);
  readonly authLoading = signal(false);
  readonly commandOpen = signal(false);
  readonly curlImportOpen = signal(false);
  readonly postmanImportOpen = signal(false);
  readonly aiAgentOpen = signal(false);
  readonly generatedTestsOpen = signal(false);
  readonly generatedMockOpen = signal(false);
  readonly darkMode = signal(localStorage.getItem('apiforge.theme') !== 'light');
  readonly showPassword = signal(false);
  readonly authError = signal('');
  readonly toast = signal<ToastState | null>(null);

  readonly organizations = signal<Organization[]>([]);
  readonly workspaces = signal<Workspace[]>([]);
  readonly collections = signal<Collection[]>([]);
  readonly requests = signal<ApiRequestSummary[]>([]);
  readonly requestDetail = signal<ApiRequestDetail | null>(null);
  readonly environments = signal<EnvironmentModel[]>([]);
  readonly activity = signal<ActivityEvent[]>([]);
  readonly filteredActivity = signal<ActivityEvent[]>([]);
  readonly auditLogs = signal<AuditLog[]>([]);
  readonly comments = signal<CommentModel[]>([]);
  readonly mockServers = signal<MockServer[]>([]);
  readonly mockRoutes = signal<MockRoute[]>([]);
  readonly mockLogs = signal<MockLog[]>([]);
  readonly monitors = signal<Monitor[]>([]);
  readonly monitorRuns = signal<MonitorRun[]>([]);
  readonly publishedDocs = signal<PublishedDoc[]>([]);
  readonly apiSpecs = signal<ApiSpec[]>([]);
  readonly governanceFindings = signal<ApiSpecValidation | null>(null);
  readonly aiConfig = signal<AiAssistantConfig | null>(null);
  readonly aiProviderStatus = signal<AiProviderStatus | null>(null);
  readonly aiResult = signal<AiAssistantAction | null>(null);
  readonly advancedAnalytics = signal<AdvancedAnalytics | null>(null);
  readonly billingOverview = signal<BillingOverview | null>(null);
  readonly buildInfo = signal<BuildInfo | null>(null);
  readonly saasSettings = signal<OrganizationSaasSettings | null>(null);
  readonly apiKeys = signal<ApiKeyModel[]>([]);
  readonly members = signal<OrganizationMember[]>([]);
  readonly organizationRoles = signal<OrganizationRole[]>([]);
  readonly invitations = signal<Invitation[]>([]);
  readonly requestHistory = signal<RequestRun[]>([]);
  readonly dashboard = signal<WorkspaceDashboard | null>(null);
  readonly managerSummary = signal<ManagerSummary | null>(null);
  readonly apiResponse = signal<ApiResponse | null>(null);
  readonly collectionRun = signal<CollectionRunResult | null>(null);
  readonly responseBody = signal('');
  readonly responseViewMode = signal<ResponseViewMode>('pretty');
  readonly realtimeStatus = signal('offline');
  readonly selectedOrganizationId = signal('');
  readonly selectedWorkspaceId = signal('');
  readonly selectedCollectionId = signal('');
  readonly selectedRequestId = signal('');
  readonly selectedEnvironmentId = signal('');
  readonly openRequestIds = signal<string[]>([]);
  readonly draggedRequestId = signal('');
  readonly collectionSearch = signal('');
  readonly requestSearch = signal('');

  readonly isSignedIn = computed(() => this.api.isAuthenticated());
  readonly selectedWorkspace = computed(() => this.workspaces().find((workspace) => workspace.id === this.selectedWorkspaceId()));
  readonly selectedCollection = computed(() => this.collections().find((collection) => collection.id === this.selectedCollectionId()));
  readonly selectedRequestSummary = computed(() => this.requests().find((request) => request.id === this.selectedRequestId()));
  readonly selectedRequest = computed(() => this.requestDetail() ?? this.selectedRequestSummary());
  readonly selectedRequestNeedsEnvironment = computed(() => (this.selectedRequest()?.url ?? '').includes('{{'));
  readonly filteredCollections = computed(() => {
    const search = this.collectionSearch().trim().toLowerCase();
    if (!search) {
      return this.collections();
    }
    return this.collections().filter((collection) =>
      [collection.name, collection.description, collection.ownerName].some((value) => (value ?? '').toLowerCase().includes(search))
      || (collection.id === this.selectedCollectionId()
        && this.requests().some((request) =>
          [request.name, request.method, request.url, request.folderName].some((value) => (value ?? '').toLowerCase().includes(search))
        ))
    );
  });
  readonly filteredRequests = computed(() => {
    const search = this.requestSearch().trim().toLowerCase();
    if (!search) {
      return this.requests();
    }
    return this.requests().filter((request) =>
      [request.name, request.method, request.url, request.folderName].some((value) => (value ?? '').toLowerCase().includes(search))
    );
  });
  readonly requestTreeGroups = computed<RequestTreeGroup[]>(() => {
    const groups = new Map<string, ApiRequestSummary[]>();
    for (const request of this.filteredRequests()) {
      const key = request.folderId || 'root';
      groups.set(key, [...(groups.get(key) ?? []), request]);
    }
    return [...groups.entries()].map(([key, requests]) => ({
      key,
      label: key === 'root' ? 'Root requests' : requests[0]?.folderName || `Folder ${key.slice(0, 8)}`,
      requests
    }));
  });
  readonly responseViewerBody = computed(() => {
    const body = this.responseBody() || this.apiResponse()?.body || '';
    if (!body) {
      return '';
    }
    if (this.responseViewMode() === 'raw') {
      return body;
    }
    if (this.responseViewMode() === 'tree') {
      try {
        return this.developerTools.tree(body);
      } catch {
        return body;
      }
    }
    if (this.looksLikeJson(body)) {
      try {
        return this.developerTools.beautify(body);
      } catch {
        return body;
      }
    }
    return body;
  });
  readonly responseViewerLanguage = computed(() => (this.responseViewMode() === 'tree' || !this.looksLikeJson(this.responseViewerBody()) ? 'plaintext' : 'json'));
  readonly responseTreeValue = computed(() => {
    const body = this.responseBody() || this.apiResponse()?.body || '';
    if (!body || !this.looksLikeJson(body)) {
      return null;
    }
    try {
      return JSON.parse(body) as unknown;
    } catch {
      return null;
    }
  });
  readonly openRequestTabs = computed(() => {
    const requests = this.requests();
    const selectedId = this.selectedRequestId();
    const ids = this.openRequestIds().filter((id) => requests.some((request) => request.id === id));
    const normalized = selectedId && !ids.includes(selectedId) ? [selectedId, ...ids] : ids;
    return normalized
      .map((id) => requests.find((request) => request.id === id))
      .filter((request): request is ApiRequestSummary => !!request);
  });
  readonly canSendRequest = computed(
    () => !!this.selectedRequestId() && !this.pageLoading() && (!this.selectedRequestNeedsEnvironment() || !!this.selectedEnvironmentId())
  );
  readonly requestActivity = computed(() => {
    const selected = this.selectedRequest();
    if (!selected) {
      return this.activity().slice(0, 6);
    }
    return this.activity()
      .filter((event) => event.entityName === selected.name || event.summary?.includes(selected.url))
      .slice(0, 6);
  });
  readonly apiHealthScore = computed(() => {
    const summary = this.managerSummary();
    if (!summary || summary.requestsSentToday === 0) {
      return 100;
    }
    return Math.max(0, Math.round(((summary.requestsSentToday - summary.failedApisToday) / summary.requestsSentToday) * 100));
  });
  readonly maxRequestsPerDay = computed(() => Math.max(1, ...((this.managerSummary()?.requestsPerDay ?? []).map((point) => point.value))));
  readonly maxFailedRequestsPerDay = computed(() => Math.max(1, ...((this.managerSummary()?.failedRequestsPerDay ?? []).map((point) => point.value))));
  readonly currentUserName = computed(() => this.api.auth()?.user.fullName || 'API DESK user');
  readonly currentUserInitials = computed(() =>
    this.currentUserName()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || 'AD'
  );
  readonly activeViewTitle = computed(() => this.navItems.find((item) => item.key === this.activeView())?.label ?? 'Dashboard');
  readonly organizationOptions = computed<PremiumSelectOption[]>(() => this.organizations().map((organization) => ({ value: organization.id, label: organization.name, meta: organization.slug })));
  readonly workspaceOptions = computed<PremiumSelectOption[]>(() => this.workspaces().map((workspace) => ({ value: workspace.id, label: workspace.name, meta: workspace.type })));
  readonly environmentOptions = computed<PremiumSelectOption[]>(() => this.environments().map((environment) => ({ value: environment.id, label: environment.name, meta: environment.isDefault ? 'Default' : `${environment.variableCount} variables` })));
  readonly collectionOptions = computed<PremiumSelectOption[]>(() => this.collections().map((collection) => ({ value: collection.id, label: collection.name, meta: `${collection.requestCount} requests` })));
  readonly aiProviderOptions = computed<PremiumSelectOption[]>(() => ['OpenAI', 'Azure OpenAI', 'Local LLM'].map((provider) => ({ value: provider, label: provider, meta: 'Configurable provider' })));
  readonly aiActionOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'GenerateTests', label: 'Generate tests', meta: 'Assertions' },
    { value: 'ExplainResponse', label: 'Explain response', meta: 'Readable summary' },
    { value: 'GenerateDocs', label: 'Generate docs', meta: 'Documentation' },
    { value: 'SuggestMocks', label: 'Suggest mocks', meta: 'Examples' },
    { value: 'FindQualityGaps', label: 'Find quality gaps', meta: 'Governance' }
  ]);
  readonly methodOptions = computed<PremiumSelectOption[]>(() => ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'].map((method) => ({ value: method, label: method, meta: method === 'GET' ? 'Read' : method === 'DELETE' ? 'Delete' : 'Write' })));
  readonly authTypeOptions = computed<PremiumSelectOption[]>(() => [
    { value: '', label: 'No Auth', meta: 'Public request' },
    { value: 'Bearer', label: 'Bearer token', meta: 'Authorization header' },
    { value: 'Basic', label: 'Basic auth', meta: 'Username/password' },
    { value: 'ApiKey', label: 'API key', meta: 'Header or query param' },
    { value: 'OAuth2', label: 'OAuth 2.0', meta: 'Bearer token workflow' }
  ]);
  readonly bodyTypeOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'none', label: 'none', meta: 'No request body' },
    { value: 'rawJson', label: 'raw JSON', meta: 'Application JSON' },
    { value: 'text', label: 'raw text', meta: 'Plain text' },
    { value: 'formData', label: 'form-data', meta: 'Multipart key/value' },
    { value: 'formUrlEncoded', label: 'x-www-form-urlencoded', meta: 'Encoded key/value' }
  ]);
  readonly specFormatOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'json', label: 'JSON', meta: 'OpenAPI JSON' },
    { value: 'yaml', label: 'YAML', meta: 'OpenAPI YAML' }
  ]);
  readonly activityMemberOptions = computed<PremiumSelectOption[]>(() => [
    { value: '', label: 'All members', meta: 'Organization team' },
    ...this.members().map((member) => ({ value: member.userId, label: member.fullName, meta: member.roleName }))
  ]);
  readonly roleOptions = computed<PremiumSelectOption[]>(() => this.organizationRoles().map((role) => ({ value: role.id, label: role.name, meta: role.scope })));
  readonly activityStatusOptions = computed<PremiumSelectOption[]>(() => [
    { value: '', label: 'Any status', meta: 'Success and failure' },
    { value: 'Success', label: 'Success', meta: 'Completed events' },
    { value: 'Failure', label: 'Failure', meta: 'Failed events' }
  ]);
  readonly utilityToolOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'Base64', label: 'Base64 encode', meta: 'Encode text' },
    { value: 'Base64 Decode', label: 'Base64 decode', meta: 'Decode text' },
    { value: 'URL Encode', label: 'URL encode', meta: 'Escape URL text' },
    { value: 'URL Decode', label: 'URL decode', meta: 'Unescape URL text' },
    { value: 'JWT Decoder', label: 'JWT decoder', meta: 'Header and payload' },
    { value: 'UUID Generator', label: 'UUID generator', meta: 'Random v4 UUID' },
    { value: 'Timestamp Converter', label: 'Timestamp converter', meta: 'Unix and ISO time' },
    { value: 'cURL Parser', label: 'cURL parser', meta: 'Request JSON' },
    { value: 'Hash Generator', label: 'Hash generator', meta: 'Web crypto digest' },
    { value: 'Regex Tester', label: 'Regex tester', meta: 'Match preview' }
  ]);
  readonly hashAlgorithmOptions = computed<PremiumSelectOption[]>(() =>
    ['SHA-1', 'SHA-256', 'SHA-384', 'SHA-512'].map((algorithm) => ({ value: algorithm, label: algorithm, meta: 'Hash algorithm' }))
  );
  readonly jsonTabs = ['Beautify', 'Validate', 'Tree View', 'Minify', 'Compare', 'Convert', 'Schema'] as const;
  readonly requestConfigTabs: RequestConfigTab[] = ['Params', 'Auth', 'Headers', 'Body', 'Tests', 'Settings'];
  readonly responseTabs: ResponseTab[] = ['Body', 'Headers', 'Cookies', 'Timeline', 'History'];
  readonly responseViewModes: ResponseViewMode[] = ['pretty', 'raw', 'tree'];
  readonly navSections: NavSection[] = [
    {
      title: 'Main',
      items: [
        { key: 'dashboard', label: 'Dashboard', hint: 'Overview', icon: 'grid' },
        { key: 'workspaces', label: 'Workspaces', hint: 'Teams', icon: 'building' }
      ]
    },
    {
      title: 'API',
      items: [
        { key: 'collections', label: 'Collections', hint: 'Library', icon: 'stack' },
        { key: 'api-client', label: 'API Client', hint: 'Runner', icon: 'bolt' },
        { key: 'environments', label: 'Environments', hint: 'Variables', icon: 'sliders' },
        { key: 'mock-servers', label: 'Mock Servers', hint: 'Examples', icon: 'bolt' },
        { key: 'monitors', label: 'Monitors', hint: 'Schedules', icon: 'pulse' },
        { key: 'documentation', label: 'Documentation', hint: 'Publish', icon: 'stack' },
        { key: 'governance', label: 'Governance', hint: 'Review', icon: 'shield' }
      ]
    },
    {
      title: 'Enterprise',
      items: [
        { key: 'ai-assistant', label: 'AI Assistant', hint: 'Provider', icon: 'terminal' },
        { key: 'analytics', label: 'Analytics', hint: 'Advanced', icon: 'chart' },
        { key: 'billing', label: 'Billing', hint: 'SaaS', icon: 'shield' }
      ]
    },
    {
      title: 'Tools',
      items: [
        { key: 'json-tools', label: 'JSON Tools', hint: 'Utilities', icon: 'braces' },
        { key: 'dev-tools', label: 'Developer Tools', hint: 'Toolkit', icon: 'terminal' }
      ]
    },
    {
      title: 'Team',
      items: [
        { key: 'activity', label: 'Activity', hint: 'Audit', icon: 'pulse' },
        { key: 'reports', label: 'Reports', hint: 'Insights', icon: 'chart' },
        { key: 'team', label: 'Team', hint: 'RBAC', icon: 'users' }
      ]
    },
    {
      title: 'Admin',
      items: [
        { key: 'settings', label: 'Settings', hint: 'Config', icon: 'shield' }
      ]
    }
  ];
  readonly navItems = this.navSections.flatMap((section) => section.items);

  loginEmail = '';
  loginPassword = '';
  registerMode = false;
  publicView: PublicView = 'landing';
  registerFullName = '';
  registerOrganization = '';
  registerWorkspace = '';
  globalSearch = '';
  contextPanelOpen = false;

  jsonTab: (typeof this.jsonTabs)[number] = 'Beautify';
  jsonInput = '';
  jsonCompareInput = '';
  jsonOutput = '';
  jsonError = '';
  jsonStats?: JsonStats;
  jsonDiffs: JsonDiff[] = [];
  jsonPath = '$';
  requestConfigTab: RequestConfigTab = 'Params';
  responseTab: ResponseTab = 'Body';
  responseSearch = '';
  requestName = '';
  requestDescription = '';
  requestMethod = 'GET';
  requestUrl = '';
  requestAuthType = '';
  requestAuthConfigJson = '';
  authBearerToken = '';
  authBasicUsername = '';
  authBasicPassword = '';
  authApiKeyName = 'X-API-Key';
  authApiKeyValue = '';
  authApiKeyLocation: 'header' | 'query' = 'header';
  authOauthToken = '';
  requestBodyType = 'none';
  requestBodyContent = '';
  requestPreScript = '';
  requestTestScript = '';
  requestTimeoutMs = 30000;
  requestFollowRedirects = true;
  requestSslVerification = true;
  requestHeadersText = '';
  requestQueryText = '';
  requestPathText = '';
  requestHeadersRows: EditableKeyValue[] = [];
  requestQueryRows: EditableKeyValue[] = [];
  requestPathRows: EditableKeyValue[] = [];
  curlCommand = '';
  importPreview?: ImportPreview;
  importSuccess?: ImportSuccess;
  importError = '';
  importTargetMode: ImportTargetMode = 'workspace';
  importWorkspaceName = '';
  importDragActive = false;
  activityUserFilter = '';
  activityEventFilter = '';
  activityStatusFilter = '';
  inviteEmail = '';
  inviteRoleId = '';
  inviteMessage = '';
  selectedMemberRoles: Record<string, string> = {};
  acceptInviteToken = '';
  lastInviteLink = '';
  commentText = '';
  mockName = '';
  mockDelayMs = 0;
  mockIsPublic = false;
  mockApiKeyRequired = false;
  selectedMockServerId = '';
  monitorName = '';
  monitorSchedule = 'every 15 minutes';
  selectedMonitorId = '';
  docsSlug = '';
  docsIsPublic = true;
  docsBrandJson = '';
  specName = '';
  specFormat = 'json';
  specContent = '';
  aiProvider = 'OpenAI';
  aiModelName = '';
  aiEndpointUrl = '';
  aiDeploymentName = '';
  aiEnabled = false;
  aiAction = 'GenerateTests';
  aiInput = '';
  aiFlowInput = '';
  aiCollectionPreview?: GeneratedCollectionPreview;
  aiCreating = false;
  generatedTests = '';
  generatedMockBody = '';
  generatedMockRandomized = false;
  settingsProductName = 'API DESK';
  settingsRetentionDays = 365;
  apiKeyName = '';
  createdPlainApiKey = '';
  utilityTool = 'Base64';
  utilityPattern = '';
  utilityFlags = 'g';
  hashAlgorithm: 'SHA-1' | 'SHA-256' | 'SHA-384' | 'SHA-512' = 'SHA-256';
  utilityInput = '';
  utilityOutput = '';
  private hub?: HubConnection;

  constructor(
    readonly api: ApiClientService,
    private readonly developerTools: DeveloperToolsService
  ) {}

  ngOnInit(): void {
    document.documentElement.classList.toggle('light', !this.darkMode());
    const auth = this.api.auth();
    if (auth) {
      this.selectedOrganizationId.set(auth.organizationId);
      this.selectedWorkspaceId.set(auth.workspaceId ?? '');
      this.loadOrganizations();
      this.connectRealtime();
    }
  }

  showLanding(): void {
    this.publicView = 'landing';
  }

  showPricing(): void {
    this.publicView = 'pricing';
  }

  showLogin(register = false): void {
    this.publicView = 'login';
    this.registerMode = register;
    this.authError.set('');
  }

  startFree(): void {
    this.showLogin(true);
  }

  viewDemo(): void {
    this.showLogin(false);
    this.loginEmail = 'admin@apiforge.local';
    this.loginPassword = 'Admin@12345';
  }

  confirmAction(message: string): boolean {
    return window.confirm(message);
  }

  submitAuth(): void {
    this.authError.set('');
    if (!this.isAuthFormValid()) {
      this.authError.set('Enter a valid email and password.');
      return;
    }

    this.authLoading.set(true);
    const request = this.registerMode
      ? this.api.register({
          email: this.loginEmail.trim(),
          password: this.loginPassword,
          fullName: this.registerFullName.trim(),
          organizationName: this.registerOrganization.trim(),
          workspaceName: this.registerWorkspace.trim()
        })
      : this.api.login(this.loginEmail.trim(), this.loginPassword);

    request.subscribe({
      next: (result) => {
        this.authLoading.set(false);
        if (!result.succeeded) {
          this.authError.set(result.message);
          return;
        }

        this.api.setSession(result.data);
        this.selectedOrganizationId.set(result.data.organizationId);
        this.selectedWorkspaceId.set(result.data.workspaceId ?? '');
        this.showToast('Signed in', `Welcome back, ${result.data.user.fullName}.`, 'success');
        this.loadOrganizations();
        this.connectRealtime();
      },
      error: (error) => {
        this.authLoading.set(false);
        this.authError.set(error?.error?.message ?? 'Authentication failed.');
      }
    });
  }

  logout(): void {
    this.api.logout();
    this.organizations.set([]);
    this.workspaces.set([]);
    this.collections.set([]);
    this.requests.set([]);
    this.requestDetail.set(null);
    this.environments.set([]);
    this.activity.set([]);
    this.filteredActivity.set([]);
    this.auditLogs.set([]);
    this.comments.set([]);
    this.mockServers.set([]);
    this.mockRoutes.set([]);
    this.mockLogs.set([]);
    this.monitors.set([]);
    this.monitorRuns.set([]);
    this.publishedDocs.set([]);
    this.apiSpecs.set([]);
    this.governanceFindings.set(null);
    this.aiConfig.set(null);
    this.aiResult.set(null);
    this.advancedAnalytics.set(null);
    this.billingOverview.set(null);
    this.saasSettings.set(null);
    this.apiKeys.set([]);
    this.members.set([]);
    this.organizationRoles.set([]);
    this.invitations.set([]);
    this.requestHistory.set([]);
    this.dashboard.set(null);
    this.managerSummary.set(null);
    this.apiResponse.set(null);
    this.collectionRun.set(null);
    this.responseBody.set('');
    this.openRequestIds.set([]);
    this.resetRequestEditor();
    this.activeView.set('dashboard');
    this.publicView = 'landing';
    void this.hub?.stop();
    this.hub = undefined;
    this.realtimeStatus.set('offline');
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((value) => !value);
  }

  toggleRegisterMode(): void {
    this.registerMode = !this.registerMode;
    this.authError.set('');
  }

  selectView(view: ViewKey): void {
    this.activeView.set(view);
    this.commandOpen.set(false);
  }

  onOrganizationChange(organizationId: string): void {
    this.selectedOrganizationId.set(organizationId);
    this.selectedWorkspaceId.set('');
    this.selectedCollectionId.set('');
    this.selectedRequestId.set('');
    this.requestSearch.set('');
    this.requestDetail.set(null);
    this.loadWorkspaces();
  }

  onWorkspaceChange(workspaceId: string): void {
    this.selectedWorkspaceId.set(workspaceId);
    this.selectedCollectionId.set('');
    this.selectedRequestId.set('');
    this.requestSearch.set('');
    this.requestDetail.set(null);
    this.comments.set([]);
    this.loadWorkspaceData();
  }

  onCollectionChange(collectionId: string): void {
    this.selectedCollectionId.set(collectionId);
    this.selectedRequestId.set('');
    this.requestDetail.set(null);
    this.requestHistory.set([]);
    this.collectionRun.set(null);
    this.requestSearch.set('');
    this.resetRequestEditor();
    this.loadRequests();
  }

  loadOrganizations(): void {
    this.shellLoading.set(true);
    this.api.organizations().subscribe({
      next: (result) => {
        this.organizations.set(result.data ?? []);
        if (!this.selectedOrganizationId()) {
          this.selectedOrganizationId.set(this.organizations()[0]?.id ?? '');
        }
        this.shellLoading.set(false);
        this.loadWorkspaces();
      },
      error: () => {
        this.shellLoading.set(false);
        this.showToast('Could not load organizations', 'Check the API connection and try again.', 'danger');
      }
    });
  }

  loadWorkspaces(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.pageLoading.set(true);
    this.api.workspaces(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        this.workspaces.set(result.data?.items ?? []);
        if (!this.selectedWorkspaceId()) {
          this.selectedWorkspaceId.set(this.workspaces()[0]?.id ?? '');
        }
        this.pageLoading.set(false);
        this.loadWorkspaceData();
      },
      error: () => {
        this.pageLoading.set(false);
        this.showToast('Could not load workspaces', 'Workspace data is unavailable.', 'danger');
      }
    });
  }

  loadWorkspaceData(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.loadDashboard();
    this.loadCollections();
    this.loadEnvironments();
    this.loadActivity();
    this.loadManagerActivity();
    this.loadAuditLogs();
    this.loadMembers();
    this.loadOrganizationRoles();
    this.loadPhase3Data();
    this.joinRealtimeWorkspace();
  }

  loadPhase3Data(): void {
    this.loadMockServers();
    this.loadMonitors();
    this.loadPublishedDocs();
    this.loadApiSpecs();
    this.loadPhase4Data();
  }

  loadPhase4Data(): void {
    this.loadBuildInfo();
    this.loadOrganizationSettings();
    this.loadAiConfig();
    this.loadAdvancedAnalytics();
    this.loadBillingOverview();
    this.loadApiKeys();
  }

  loadBuildInfo(): void {
    this.api.buildInfo().subscribe({
      next: (result) => this.buildInfo.set(result.data ?? null),
      error: () => this.showToast('Build info failed', 'Could not load deployment metadata.', 'danger')
    });
  }

  loadDashboard(): void {
    const workspaceId = this.selectedWorkspaceId();
    this.pageLoading.set(true);
    this.api.workspaceDashboard(workspaceId).subscribe({
      next: (result) => {
        this.dashboard.set(result.data);
        this.pageLoading.set(false);
      },
      error: () => {
        this.pageLoading.set(false);
        this.showToast('Dashboard failed', 'Could not load dashboard metrics.', 'danger');
      }
    });

    this.api.managerSummary(workspaceId).subscribe({
      next: (result) => this.managerSummary.set(result.data),
      error: () => this.showToast('Manager summary failed', 'Could not load manager metrics.', 'danger')
    });
  }

  loadCollections(): void {
    this.api.collections(this.selectedWorkspaceId()).subscribe({
      next: (result) => {
        this.collections.set(result.data?.items ?? []);
        if (!this.selectedCollectionId()) {
          this.selectedCollectionId.set(this.collections()[0]?.id ?? '');
        }
        if (this.selectedCollectionId()) {
          this.loadRequests();
        } else {
          this.requests.set([]);
        }
      },
      error: () => this.showToast('Collections failed', 'Could not load collections.', 'danger')
    });
  }

  loadRequests(): void {
    if (!this.selectedCollectionId()) {
      this.requests.set([]);
      return;
    }

    this.api.collectionRequests(this.selectedCollectionId()).subscribe({
      next: (result) => {
        this.requests.set(result.data ?? []);
        if (!this.selectedRequestId()) {
          this.selectedRequestId.set(this.requests()[0]?.id ?? '');
        }
        if (this.selectedRequestId()) {
          this.rememberOpenRequest(this.selectedRequestId());
        }
        this.loadRequestDetail();
      },
      error: () => this.showToast('Requests failed', 'Could not load collection requests.', 'danger')
    });
  }

  selectRequest(requestId: string): void {
    this.selectedRequestId.set(requestId);
    this.rememberOpenRequest(requestId);
    this.requestDetail.set(null);
    this.requestHistory.set([]);
    this.apiResponse.set(null);
    this.responseBody.set('');
    this.collectionRun.set(null);
    this.loadRequestDetail();
  }

  closeRequestTab(requestId: string, event: Event): void {
    event.stopPropagation();
    const remaining = this.openRequestIds().filter((id) => id !== requestId);
    this.openRequestIds.set(remaining);
    if (this.selectedRequestId() === requestId) {
      const nextId = remaining[0] ?? this.requests()[0]?.id ?? '';
      this.selectedRequestId.set(nextId);
      if (nextId) {
        this.loadRequestDetail();
      } else {
        this.resetRequestEditor();
        this.requestDetail.set(null);
      }
    }
  }

  dragRequestTab(requestId: string): void {
    this.draggedRequestId.set(requestId);
  }

  dropRequestTab(targetRequestId: string): void {
    const sourceId = this.draggedRequestId();
    if (!sourceId || sourceId === targetRequestId) {
      this.draggedRequestId.set('');
      return;
    }

    const ids = [...this.openRequestIds()];
    const from = ids.indexOf(sourceId);
    const to = ids.indexOf(targetRequestId);
    if (from > -1 && to > -1) {
      ids.splice(from, 1);
      ids.splice(to, 0, sourceId);
      this.openRequestIds.set(ids);
    }
    this.draggedRequestId.set('');
  }

  loadRequestDetail(): void {
    if (!this.selectedRequestId()) {
      this.requestDetail.set(null);
      return;
    }

    this.api.requestDetail(this.selectedRequestId()).subscribe({
      next: (result) => {
        this.requestDetail.set(result.data);
        if (result.data) {
          this.populateRequestEditor(result.data);
          this.loadRequestHistory();
          this.loadComments();
        }
      },
      error: () => this.showToast('Request detail failed', 'Could not load request configuration.', 'danger')
    });
  }

  loadRequestHistory(): void {
    if (!this.selectedRequestId()) {
      this.requestHistory.set([]);
      return;
    }

    this.api.requestHistory(this.selectedRequestId(), 25).subscribe({
      next: (result) => this.requestHistory.set(result.data ?? []),
      error: () => this.showToast('History failed', 'Could not load request history.', 'danger')
    });
  }

  loadEnvironments(): void {
    this.api.environments(this.selectedWorkspaceId()).subscribe({
      next: (result) => {
        this.environments.set(result.data?.items ?? []);
        if (!this.selectedEnvironmentId()) {
          this.selectedEnvironmentId.set(this.environments().find((environment) => environment.isDefault)?.id ?? this.environments()[0]?.id ?? '');
        }
      },
      error: () => this.showToast('Environments failed', 'Could not load environments.', 'danger')
    });
  }

  loadActivity(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.activity(this.selectedOrganizationId(), this.selectedWorkspaceId()).subscribe({
      next: (result) => this.activity.set(result.data?.items ?? []),
      error: () => this.showToast('Activity failed', 'Could not load activity feed.', 'danger')
    });
  }

  loadManagerActivity(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.activityFiltered({
      organizationId: this.selectedOrganizationId(),
      workspaceId: this.selectedWorkspaceId() || undefined,
      userId: this.activityUserFilter || undefined,
      eventType: this.activityEventFilter.trim() || undefined,
      status: this.activityStatusFilter || undefined,
      count: 100
    }).subscribe({
      next: (result) => this.filteredActivity.set(result.data?.items ?? []),
      error: () => this.showToast('Manager feed failed', 'Could not load filtered team activity.', 'danger')
    });
  }

  loadAuditLogs(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.auditLogs(this.selectedOrganizationId(), this.selectedWorkspaceId(), this.activityUserFilter || undefined).subscribe({
      next: (result) => this.auditLogs.set(result.data?.items ?? []),
      error: () => this.showToast('Audit logs failed', 'Could not load immutable audit logs.', 'danger')
    });
  }

  loadComments(): void {
    if (!this.selectedWorkspaceId() || !this.selectedRequestId()) {
      this.comments.set([]);
      return;
    }

    this.api.comments(this.selectedWorkspaceId(), 'Request', this.selectedRequestId()).subscribe({
      next: (result) => this.comments.set(result.data ?? []),
      error: () => this.showToast('Comments failed', 'Could not load request comments.', 'danger')
    });
  }

  loadMembers(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.members(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        const members = result.data?.items ?? [];
        this.members.set(members);
        const roleByName = new Map(this.organizationRoles().map((role) => [role.name, role.id]));
        this.selectedMemberRoles = Object.fromEntries(members.map((member) => [member.id, roleByName.get(member.roleName) ?? '']));
      },
      error: () => this.showToast('Team failed', 'Could not load organization members.', 'danger')
    });
  }

  loadOrganizationRoles(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.organizationRoles(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        this.organizationRoles.set(result.data ?? []);
        if (!this.inviteRoleId) {
          this.inviteRoleId = this.organizationRoles().find((role) => role.name === 'Editor' || role.name === 'Developer')?.id ?? this.organizationRoles()[0]?.id ?? '';
        }
        const roleByName = new Map(this.organizationRoles().map((role) => [role.name, role.id]));
        this.selectedMemberRoles = Object.fromEntries(this.members().map((member) => [member.id, roleByName.get(member.roleName) ?? '']));
      },
      error: () => this.showToast('Roles failed', 'Could not load organization roles.', 'danger')
    });
  }

  inviteMember(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }
    if (!this.inviteEmail.trim() || !this.inviteRoleId) {
      this.showToast('Invite incomplete', 'Email and role are required.', 'danger');
      return;
    }

    this.pageLoading.set(true);
    this.api.inviteMember(this.selectedOrganizationId(), {
      email: this.inviteEmail.trim(),
      roleId: this.inviteRoleId,
      message: this.inviteMessage.trim() || undefined
    }).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Invite failed', result.message, 'danger');
          return;
        }
        this.invitations.update((items) => [result.data, ...items]);
        this.lastInviteLink = this.inviteLink(result.data);
        this.inviteEmail = '';
        this.inviteMessage = '';
        this.loadActivity();
        this.loadManagerActivity();
        this.showToast('Invitation created', `Invite link is ready for ${result.data.email}.`, 'success');
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Invite failed', error?.error?.message ?? 'Could not create invitation.', 'danger');
      }
    });
  }

  updateMemberStatus(member: OrganizationMember, status: string): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.updateMemberStatus(this.selectedOrganizationId(), member.id, status).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Status failed', result.message, 'danger');
          return;
        }
        this.members.update((items) => items.map((item) => item.id === member.id ? { ...item, status } : item));
        this.loadActivity();
        this.loadManagerActivity();
        this.showToast('Member updated', `${member.fullName} is now ${status}.`, 'success');
      },
      error: (error) => this.showToast('Status failed', error?.error?.message ?? 'Could not update member status.', 'danger')
    });
  }

  copyInviteLink(invite?: Invitation): void {
    const link = invite ? this.inviteLink(invite) : this.lastInviteLink;
    if (!link) {
      this.showToast('No invite link', 'Create an invitation first.', 'danger');
      return;
    }
    navigator.clipboard.writeText(link);
    this.showToast('Invite link copied', 'SMTP is not configured, so share this link manually.', 'success');
  }

  inviteLink(invite: Invitation): string {
    const token = invite.inviteToken || invite.id;
    return `${window.location.origin}/invite/${token}`;
  }

  regenerateInvite(invite: Invitation): void {
    if (!this.selectedOrganizationId()) {
      return;
    }
    this.api.regenerateInvite(this.selectedOrganizationId(), invite.id).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Invite failed', result.message, 'danger');
          return;
        }
        this.invitations.update((items) => items.map((item) => item.id === invite.id ? result.data! : item));
        this.lastInviteLink = this.inviteLink(result.data);
        this.loadActivity();
        this.showToast('Invite regenerated', 'A fresh invite link is ready.', 'success');
      },
      error: (error) => this.showToast('Invite failed', error?.error?.message ?? 'Could not regenerate invitation.', 'danger')
    });
  }

  revokeInvite(invite: Invitation): void {
    if (!this.selectedOrganizationId() || !confirm(`Revoke invite for ${invite.email}?`)) {
      return;
    }
    this.api.revokeInvite(this.selectedOrganizationId(), invite.id).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Revoke failed', result.message, 'danger');
          return;
        }
        this.invitations.update((items) => items.map((item) => item.id === invite.id ? { ...item, status: 'Revoked' } : item));
        this.loadActivity();
        this.showToast('Invite revoked', `${invite.email} can no longer use that invite.`, 'success');
      },
      error: (error) => this.showToast('Revoke failed', error?.error?.message ?? 'Could not revoke invitation.', 'danger')
    });
  }

  acceptInviteFromToken(): void {
    const token = this.acceptInviteToken.trim();
    if (!token) {
      this.showToast('Token required', 'Paste an invite token or invite link first.', 'danger');
      return;
    }
    const normalized = token.includes('/invite/') ? token.split('/invite/').pop() ?? token : token;
    this.api.acceptInvite(normalized).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Accept failed', result.message, 'danger');
          return;
        }
        this.acceptInviteToken = '';
        this.loadOrganizations();
        this.loadActivity();
        this.showToast('Invite accepted', 'The organization membership was activated.', 'success');
      },
      error: (error) => this.showToast('Accept failed', error?.error?.message ?? 'Could not accept invitation.', 'danger')
    });
  }

  changeMemberRole(member: OrganizationMember): void {
    const roleId = this.selectedMemberRoles[member.id];
    if (!this.selectedOrganizationId() || !roleId) {
      return;
    }
    this.api.changeMemberRole(this.selectedOrganizationId(), member.id, roleId).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Role failed', result.message, 'danger');
          return;
        }
        this.loadMembers();
        this.loadActivity();
        this.loadManagerActivity();
        this.showToast('Role updated', `${member.fullName}'s role was changed.`, 'success');
      },
      error: (error) => this.showToast('Role failed', error?.error?.message ?? 'Could not change member role.', 'danger')
    });
  }

  loadMockServers(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.api.mockServers(this.selectedWorkspaceId()).subscribe({
      next: (result) => {
        this.mockServers.set(result.data ?? []);
        if (!this.selectedMockServerId && this.mockServers().length) {
          this.selectedMockServerId = this.mockServers()[0].id;
          this.loadSelectedMockDetails();
        }
      },
      error: () => this.showToast('Mock servers failed', 'Could not load mock servers.', 'danger')
    });
  }

  createMockServer(): void {
    if (!this.selectedWorkspaceId() || !this.selectedCollectionId()) {
      this.showToast('Collection required', 'Select a collection before creating a mock server.', 'danger');
      return;
    }

    const name = this.mockName.trim() || `${this.selectedCollection()?.name ?? 'Collection'} mock`;
    this.api.createMockServer(this.selectedWorkspaceId(), {
      collectionId: this.selectedCollectionId(),
      name,
      isPublic: this.mockIsPublic,
      apiKeyRequired: this.mockApiKeyRequired,
      delayMs: Number(this.mockDelayMs) || 0
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Mock failed', result.message, 'danger');
          return;
        }
        this.mockName = '';
        this.selectedMockServerId = result.data.id;
        this.mockServers.update((items) => [result.data, ...items]);
        this.loadSelectedMockDetails();
        this.loadActivity();
        this.showToast('Mock server created', `${result.data.routeCount} routes generated.`, 'success');
      },
      error: (error) => this.showToast('Mock failed', error?.error?.message ?? 'Could not create mock server.', 'danger')
    });
  }

  loadSelectedMockDetails(): void {
    if (!this.selectedMockServerId) {
      this.mockRoutes.set([]);
      this.mockLogs.set([]);
      return;
    }

    this.api.mockRoutes(this.selectedMockServerId).subscribe({
      next: (result) => this.mockRoutes.set(result.data ?? []),
      error: () => this.showToast('Mock routes failed', 'Could not load generated mock routes.', 'danger')
    });
    this.api.mockLogs(this.selectedMockServerId).subscribe({
      next: (result) => this.mockLogs.set(result.data ?? []),
      error: () => this.showToast('Mock logs failed', 'Could not load mock logs.', 'danger')
    });
  }

  loadMonitors(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.api.monitors(this.selectedWorkspaceId()).subscribe({
      next: (result) => {
        this.monitors.set(result.data ?? []);
        if (!this.selectedMonitorId && this.monitors().length) {
          this.selectedMonitorId = this.monitors()[0].id;
          this.loadMonitorRuns();
        }
      },
      error: () => this.showToast('Monitors failed', 'Could not load monitors.', 'danger')
    });
  }

  createMonitor(): void {
    if (!this.selectedWorkspaceId() || !this.selectedCollectionId()) {
      this.showToast('Collection required', 'Select a collection before creating a monitor.', 'danger');
      return;
    }

    this.api.createMonitor(this.selectedWorkspaceId(), {
      collectionId: this.selectedCollectionId(),
      environmentId: this.selectedEnvironmentId() || undefined,
      name: this.monitorName.trim() || `${this.selectedCollection()?.name ?? 'Collection'} monitor`,
      scheduleExpression: this.monitorSchedule.trim(),
      isEnabled: true
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Monitor failed', result.message, 'danger');
          return;
        }
        this.monitorName = '';
        this.selectedMonitorId = result.data.id;
        this.monitors.update((items) => [result.data, ...items]);
        this.loadActivity();
        this.showToast('Monitor created', result.data.scheduleExpression, 'success');
      },
      error: (error) => this.showToast('Monitor failed', error?.error?.message ?? 'Could not create monitor.', 'danger')
    });
  }

  runMonitor(monitorId: string): void {
    this.pageLoading.set(true);
    this.api.runMonitor(monitorId).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Monitor run failed', result.message, 'danger');
          return;
        }
        this.collectionRun.set(result.data);
        this.selectedMonitorId = monitorId;
        this.loadMonitors();
        this.loadMonitorRuns();
        this.loadActivity();
        this.showToast('Monitor run complete', `${result.data.passed}/${result.data.totalRequests} passed.`, result.data.failed === 0 ? 'success' : 'danger');
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Monitor run failed', error?.error?.message ?? 'Could not run monitor.', 'danger');
      }
    });
  }

  loadMonitorRuns(): void {
    if (!this.selectedMonitorId) {
      this.monitorRuns.set([]);
      return;
    }

    this.api.monitorRuns(this.selectedMonitorId).subscribe({
      next: (result) => this.monitorRuns.set(result.data ?? []),
      error: () => this.showToast('Monitor history failed', 'Could not load monitor runs.', 'danger')
    });
  }

  loadPublishedDocs(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.api.publishedDocs(this.selectedWorkspaceId()).subscribe({
      next: (result) => this.publishedDocs.set(result.data ?? []),
      error: () => this.showToast('Docs failed', 'Could not load published docs.', 'danger')
    });
  }

  publishDocs(): void {
    if (!this.selectedWorkspaceId() || !this.selectedCollectionId()) {
      this.showToast('Collection required', 'Select a collection before publishing docs.', 'danger');
      return;
    }

    const slug = this.docsSlug.trim() || (this.selectedCollection()?.name ?? 'api-docs').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    this.api.publishDocs(this.selectedWorkspaceId(), {
      collectionId: this.selectedCollectionId(),
      slug,
      isPublic: this.docsIsPublic,
      brandJson: this.docsBrandJson.trim() || undefined
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Publish failed', result.message, 'danger');
          return;
        }
        this.docsSlug = '';
        this.publishedDocs.update((items) => [result.data, ...items]);
        this.loadActivity();
        this.showToast('Docs published', `/api/docs/${result.data.slug}`, 'success');
      },
      error: (error) => this.showToast('Publish failed', error?.error?.message ?? 'Could not publish docs.', 'danger')
    });
  }

  unpublishDocs(docId: string): void {
    this.api.unpublishDocs(docId).subscribe({
      next: () => {
        this.publishedDocs.update((items) => items.filter((item) => item.id !== docId));
        this.showToast('Docs unpublished', 'Documentation link was removed.', 'success');
      },
      error: () => this.showToast('Unpublish failed', 'Could not unpublish docs.', 'danger')
    });
  }

  loadApiSpecs(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.api.apiSpecs(this.selectedWorkspaceId()).subscribe({
      next: (result) => this.apiSpecs.set(result.data?.items ?? []),
      error: () => this.showToast('Specs failed', 'Could not load API specs.', 'danger')
    });
  }

  uploadApiSpec(): void {
    if (!this.selectedWorkspaceId()) {
      return;
    }

    this.api.uploadApiSpec(this.selectedWorkspaceId(), {
      collectionId: this.selectedCollectionId() || undefined,
      name: this.specName.trim() || 'OpenAPI spec',
      format: this.specFormat,
      content: this.specContent
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Spec validation failed', result.message, 'danger');
          return;
        }
        this.governanceFindings.set(result.data);
        this.apiSpecs.update((items) => [result.data.spec, ...items]);
        this.loadActivity();
        this.showToast('Spec validated', `${result.data.findings.length} findings returned.`, result.data.spec.validationStatus === 'Passed' ? 'success' : 'danger');
      },
      error: (error) => this.showToast('Spec upload failed', error?.error?.message ?? 'Could not upload API spec.', 'danger')
    });
  }

  loadOrganizationSettings(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.organizationSettings(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        if (result.data) {
          this.saasSettings.set(result.data);
          this.settingsProductName = result.data.productName;
          this.settingsRetentionDays = result.data.retentionDays;
        }
      },
      error: () => this.showToast('Settings failed', 'Could not load SaaS settings.', 'danger')
    });
  }

  saveOrganizationSettings(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.saveOrganizationSettings(this.selectedOrganizationId(), {
      productName: this.settingsProductName,
      retentionDays: Number(this.settingsRetentionDays) || 365
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Settings failed', result.message, 'danger');
          return;
        }
        this.saasSettings.set(result.data);
        this.productName.set(result.data.productName);
        this.showToast('Settings saved', 'Organization SaaS settings were updated.', 'success');
      },
      error: (error) => this.showToast('Settings failed', error?.error?.message ?? 'Could not save settings.', 'danger')
    });
  }

  loadAiConfig(): void {
    if (!this.selectedOrganizationId()) return;
    this.loadAiProviderStatus();
    this.api.aiConfig(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        this.aiConfig.set(result.data);
        if (result.data) {
          this.aiProvider = result.data.provider;
          this.aiModelName = result.data.modelName ?? '';
          this.aiEndpointUrl = result.data.endpointUrl ?? '';
          this.aiDeploymentName = result.data.deploymentName ?? '';
          this.aiEnabled = result.data.isEnabled;
        }
      },
      error: () => this.showToast('AI config failed', 'Could not load AI provider settings.', 'danger')
    });
  }

  loadAiProviderStatus(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.aiProviderStatus(this.selectedOrganizationId()).subscribe({
      next: (result) => {
        if (result.succeeded && result.data) {
          this.aiProviderStatus.set(result.data);
        }
      },
      error: () => this.aiProviderStatus.set({ configured: false, providerName: 'Fallback', modelName: 'not configured', fallbackEnabled: true, timeoutSeconds: 20 })
    });
  }

  saveAiConfig(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.saveAiConfig(this.selectedOrganizationId(), {
      provider: this.aiProvider,
      modelName: this.aiModelName || undefined,
      endpointUrl: this.aiEndpointUrl || undefined,
      deploymentName: this.aiDeploymentName || undefined,
      isEnabled: this.aiEnabled
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('AI config failed', result.message, 'danger');
          return;
        }
        this.aiConfig.set(result.data);
        this.loadAiProviderStatus();
        this.showToast('AI config saved', `${result.data.provider} is ${result.data.isEnabled ? 'enabled' : 'disabled'}.`, 'success');
      },
      error: (error) => this.showToast('AI config failed', error?.error?.message ?? 'Could not save AI config.', 'danger')
    });
  }

  runAiAssistant(): void {
    if (!this.selectedWorkspaceId()) return;
    this.api.runAiAction(this.selectedWorkspaceId(), {
      action: this.aiAction,
      collectionId: this.selectedCollectionId() || undefined,
      requestId: this.selectedRequestId() || undefined,
      input: this.aiInput || undefined
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('AI action failed', result.message, 'danger');
          return;
        }
        this.aiResult.set(result.data);
        this.loadActivity();
        this.showToast('AI assistant', result.data.providerStatus, 'success');
      },
      error: (error) => this.showToast('AI action failed', error?.error?.message ?? 'Could not run AI assistant action.', 'danger')
    });
  }

  setAiExample(value: string): void {
    this.aiFlowInput = value;
  }

  generateAiCollectionPreview(): void {
    const input = this.aiFlowInput.trim();
    if (!input) {
      this.showToast('Describe a flow', 'Add a business/API flow before generating a collection.', 'danger');
      return;
    }

    this.aiCollectionPreview = this.buildFallbackCollection(input);
    this.showToast('AI fallback generated', this.aiCollectionPreview.providerStatus, 'success');
  }

  approveAiCollection(): void {
    if (!this.aiCollectionPreview || !this.selectedWorkspaceId()) {
      this.showToast('No preview', 'Generate and review a collection first.', 'danger');
      return;
    }

    this.aiCreating = true;
    this.api.importCollection(this.selectedWorkspaceId(), this.aiCollectionPreview.payload).subscribe({
      next: (result) => {
        this.aiCreating = false;
        if (!result.succeeded || !result.data) {
          this.showToast('Create failed', result.message, 'danger');
          return;
        }
        this.selectedCollectionId.set(result.data.collectionId);
        this.selectedRequestId.set('');
        this.aiAgentOpen.set(false);
        this.activeView.set('api-client');
        this.showToast('Collection created', `${result.data.name} is ready with ${result.data.requestCount} requests.`, 'success');
        this.loadCollections();
        this.loadActivity();
      },
      error: (error) => {
        this.aiCreating = false;
        this.showToast('Create failed', error?.error?.message ?? 'Could not create AI collection.', 'danger');
      }
    });
  }

  generateTestsForSelectedRequest(): void {
    if (!this.requestUrl.trim()) {
      this.showToast('Request required', 'Open a request before generating tests.', 'danger');
      return;
    }

    const pathHint = this.requestUrl.replace(/\{\{[^}]+}}/g, '').split('?')[0];
    const bodyChecks = this.looksLikeJson(this.responseBody() || this.apiResponse()?.body || '')
      ? ['jsonPathExists("$.id")', 'jsonPathExists("$.data")']
      : ['bodyContains("success")'];
    this.generatedTests = [
      `statusCodeEquals(${this.requestMethod === 'POST' ? 201 : 200})`,
      'responseTimeLessThan(1500)',
      'headerExists("Content-Type")',
      ...bodyChecks,
      `case("unauthorized", "${pathHint}", 401)`,
      `case("validation failure", "${pathHint}", 400)`,
      this.requestMethod === 'GET' ? `case("not found", "${pathHint}/missing-id", 404)` : ''
    ].filter(Boolean).join('\n');
    this.generatedTestsOpen.set(true);
  }

  saveGeneratedTests(): void {
    if (!this.generatedTests.trim()) {
      return;
    }
    this.requestTestScript = this.generatedTests;
    this.generatedTestsOpen.set(false);
    this.showToast('Tests staged', 'Review the Tests tab and save the request.', 'success');
    this.requestConfigTab = 'Tests';
  }

  generateMockForSelectedRequest(): void {
    if (!this.requestUrl.trim()) {
      this.showToast('Request required', 'Open a request before generating a mock response.', 'danger');
      return;
    }

    this.generatedMockBody = JSON.stringify(this.mockPayloadForUrl(this.requestUrl, this.generatedMockRandomized), null, 2);
    this.generatedMockOpen.set(true);
  }

  saveGeneratedMock(): void {
    if (!this.selectedRequestId() || !this.generatedMockBody.trim()) {
      this.showToast('Request required', 'Save or select a request before saving a mock example.', 'danger');
      return;
    }

    this.api.saveResponseExample(this.selectedRequestId(), {
      name: this.generatedMockRandomized ? 'Generated randomized mock' : 'Generated mock response',
      statusCode: 200,
      headersJson: JSON.stringify({ 'Content-Type': ['application/json'] }, null, 2),
      body: this.generatedMockBody,
      contentType: 'application/json'
    }).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Mock failed', result.message, 'danger');
          return;
        }
        this.generatedMockOpen.set(false);
        this.showToast('Mock example saved', 'The generated response was saved as a request example.', 'success');
        this.loadActivity();
      },
      error: (error) => this.showToast('Mock failed', error?.error?.message ?? 'Could not save generated mock.', 'danger')
    });
  }

  private buildFallbackCollection(input: string): GeneratedCollectionPreview {
    const lower = input.toLowerCase();
    const requests: ImportApiRequestWithFolderPayload[] = [];
    const folders: string[][] = [];
    const addFolder = (name: string) => {
      if (!folders.some((folder) => folder[0] === name)) {
        folders.push([name]);
      }
    };
    const addRequest = (folder: string, name: string, method: string, url: string, body?: unknown) => {
      addFolder(folder);
      requests.push({
        folderPath: [folder],
        name,
        description: `Generated from: ${input}`,
        method,
        url,
        authType: folder === 'Auth' ? undefined : 'Bearer',
        authConfigJson: folder === 'Auth' ? undefined : JSON.stringify({ token: '{{token}}' }),
        bodyType: body ? 'rawJson' : 'none',
        bodyContent: body ? JSON.stringify(body, null, 2) : '',
        preRequestScript: '',
        testScript: ['statusCodeEquals(200)', 'responseTimeLessThan(1500)', 'headerExists("Content-Type")'].join('\n'),
        timeoutMs: 30000,
        followRedirects: true,
        sslVerification: true,
        headers: folder === 'Auth' ? [] : [{ key: 'Authorization', value: 'Bearer {{token}}', enabled: true, isSecret: true }],
        queryParams: [],
        pathParams: []
      });
    };

    if (/(login|auth|token|signin|sign in|register|signup)/i.test(lower)) {
      addRequest('Auth', 'Login user', 'POST', '{{baseUrl}}/auth/login', { email: '{{email}}', password: '{{password}}' });
      addRequest('Auth', 'Register user', 'POST', '{{baseUrl}}/auth/register', { name: '{{userName}}', email: '{{email}}', password: '{{password}}' });
    }
    if (/(profile|user|customer|parent|student|teacher)/i.test(lower)) {
      addRequest('Users', 'Get profile', 'GET', '{{baseUrl}}/users/{{userId}}');
      addRequest('Users', 'Update profile', 'PUT', '{{baseUrl}}/users/{{userId}}', { fullName: '{{userName}}', phone: '{{phone}}' });
    }
    if (/(invoice|payment|voucher|merchant|school|fee|fees|pay|paisay|paisa|bill|receipt)/i.test(lower)) {
      addRequest('Billing', 'Create invoice', 'POST', '{{baseUrl}}/merchants/{{merchantId}}/invoices', { customerId: '{{userId}}', amount: 2500, currency: 'PKR' });
      addRequest('Billing', 'Pay invoice', 'POST', '{{baseUrl}}/invoices/{{invoiceId}}/payments', { method: 'card', reference: '{{paymentReference}}' });
      addRequest('Billing', 'Get voucher', 'GET', '{{baseUrl}}/payments/{{paymentId}}/voucher');
      addRequest('Billing', 'Download receipt', 'GET', '{{baseUrl}}/payments/{{paymentId}}/receipt');
    }
    if (/(order|product|catalog|commerce)/i.test(lower)) {
      addRequest('Commerce', 'List products', 'GET', '{{baseUrl}}/products');
      addRequest('Commerce', 'Create order', 'POST', '{{baseUrl}}/orders', { customerId: '{{userId}}', productId: '{{productId}}', quantity: 1 });
    }
    if (/(expense|approval|approve|request|workflow)/i.test(lower)) {
      addRequest('Approvals', 'Submit request', 'POST', '{{baseUrl}}/approval-requests', { requesterId: '{{userId}}', amount: 1200, reason: 'Team expense' });
      addRequest('Approvals', 'Approve request', 'POST', '{{baseUrl}}/approval-requests/{{requestId}}/approve', { approverId: '{{approverId}}' });
    }
    if (/(dashboard|report|reporting|analytics)/i.test(lower)) {
      addRequest('Reports', 'Dashboard summary', 'GET', '{{baseUrl}}/reports/dashboard');
      addRequest('Reports', 'Export report', 'GET', '{{baseUrl}}/reports/export?from={{fromDate}}&to={{toDate}}');
    }
    if (!requests.length) {
      addRequest('API Flow', 'Health check', 'GET', '{{baseUrl}}/health');
      addRequest('API Flow', 'Create resource', 'POST', '{{baseUrl}}/resources', { name: 'Sample resource' });
    }

    const variables = this.uniqueValues(['baseUrl', 'token', 'userId', 'merchantId', 'invoiceId', 'paymentId', ...requests.flatMap((request) => this.extractVariables(`${request.url}\n${request.bodyContent}`))]);
    const provider = this.aiProviderStatus();
    return {
      providerStatus: provider?.configured
        ? `${provider.providerName} configured (${provider.modelName}). Preview uses validated deterministic collection creation.`
        : 'AI provider not configured. Deterministic fallback generated this runnable collection.',
      collectionName: this.titleFromFlow(input),
      folders,
      variables,
      tests: ['Success response', 'Unauthorized request', 'Validation failure', 'Response time budget', 'JSON path existence'],
      mockExamples: ['User profile JSON', 'Invoice/payment JSON', 'Voucher JSON', 'Order/product JSON'],
      payload: {
        name: this.titleFromFlow(input),
        description: `AI Agent Orchestra generated collection. Source flow: ${input}`,
        folders,
        requests
      }
    };
  }

  private titleFromFlow(input: string): string {
    const cleaned = input.replace(/[^\w\s-]/g, ' ').trim().split(/\s+/).slice(0, 5).join(' ');
    return cleaned ? `${cleaned} API Flow` : 'AI Generated API Flow';
  }

  private mockPayloadForUrl(url: string, randomized: boolean): Record<string, unknown> {
    const stamp = randomized ? Date.now() : 1001;
    const lower = url.toLowerCase();
    if (lower.includes('invoice') || lower.includes('payment') || lower.includes('voucher')) {
      return { id: `pay_${stamp}`, status: 'paid', amount: 2500, currency: 'PKR', voucherCode: `VCH-${stamp}`, paidOnUtc: new Date().toISOString() };
    }
    if (lower.includes('merchant') || lower.includes('customer') || lower.includes('user') || lower.includes('profile')) {
      return { id: `usr_${stamp}`, fullName: 'API Desk User', email: 'user@example.com', role: 'customer', active: true };
    }
    if (lower.includes('product') || lower.includes('order')) {
      return { id: `ord_${stamp}`, status: 'confirmed', items: [{ sku: 'SKU-100', name: 'Sample Product', quantity: 1 }], total: 1200 };
    }
    return { id: `mock_${stamp}`, status: 'success', message: 'Generated mock response' };
  }

  loadAdvancedAnalytics(): void {
    if (!this.selectedWorkspaceId()) return;
    this.api.advancedAnalytics(this.selectedWorkspaceId()).subscribe({
      next: (result) => this.advancedAnalytics.set(result.data),
      error: () => this.showToast('Analytics failed', 'Could not load advanced analytics.', 'danger')
    });
  }

  loadBillingOverview(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.billingOverview(this.selectedOrganizationId()).subscribe({
      next: (result) => this.billingOverview.set(result.data),
      error: () => this.showToast('Billing failed', 'Could not load billing overview.', 'danger')
    });
  }

  loadApiKeys(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.apiKeys(this.selectedOrganizationId()).subscribe({
      next: (result) => this.apiKeys.set(result.data ?? []),
      error: () => this.showToast('API keys failed', 'Could not load API keys.', 'danger')
    });
  }

  createApiKey(): void {
    if (!this.selectedOrganizationId()) return;
    this.api.createApiKey(this.selectedOrganizationId(), {
      workspaceId: this.selectedWorkspaceId() || undefined,
      name: this.apiKeyName.trim() || 'Workspace API key'
    }).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('API key failed', result.message, 'danger');
          return;
        }
        this.createdPlainApiKey = result.data.plainTextKey;
        this.apiKeys.update((items) => [result.data.apiKey, ...items]);
        this.apiKeyName = '';
        this.showToast('API key created', 'Copy the key now; it is shown once.', 'success');
      },
      error: (error) => this.showToast('API key failed', error?.error?.message ?? 'Could not create API key.', 'danger')
    });
  }

  addComment(): void {
    const body = this.commentText.trim();
    if (!body || !this.selectedWorkspaceId() || !this.selectedRequestId()) {
      this.showToast('Comment required', 'Select a request and enter a comment.', 'danger');
      return;
    }

    this.api.createComment(this.selectedWorkspaceId(), 'Request', this.selectedRequestId(), body).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Comment failed', result.message, 'danger');
          return;
        }
        this.commentText = '';
        this.comments.update((items) => [result.data, ...items]);
        this.loadActivity();
        this.loadManagerActivity();
        this.showToast('Comment added', 'The request discussion was updated.', 'success');
      },
      error: (error) => this.showToast('Comment failed', error?.error?.message ?? 'The comment could not be saved.', 'danger')
    });
  }

  sendSelectedRequest(): void {
    if (!this.selectedRequestId()) {
      this.showToast('Select a request', 'Choose a request before sending.', 'danger');
      return;
    }

    if (this.selectedRequestNeedsEnvironment() && !this.selectedEnvironmentId()) {
      this.showToast('Select an environment', 'This request uses variables and needs an environment before it can run.', 'danger');
      return;
    }

    this.pageLoading.set(true);
    this.api.sendRequest(this.selectedRequestId(), this.selectedEnvironmentId()).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded) {
          this.showToast('Request failed', result.message, 'danger');
          return;
        }

        this.apiResponse.set(result.data);
        this.responseBody.set(this.tryFormatJson(result.data.body));
        const statusText = result.data.statusText ? ` ${result.data.statusText}` : '';
        this.showToast('Request complete', `${result.data.statusCode}${statusText} in ${result.data.elapsedMs}ms.`, result.data.succeeded ? 'success' : 'danger');
        this.loadDashboard();
        this.loadActivity();
        this.loadRequestHistory();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Request failed', error?.error?.message ?? 'The request could not be sent.', 'danger');
      }
    });
  }

  createNewRequest(): void {
    if (!this.selectedWorkspaceId() || !this.selectedCollectionId()) {
      this.showToast('Select a collection', 'Choose a collection before creating a request.', 'danger');
      return;
    }

    this.resetRequestEditor();
    this.requestName = 'New request';
    this.requestUrl = 'https://example.com';
    this.requestMethod = 'GET';
    const payload = this.buildRequestPayload(1);
    this.pageLoading.set(true);
    this.api.createRequest(this.selectedCollectionId(), payload).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Create failed', result.message, 'danger');
          return;
        }
        this.showToast('Request created', result.data.name, 'success');
        this.selectedRequestId.set(result.data.id);
        this.requestDetail.set(result.data);
        this.populateRequestEditor(result.data);
        this.loadCollections();
        this.loadRequestHistory();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Create failed', error?.error?.message ?? 'The request could not be created.', 'danger');
      }
    });
  }

  saveSelectedRequest(): void {
    if (!this.selectedRequestId() || !this.requestDetail()) {
      this.createNewRequest();
      return;
    }

    const payload = this.buildRequestPayload(this.requestDetail()?.versionNumber ?? 1);
    if (!payload.name.trim() || !payload.url.trim()) {
      this.showToast('Request is incomplete', 'Name and URL are required before saving.', 'danger');
      return;
    }

    this.pageLoading.set(true);
    this.api.updateRequest(this.selectedRequestId(), payload).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Save failed', result.message, 'danger');
          return;
        }
        this.requestDetail.set(result.data);
        this.populateRequestEditor(result.data);
        this.showToast('Request saved', `${result.data.name} updated to v${result.data.versionNumber}.`, 'success');
        this.loadRequests();
        this.loadActivity();
        this.loadRequestHistory();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Save failed', error?.error?.message ?? 'The request could not be saved.', 'danger');
      }
    });
  }

  runSelectedCollection(): void {
    if (!this.selectedCollectionId()) {
      this.showToast('Select a collection', 'Choose a collection before running it.', 'danger');
      return;
    }

    this.pageLoading.set(true);
    this.api.runCollection(this.selectedCollectionId(), this.selectedEnvironmentId()).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Collection run failed', result.message, 'danger');
          return;
        }
        this.collectionRun.set(result.data);
        this.responseTab = 'History';
        this.showToast('Collection run complete', `${result.data.passed}/${result.data.totalRequests} requests passed.`, result.data.failed ? 'danger' : 'success');
        this.loadDashboard();
        this.loadActivity();
        this.loadRequestHistory();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Collection run failed', error?.error?.message ?? 'The collection could not be run.', 'danger');
      }
    });
  }

  exportSelectedCollection(): void {
    if (!this.selectedCollectionId()) {
      this.showToast('Select a collection', 'Choose a collection before exporting.', 'danger');
      return;
    }

    this.api.exportCollection(this.selectedCollectionId()).subscribe({
      next: (result) => {
        if (!result.succeeded || !result.data) {
          this.showToast('Export failed', result.message, 'danger');
          return;
        }
        const safeName = (result.data.collection.name || 'collection').replace(/[^a-z0-9-_]+/gi, '-').toLowerCase();
        this.downloadBlob(`${safeName}.apidesk.collection.json`, JSON.stringify(result.data, null, 2), 'application/json');
        this.showToast('Collection exported', `${result.data.requests.length} requests exported.`, 'success');
        this.loadActivity();
      },
      error: (error) => this.showToast('Export failed', error?.error?.message ?? 'The collection could not be exported.', 'danger')
    });
  }

  openPostmanImport(): void {
    if (!this.selectedWorkspaceId() && !this.selectedOrganizationId()) {
      this.showToast('Organization required', 'Select an organization before importing.', 'danger');
      return;
    }

    this.importPreview = undefined;
    this.importSuccess = undefined;
    this.importError = '';
    this.importTargetMode = this.selectedWorkspaceId() ? 'workspace' : 'newWorkspace';
    this.importWorkspaceName = '';
    this.postmanImportOpen.set(true);
  }

  continueImportEnvironment(): void {
    this.importPreview = undefined;
    this.importSuccess = undefined;
    this.importError = '';
    this.importTargetMode = 'workspace';
    this.importWorkspaceName = '';
  }

  openImportedCollection(): void {
    if (this.importSuccess?.collectionId) {
      this.selectedCollectionId.set(this.importSuccess.collectionId);
    }
    this.postmanImportOpen.set(false);
    this.activeView.set('api-client');
    this.loadCollections();
  }

  runImportedCollectionFirstRequest(): void {
    this.openImportedCollection();
    window.setTimeout(() => {
      const first = this.requests()[0]?.id;
      if (first) {
        this.selectRequest(first);
      }
    }, 350);
  }

  goToView(view: ViewKey): void {
    this.activeView.set(view);
  }

  goToTeamInvites(): void {
    this.activeView.set('team');
    window.setTimeout(() => {
      const input = document.querySelector<HTMLInputElement>('input[name="inviteEmail"], input[placeholder="teammate@company.com"]');
      input?.focus();
    });
  }

  goToMocksOrDocs(): void {
    if (this.selectedCollectionId()) {
      this.activeView.set('mock-servers');
      return;
    }
    this.activeView.set('collections');
    this.showToast('Collection required', 'Import or create a collection first, then API Desk can generate mocks and documentation from it.', 'danger');
  }

  createWorkspaceQuick(): void {
    if (!this.selectedOrganizationId()) {
      this.showToast('Organization required', 'Select an organization before creating a workspace.', 'danger');
      return;
    }

    const name = `API Workspace ${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    this.pageLoading.set(true);
    this.api.createWorkspace({
      organizationId: this.selectedOrganizationId(),
      name,
      type: 'Team',
      description: 'Created from dashboard onboarding.'
    }).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Workspace failed', result.message, 'danger');
          return;
        }
        this.selectedWorkspaceId.set(result.data.id);
        this.showToast('Workspace created', `${result.data.name} is ready.`, 'success');
        this.loadWorkspaces();
        this.activeView.set('workspaces');
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Workspace failed', error?.error?.message ?? 'Could not create a workspace.', 'danger');
      }
    });
  }

  createStarterRequestFlow(): void {
    if (!this.selectedWorkspaceId()) {
      this.showToast('Workspace required', 'Create or select a workspace before adding requests.', 'danger');
      return;
    }

    if (this.selectedCollectionId()) {
      this.activeView.set('api-client');
      this.createNewRequest();
      return;
    }

    const payload: ImportCollectionPayload = {
      name: 'Starter API Collection',
      description: 'A starter collection created from onboarding.',
      folders: [['Getting started']],
      requests: [{
        folderPath: ['Getting started'],
        name: 'Health check',
        description: 'First request for validating the API runner.',
        method: 'GET',
        url: 'https://postman-echo.com/get?source=api-desk',
        bodyType: 'none',
        bodyContent: '',
        timeoutMs: 30000,
        followRedirects: true,
        sslVerification: true,
        headers: [],
        queryParams: [],
        pathParams: []
      }]
    };

    this.pageLoading.set(true);
    this.api.importCollection(this.selectedWorkspaceId(), payload).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Request failed', result.message, 'danger');
          return;
        }
        this.selectedCollectionId.set(result.data.collectionId);
        this.activeView.set('api-client');
        this.showToast('Starter request created', 'Open the Health check request and press Send.', 'success');
        this.loadCollections();
        this.loadActivity();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Request failed', error?.error?.message ?? 'Could not create the starter request.', 'danger');
      }
    });
  }

  createEnvironmentQuick(): void {
    if (!this.selectedWorkspaceId()) {
      this.showToast('Workspace required', 'Select a workspace before creating an environment.', 'danger');
      return;
    }

    this.pageLoading.set(true);
    this.api.createEnvironment({
      workspaceId: this.selectedWorkspaceId(),
      name: `Local ${this.environments().length + 1}`,
      isDefault: this.environments().length === 0
    }).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.showToast('Environment failed', result.message, 'danger');
          return;
        }
        this.selectedEnvironmentId.set(result.data.id);
        this.showToast('Environment created', `${result.data.name} is selected.`, 'success');
        this.activeView.set('environments');
        this.loadEnvironments();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Environment failed', error?.error?.message ?? 'Could not create an environment.', 'danger');
      }
    });
  }

  importCollectionFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.loadPostmanImportFile(file);
    input.value = '';
  }

  onPostmanDrop(event: DragEvent): void {
    event.preventDefault();
    this.importDragActive = false;
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.loadPostmanImportFile(file);
    }
  }

  loadPostmanImportFile(file: File): void {
    const reader = new FileReader();
    reader.onload = () => {
      try {
        this.importSuccess = undefined;
        this.importPreview = this.buildImportPreview(JSON.parse(String(reader.result ?? '{}')), file.name);
        this.importWorkspaceName = `${this.importPreview.collectionName} Workspace`;
        this.importError = '';
      } catch (error) {
        this.importPreview = undefined;
        this.importError = error instanceof Error ? error.message : 'Unsupported collection JSON.';
      }
    };
    reader.readAsText(file);
  }

  confirmPostmanImport(): void {
    if (!this.importPreview) {
      this.importError = 'Upload a valid Postman collection first.';
      return;
    }

    if (this.importPreview.environmentPayload && !this.importPreview.payload) {
      this.importPostmanEnvironment(this.importPreview.environmentPayload);
      return;
    }

    if (this.importTargetMode === 'mergeCollection') {
      this.mergeImportIntoCurrentCollection(this.importPreview.payload!);
      return;
    }

    if (this.importTargetMode === 'newWorkspace') {
      if (!this.selectedOrganizationId()) {
        this.importError = 'Select an organization before creating a workspace.';
        return;
      }

      this.pageLoading.set(true);
      this.api.createWorkspace({
        organizationId: this.selectedOrganizationId(),
        name: this.importWorkspaceName.trim() || `${this.importPreview.collectionName} Workspace`,
        type: 'Team',
        description: `Created from ${this.importPreview.fileName}`
      }).subscribe({
        next: (workspaceResult) => {
          if (!workspaceResult.succeeded || !workspaceResult.data) {
            this.pageLoading.set(false);
            this.importError = workspaceResult.message;
            return;
          }
          this.selectedWorkspaceId.set(workspaceResult.data.id);
          this.importIntoWorkspace(workspaceResult.data.id, this.importPreview!.payload!);
        },
        error: (error) => {
          this.pageLoading.set(false);
          this.importError = error?.error?.message ?? 'Could not create workspace for import.';
        }
      });
      return;
    }

    this.importIntoWorkspace(this.selectedWorkspaceId(), this.importPreview.payload!);
  }

  private importPostmanEnvironment(environmentPayload: NonNullable<ImportPreview['environmentPayload']>): void {
    if (!this.selectedWorkspaceId()) {
      this.importError = 'Select a workspace before importing an environment.';
      return;
    }

    this.pageLoading.set(true);
    this.api.createEnvironment({
      workspaceId: this.selectedWorkspaceId(),
      name: environmentPayload.name,
      isDefault: this.environments().length === 0
    }).subscribe({
      next: (environmentResult) => {
        if (!environmentResult.succeeded || !environmentResult.data) {
          this.pageLoading.set(false);
          this.importError = environmentResult.message;
          return;
        }

        this.api.upsertEnvironmentVariables(environmentResult.data.id, environmentPayload.variables).subscribe({
          next: (variablesResult) => {
            this.pageLoading.set(false);
            if (!variablesResult.succeeded) {
              this.importError = variablesResult.message;
              return;
            }

            this.importSuccess = {
              kind: 'environment',
              title: 'Environment imported',
              message: `${environmentPayload.name} is ready for variable-powered requests.`,
              environmentId: environmentResult.data.id,
              variableCount: environmentPayload.variables.length
            };
            this.importPreview = undefined;
            this.selectedEnvironmentId.set(environmentResult.data.id);
            this.showToast('Environment imported', `${environmentPayload.variables.length} variables were imported.`, 'success');
            this.loadEnvironments();
          },
          error: (error) => {
            this.pageLoading.set(false);
            this.importError = error?.error?.message ?? 'Environment variables could not be imported.';
          }
        });
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.importError = error?.error?.message ?? 'Environment could not be imported.';
      }
    });
  }

  private importIntoWorkspace(workspaceId: string, payload: ImportCollectionPayload): void {
    this.pageLoading.set(true);
    this.api.importCollection(workspaceId, payload).subscribe({
      next: (result) => {
        this.pageLoading.set(false);
        if (!result.succeeded || !result.data) {
          this.importError = result.message;
          return;
        }
        this.selectedCollectionId.set(result.data.collectionId);
        this.selectedRequestId.set('');
        this.requestDetail.set(null);
        this.importSuccess = {
          kind: 'collection',
          title: 'Collection imported',
          message: `${payload.name} is ready in ${this.selectedWorkspace()?.name || 'this workspace'}.`,
          collectionId: result.data.collectionId,
          requestCount: result.data.requestCount
        };
        this.importPreview = undefined;
        this.showToast('Collection imported', `${result.data.requestCount} requests imported.`, 'success');
        this.loadWorkspaces();
        this.loadCollections();
        this.loadActivity();
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.importError = error?.error?.message ?? 'The collection could not be imported.';
      }
    });
  }

  private mergeImportIntoCurrentCollection(payload: ImportCollectionPayload): void {
    if (!this.selectedCollectionId()) {
      this.importError = 'Select a collection before merging.';
      return;
    }

    const requests = payload.requests;
    if (!requests.length) {
      this.importError = 'No requests were found to merge.';
      return;
    }

    this.pageLoading.set(true);
    let index = 0;
    const createNext = () => {
      const request = requests[index++];
      if (!request) {
        this.pageLoading.set(false);
        this.importSuccess = {
          kind: 'collection',
          title: 'Collection merged',
          message: `${requests.length} requests were added to ${this.selectedCollection()?.name}.`,
          collectionId: this.selectedCollectionId(),
          requestCount: requests.length
        };
        this.importPreview = undefined;
        this.showToast('Collection merged', `${requests.length} requests merged into ${this.selectedCollection()?.name}.`, 'success');
        this.loadRequests();
        this.loadCollections();
        this.loadActivity();
        return;
      }

      this.api.createRequest(this.selectedCollectionId(), {
        ...request,
        workspaceId: this.selectedWorkspaceId(),
        collectionId: this.selectedCollectionId(),
        versionNumber: 1
      }).subscribe({
        next: () => createNext(),
        error: (error) => {
          this.pageLoading.set(false);
          this.importError = error?.error?.message ?? `Could not merge request ${request.name}.`;
        }
      });
    };
    createNext();
  }

  runJsonTool(): void {
    this.jsonError = '';
    this.jsonDiffs = [];
    try {
      if (this.jsonTab === 'Beautify') {
        this.jsonOutput = this.developerTools.beautify(this.jsonInput);
      } else if (this.jsonTab === 'Minify') {
        this.jsonOutput = this.developerTools.minify(this.jsonInput);
      } else if (this.jsonTab === 'Validate') {
        const result = this.developerTools.validate(this.jsonInput);
        this.jsonOutput = result.valid ? 'JSON is valid.' : `Invalid JSON at ${result.line ?? '?'}:${result.column ?? '?'}\n${result.error}`;
      } else if (this.jsonTab === 'Tree View') {
        this.jsonOutput = this.developerTools.tree(this.jsonInput);
      } else if (this.jsonTab === 'Compare') {
        this.jsonDiffs = this.developerTools.compare(this.jsonInput, this.jsonCompareInput);
        this.jsonOutput = this.jsonDiffs.length ? JSON.stringify(this.jsonDiffs, null, 2) : 'Objects are equal.';
      } else if (this.jsonTab === 'Convert') {
        this.jsonOutput = [
          this.developerTools.toTypeScript(this.jsonInput),
          '',
          this.developerTools.toCSharp(this.jsonInput),
          '',
          this.developerTools.toSql(this.jsonInput)
        ].join('\n\n');
      } else {
        this.jsonOutput = JSON.stringify(this.developerTools.jsonPath(this.jsonInput, this.jsonPath), null, 2);
      }
      this.jsonStats = this.developerTools.stats(this.jsonInput);
    } catch (error) {
      this.jsonError = error instanceof Error ? error.message : 'JSON tool failed.';
    }
  }

  sortAndFormat(): void {
    try {
      this.jsonOutput = this.developerTools.beautify(this.jsonInput, true);
      this.jsonStats = this.developerTools.stats(this.jsonInput);
    } catch (error) {
      this.jsonError = error instanceof Error ? error.message : 'Could not sort keys.';
    }
  }

  copyJsonOutput(): void {
    navigator.clipboard.writeText(this.jsonOutput || '');
    this.showToast('Copied', 'Output copied to clipboard.', 'success');
  }

  async pasteJsonFromClipboard(): Promise<void> {
    this.jsonInput = await navigator.clipboard.readText();
    this.showToast('Pasted', 'Clipboard content loaded into the JSON editor.', 'success');
  }

  uploadJsonFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.jsonInput = String(reader.result ?? '');
      this.showToast('File loaded', `${file.name} is ready in the editor.`, 'success');
      input.value = '';
    };
    reader.readAsText(file);
  }

  downloadJsonOutput(): void {
    const blob = new Blob([this.jsonOutput || this.jsonInput || ''], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'apiforge-json-output.json';
    link.click();
    URL.revokeObjectURL(url);
  }

  maskJsonSecrets(): void {
    try {
      this.jsonOutput = this.developerTools.maskSensitiveJson(this.jsonInput);
      this.jsonStats = this.developerTools.stats(this.jsonOutput);
      this.showToast('Masked', 'Sensitive keys were masked in the output.', 'success');
    } catch (error) {
      this.jsonError = error instanceof Error ? error.message : 'Could not mask JSON.';
    }
  }

  escapeJsonInput(): void {
    this.jsonOutput = this.developerTools.escapeJsonString(this.jsonInput);
  }

  unescapeJsonInput(): void {
    try {
      this.jsonOutput = this.developerTools.unescapeJsonString(this.jsonInput);
    } catch (error) {
      this.jsonError = error instanceof Error ? error.message : 'Could not unescape JSON string.';
    }
  }

  copyResponseBody(): void {
    navigator.clipboard.writeText(this.responseViewerBody() || this.responseBody() || this.apiResponse()?.body || '');
    this.showToast('Copied', 'Response body copied to clipboard.', 'success');
  }

  downloadResponseBody(): void {
    const response = this.apiResponse();
    if (!response) {
      this.showToast('No response', 'Send a request before downloading the response.', 'danger');
      return;
    }

    const content = this.responseViewerBody() || this.responseBody() || response.body || '';
    const contentType = response.contentType || response.headers?.['Content-Type']?.[0] || 'text/plain';
    const extension = contentType.toLowerCase().includes('json') || this.looksLikeJson(content) ? 'json' : 'txt';
    this.downloadBlob(`api-desk-response-${response.statusCode}.${extension}`, content, contentType);
    this.showToast('Downloaded', 'Response body was downloaded.', 'success');
  }

  copyRequestAsCurl(): void {
    const headers = this.toPayloadKeyValues(this.requestHeadersRows)
      .map((header) => `-H "${header.key}: ${header.value ?? ''}"`)
      .join(' ');
    const body = this.requestBodyType !== 'none' && this.requestBodyContent.trim()
      ? ` --data '${this.requestBodyContent.replace(/'/g, "\\'")}'`
      : '';
    navigator.clipboard.writeText(`curl -X ${this.requestMethod} "${this.requestUrl}" ${headers}${body}`.trim());
    this.showToast('Copied', 'Current request copied as cURL.', 'success');
  }

  onAuthTypeChange(value: string): void {
    this.requestAuthType = value;
    this.hydrateAuthFieldsFromConfig();
    if (!value) {
      this.requestAuthConfigJson = '';
    }
  }

  applyAuthToRequest(): void {
    this.syncAuthConfigFromFields();

    if (this.requestAuthType === 'Bearer' || this.requestAuthType === 'OAuth2') {
      const token = this.requestAuthType === 'OAuth2' ? this.authOauthToken : this.authBearerToken;
      if (token.trim()) {
        this.upsertKeyValueRow('headers', 'Authorization', `Bearer ${token.trim()}`, true);
      }
    }

    if (this.requestAuthType === 'ApiKey') {
      const key = this.authApiKeyName.trim();
      if (key && this.authApiKeyValue.trim()) {
        this.upsertKeyValueRow(this.authApiKeyLocation === 'query' ? 'query' : 'headers', key, this.authApiKeyValue.trim(), true);
      }
    }

    this.showToast('Auth applied', 'Auth settings were applied to the request safely.', 'success');
  }

  insertVariableIntoUrl(variableName: string): void {
    const token = `{{${variableName}}}`;
    if (!this.requestUrl.includes(token)) {
      this.requestUrl = `${this.requestUrl}${this.requestUrl.endsWith('/') || !this.requestUrl ? '' : '/'}${token}`;
    }
  }

  openCurlImport(): void {
    this.curlCommand = '';
    this.curlImportOpen.set(true);
  }

  importCurlToRequest(): void {
    if (!this.curlCommand.trim()) {
      this.showToast('cURL required', 'Paste a cURL command before importing.', 'danger');
      return;
    }

    try {
      const parsed = JSON.parse(this.developerTools.parseCurl(this.curlCommand)) as {
        method?: string;
        url?: string;
        headers?: { key: string; value: string }[];
        body?: string;
      };
      this.requestMethod = (parsed.method || 'GET').toUpperCase();
      this.requestUrl = parsed.url || this.requestUrl;
      this.requestHeadersRows = (parsed.headers ?? []).map((item) => this.toEditableKeyValue(item.key, item.value));
      this.syncTextFromRows('headers');
      if (parsed.body) {
        this.requestBodyContent = parsed.body;
        this.requestBodyType = this.looksLikeJson(parsed.body) ? 'rawJson' : 'text';
        this.requestConfigTab = 'Body';
      } else {
        this.requestConfigTab = 'Headers';
      }
      if (!this.requestName.trim() || this.requestName === 'New request') {
        this.requestName = this.nameFromUrl(this.requestUrl);
      }
      this.curlImportOpen.set(false);
      this.showToast('cURL imported', 'Method, URL, headers, and body were loaded into the request editor.', 'success');
    } catch (error) {
      this.showToast('Import failed', error instanceof Error ? error.message : 'Could not parse cURL.', 'danger');
    }
  }

  addKeyValueRow(kind: EditableKeyValueKind): void {
    this.rowsFor(kind).push(this.toEditableKeyValue('', ''));
    this.syncTextFromRows(kind);
  }

  removeKeyValueRow(kind: EditableKeyValueKind, id: string): void {
    if (kind === 'headers') this.requestHeadersRows = this.requestHeadersRows.filter((item) => item.id !== id);
    if (kind === 'query') this.requestQueryRows = this.requestQueryRows.filter((item) => item.id !== id);
    if (kind === 'path') this.requestPathRows = this.requestPathRows.filter((item) => item.id !== id);
    this.syncTextFromRows(kind);
  }

  syncTextFromRows(kind: EditableKeyValueKind): void {
    this.rowsFor(kind).forEach((item) => {
      item.isSecret = item.isSecret || this.isSensitiveKey(item.key);
    });
    const text = this.rowsFor(kind)
      .filter((item) => item.enabled !== false && item.key.trim())
      .map((item) => `${item.key.trim()}: ${item.value ?? ''}`)
      .join('\n');
    if (kind === 'headers') this.requestHeadersText = text;
    if (kind === 'query') this.requestQueryText = text;
    if (kind === 'path') this.requestPathText = text;
  }

  syncRowsFromText(kind: EditableKeyValueKind): void {
    const rows = this.rowsFromText(kind === 'headers' ? this.requestHeadersText : kind === 'query' ? this.requestQueryText : this.requestPathText);
    if (kind === 'headers') this.requestHeadersRows = rows;
    if (kind === 'query') this.requestQueryRows = rows;
    if (kind === 'path') this.requestPathRows = rows;
  }

  saveCurrentResponseAsExample(): void {
    const response = this.apiResponse();
    if (!this.selectedRequestId() || !response) {
      this.showToast('No response', 'Send a request before saving an example.', 'danger');
      return;
    }

    this.api.saveResponseExample(this.selectedRequestId(), {
      name: `${this.requestName || 'Response'} - ${response.statusCode}`,
      statusCode: response.statusCode,
      headersJson: JSON.stringify(response.headers ?? {}, null, 2),
      body: response.body ?? '',
      contentType: response.contentType ?? undefined
    }).subscribe({
      next: (result) => {
        if (!result.succeeded) {
          this.showToast('Example failed', result.message, 'danger');
          return;
        }
        this.showToast('Example saved', 'The response is now available as a request example.', 'success');
        this.loadActivity();
        this.loadManagerActivity();
      },
      error: (error) => this.showToast('Example failed', error?.error?.message ?? 'Could not save the response example.', 'danger')
    });
  }

  exportActivityCsv(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.exportActivityCsv(this.selectedOrganizationId(), this.selectedWorkspaceId(), this.activityUserFilter || undefined).subscribe({
      next: (csv) => this.downloadBlob('api-desk-activity.csv', csv, 'text/csv'),
      error: () => this.showToast('Export failed', 'Could not export activity CSV.', 'danger')
    });
  }

  exportAuditCsv(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.exportAuditCsv(this.selectedOrganizationId(), this.selectedWorkspaceId(), this.activityUserFilter || undefined).subscribe({
      next: (csv) => this.downloadBlob('api-desk-audit.csv', csv, 'text/csv'),
      error: () => this.showToast('Export failed', 'Could not export audit CSV.', 'danger')
    });
  }

  private connectRealtime(): void {
    if (this.hub || !this.api.accessToken) {
      return;
    }

    this.hub = new HubConnectionBuilder()
      .withUrl(this.api.collaborationHubUrl, { accessTokenFactory: () => this.api.accessToken ?? '' })
      .withAutomaticReconnect()
      .build();

    this.hub.onreconnecting(() => this.realtimeStatus.set('reconnecting'));
    this.hub.onreconnected(() => {
      this.realtimeStatus.set('online');
      this.joinRealtimeWorkspace();
    });
    this.hub.onclose(() => this.realtimeStatus.set('offline'));

    this.hub.on('commentCreated', (comment: CommentModel) => {
      if (comment.entityId === this.selectedRequestId()) {
        this.comments.update((items) => items.some((item) => item.id === comment.id) ? items : [comment, ...items]);
      }
      this.loadActivity();
      this.loadManagerActivity();
    });

    this.hub.on('requestRunCompleted', () => {
      this.loadDashboard();
      this.loadActivity();
      this.loadManagerActivity();
      this.loadRequestHistory();
    });

    this.hub.on('collectionRunCompleted', () => {
      this.loadDashboard();
      this.loadActivity();
      this.loadManagerActivity();
    });

    this.hub.on('responseExampleSaved', () => {
      this.loadActivity();
      this.loadManagerActivity();
    });

    this.hub.start()
      .then(() => {
        this.realtimeStatus.set('online');
        this.joinRealtimeWorkspace();
      })
      .catch(() => this.realtimeStatus.set('offline'));
  }

  private joinRealtimeWorkspace(): void {
    const workspaceId = this.selectedWorkspaceId();
    if (!workspaceId || this.hub?.state !== 'Connected') {
      return;
    }

    void this.hub.invoke('JoinWorkspace', workspaceId);
  }

  notifyPhase(message: string): void {
    this.showToast('Planned workflow', message, 'default');
  }

  async runUtility(): Promise<void> {
    try {
      if (this.utilityTool === 'Base64') {
        this.utilityOutput = btoa(this.utilityInput);
      } else if (this.utilityTool === 'Base64 Decode') {
        this.utilityOutput = atob(this.utilityInput);
      } else if (this.utilityTool === 'URL Encode') {
        this.utilityOutput = encodeURIComponent(this.utilityInput);
      } else if (this.utilityTool === 'URL Decode') {
        this.utilityOutput = decodeURIComponent(this.utilityInput);
      } else if (this.utilityTool === 'JWT Decoder') {
        this.utilityOutput = this.developerTools.decodeJwt(this.utilityInput);
      } else if (this.utilityTool === 'UUID Generator') {
        this.utilityOutput = crypto.randomUUID();
      } else if (this.utilityTool === 'Timestamp Converter') {
        const date = this.utilityInput ? new Date(Number(this.utilityInput)) : new Date();
        this.utilityOutput = JSON.stringify({ iso: date.toISOString(), unixSeconds: Math.floor(date.getTime() / 1000), local: date.toString() }, null, 2);
      } else if (this.utilityTool === 'cURL Parser') {
        this.utilityOutput = this.developerTools.parseCurl(this.utilityInput);
      } else if (this.utilityTool === 'Hash Generator') {
        const digest = await this.developerTools.hash(this.utilityInput, this.hashAlgorithm);
        this.utilityOutput = JSON.stringify({ algorithm: this.hashAlgorithm, digest }, null, 2);
      } else if (this.utilityTool === 'Regex Tester') {
        this.utilityOutput = JSON.stringify(this.developerTools.testRegex(this.utilityPattern, this.utilityInput, this.utilityFlags), null, 2);
      } else {
        this.utilityOutput = 'Select a supported utility.';
      }
    } catch (error) {
      this.utilityOutput = error instanceof Error ? error.message : 'Utility failed.';
    }
  }

  toggleTheme(): void {
    this.darkMode.update((value) => !value);
    localStorage.setItem('apiforge.theme', this.darkMode() ? 'dark' : 'light');
    document.documentElement.classList.toggle('light', !this.darkMode());
  }

  methodTone(method?: string): 'default' | 'success' | 'danger' | 'accent' {
    if (method === 'GET') {
      return 'success';
    }
    if (method === 'DELETE') {
      return 'danger';
    }
    return 'accent';
  }

  activityTone(status?: string): 'default' | 'success' | 'danger' | 'accent' {
    if (status === 'Success') {
      return 'success';
    }
    if (status === 'Failure') {
      return 'danger';
    }
    return 'default';
  }

  formatBytes(value?: number): string {
    const size = value ?? 0;
    if (size < 1024) {
      return `${size} B`;
    }
    if (size < 1024 * 1024) {
      return `${(size / 1024).toFixed(1)} KB`;
    }
    return `${(size / 1024 / 1024).toFixed(1)} MB`;
  }

  requestBodyLanguage(): string {
    return this.requestBodyType === 'rawJson' ? 'json' : 'text';
  }

  responseStatusLabel(): string {
    const response = this.apiResponse();
    if (!response) {
      return 'Status -';
    }

    return `Status ${response.statusCode}${response.statusText ? ` ${response.statusText}` : ''}`;
  }

  mockServerUrl(slug: string): string {
    const origin = typeof window === 'undefined' ? '' : window.location.origin;
    return `${origin}/api/mock/${slug}`;
  }

  responseHeaderEntries(): { key: string; value: string }[] {
    return Object.entries(this.apiResponse()?.headers ?? {}).map(([key, value]) => ({ key, value: value.join(', ') }));
  }

  responseCookieEntries(): { key: string; value: string }[] {
    return Object.entries(this.apiResponse()?.cookies ?? {}).map(([key, value]) => ({ key, value: value.join(', ') }));
  }

  requestVariables(): string[] {
    return this.uniqueValues([
      ...this.extractVariables(this.requestUrl),
      ...this.extractVariables(this.requestBodyContent),
      ...this.extractVariables(this.requestHeadersRows.map((row) => `${row.key}:${row.value ?? ''}`).join('\n')),
      ...this.extractVariables(this.requestQueryRows.map((row) => `${row.key}:${row.value ?? ''}`).join('\n'))
    ]);
  }

  responseSearchMatches(): number {
    const search = this.responseSearch.trim().toLowerCase();
    const body = (this.responseBody() || this.apiResponse()?.body || '').toLowerCase();
    if (!search || !body) {
      return 0;
    }
    return body.split(search).length - 1;
  }

  billingUsagePercent(): number {
    const overview = this.billingOverview();
    const limit = overview?.plans.find((plan) => plan.id === overview.subscription?.billingPlanId)?.includedRequests ?? 50000;
    return Math.min(100, Math.round(((overview?.requestsThisPeriod ?? 0) / Math.max(1, limit)) * 100));
  }

  planFeatures(plan: BillingPlan): string[] {
    try {
      const parsed = JSON.parse(plan.featuresJson) as unknown;
      return Array.isArray(parsed) ? parsed.map((item) => String(item)) : [];
    } catch {
      return [];
    }
  }

  keyValuePreviewRows(value: string): KeyValueItem[] {
    return this.parseKeyValueText(value);
  }

  chartHeight(value: number, max: number): number {
    return Math.max(6, Math.round((value / Math.max(1, max)) * 92));
  }

  collectionFreshness(collection: Collection): string {
    const value = collection.modifiedOn ?? collection.createdOn;
    return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
  }

  maskValue(value?: string, isSecret = false): string {
    if (!value) {
      return '';
    }
    return isSecret ? '********' : value;
  }

  private populateRequestEditor(request: ApiRequestDetail): void {
    this.requestName = request.name ?? '';
    this.requestDescription = request.description ?? '';
    this.requestMethod = request.method ?? 'GET';
    this.requestUrl = request.url ?? '';
    this.requestAuthType = request.authType ?? '';
    this.requestAuthConfigJson = request.authConfigJson ?? '';
    this.requestBodyType = request.bodyType ?? 'none';
    this.requestBodyContent = request.bodyContent ?? '';
    this.requestPreScript = '';
    this.requestTestScript = '';
    this.requestTimeoutMs = request.timeoutMs || 30000;
    this.requestFollowRedirects = request.followRedirects;
    this.requestSslVerification = request.sslVerification;
    this.requestHeadersText = this.keyValuesToText(request.headers ?? []);
    this.requestQueryText = this.keyValuesToText(request.queryParams ?? []);
    this.requestPathText = this.keyValuesToText(request.pathParams ?? []);
    this.requestHeadersRows = this.toEditableRows(request.headers ?? []);
    this.requestQueryRows = this.toEditableRows(request.queryParams ?? []);
    this.requestPathRows = this.toEditableRows(request.pathParams ?? []);
    this.hydrateAuthFieldsFromConfig();
  }

  private resetRequestEditor(): void {
    this.requestName = '';
    this.requestDescription = '';
    this.requestMethod = 'GET';
    this.requestUrl = '';
    this.requestAuthType = '';
    this.requestAuthConfigJson = '';
    this.requestBodyType = 'none';
    this.requestBodyContent = '';
    this.requestPreScript = '';
    this.requestTestScript = '';
    this.requestTimeoutMs = 30000;
    this.requestFollowRedirects = true;
    this.requestSslVerification = true;
    this.requestHeadersText = '';
    this.requestQueryText = '';
    this.requestPathText = '';
    this.requestHeadersRows = [];
    this.requestQueryRows = [];
    this.requestPathRows = [];
    this.authBearerToken = '';
    this.authBasicUsername = '';
    this.authBasicPassword = '';
    this.authApiKeyName = 'X-API-Key';
    this.authApiKeyValue = '';
    this.authApiKeyLocation = 'header';
    this.authOauthToken = '';
  }

  private buildRequestPayload(versionNumber: number): SaveApiRequestPayload {
    this.syncAuthConfigFromFields();
    return {
      workspaceId: this.selectedWorkspaceId(),
      collectionId: this.selectedCollectionId(),
      name: this.requestName.trim(),
      description: this.requestDescription.trim() || undefined,
      method: this.requestMethod,
      url: this.requestUrl.trim(),
      authType: this.requestAuthType.trim() || undefined,
      authConfigJson: this.requestAuthConfigJson.trim() || undefined,
      bodyType: this.requestBodyType || 'none',
      bodyContent: this.requestBodyContent,
      preRequestScript: this.requestPreScript,
      testScript: this.requestTestScript,
      timeoutMs: Number(this.requestTimeoutMs) || 30000,
      followRedirects: this.requestFollowRedirects,
      sslVerification: this.requestSslVerification,
      headers: this.toPayloadKeyValues(this.requestHeadersRows),
      queryParams: this.toPayloadKeyValues(this.requestQueryRows),
      pathParams: this.toPayloadKeyValues(this.requestPathRows),
      versionNumber
    };
  }

  private keyValuesToText(items: KeyValueItem[]): string {
    return items
      .filter((item) => item.enabled !== false)
      .map((item) => `${item.key}: ${item.value ?? ''}`)
      .join('\n');
  }

  private rowsFor(kind: EditableKeyValueKind): EditableKeyValue[] {
    if (kind === 'headers') return this.requestHeadersRows;
    if (kind === 'query') return this.requestQueryRows;
    return this.requestPathRows;
  }

  private rowsFromText(value: string): EditableKeyValue[] {
    return this.parseKeyValueText(value).map((item) => this.toEditableKeyValue(item.key, item.value, item.enabled, item.isSecret));
  }

  private syncAuthConfigFromFields(): void {
    if (this.requestAuthType === 'Bearer') {
      this.requestAuthConfigJson = JSON.stringify({ token: this.authBearerToken }, null, 2);
      return;
    }
    if (this.requestAuthType === 'Basic') {
      this.requestAuthConfigJson = JSON.stringify({ username: this.authBasicUsername, password: this.authBasicPassword }, null, 2);
      return;
    }
    if (this.requestAuthType === 'ApiKey') {
      this.requestAuthConfigJson = JSON.stringify({ name: this.authApiKeyName, value: this.authApiKeyValue, location: this.authApiKeyLocation }, null, 2);
      return;
    }
    if (this.requestAuthType === 'OAuth2') {
      this.requestAuthConfigJson = JSON.stringify({ token: this.authOauthToken, grantType: 'manual_bearer' }, null, 2);
      return;
    }
  }

  private hydrateAuthFieldsFromConfig(): void {
    if (!this.requestAuthConfigJson.trim()) {
      return;
    }

    try {
      const config = JSON.parse(this.requestAuthConfigJson) as Record<string, string>;
      this.authBearerToken = config['token'] ?? this.authBearerToken;
      this.authOauthToken = config['token'] ?? this.authOauthToken;
      this.authBasicUsername = config['username'] ?? this.authBasicUsername;
      this.authBasicPassword = config['password'] ?? this.authBasicPassword;
      this.authApiKeyName = config['name'] ?? this.authApiKeyName;
      this.authApiKeyValue = config['value'] ?? this.authApiKeyValue;
      this.authApiKeyLocation = config['location'] === 'query' ? 'query' : 'header';
    } catch {
      // Keep raw config editable if an older request has non-standard auth JSON.
    }
  }

  private upsertKeyValueRow(kind: EditableKeyValueKind, key: string, value: string, isSecret = false): void {
    const rows = this.rowsFor(kind);
    const existing = rows.find((row) => row.key.toLowerCase() === key.toLowerCase());
    if (existing) {
      existing.value = value;
      existing.enabled = true;
      existing.isSecret = isSecret || this.isSensitiveKey(key);
    } else {
      rows.unshift(this.toEditableKeyValue(key, value, true, isSecret || this.isSensitiveKey(key)));
    }
    this.syncTextFromRows(kind);
  }

  private toEditableRows(items: KeyValueItem[]): EditableKeyValue[] {
    return items.map((item) => this.toEditableKeyValue(item.key, item.value, item.enabled, item.isSecret));
  }

  private rememberOpenRequest(requestId: string): void {
    if (!requestId) {
      return;
    }

    this.openRequestIds.update((ids) => [requestId, ...ids.filter((id) => id !== requestId)].slice(0, 9));
  }

  private toEditableKeyValue(key: string, value?: string, enabled = true, isSecret?: boolean): EditableKeyValue {
    return {
      id: crypto.randomUUID(),
      key,
      value: value ?? '',
      enabled,
      isSecret: isSecret ?? this.isSensitiveKey(key)
    };
  }

  private toPayloadKeyValues(rows: EditableKeyValue[]): KeyValueItem[] {
    return rows
      .filter((item) => item.enabled !== false && item.key.trim())
      .map((item) => ({
        key: item.key.trim(),
        value: item.value ?? '',
        enabled: item.enabled !== false,
        isSecret: item.isSecret || this.isSensitiveKey(item.key)
      }));
  }

  private looksLikeJson(value: string): boolean {
    const trimmed = value.trim();
    if (!trimmed || !/^[{[]/.test(trimmed)) {
      return false;
    }
    try {
      JSON.parse(trimmed);
      return true;
    } catch {
      return false;
    }
  }

  private nameFromUrl(value: string): string {
    try {
      const url = new URL(value.replace(/\{\{[^}]+\}\}/g, 'example.com'));
      const lastSegment = url.pathname.split('/').filter(Boolean).at(-1);
      return lastSegment ? `${this.requestMethod} ${lastSegment}` : `${this.requestMethod} request`;
    } catch {
      return `${this.requestMethod} request`;
    }
  }

  private parseKeyValueText(value: string): KeyValueItem[] {
    return value
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith('#'))
      .map((line) => {
        const separator = line.includes(':') ? ':' : '=';
        const index = line.indexOf(separator);
        const key = index >= 0 ? line.slice(0, index).trim() : line;
        const parsedValue = index >= 0 ? line.slice(index + 1).trim() : '';
        return { key, value: parsedValue, enabled: true, isSecret: this.isSensitiveKey(key) };
      })
      .filter((item) => !!item.key);
  }

  private isSensitiveKey(key: string): boolean {
    return /(password|token|secret|api[-_]?key|authorization|cookie)/i.test(key);
  }

  private extractVariables(value: string): string[] {
    return [...value.matchAll(/\{\{\s*([^}]+?)\s*\}\}/g)]
      .map((match) => match[1]?.trim())
      .filter((item): item is string => !!item);
  }

  private uniqueValues(values: string[]): string[] {
    return [...new Set(values)].slice(0, 14);
  }

  private buildImportPreview(raw: unknown, fileName: string): ImportPreview {
    const payload = raw as any;
    const unsupportedItems: string[] = [];

    if (this.isPostmanEnvironment(payload)) {
      const variables = (payload.values ?? [])
        .filter((item: any) => item?.key)
        .map((item: any) => ({
          key: String(item.key),
          value: item.value == null ? '' : String(item.value),
          scope: 'Environment',
          isSecret: this.isSensitiveKey(String(item.key)) || String(item.type ?? '').toLowerCase() === 'secret',
          enabled: item.enabled !== false
        }));

      return {
        fileName,
        collectionName: payload.name ?? fileName.replace(/\.json$/i, ''),
        folderCount: 0,
        requestCount: 0,
        environmentVariableCount: variables.length,
        authTypes: ['Environment'],
        variables: variables.map((variable: any) => variable.key),
        scriptsDetected: 0,
        unsupportedItems: [],
        environmentPayload: {
          name: payload.name ?? fileName.replace(/\.json$/i, ''),
          variables
        }
      };
    }

    if (payload?.info && Array.isArray(payload.item)) {
      const schema = String(payload.info.schema ?? '');
      if (schema && !schema.includes('v2.1')) {
        throw new Error(`Unsupported Postman schema (${schema}). Please export as Postman Collection v2.1 JSON.`);
      }
      if (!schema) {
        unsupportedItems.push('Postman schema field is missing; import will continue with best-effort parsing.');
      }
    }

    const normalized = this.normalizeCollectionImport(raw, fileName);
    const folders = normalized.folders ?? [];
    const authTypes = this.detectImportAuthTypes(payload, normalized);
    const variables = this.uniqueValues([
      ...this.detectPostmanVariables(payload),
      ...normalized.requests.flatMap((request) => [
        ...this.extractVariables(request.url),
        ...this.extractVariables(request.bodyContent ?? ''),
        ...request.headers.flatMap((header) => [header.key, header.value ?? '']).flatMap((value) => this.extractVariables(value))
      ])
    ]);
    const scriptsDetected = this.countPostmanScripts(payload);
    unsupportedItems.push(...this.detectUnsupportedPostmanItems(payload));

    return {
      fileName,
      collectionName: normalized.name,
      folderCount: folders.length,
      requestCount: normalized.requests.length,
      environmentVariableCount: 0,
      authTypes,
      variables,
      scriptsDetected,
      unsupportedItems: this.uniqueValues(unsupportedItems),
      payload: normalized
    };
  }

  private isPostmanEnvironment(payload: any): boolean {
    return Array.isArray(payload?.values)
      && (payload?._postman_variable_scope === 'environment'
        || String(payload?.postman_variable_scope ?? '').toLowerCase() === 'environment'
        || payload?.name);
  }

  private detectImportAuthTypes(raw: any, payload: ImportCollectionPayload): string[] {
    const types: string[] = [];
    const visit = (items: any[] = []) => {
      for (const item of items) {
        if (item.request?.auth?.type) {
          types.push(item.request.auth.type);
        }
        if (Array.isArray(item.item)) {
          visit(item.item);
        }
      }
    };
    visit(raw?.item ?? []);
    types.push(...payload.requests.map((request) => request.authType || '').filter(Boolean));
    return this.uniqueValues(types.length ? types : ['No Auth']);
  }

  private detectPostmanVariables(raw: any): string[] {
    const variables = Array.isArray(raw?.variable) ? raw.variable.map((item: any) => item.key ?? item.name ?? '') : [];
    return variables.filter(Boolean);
  }

  private countPostmanScripts(raw: any): number {
    let count = 0;
    const visit = (items: any[] = []) => {
      for (const item of items) {
        if (Array.isArray(item.event)) {
          count += item.event.filter((event: any) => event.listen === 'test' || event.listen === 'prerequest').length;
        }
        if (Array.isArray(item.item)) {
          visit(item.item);
        }
      }
    };
    if (Array.isArray(raw?.event)) {
      count += raw.event.length;
    }
    visit(raw?.item ?? []);
    return count;
  }

  private detectUnsupportedPostmanItems(raw: any): string[] {
    const unsupported: string[] = [];
    const visit = (items: any[] = []) => {
      for (const item of items) {
        const mode = item.request?.body?.mode;
        if (mode && !['raw', 'urlencoded', 'formdata'].includes(mode)) {
          unsupported.push(`Body mode '${mode}' imported as text placeholder.`);
        }
        if (item.request?.auth?.type === 'oauth2') {
          unsupported.push('OAuth 2 token exchange settings are imported as manual bearer placeholders.');
        }
        if (Array.isArray(item.item)) {
          visit(item.item);
        }
      }
    };
    visit(raw?.item ?? []);
    return unsupported;
  }

  private normalizeCollectionImport(raw: unknown, fallbackName: string): ImportCollectionPayload {
    const payload = raw as any;
    if (payload?.formatVersion && payload?.collection && Array.isArray(payload.requests)) {
      return {
        name: payload.collection.name ?? fallbackName,
        description: payload.collection.description,
        folders: this.uniqueFolderPaths(payload.requests.map((request: any) => request.folderPath).filter(Array.isArray)),
        requests: payload.requests.map((request: any) => ({
          ...this.normalizeApiRequest(request),
          folderPath: Array.isArray(request.folderPath) ? request.folderPath : undefined
        }))
      };
    }

    if (payload?.info && Array.isArray(payload.item)) {
      return {
        name: payload.info.name ?? fallbackName,
        description: payload.info.description,
        folders: this.collectPostmanFolderPaths(payload.item),
        requests: this.flattenPostmanItems(payload.item)
      };
    }

    if (Array.isArray(payload?.requests)) {
      return {
        name: payload.name ?? fallbackName,
        description: payload.description,
        folders: this.uniqueFolderPaths(payload.requests.map((request: any) => request.folderPath).filter(Array.isArray)),
        requests: payload.requests.map((request: any) => ({
          ...this.normalizeApiRequest(request),
          folderPath: Array.isArray(request.folderPath) ? request.folderPath : undefined
        }))
      };
    }

    throw new Error('Only API DESK exports, simplified collection JSON, and Postman collection JSON are supported.');
  }

  private flattenPostmanItems(items: any[], folderPath: string[] = []): ImportApiRequestWithFolderPayload[] {
    return items.flatMap((item) => {
      if (Array.isArray(item.item)) {
        const nextPath = item.request ? folderPath : [...folderPath, item.name ?? 'Folder'];
        return this.flattenPostmanItems(item.item, nextPath);
      }

      const request = item.request;
      if (!request) {
        return [];
      }

      const headers = Array.isArray(request.header)
        ? request.header.map((header: any) => ({
            key: header.key ?? header.name ?? '',
            value: header.value ?? '',
            enabled: header.disabled !== true,
            isSecret: this.isSensitiveKey(header.key ?? header.name ?? '')
          }))
        : [];
      const url = typeof request.url === 'string' ? request.url : request.url?.raw ?? this.buildPostmanUrl(request.url);
      const queryParams = Array.isArray(request.url?.query)
        ? request.url.query.map((param: any) => ({
            key: param.key ?? '',
            value: param.value ?? '',
            enabled: param.disabled !== true,
            isSecret: this.isSensitiveKey(param.key ?? '')
          }))
        : [];

      return [
        {
          ...this.normalizeApiRequest({
          name: item.name ?? request.name ?? 'Imported request',
          description: request.description,
          method: request.method ?? 'GET',
          url,
          authType: this.normalizePostmanAuthType(request.auth?.type),
          authConfigJson: this.normalizePostmanAuthConfig(request.auth),
          bodyType: this.normalizePostmanBodyType(request.body),
          bodyContent: this.normalizePostmanBodyContent(request.body),
          preRequestScript: this.extractPostmanScript(item.event, 'prerequest'),
          testScript: this.extractPostmanScript(item.event, 'test'),
          headers,
          queryParams,
          pathParams: []
          }),
          folderPath
        }
      ];
    });
  }

  private collectPostmanFolderPaths(items: any[], folderPath: string[] = []): string[][] {
    return items.flatMap((item) => {
      if (!Array.isArray(item.item) || item.request) {
        return [];
      }

      const nextPath = [...folderPath, item.name ?? 'Folder'];
      return [nextPath, ...this.collectPostmanFolderPaths(item.item, nextPath)];
    });
  }

  private uniqueFolderPaths(paths: string[][]): string[][] {
    const seen = new Set<string>();
    return paths.filter((path) => {
      const key = path.join('/');
      if (!key || seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
  }

  private buildPostmanUrl(url: any): string {
    if (!url) {
      return 'https://example.com';
    }
    const protocol = url.protocol ? `${url.protocol}://` : '';
    const host = Array.isArray(url.host) ? url.host.join('.') : url.host ?? '';
    const path = Array.isArray(url.path) ? `/${url.path.join('/')}` : url.path ? `/${url.path}` : '';
    return `${protocol}${host}${path}` || 'https://example.com';
  }

  private normalizePostmanBodyType(body: any): string {
    if (!body?.mode) return 'none';
    if (body.mode === 'raw') {
      const language = String(body.options?.raw?.language ?? '').toLowerCase();
      return language === 'json' || this.looksLikeJson(body.raw ?? '') ? 'rawJson' : 'rawText';
    }
    if (body.mode === 'urlencoded') return 'formUrlEncoded';
    if (body.mode === 'formdata') return 'formData';
    return 'rawText';
  }

  private normalizePostmanBodyContent(body: any): string {
    if (!body?.mode) return '';
    if (body.mode === 'raw') return body.raw ?? '';
    if (body.mode === 'urlencoded') {
      return (body.urlencoded ?? [])
        .filter((item: any) => item?.disabled !== true && item?.key)
        .map((item: any) => `${item.key}=${item.value ?? ''}`)
        .join('\n');
    }
    if (body.mode === 'formdata') {
      return (body.formdata ?? [])
        .filter((item: any) => item?.disabled !== true && item?.key)
        .map((item: any) => `${item.key}=${item.value ?? ''}`)
        .join('\n');
    }
    return body.raw ?? '';
  }

  private normalizePostmanAuthType(auth: string | undefined): string | undefined {
    if (!auth) return undefined;
    if (auth === 'bearer') return 'Bearer';
    if (auth === 'basic') return 'Basic';
    if (auth === 'apikey') return 'ApiKey';
    if (auth === 'oauth2') return 'OAuth2';
    return auth;
  }

  private normalizePostmanAuthConfig(auth: any): string | undefined {
    if (!auth?.type) return undefined;
    const read = (name: string) => (auth[auth.type] ?? []).find((item: any) => item.key === name)?.value;
    if (auth.type === 'bearer') return JSON.stringify({ token: read('token') ?? '' });
    if (auth.type === 'basic') return JSON.stringify({ username: read('username') ?? '', password: read('password') ?? '' });
    if (auth.type === 'apikey') return JSON.stringify({ name: read('key') ?? 'X-API-Key', value: read('value') ?? '', location: read('in') ?? 'header' });
    if (auth.type === 'oauth2') return JSON.stringify({ token: read('accessToken') ?? read('token') ?? '', grantType: 'manual_bearer' });
    return JSON.stringify(auth);
  }

  private extractPostmanScript(events: any[] | undefined, listen: 'prerequest' | 'test'): string | undefined {
    const script = (events ?? []).find((event) => event.listen === listen)?.script?.exec;
    return Array.isArray(script) ? script.join('\n') : undefined;
  }

  private normalizeApiRequest(request: any): ImportApiRequestPayload {
    return {
      name: request.name ?? 'Imported request',
      description: request.description,
      method: (request.method ?? 'GET').toUpperCase(),
      url: request.url ?? 'https://example.com',
      authType: request.authType,
      authConfigJson: request.authConfigJson,
      bodyType: request.bodyType ?? 'none',
      bodyContent: request.bodyContent ?? '',
      preRequestScript: request.preRequestScript,
      testScript: request.testScript,
      timeoutMs: request.timeoutMs || 30000,
      followRedirects: request.followRedirects ?? true,
      sslVerification: request.sslVerification ?? true,
      headers: request.headers ?? [],
      queryParams: request.queryParams ?? [],
      pathParams: request.pathParams ?? []
    };
  }

  private downloadBlob(fileName: string, content: string, type: string): void {
    const blob = new Blob([content], { type });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  private isAuthFormValid(): boolean {
    const emailOk = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.loginEmail.trim());
    const passwordOk = this.loginPassword.length >= 8;
    const registerOk = !this.registerMode || (!!this.registerFullName.trim() && !!this.registerOrganization.trim());
    return emailOk && passwordOk && registerOk;
  }

  private tryFormatJson(value: string): string {
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  private showToast(title: string, message: string, tone: ToastState['tone'] = 'default'): void {
    this.toast.set({ title, message, tone });
    window.setTimeout(() => this.toast.set(null), 3600);
  }
}
