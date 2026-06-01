import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiClientService } from './core/api-client.service';
import {
  ActivityEvent,
  ApiRequestSummary,
  ApiResponse,
  Collection,
  EnvironmentModel,
  ManagerSummary,
  Organization,
  Workspace,
  WorkspaceDashboard
} from './core/api.models';
import { DeveloperToolsService, JsonDiff, JsonStats } from './features/developer-tools/developer-tools.service';
import { MonacoEditorComponent } from './shared/monaco-editor.component';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, MonacoEditorComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  readonly productName = signal('ApiForge Pro');
  readonly activeView = signal<'dashboard' | 'client' | 'environments' | 'activity' | 'team' | 'json-tools' | 'dev-tools' | 'settings'>('dashboard');
  readonly loading = signal(false);
  readonly toast = signal('');
  readonly commandOpen = signal(false);
  readonly darkMode = signal(true);
  readonly isSignedIn = computed(() => this.api.isAuthenticated());
  readonly jsonTabs = ['Beautify', 'Validate', 'Tree View', 'Minify', 'Compare', 'Convert', 'Schema'] as const;

  loginEmail = '';
  loginPassword = '';
  registerMode = false;
  registerFullName = '';
  registerOrganization = '';
  registerWorkspace = '';

  organizations: Organization[] = [];
  workspaces: Workspace[] = [];
  collections: Collection[] = [];
  requests: ApiRequestSummary[] = [];
  environments: EnvironmentModel[] = [];
  activity: ActivityEvent[] = [];
  selectedOrganizationId = '';
  selectedWorkspaceId = '';
  selectedCollectionId = '';
  selectedRequestId = '';
  selectedEnvironmentId = '';
  dashboard?: WorkspaceDashboard;
  managerSummary?: ManagerSummary;
  apiResponse?: ApiResponse;
  responseBody = '';
  globalSearch = '';

  jsonTab: 'Beautify' | 'Validate' | 'Tree View' | 'Minify' | 'Compare' | 'Convert' | 'Schema' = 'Beautify';
  jsonInput = '';
  jsonCompareInput = '';
  jsonOutput = '';
  jsonError = '';
  jsonStats?: JsonStats;
  jsonDiffs: JsonDiff[] = [];
  jsonPath = '$';
  utilityTool = 'Base64';
  utilityInput = '';
  utilityOutput = '';

  constructor(
    readonly api: ApiClientService,
    private readonly developerTools: DeveloperToolsService,
    private readonly changeDetector: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (this.api.accessToken) {
      this.loadOrganizations();
    }
  }

  submitAuth(): void {
    this.loading.set(true);
    const request = this.registerMode
      ? this.api.register({
          email: this.loginEmail,
          password: this.loginPassword,
          fullName: this.registerFullName,
          organizationName: this.registerOrganization,
          workspaceName: this.registerWorkspace
        })
      : this.api.login(this.loginEmail, this.loginPassword);

    request.subscribe({
      next: (result) => {
        this.loading.set(false);
        if (!result.succeeded) {
          this.showToast(result.message);
          return;
        }
        this.api.setSession(result.data);
        this.selectedOrganizationId = result.data.organizationId;
        this.selectedWorkspaceId = result.data.workspaceId ?? '';
        this.loadOrganizations();
      },
      error: (error) => {
        this.loading.set(false);
        this.showToast(error?.error?.message ?? 'Authentication failed.');
      }
    });
  }

  logout(): void {
    this.api.logout();
    this.organizations = [];
    this.workspaces = [];
    this.collections = [];
    this.requests = [];
    this.environments = [];
    this.activity = [];
    this.dashboard = undefined;
    this.managerSummary = undefined;
  }

  loadOrganizations(): void {
    this.loading.set(true);
    this.api.organizations().subscribe({
      next: (result) => {
        this.organizations = result.data ?? [];
        this.selectedOrganizationId ||= this.organizations[0]?.id ?? '';
        this.loading.set(false);
        if (this.selectedOrganizationId) {
          this.loadWorkspaces();
        }
      },
      error: () => {
        this.loading.set(false);
        this.showToast('Could not load organizations.');
      }
    });
  }

  loadWorkspaces(): void {
    if (!this.selectedOrganizationId) {
      return;
    }
    this.loading.set(true);
    this.api.workspaces(this.selectedOrganizationId).subscribe({
      next: (result) => {
        this.workspaces = result.data?.items ?? [];
        this.selectedWorkspaceId ||= this.workspaces[0]?.id ?? '';
        this.loading.set(false);
        this.loadWorkspaceData();
      },
      error: () => {
        this.loading.set(false);
        this.showToast('Could not load workspaces.');
      }
    });
  }

  loadWorkspaceData(): void {
    if (!this.selectedWorkspaceId) {
      return;
    }
    this.loadDashboard();
    this.loadCollections();
    this.loadEnvironments();
    this.loadActivity();
  }

  loadDashboard(): void {
    this.api.workspaceDashboard(this.selectedWorkspaceId).subscribe((result) => (this.dashboard = result.data));
    this.api.managerSummary(this.selectedWorkspaceId).subscribe((result) => (this.managerSummary = result.data));
  }

  loadCollections(): void {
    this.api.collections(this.selectedWorkspaceId).subscribe((result) => {
      this.collections = result.data?.items ?? [];
      this.selectedCollectionId ||= this.collections[0]?.id ?? '';
      if (this.selectedCollectionId) {
        this.loadRequests();
      }
    });
  }

  loadRequests(): void {
    this.api.collectionRequests(this.selectedCollectionId).subscribe((result) => {
      this.requests = result.data ?? [];
      this.selectedRequestId ||= this.requests[0]?.id ?? '';
    });
  }

  loadEnvironments(): void {
    this.api.environments(this.selectedWorkspaceId).subscribe((result) => {
      this.environments = result.data?.items ?? [];
      this.selectedEnvironmentId ||= this.environments.find((environment) => environment.isDefault)?.id ?? this.environments[0]?.id ?? '';
    });
  }

  loadActivity(): void {
    this.api.activity(this.selectedOrganizationId, this.selectedWorkspaceId).subscribe((result) => (this.activity = result.data?.items ?? []));
  }

  sendSelectedRequest(): void {
    if (!this.selectedRequestId) {
      this.showToast('Select a request first.');
      return;
    }
    this.loading.set(true);
    this.api.sendRequest(this.selectedRequestId, this.selectedEnvironmentId).subscribe({
      next: (result) => {
        this.loading.set(false);
        if (!result.succeeded) {
          this.showToast(result.message);
          return;
        }
        this.apiResponse = result.data;
        this.responseBody = this.tryFormatJson(result.data.body);
        this.changeDetector.detectChanges();
        this.loadDashboard();
        this.loadActivity();
      },
      error: (error) => {
        this.loading.set(false);
        this.showToast(error?.error?.message ?? 'Request failed.');
      }
    });
  }

  selectedRequest(): ApiRequestSummary | undefined {
    return this.requests.find((request) => request.id === this.selectedRequestId);
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
    this.showToast('Copied.');
  }

  runUtility(): void {
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
      } else {
        this.utilityOutput = 'Placeholder ready for implementation.';
      }
    } catch (error) {
      this.utilityOutput = error instanceof Error ? error.message : 'Utility failed.';
    }
  }

  toggleTheme(): void {
    this.darkMode.update((value) => !value);
    document.documentElement.classList.toggle('light', !this.darkMode());
  }

  private tryFormatJson(value: string): string {
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  private showToast(message: string): void {
    this.toast.set(message);
    window.setTimeout(() => this.toast.set(''), 3000);
  }
}
