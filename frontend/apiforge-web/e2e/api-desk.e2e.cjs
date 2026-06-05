const { chromium } = require('playwright');

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:4200';
const email = process.env.E2E_EMAIL || 'admin@apiforge.local';
const password = process.env.E2E_PASSWORD || 'Admin@12345';

const checks = [];

async function check(name, fn) {
  const started = Date.now();
  try {
    await fn();
    checks.push({ name, status: 'PASS', ms: Date.now() - started });
  } catch (error) {
    checks.push({ name, status: 'FAIL', ms: Date.now() - started, error: error.message });
  }
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function visible(page, text) {
  try {
    await page.getByText(text, { exact: false }).first().waitFor({ state: 'visible', timeout: 12000 });
    return true;
  } catch {
    return false;
  }
}

async function clickFirst(page, locator) {
  await locator.first().click({ timeout: 12000 });
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 950 } });
  const consoleErrors = [];
  const failedRequests = [];

  page.on('console', (message) => {
    if (message.type() === 'error') {
      const text = message.text();
      if (!/Failed to load resource.*(favicon|auth\/login|400 \(Bad Request\))/i.test(text)) {
        consoleErrors.push(text);
      }
    }
  });

  page.on('requestfailed', (request) => {
    const url = request.url();
    const failure = request.failure()?.errorText || '';
    if (!url.includes('/sockjs-node') && !url.includes('/favicon') && !url.includes('/api/auth/login') && !/ERR_ABORTED/i.test(failure)) {
      failedRequests.push(`${request.method()} ${url} ${failure}`);
    }
  });

  await check('public landing page loads', async () => {
    await page.goto(baseUrl, { waitUntil: 'networkidle' });
    assert(await visible(page, 'Lightweight AI-powered Postman alternative'), 'Landing hero not visible.');
    assert(await visible(page, 'Postman import'), 'Landing feature grid not visible.');
  });

  await check('pricing page loads', async () => {
    await clickFirst(page, page.getByRole('button', { name: 'Pricing' }));
    assert(await visible(page, 'Pricing'), 'Pricing content not visible.');
    assert(await visible(page, 'Free beta'), 'Free beta plan not visible.');
  });

  await check('login failure state works', async () => {
    await clickFirst(page, page.getByRole('button', { name: 'Login' }));
    await page.locator('input[name="email"]').fill(email);
    await page.locator('input[name="password"]').fill('WrongPassword!');
    await clickFirst(page, page.getByRole('button', { name: 'Sign in' }));
    await page.waitForTimeout(500);
    const bodyText = await page.locator('body').innerText();
    assert(/invalid|failed|wrong|credential|password/i.test(bodyText), 'Invalid-login error was not shown.');
  });

  await check('login success and dashboard load', async () => {
    await page.locator('input[name="password"]').fill(password);
    await clickFirst(page, page.getByRole('button', { name: 'Sign in' }));
    await page.waitForLoadState('networkidle');
    assert(await visible(page, 'Workspace dashboard'), 'Dashboard did not load after login.');
    assert(await visible(page, 'Live operational view'), 'Dashboard real-data header missing.');
  });

  await check('refresh preserves protected session', async () => {
    await page.reload({ waitUntil: 'networkidle' });
    assert(await visible(page, 'Workspace dashboard'), 'Protected session did not survive refresh.');
  });

  await check('api client page loads', async () => {
    await clickFirst(page, page.locator('[data-view="api-client"]'));
    assert(await visible(page, 'API Client'), 'API Client view not visible.');
    assert(await visible(page, 'Collections'), 'API Client collections rail not visible.');
    assert(await visible(page, 'Send'), 'Send action not visible.');
    assert(await visible(page, 'Save'), 'Save action not visible.');
  });

  await check('json tools page loads', async () => {
    await clickFirst(page, page.locator('[data-view="json-tools"]'));
    assert(await visible(page, 'JSON tools'), 'JSON tools view not visible.');
    assert(await visible(page, 'Formatter, validator'), 'JSON tools description not visible.');
  });

  await check('team page loads with invite controls', async () => {
    await clickFirst(page, page.locator('[data-view="team"]'));
    assert(await visible(page, 'Team and roles'), 'Team view not visible.');
    assert(await visible(page, 'Add a member'), 'Member invite panel not visible.');
    assert(await visible(page, 'Send invite'), 'Invite action not visible.');
  });

  await check('settings build info loads', async () => {
    await clickFirst(page, page.locator('[data-view="settings"]'));
    assert(await visible(page, 'Workspace settings'), 'Settings section not visible.');
    assert(await visible(page, 'Frontend commit'), 'Frontend build metadata not visible.');
    assert(await visible(page, 'Backend commit'), 'Backend build metadata not visible.');
    assert(await visible(page, 'Environment'), 'Environment metadata not visible.');
  });

  await check('mobile landing sanity', async () => {
    await page.getByRole('button', { name: 'Logout' }).last().click({ timeout: 12000 });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(baseUrl, { waitUntil: 'networkidle' });
    assert(await visible(page, 'Lightweight AI-powered Postman alternative'), 'Mobile landing hero not visible.');
  });

  await browser.close();

  if (consoleErrors.length) {
    checks.push({ name: 'console errors', status: 'FAIL', ms: 0, error: consoleErrors.join('\n') });
  } else {
    checks.push({ name: 'console errors', status: 'PASS', ms: 0 });
  }

  if (failedRequests.length) {
    checks.push({ name: 'failed browser network requests', status: 'FAIL', ms: 0, error: failedRequests.join('\n') });
  } else {
    checks.push({ name: 'failed browser network requests', status: 'PASS', ms: 0 });
  }

  for (const result of checks) {
    const suffix = result.error ? ` - ${result.error}` : '';
    console.log(`${result.status} ${result.name} (${result.ms}ms)${suffix}`);
  }

  const failed = checks.filter((result) => result.status !== 'PASS');
  if (failed.length) {
    process.exitCode = 1;
  }
})();
