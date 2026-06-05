import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
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
