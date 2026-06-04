import { ImportApiRequestWithFolderPayload, ImportCollectionPayload } from './api.models';

export interface FallbackCollection {
  providerStatus: string;
  variables: string[];
  payload: ImportCollectionPayload;
}

export function generateFallbackCollection(input: string): FallbackCollection {
  const lower = input.toLowerCase();
  const folders: string[][] = [];
  const requests: ImportApiRequestWithFolderPayload[] = [];
  const add = (folder: string, name: string, method: string, url: string, body?: unknown) => {
    if (!folders.some((item) => item[0] === folder)) folders.push([folder]);
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
      timeoutMs: 30000,
      followRedirects: true,
      sslVerification: true,
      headers: folder === 'Auth' ? [] : [{ key: 'Authorization', value: 'Bearer {{token}}', enabled: true, isSecret: true }],
      queryParams: [],
      pathParams: []
    });
  };

  if (/(login|auth|register|signin|signup)/i.test(lower)) {
    add('Auth', 'Login user', 'POST', '{{baseUrl}}/auth/login', { email: '{{email}}', password: '{{password}}' });
  }
  if (/(profile|user|customer|parent|student|school)/i.test(lower)) {
    add('Users', 'Get profile', 'GET', '{{baseUrl}}/users/{{userId}}');
  }
  if (/(invoice|payment|voucher|merchant|fee|fees|pay|paisay|paisa|bill|receipt)/i.test(lower)) {
    add('Billing', 'Create invoice', 'POST', '{{baseUrl}}/merchants/{{merchantId}}/invoices', { customerId: '{{userId}}', amount: 2500 });
    add('Billing', 'Pay invoice', 'POST', '{{baseUrl}}/invoices/{{invoiceId}}/payments', { reference: '{{paymentReference}}' });
  }
  if (/(expense|approval|approve|request)/i.test(lower)) {
    add('Approvals', 'Submit approval request', 'POST', '{{baseUrl}}/approval-requests', { requesterId: '{{userId}}', amount: 1000 });
  }
  if (/(report|dashboard|analytics)/i.test(lower)) {
    add('Reports', 'Dashboard summary', 'GET', '{{baseUrl}}/reports/dashboard');
  }
  if (!requests.length) {
    add('API Flow', 'Health check', 'GET', '{{baseUrl}}/health');
  }

  const body = requests.map((request) => `${request.url}\n${request.bodyContent}`).join('\n');
  return {
    providerStatus: 'AI provider not configured. Deterministic fallback generated this runnable collection.',
    variables: unique(['baseUrl', 'token', ...extractVariables(body)]),
    payload: {
      name: titleFromFlow(input),
      description: `AI Agent Orchestra fallback collection. Source flow: ${input}`,
      folders,
      requests
    }
  };
}

function titleFromFlow(input: string): string {
  const cleaned = input.replace(/[^\w\s-]/g, ' ').trim().split(/\s+/).slice(0, 5).join(' ');
  return cleaned ? `${cleaned} API Flow` : 'AI Generated API Flow';
}

function extractVariables(value: string): string[] {
  return [...value.matchAll(/\{\{\s*([^{}\s]+)\s*\}\}/g)].map((match) => match[1]);
}

function unique(values: string[]): string[] {
  return [...new Set(values.filter(Boolean))];
}
