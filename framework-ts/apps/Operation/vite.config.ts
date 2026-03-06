import path from 'node:path';
import { spawn } from 'node:child_process';
import { defineConfig } from 'vite';

const IMPORT_ENDPOINT = '/__operation/import-reward-id-catalog';
const operationDir = __dirname;
const cacheDir = path.resolve(operationDir, '../../node_modules/.vite/operation');

function writeJson(
  res: {
    statusCode: number;
    setHeader: (name: string, value: string) => void;
    end: (body: string) => void;
  },
  statusCode: number,
  payload: unknown,
) {
  res.statusCode = statusCode;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.end(JSON.stringify(payload));
}

export default defineConfig({
  cacheDir,
  plugins: [
    {
      name: 'operation-import-reward-id-catalog-api',
      configureServer(server) {
        server.middlewares.use((req, res, next) => {
          if (!req.url?.startsWith(IMPORT_ENDPOINT)) {
            next();
            return;
          }

          if (req.method !== 'POST') {
            writeJson(res, 405, { ok: false, error: 'Method not allowed' });
            return;
          }

          const url = new URL(req.url, 'http://localhost');
          const dryRun = (
            url.searchParams.get('dryRun') === '1'
            || url.searchParams.get('dryRun') === 'true'
          );

          const scriptPath = path.resolve(operationDir, 'scripts/import-reward-id-catalog.mjs');
          const args = [scriptPath];
          if (dryRun) args.push('--dry-run');

          const child = spawn(process.execPath, args, {
            cwd: operationDir,
            env: process.env,
          });

          let stdout = '';
          let stderr = '';

          child.stdout.on('data', (chunk: Buffer | string) => {
            stdout += chunk.toString();
          });
          child.stderr.on('data', (chunk: Buffer | string) => {
            stderr += chunk.toString();
          });

          child.on('error', (error) => {
            writeJson(res, 500, {
              ok: false,
              error: error instanceof Error ? error.message : String(error),
              stdout,
              stderr,
            });
          });

          child.on('close', (code) => {
            writeJson(res, code === 0 ? 200 : 500, {
              ok: code === 0,
              code,
              stdout,
              stderr,
            });
          });
        });
      },
    },
  ],
  server: {
    port: 5173,
    host: '127.0.0.1',
  },
});
