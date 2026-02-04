/**
 * WebGL Server
 *
 * NestJS-based static file server for Unity WebGL builds.
 * Serves files from WEBGL_ROOT with SPA fallback.
 */

import 'reflect-metadata';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { NestFactory } from '@nestjs/core';
import { NestExpressApplication } from '@nestjs/platform-express';
import { AppModule } from './app.module';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const DEFAULT_PORT = 8081;
const DEFAULT_WEBGL_ROOT = 'output/unity-webgl/UnityExample';

// Parse and validate WEBGL_PORT
function resolvePort(): number {
    const portRaw = process.env.WEBGL_PORT;
    if (!portRaw) {
        return DEFAULT_PORT;
    }

    const port = Number(portRaw);
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
        console.error(`[WebGLServer] Invalid WEBGL_PORT: "${portRaw}". Must be an integer 1..65535.`);
        process.exit(1);
    }
    return port;
}

const port = resolvePort();

// Resolve WebGL root path
// - If WEBGL_ROOT env is set: resolve from cwd
// - Otherwise: resolve from repo root (4 levels up from src/main.ts)
const repoRoot = path.resolve(__dirname, '../../../../');
const webglRoot = process.env.WEBGL_ROOT
    ? path.resolve(process.cwd(), process.env.WEBGL_ROOT)
    : path.resolve(repoRoot, DEFAULT_WEBGL_ROOT);

async function bootstrap() {
    // Validate index.html exists
    const indexPath = path.join(webglRoot, 'index.html');
    if (!fs.existsSync(indexPath)) {
        console.error(`[WebGLServer] WebGL build not found: ${indexPath}`);
        console.error(`[WebGLServer] Build Unity WebGL output to WEBGL_ROOT (default: ${DEFAULT_WEBGL_ROOT})`);
        process.exit(1);
    }

    const app = await NestFactory.create<NestExpressApplication>(AppModule);

    // Serve static files from WebGL build output
    app.useStaticAssets(webglRoot, {
        // Ensure proper MIME types for WebGL files
        setHeaders: (res, filePath) => {
            if (filePath.endsWith('.wasm')) {
                res.setHeader('Content-Type', 'application/wasm');
            }
            if (filePath.endsWith('.data') || filePath.endsWith('.data.gz')) {
                res.setHeader('Content-Type', 'application/octet-stream');
            }
            if (filePath.endsWith('.gz')) {
                res.setHeader('Content-Encoding', 'gzip');
            }
            if (filePath.endsWith('.br')) {
                res.setHeader('Content-Encoding', 'br');
            }
        },
    });

    // SPA fallback: serve index.html for unmatched routes
    app.use((req, res, next) => {
        // Skip API routes if any
        if (req.path.startsWith('/api') || req.path.startsWith('/health')) {
            return next();
        }

        // Check if request is for a file with extension
        const hasExtension = path.extname(req.path).length > 0;
        if (hasExtension) {
            return next();
        }

        // Fallback to index.html for SPA routing
        res.sendFile(indexPath);
    });

    await app.listen(port);

    console.log(`[WebGLServer] WEBGL_ROOT=${webglRoot}`);
    console.log(`[WebGLServer] Listening on http://localhost:${port}`);
}

bootstrap().catch((err) => {
    console.error('[WebGLServer] Failed to start:', err);
    process.exit(1);
});
