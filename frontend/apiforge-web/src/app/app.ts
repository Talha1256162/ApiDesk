import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClientService } from './core/api-client.service';
import {
  ActivityEvent,
  ApiRequestDetail,
  ApiRequestSummary,
  ApiResponse,
  Collection,
  EnvironmentModel,
  ManagerSummary,
  Organization,
  OrganizationMember,
  Workspace,
  WorkspaceDashboard
} from './core/api.models';
import { DeveloperToolsService, JsonDiff, JsonStats } from './features/developer-tools/developer-tools.service';
import { MonacoEditorComponent } from './shared/monaco-editor.component';
import { BadgeComponent } from './shared/ui/badge.component';
import { EmptyStateComponent } from './shared/ui/empty-state.component';
import { SkeletonComponent } from './shared/ui/skeleton.component';
import { StatCardComponent } from './shared/ui/stat-card.component';
import { ToastComponent } from './shared/ui/toast.component';

type ViewKey =
  | 'dashboard'
  | 'workspaces'
  | 'collections'
  | 'api-client'
  | 'environments'
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
type ResponseTab = 'Body' | 'Headers' | 'Cookies' | 'Timeline';

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorComponent,
    BadgeComponent,
    EmptyStateComponent,
    SkeletonComponent,
    StatCardComponent,
    ToastComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  readonly productName = signal('ApiForge Pro');
  readonly activeView = signal<ViewKey>('dashboard');
  readonly shellLoading = signal(false);
  readonly pageLoading = signal(false);
  readonly authLoading = signal(false);
  readonly commandOpen = signal(false);
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
  readonly members = signal<OrganizationMember[]>([]);
  readonly dashboard = signal<WorkspaceDashboard | null>(null);
  readonly managerSummary = signal<ManagerSummary | null>(null);
  readonly apiResponse = signal<ApiResponse | null>(null);
  readonly responseBody = signal('');
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
  readonly currentUserName = computed(() => this.api.auth()?.user.fullName || 'ApiForge user');
  readonly currentUserInitials = computed(() =>
    this.currentUserName()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || 'AF'
  );
  readonly activeViewTitle = computed(() => this.navItems.find((item) => item.key === this.activeView())?.label ?? 'Command Center');
  readonly jsonTabs = ['Beautify', 'Validate', 'Tree View', 'Minify', 'Compare', 'Convert', 'Schema'] as const;
  readonly requestConfigTabs: RequestConfigTab[] = ['Params', 'Auth', 'Headers', 'Body', 'Tests', 'Settings'];
  readonly responseTabs: ResponseTab[] = ['Body', 'Headers', 'Cookies', 'Timeline'];
  readonly navSections: NavSection[] = [
    {
      title: 'Command',
      items: [
        { key: 'dashboard', label: 'Command Center', hint: 'Overview', icon: 'grid' },
        { key: 'api-client', label: 'API Workbench', hint: 'Runner', icon: 'bolt' },
        { key: 'collections', label: 'Collections', hint: 'Library', icon: 'stack' },
        { key: 'environments', label: 'Environments', hint: 'Variables', icon: 'sliders' }
      ]
    },
    {
      title: 'Intelligence',
      items: [
        { key: 'activity', label: 'Activity Feed', hint: 'Audit', icon: 'pulse' },
        { key: 'reports', label: 'Executive Reports', hint: 'Insights', icon: 'chart' },
        { key: 'team', label: 'Team & RBAC', hint: 'Members', icon: 'users' }
      ]
    },
    {
      title: 'Developer Suite',
      items: [
        { key: 'json-tools', label: 'JSON Tools', hint: 'Utilities', icon: 'braces' },
        { key: 'dev-tools', label: 'Developer Tools', hint: 'Toolkit', icon: 'terminal' }
      ]
    },
    {
      title: 'Enterprise',
      items: [
        { key: 'workspaces', label: 'Workspaces', hint: 'Teams', icon: 'building' },
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
  utilityTool = 'Base64';
  utilityPattern = '';
  utilityFlags = 'g';
  hashAlgorithm: 'SHA-1' | 'SHA-256' | 'SHA-384' | 'SHA-512' = 'SHA-256';
  utilityInput = '';
  utilityOutput = '';

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
    this.members.set([]);
    this.dashboard.set(null);
    this.managerSummary.set(null);
    this.apiResponse.set(null);
    this.responseBody.set('');
    this.activeView.set('dashboard');
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
    this.loadWorkspaceData();
  }

  onCollectionChange(collectionId: string): void {
    this.selectedCollectionId.set(collectionId);
    this.selectedRequestId.set('');
    this.requestDetail.set(null);
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
    this.loadMembers();
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
    this.apiResponse.set(null);
    this.responseBody.set('');
    this.loadRequestDetail();
  }

  loadRequestDetail(): void {
    if (!this.selectedRequestId()) {
      this.requestDetail.set(null);
      return;
    }

    this.api.requestDetail(this.selectedRequestId()).subscribe({
      next: (result) => this.requestDetail.set(result.data),
      error: () => this.showToast('Request detail failed', 'Could not load request configuration.', 'danger')
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

  loadMembers(): void {
    if (!this.selectedOrganizationId()) {
      return;
    }

    this.api.members(this.selectedOrganizationId()).subscribe({
      next: (result) => this.members.set(result.data?.items ?? []),
      error: () => this.showToast('Team failed', 'Could not load organization members.', 'danger')
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
      },
      error: (error) => {
        this.pageLoading.set(false);
        this.showToast('Request failed', error?.error?.message ?? 'The request could not be sent.', 'danger');
      }
    });
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
