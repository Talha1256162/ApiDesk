import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from '../app';
import { ApiClientService } from './api-client.service';
import { generateFallbackCollection } from './ai-fallback-generator';
import { resolveTemplateVariables } from './environment-resolver';
import { parsePostmanCollectionV21 } from './postman-importer';
import { canEditWorkspaceContent, canManageMember } from './team-permissions';

describe('API Desk product helpers', () => {
  it('resolves environment variables and reports missing values', () => {
    const result = resolveTemplateVariables('{{baseUrl}}/users/{{userId}}/{{missing}}', {
      baseUrl: 'https://api.example.com',
      userId: '123'
    });

    expect(result.value).toBe('https://api.example.com/users/123/{{missing}}');
    expect(result.missing).toEqual(['missing']);
  });

  it('parses Postman v2.1 folders and requests', () => {
    const preview = parsePostmanCollectionV21(JSON.stringify({
      info: { name: 'Sample API', schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json' },
      variable: [{ key: 'baseUrl' }],
      item: [
        {
          name: 'Auth',
          item: [
            {
              name: 'Login',
              request: {
                method: 'POST',
                url: { raw: '{{baseUrl}}/auth/login' },
                header: [{ key: 'Authorization', value: 'Bearer {{token}}' }],
                body: { mode: 'raw', raw: '{"email":"{{email}}"}' },
                auth: { type: 'bearer' }
              },
              event: [{ listen: 'test' }]
            }
          ]
        }
      ]
    }));

    expect(preview.collectionName).toBe('Sample API');
    expect(preview.folderCount).toBe(1);
    expect(preview.requestCount).toBe(1);
    expect(preview.authTypes).toContain('Bearer');
    expect(preview.variables).toContain('baseUrl');
    expect(preview.scriptsDetected).toBe(1);
    expect(preview.payload.requests[0].folderPath).toEqual(['Auth']);
    expect(preview.payload.requests[0].authType).toBe('Bearer');
    expect(preview.payload.requests[0].bodyType).toBe('rawJson');
  });

  it('generates practical fallback collections for Roman Urdu payment flows', () => {
    const collection = generateFallbackCollection('School parent fees ka bill pay kare aur receipt mile');

    expect(collection.payload.requests.length).toBeGreaterThan(1);
    expect(collection.payload.requests.some((request) => request.name.includes('Pay invoice'))).toBeTrue();
    expect(collection.variables).toContain('baseUrl');
  });

  it('applies team permission rules', () => {
    expect(canManageMember('Owner', 'Owner', 'remove')).toBeTrue();
    expect(canManageMember('Admin', 'Owner', 'remove')).toBeFalse();
    expect(canEditWorkspaceContent('Editor')).toBeTrue();
    expect(canEditWorkspaceContent('Viewer')).toBeFalse();
  });
});

describe('ApiClientService', () => {
  let http: HttpTestingController;
  let api: ApiClientService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [ApiClientService, provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(ApiClientService);
  });

  afterEach(() => http.verify());

  it('posts login credentials to the backend auth endpoint', () => {
    api.login('admin@apiforge.local', 'Admin@12345').subscribe();
    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.email).toBe('admin@apiforge.local');
    request.flush({ succeeded: false, message: 'invalid', data: null, errors: [] });
  });

  it('sends saved requests through the backend runner endpoint', () => {
    api.sendRequest('request-1', 'env-1').subscribe();
    const request = http.expectOne('/api/requests/request-1/send');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.environmentId).toBe('env-1');
    request.flush({ succeeded: true, message: 'Success', data: {}, errors: [] });
  });

  it('refreshes and stores a new auth session', () => {
    localStorage.setItem('apiforge.refreshToken', 'refresh-1');
    api.refreshSession().subscribe((session) => {
      expect(session.accessToken).toBe('access-2');
    });

    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.refreshToken).toBe('refresh-1');
    request.flush({
      succeeded: true,
      message: 'Success',
      data: {
        user: { id: 'user-1', email: 'admin@apiforge.local', fullName: 'Admin' },
        organizationId: 'org-1',
        workspaceId: 'workspace-1',
        accessToken: 'access-2',
        refreshToken: 'refresh-2',
        accessTokenExpiresOnUtc: new Date().toISOString(),
        refreshTokenExpiresOnUtc: new Date().toISOString()
      },
      errors: []
    });

    expect(localStorage.getItem('apiforge.accessToken')).toBe('access-2');
    expect(localStorage.getItem('apiforge.refreshToken')).toBe('refresh-2');
  });

  it('revokes refresh token on logout and clears local session', () => {
    localStorage.setItem('apiforge.accessToken', 'access-1');
    localStorage.setItem('apiforge.refreshToken', 'refresh-1');

    api.logout();
    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.refreshToken).toBe('refresh-1');
    request.flush({ succeeded: true, message: 'Success', data: null, errors: [] });

    expect(localStorage.getItem('apiforge.accessToken')).toBeNull();
    expect(localStorage.getItem('apiforge.refreshToken')).toBeNull();
  });
});

describe('App collection and request search', () => {
  let app: App;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    app = TestBed.createComponent(App).componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('typing in Search collections filters collection list by collection metadata', () => {
    app.collections.set([
      collection('collection-1', 'Identity APIs', 'Authentication and profile endpoints', 'Talha'),
      collection('collection-2', 'Billing APIs', 'Invoices and vouchers', 'Finance Team')
    ]);

    app.collectionSearch.set('billing');
    expect(app.filteredCollections().map((item) => item.name)).toEqual(['Billing APIs']);

    app.collectionSearch.set('talha');
    expect(app.filteredCollections().map((item) => item.name)).toEqual(['Identity APIs']);
  });

  it('typing in Search requests in collection filters request list by request name', () => {
    app.requests.set([
      request('request-1', 'Login user', 'POST', '{{baseUrl}}/auth/login', 'Auth'),
      request('request-2', 'Get profile', 'GET', '{{baseUrl}}/users/me', 'Users')
    ]);

    app.requestSearch.set('profile');
    expect(app.filteredRequests().map((item) => item.name)).toEqual(['Get profile']);
  });

  it('request method search works', () => {
    app.requests.set([
      request('request-1', 'Create invoice', 'POST', '{{baseUrl}}/invoices', 'Billing'),
      request('request-2', 'List invoices', 'GET', '{{baseUrl}}/invoices', 'Billing')
    ]);

    app.requestSearch.set('post');
    expect(app.filteredRequests().map((item) => item.name)).toEqual(['Create invoice']);
  });

  it('request URL search works', () => {
    app.requests.set([
      request('request-1', 'Pay invoice', 'POST', '{{baseUrl}}/payments/invoice', 'Billing'),
      request('request-2', 'Get voucher', 'GET', '{{baseUrl}}/vouchers/latest', 'Vouchers')
    ]);

    app.requestSearch.set('vouchers');
    expect(app.filteredRequests().map((item) => item.name)).toEqual(['Get voucher']);
  });

  it('request folder name search works', () => {
    app.requests.set([
      request('request-1', 'Create customer', 'POST', '{{baseUrl}}/customers', 'Customers'),
      request('request-2', 'Create merchant', 'POST', '{{baseUrl}}/merchants', 'Merchant Onboarding')
    ]);

    app.requestSearch.set('merchant onboarding');
    expect(app.filteredRequests().map((item) => item.name)).toEqual(['Create merchant']);
  });

  it('collection search can match loaded requests in the selected collection', () => {
    app.collections.set([
      collection('collection-1', 'Platform APIs', 'Core platform', 'Talha'),
      collection('collection-2', 'Reporting APIs', 'Dashboard data', 'Team')
    ]);
    app.selectedCollectionId.set('collection-1');
    app.requests.set([
      request('request-1', 'Create voucher', 'POST', '{{baseUrl}}/vouchers', 'Rewards')
    ]);

    app.collectionSearch.set('voucher');
    expect(app.filteredCollections().map((item) => item.name)).toEqual(['Platform APIs']);
  });

  it('sidebar groups start collapsed and toggle expanded state into localStorage', () => {
    const apiGroup = app.navSections.flatMap((section) => section.groups ?? []).find((group) => group.id === 'api-client');
    expect(apiGroup).toBeTruthy();
    expect(app.isNavGroupExpanded(apiGroup!)).toBeFalse();

    app.toggleNavGroup(apiGroup!);

    expect(app.isNavGroupExpanded(apiGroup!)).toBeTrue();
    expect(JSON.parse(localStorage.getItem('apiforge.sidebar.expandedGroups') ?? '[]')).toContain('api-client');
    expect(app.activeView()).toBe('api-client');
  });

  it('sidebar restores expanded groups from localStorage', () => {
    localStorage.setItem('apiforge.sidebar.expandedGroups', JSON.stringify(['tools']));
    const restored = TestBed.createComponent(App).componentInstance;
    const toolsGroup = restored.navSections.flatMap((section) => section.groups ?? []).find((group) => group.id === 'tools');

    expect(toolsGroup).toBeTruthy();
    expect(restored.isNavGroupExpanded(toolsGroup!)).toBeTrue();
  });

  it('active child route keeps its parent group expanded', () => {
    const teamGroup = app.navSections.flatMap((section) => section.groups ?? []).find((group) => group.id === 'team');
    expect(teamGroup).toBeTruthy();

    app.selectView('governance');

    expect(app.isNavGroupExpanded(teamGroup!)).toBeTrue();
    expect(app.navGroupHasActiveView(teamGroup!)).toBeTrue();
  });

  it('body type none exposes an empty body state instead of a structured payload', () => {
    app.onBodyTypeChange('none');

    expect(app.requestBodyType).toBe('none');
    expect(app.isRawBodyType()).toBeFalse();
    expect(app.isStructuredBodyType()).toBeFalse();
    expect(app.requestTabBadge('Body')).toBe('');
  });

  it('raw JSON body editor mode formats JSON without changing send behavior fields', () => {
    app.onBodyTypeChange('rawJson');
    app.requestBodyContent = '{"name":"Talha","active":true}';

    app.beautifyRequestJson();

    expect(app.isRawBodyType()).toBeTrue();
    expect(app.requestBodyLanguage()).toBe('json');
    expect(app.requestBodyContent).toContain('\n  "name": "Talha"');
    expect(app.requestTabBadge('Body')).toBe('1');
  });

  it('form-data body rows sync into request body content', () => {
    app.onBodyTypeChange('formData');
    app.requestBodyRows[0].key = 'customerId';
    app.requestBodyRows[0].value = '123';
    app.syncBodyContentFromRows();

    expect(app.isStructuredBodyType()).toBeTrue();
    expect(app.requestBodyContent).toBe('customerId=123');
    expect(app.requestTabBadge('Body')).toBe('1');
  });

  it('x-www-form-urlencoded body rows hydrate from saved body content', () => {
    app.requestBodyContent = 'email=admin@example.com\npassword=secret';
    app.onBodyTypeChange('formUrlEncoded');

    expect(app.requestBodyRows.length).toBe(2);
    expect(app.requestBodyRows[0].key).toBe('email');
    expect(app.requestBodyRows[1].isSecret).toBeTrue();
  });

  it('response tone maps HTTP status ranges to visual states', () => {
    app.apiResponse.set({ statusCode: 201, statusText: 'Created', body: '{}', headers: {}, cookies: {}, elapsedMs: 20, sizeBytes: 2, succeeded: true } as never);
    expect(app.responseTone()).toBe('success');

    app.apiResponse.set({ statusCode: 404, statusText: 'Not Found', body: '', headers: {}, cookies: {}, elapsedMs: 20, sizeBytes: 0, succeeded: false } as never);
    expect(app.responseTone()).toBe('client-error');
  });

  it('creates a demo workspace with collection, environment, variables, and requests', () => {
    app.selectedOrganizationId.set('org-1');
    app.createDemoWorkspace();

    const workspaceRequest = http.expectOne('/api/workspaces');
    expect(workspaceRequest.request.method).toBe('POST');
    expect(workspaceRequest.request.body.name).toBe('Demo Workspace');
    workspaceRequest.flush({
      succeeded: true,
      message: 'Success',
      data: {
        id: 'demo-workspace',
        organizationId: 'org-1',
        name: 'Demo Workspace',
        slug: 'demo-workspace',
        type: 'Team',
        description: 'Demo workspace - safe to delete after exploring API Desk.',
        createdOn: new Date().toISOString()
      },
      errors: []
    });

    const importRequest = http.expectOne('/api/workspaces/demo-workspace/collections/import');
    expect(importRequest.request.method).toBe('POST');
    expect(importRequest.request.body.name).toBe('API Desk Demo Collection');
    expect(importRequest.request.body.requests.map((item: { name: string }) => item.name)).toEqual([
      'GET Demo Users',
      'POST Create Demo User',
      'GET Demo Invoice',
      'POST Demo Payment'
    ]);
    importRequest.flush({
      succeeded: true,
      message: 'Success',
      data: { collectionId: 'demo-collection', name: 'API Desk Demo Collection', requestCount: 4 },
      errors: []
    });

    const environmentRequest = http.expectOne('/api/environments');
    expect(environmentRequest.request.method).toBe('POST');
    expect(environmentRequest.request.body.name).toBe('Demo Environment');
    environmentRequest.flush({
      succeeded: true,
      message: 'Success',
      data: {
        id: 'demo-environment',
        workspaceId: 'demo-workspace',
        name: 'Demo Environment',
        isDefault: true,
        variableCount: 0,
        secretCount: 0,
        versionNumber: 1,
        createdOn: new Date().toISOString()
      },
      errors: []
    });

    const variablesRequest = http.expectOne('/api/environments/demo-environment/variables');
    expect(variablesRequest.request.method).toBe('PUT');
    expect(variablesRequest.request.body.variables.map((item: { key: string }) => item.key)).toEqual(['baseUrl', 'apiKey', 'merchantId', 'userId']);
    variablesRequest.flush({ succeeded: true, message: 'Success', data: [], errors: [] });

    expect(app.selectedWorkspaceId()).toBe('demo-workspace');
    expect(app.selectedCollectionId()).toBe('demo-collection');
    expect(app.selectedEnvironmentId()).toBe('demo-environment');

    flushPendingAppReloads(http);
  });

  it('deletes a clearly marked demo workspace through the workspace API', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    app.workspaces.set([collectionWorkspace('demo-workspace', 'Demo Workspace')]);
    app.selectedWorkspaceId.set('demo-workspace');

    app.deleteDemoWorkspace('demo-workspace');

    const deleteRequest = http.expectOne('/api/workspaces/demo-workspace');
    expect(deleteRequest.request.method).toBe('DELETE');
    deleteRequest.flush({ succeeded: true, message: 'Success', data: null, errors: [] });

    expect(app.selectedWorkspaceId()).toBe('');
    flushPendingAppReloads(http);
  });
});

function collection(id: string, name: string, description: string, ownerName: string) {
  return {
    id,
    workspaceId: 'workspace-1',
    name,
    description,
    ownerUserId: 'user-1',
    ownerName,
    requestCount: 1,
    versionNumber: 1,
    createdOn: new Date().toISOString()
  };
}

function request(id: string, name: string, method: string, url: string, folderName?: string) {
  return {
    id,
    collectionId: 'collection-1',
    folderId: folderName ? `${folderName.toLowerCase().replace(/\s+/g, '-')}-folder` : undefined,
    folderName,
    name,
    method,
    url,
    modifiedOn: new Date().toISOString()
  };
}

function collectionWorkspace(id: string, name: string) {
  return {
    id,
    organizationId: 'org-1',
    name,
    slug: name.toLowerCase().replace(/\s+/g, '-'),
    type: 'Team',
    description: 'Demo workspace - safe to delete after exploring API Desk.',
    createdOn: new Date().toISOString()
  };
}

function flushPendingAppReloads(http: HttpTestingController) {
  for (let i = 0; i < 6; i++) {
    const pending = http.match(() => true);
    if (!pending.length) {
      return;
    }
    for (const request of pending) {
      const url = request.request.url;
      if (url.includes('/dashboard') || url.includes('/manager-summary')) {
        request.flush({ succeeded: true, message: 'Success', data: null, errors: [] });
      } else if (url.includes('/roles')) {
        request.flush({ succeeded: true, message: 'Success', data: [], errors: [] });
      } else if (url.includes('/members') || url.includes('/invites') || url.includes('/mock-servers') || url.includes('/monitors') || url.includes('/published-docs') || url.includes('/api-specs') || url.includes('/analytics') || url.includes('/api-keys')) {
        request.flush({ succeeded: true, message: 'Success', data: [], errors: [] });
      } else if (url.includes('/billing') || url.includes('/settings') || url.includes('/build-info')) {
        request.flush({ succeeded: true, message: 'Success', data: null, errors: [] });
      } else {
        request.flush({ succeeded: true, message: 'Success', data: { items: [], total: 0 }, errors: [] });
      }
    }
  }
}
