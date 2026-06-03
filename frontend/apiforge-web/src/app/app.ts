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
  AuditLog,
  ApiRequestDetail,
  ApiRequestSummary,
  ApiResponse,
  ApiSpec,
  ApiSpecValidation,
  ApiKeyModel,
  BillingOverview,
  CommentModel,
  Collection,
  CollectionRunResult,
  EnvironmentModel,
  ImportApiRequestPayload,
  ImportCollectionPayload,
  KeyValueItem,
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
  RequestRun,
  SaveApiRequestPayload,
  Workspace,
  WorkspaceDashboard
} from './core/api.models';
import { DeveloperToolsService, JsonDiff, JsonStats } from './features/developer-tools/developer-tools.service';
import { MonacoEditorComponent } from './shared/monaco-editor.component';
import { BadgeComponent } from './shared/ui/badge.component';
import { EmptyStateComponent } from './shared/ui/empty-state.component';
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
type EditableKeyValue = KeyValueItem & { id: string };
type EditableKeyValueKind = 'headers' | 'query' | 'path';
type RequestTreeGroup = { key: string; label: string; requests: ApiRequestSummary[] };

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorComponent,
    BadgeComponent,
    EmptyStateComponent,
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
  readonly aiResult = signal<AiAssistantAction | null>(null);
  readonly advancedAnalytics = signal<AdvancedAnalytics | null>(null);
  readonly billingOverview = signal<BillingOverview | null>(null);
  readonly saasSettings = signal<OrganizationSaasSettings | null>(null);
  readonly apiKeys = signal<ApiKeyModel[]>([]);
  readonly members = signal<OrganizationMember[]>([]);
  readonly requestHistory = signal<RequestRun[]>([]);
  readonly dashboard = signal<WorkspaceDashboard | null>(null);
  readonly managerSummary = signal<ManagerSummary | null>(null);
  readonly apiResponse = signal<ApiResponse | null>(null);
  readonly collectionRun = signal<CollectionRunResult | null>(null);
  readonly responseBody = signal('');
  readonly realtimeStatus = signal('offline');
  readonly selectedOrganizationId = signal('');
  readonly selectedWorkspaceId = signal('');
  readonly selectedCollectionId = signal('');
  readonly selectedRequestId = signal('');
  readonly selectedEnvironmentId = signal('');

  readonly isSignedIn = computed(() => this.api.isAuthenticated());
  readonly selectedWorkspace = computed(() => this.workspaces().find((workspace) => workspace.id === this.selectedWorkspaceId()));
  readonly selectedCollection = computed(() => this.collections().find((collection) => collection.id === this.selectedCollectionId()));
  readonly selectedRequestSummary = computed(() => this.requests().find((request) => request.id === this.selectedRequestId()));
  readonly selectedRequest = computed(() => this.requestDetail() ?? this.selectedRequestSummary());
  readonly selectedRequestNeedsEnvironment = computed(() => (this.selectedRequest()?.url ?? '').includes('{{'));
  readonly filteredCollections = computed(() => {
    const search = this.collectionSearch.trim().toLowerCase();
    if (!search) {
      return this.collections();
    }
    return this.collections().filter((collection) =>
      [collection.name, collection.description, collection.ownerName].some((value) => (value ?? '').toLowerCase().includes(search))
    );
  });
  readonly filteredRequests = computed(() => {
    const search = this.requestSearch.trim().toLowerCase();
    if (!search) {
      return this.requests();
    }
    return this.requests().filter((request) =>
      [request.name, request.method, request.url].some((value) => (value ?? '').toLowerCase().includes(search))
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
    { value: 'Basic', label: 'Basic auth', meta: 'Username/password' }
  ]);
  readonly bodyTypeOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'none', label: 'none', meta: 'No request body' },
    { value: 'rawJson', label: 'raw JSON', meta: 'Application JSON' },
    { value: 'text', label: 'text', meta: 'Plain text' }
  ]);
  readonly specFormatOptions = computed<PremiumSelectOption[]>(() => [
    { value: 'json', label: 'JSON', meta: 'OpenAPI JSON' },
    { value: 'yaml', label: 'YAML', meta: 'OpenAPI YAML' }
  ]);
  readonly activityMemberOptions = computed<PremiumSelectOption[]>(() => [
    { value: '', label: 'All members', meta: 'Organization team' },
    ...this.members().map((member) => ({ value: member.userId, label: member.fullName, meta: member.roleName }))
  ]);
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
  registerFullName = '';
  registerOrganization = '';
  registerWorkspace = '';
  globalSearch = '';
  collectionSearch = '';
  requestSearch = '';
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
  requestName = '';
  requestDescription = '';
  requestMethod = 'GET';
  requestUrl = '';
  requestAuthType = '';
  requestAuthConfigJson = '';
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
  activityUserFilter = '';
  activityEventFilter = '';
  activityStatusFilter = '';
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
    this.requestHistory.set([]);
    this.dashboard.set(null);
    this.managerSummary.set(null);
    this.apiResponse.set(null);
    this.collectionRun.set(null);
    this.responseBody.set('');
    this.resetRequestEditor();
    this.activeView.set('dashboard');
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
    this.requestDetail.set(null);
    this.loadWorkspaces();
  }

  onWorkspaceChange(workspaceId: string): void {
    this.selectedWorkspaceId.set(workspaceId);
    this.selectedCollectionId.set('');
    this.selectedRequestId.set('');
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
    this.requestSearch = '';
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
    this.loadOrganizationSettings();
    this.loadAiConfig();
    this.loadAdvancedAnalytics();
    this.loadBillingOverview();
    this.loadApiKeys();
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
        this.loadRequestDetail();
      },
      error: () => this.showToast('Requests failed', 'Could not load collection requests.', 'danger')
    });
  }

  selectRequest(requestId: string): void {
    this.selectedRequestId.set(requestId);
    this.requestDetail.set(null);
    this.requestHistory.set([]);
    this.apiResponse.set(null);
    this.responseBody.set('');
    this.collectionRun.set(null);
    this.loadRequestDetail();
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
      next: (result) => this.members.set(result.data?.items ?? []),
      error: () => this.showToast('Team failed', 'Could not load organization members.', 'danger')
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
        this.showToast('Request complete', `${result.data.statusCode} in ${result.data.elapsedMs}ms.`, result.data.succeeded ? 'success' : 'danger');
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

  importCollectionFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.selectedWorkspaceId()) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      try {
        const payload = this.normalizeCollectionImport(JSON.parse(String(reader.result ?? '{}')), file.name);
        this.pageLoading.set(true);
        this.api.importCollection(this.selectedWorkspaceId(), payload).subscribe({
          next: (result) => {
            this.pageLoading.set(false);
            input.value = '';
            if (!result.succeeded || !result.data) {
              this.showToast('Import failed', result.message, 'danger');
              return;
            }
            this.selectedCollectionId.set(result.data.collectionId);
            this.selectedRequestId.set('');
            this.requestDetail.set(null);
            this.showToast('Collection imported', `${result.data.requestCount} requests imported.`, 'success');
            this.loadCollections();
            this.loadActivity();
          },
          error: (error) => {
            this.pageLoading.set(false);
            input.value = '';
            this.showToast('Import failed', error?.error?.message ?? 'The collection could not be imported.', 'danger');
          }
        });
      } catch (error) {
        input.value = '';
        this.showToast('Import failed', error instanceof Error ? error.message : 'Unsupported collection JSON.', 'danger');
      }
    };
    reader.readAsText(file);
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
    navigator.clipboard.writeText(this.responseBody() || this.apiResponse()?.body || '');
    this.showToast('Copied', 'Response body copied to clipboard.', 'success');
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

  responseHeaderEntries(): { key: string; value: string }[] {
    return Object.entries(this.apiResponse()?.headers ?? {}).map(([key, value]) => ({ key, value: value.join(', ') }));
  }

  responseCookieEntries(): { key: string; value: string }[] {
    return Object.entries(this.apiResponse()?.cookies ?? {}).map(([key, value]) => ({ key, value: value.join(', ') }));
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
  }

  private buildRequestPayload(versionNumber: number): SaveApiRequestPayload {
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

  private toEditableRows(items: KeyValueItem[]): EditableKeyValue[] {
    return items.map((item) => this.toEditableKeyValue(item.key, item.value, item.enabled, item.isSecret));
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

  private normalizeCollectionImport(raw: unknown, fallbackName: string): ImportCollectionPayload {
    const payload = raw as any;
    if (payload?.formatVersion && payload?.collection && Array.isArray(payload.requests)) {
      return {
        name: payload.collection.name ?? fallbackName,
        description: payload.collection.description,
        requests: payload.requests.map((request: any) => this.normalizeApiRequest(request))
      };
    }

    if (payload?.info && Array.isArray(payload.item)) {
      return {
        name: payload.info.name ?? fallbackName,
        description: payload.info.description,
        requests: this.flattenPostmanItems(payload.item)
      };
    }

    if (Array.isArray(payload?.requests)) {
      return {
        name: payload.name ?? fallbackName,
        description: payload.description,
        requests: payload.requests.map((request: any) => this.normalizeApiRequest(request))
      };
    }

    throw new Error('Only API DESK exports, simplified collection JSON, and Postman collection JSON are supported.');
  }

  private flattenPostmanItems(items: any[]): ImportApiRequestPayload[] {
    return items.flatMap((item) => {
      if (Array.isArray(item.item)) {
        return this.flattenPostmanItems(item.item);
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
        this.normalizeApiRequest({
          name: item.name ?? request.name ?? 'Imported request',
          description: request.description,
          method: request.method ?? 'GET',
          url,
          bodyType: request.body?.mode === 'raw' ? 'rawJson' : 'none',
          bodyContent: request.body?.raw ?? '',
          headers,
          queryParams,
          pathParams: []
        })
      ];
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
