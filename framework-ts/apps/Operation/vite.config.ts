import path from 'node:path';
import { defineConfig } from 'vite';

const operationDir = __dirname;
const cacheDir = path.resolve(operationDir, '../../node_modules/.vite/operation');

export default defineConfig({
  cacheDir,
  server: {
    port: 5173,
    host: '127.0.0.1',
  },
});
