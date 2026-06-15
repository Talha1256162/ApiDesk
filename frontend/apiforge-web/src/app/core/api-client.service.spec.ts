import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClientService } from './api-client.service';

describe('ApiClientService pagination', () => {
  let service: ApiClientService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [ApiClientService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ApiClientService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('sends collection pagination, search, and sorting params', () => {
    service.collections('workspace-1', {
      offset: 25,
      count: 25,
      searchString: 'payments',
      sorting: 'name asc'
    }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/workspaces/workspace-1/collections');
    expect(request.request.params.get('offset')).toBe('25');
    expect(request.request.params.get('count')).toBe('25');
    expect(request.request.params.get('searchString')).toBe('payments');
    expect(request.request.params.get('sorting')).toBe('name asc');
    request.flush({ succeeded: true, message: 'Success', data: { items: [], totalCount: 0, offset: 25, count: 25 }, errors: [] });
  });

  it('sends member page-size changes as backend count', () => {
    service.members('org-1', { offset: 48, count: 24 }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/organizations/org-1/members');
    expect(request.request.params.get('offset')).toBe('48');
    expect(request.request.params.get('count')).toBe('24');
    request.flush({ succeeded: true, message: 'Success', data: { items: [], totalCount: 0, offset: 48, count: 24 }, errors: [] });
  });

  it('resets searched beta feedback to the requested page contract', () => {
    service.betaFeedback('org-1', { offset: 0, count: 10, searchString: 'login', sorting: 'createdOn desc' }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/organizations/org-1/beta-feedback');
    expect(request.request.params.get('offset')).toBe('0');
    expect(request.request.params.get('count')).toBe('10');
    expect(request.request.params.get('searchString')).toBe('login');
    expect(request.request.params.get('sorting')).toBe('createdOn desc');
    request.flush({ succeeded: true, message: 'Success', data: { items: [], totalCount: 0, offset: 0, count: 10 }, errors: [] });
  });
});
