import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

test('Electron build downloader retains HTTP proxy support with its patched agent', () => {
  const result = spawnSync(process.execPath, ['-e', `
    const assert = require('node:assert/strict');
    const http = require('node:http');
    const { createRequire } = require('node:module');
    for (const name of Object.keys(process.env)) {
      if (/^GLOBAL_AGENT_|(^|_)PROXY$/i.test(name)) delete process.env[name];
    }
    delete process.env.ELECTRON_GET_USE_PROXY;
    const builderRequire = createRequire(require.resolve('app-builder-lib/package.json'));
    const downloader = builderRequire('@electron/get');
    const requests = [];
    const server = http.createServer((request, response) => {
      requests.push(request.url);
      response.end('proxied-download');
    });
    server.listen(0, '127.0.0.1', async () => {
      try {
        process.env.GLOBAL_AGENT_HTTP_PROXY = 'http://127.0.0.1:' + server.address().port;
        process.env.GLOBAL_AGENT_NO_PROXY = '';
        downloader.initializeProxy();
        const content = await new Promise((resolve, reject) => {
          const request = http.get('http://udt-proxy-check.invalid/package', response => {
            let body = '';
            response.setEncoding('utf8');
            response.on('data', chunk => { body += chunk; });
            response.on('end', () => resolve(body));
          });
          request.setTimeout(5000, () => request.destroy(new Error('Proxy request timed out.')));
          request.on('error', reject);
        });
        assert.equal(content, 'proxied-download');
        assert.deepEqual(requests, ['http://udt-proxy-check.invalid/package']);
      } catch (error) {
        console.error(error);
        process.exitCode = 1;
      } finally {
        server.closeAllConnections();
        server.close();
      }
    });
  `], {
    cwd: fileURLToPath(new URL('..', import.meta.url)),
    encoding: 'utf8',
    timeout: 15000,
    env: { ...process.env, ELECTRON_GET_USE_PROXY: '' }
  })
  assert.equal(result.status, 0, result.stderr || String(result.error ?? result.signal))
})
