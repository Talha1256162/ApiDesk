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

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    app = TestBed.createComponent(App).componentInstance;
  });

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
