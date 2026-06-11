import { ImportApiRequestWithFolderPayload, ImportCollectionPayload } from './api.models';

export interface PostmanImportPreview {
  collectionName: string;
  folderCount: number;
  requestCount: number;
  authTypes: string[];
  variables: string[];
  scriptsDetected: number;
  unsupportedItems: string[];
  payload: ImportCollectionPayload;
}

type PostmanItem = {
  name?: string;
  item?: PostmanItem[];
  request?: {
    method?: string;
    url?: string | { raw?: string; query?: { key?: string; value?: string; disabled?: boolean }[] };
    header?: { key?: string; value?: string; disabled?: boolean }[];
    auth?: { type?: string };
    body?: {
      mode?: string;
      raw?: string;
      options?: { raw?: { language?: string } };
      urlencoded?: { key?: string; value?: string; disabled?: boolean }[];
      formdata?: { key?: string; value?: string; disabled?: boolean }[];
    };
  };
  event?: unknown[];
};

type PostmanBody = NonNullable<NonNullable<PostmanItem['request']>['body']>;

export function parsePostmanCollectionV21(source: string): PostmanImportPreview {
  let root: { info?: { name?: string; schema?: string }; item?: PostmanItem[]; variable?: { key?: string }[] };
  try {
    root = JSON.parse(source);
  } catch (error) {
    throw new Error(error instanceof Error ? `Invalid JSON: ${error.message}` : 'Invalid JSON.');
  }

  if (!root.info?.schema?.includes('v2.1.0') || !Array.isArray(root.item)) {
    throw new Error('Unsupported Postman collection. Apeiron expects Postman Collection v2.1 JSON.');
  }

  const folders: string[][] = [];
  const requests: ImportApiRequestWithFolderPayload[] = [];
  const authTypes = new Set<string>();
  const variables = new Set<string>((root.variable ?? []).map((item) => item.key ?? '').filter(Boolean));
  let scriptsDetected = 0;

  const walk = (items: PostmanItem[], path: string[]) => {
    for (const item of items) {
      if (item.event?.length) scriptsDetected += item.event.length;
      if (Array.isArray(item.item)) {
        const nextPath = [...path, item.name || 'Folder'];
        folders.push(nextPath);
        walk(item.item, nextPath);
        continue;
      }

      if (!item.request) continue;
      const url = typeof item.request.url === 'string' ? item.request.url : item.request.url?.raw ?? '';
      extractVariables(`${url} ${JSON.stringify(item.request.body ?? {})}`).forEach((key) => variables.add(key));
      if (item.request.auth?.type) authTypes.add(normalizeAuthType(item.request.auth.type) ?? item.request.auth.type);
      requests.push({
        folderPath: path.length ? path : undefined,
        name: item.name || `${item.request.method || 'GET'} request`,
        method: (item.request.method || 'GET').toUpperCase(),
        url,
        authType: normalizeAuthType(item.request.auth?.type),
        authConfigJson: item.request.auth ? JSON.stringify(item.request.auth) : undefined,
        bodyType: normalizeBodyType(item.request.body),
        bodyContent: normalizeBodyContent(item.request.body),
        timeoutMs: 30000,
        followRedirects: true,
        sslVerification: true,
        headers: (item.request.header ?? []).map((header) => ({ key: header.key ?? '', value: header.value ?? '', enabled: !header.disabled, isSecret: /token|secret|password|authorization|cookie/i.test(header.key ?? '') })),
        queryParams: typeof item.request.url === 'object'
          ? (item.request.url.query ?? []).map((query) => ({ key: query.key ?? '', value: query.value ?? '', enabled: !query.disabled, isSecret: false }))
          : [],
        pathParams: []
      });
    }
  };

  walk(root.item, []);

  return {
    collectionName: root.info.name || 'Imported Postman Collection',
    folderCount: folders.length,
    requestCount: requests.length,
    authTypes: [...authTypes],
    variables: [...variables],
    scriptsDetected,
    unsupportedItems: [],
    payload: {
      name: root.info.name || 'Imported Postman Collection',
      folders,
      requests
    }
  };
}

function extractVariables(value: string): string[] {
  return [...value.matchAll(/\{\{\s*([^{}\s]+)\s*\}\}/g)].map((match) => match[1]);
}

function normalizeBodyType(body: PostmanBody | undefined): string {
  if (!body?.mode) return 'none';
  if (body.mode === 'raw') {
    return body.options?.raw?.language?.toLowerCase() === 'json' || looksLikeJson(body.raw ?? '') ? 'rawJson' : 'rawText';
  }
  if (body.mode === 'urlencoded') return 'formUrlEncoded';
  if (body.mode === 'formdata') return 'formData';
  return 'rawText';
}

function normalizeBodyContent(body: PostmanBody | undefined): string {
  if (!body?.mode) return '';
  if (body.mode === 'raw') return body.raw ?? '';
  if (body.mode === 'urlencoded') {
    return (body.urlencoded ?? []).filter((item: { key?: string; value?: string; disabled?: boolean }) => item.disabled !== true && item.key).map((item: { key?: string; value?: string }) => `${item.key}=${item.value ?? ''}`).join('\n');
  }
  if (body.mode === 'formdata') {
    return (body.formdata ?? []).filter((item: { key?: string; value?: string; disabled?: boolean }) => item.disabled !== true && item.key).map((item: { key?: string; value?: string }) => `${item.key}=${item.value ?? ''}`).join('\n');
  }
  return body.raw ?? '';
}

function normalizeAuthType(type?: string): string | undefined {
  if (!type) return undefined;
  if (type === 'bearer') return 'Bearer';
  if (type === 'basic') return 'Basic';
  if (type === 'apikey') return 'ApiKey';
  if (type === 'oauth2') return 'OAuth2';
  return type;
}

function looksLikeJson(value: string): boolean {
  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}
