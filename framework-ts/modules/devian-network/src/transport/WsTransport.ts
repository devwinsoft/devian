/**
 * WebSocket Transport
 * 
 * - Manages WebSocket connections and sessions
 * - Provides binary message events
 * - Does NOT contain any opcode/protocol logic
 */

import { WebSocketServer, WebSocket } from 'ws';
import type { ITransport, ITransportEvents } from './ITransport';

/** WebSocket transport options */
export interface WsTransportOptions {
    port: number;
    host?: string;
}

/** Session info */
interface SessionInfo {
    id: number;
    socket: WebSocket;
    connectedAt: Date;
}

/**
 * WebSocket Transport
 */
export class WsTransport implements ITransport {
    private wss: WebSocketServer | null = null;
    private sessions = new Map<number, SessionInfo>();
    private nextSessionId = 1;
    private readonly options: WsTransportOptions;
    private readonly events: ITransportEvents;

    constructor(options: WsTransportOptions, events: ITransportEvents = {}) {
        this.options = options;
        this.events = events;
    }

    async start(): Promise<void> {
        return new Promise((resolve, reject) => {
            this.wss = new WebSocketServer({
                port: this.options.port,
                host: this.options.host,
            });

            this.wss.on('listening', () => {
                console.log(`[WsTransport] Listening on ${this.options.host ?? '0.0.0.0'}:${this.options.port}`);
                resolve();
            });

            this.wss.on('error', (error) => {
                reject(error);
            });

            this.wss.on('connection', (socket) => {
                this.handleConnection(socket);
            });
        });
    }

    async stop(): Promise<void> {
        return new Promise((resolve) => {
            if (this.wss) {
                // Close all sessions
                for (const session of this.sessions.values()) {
                    session.socket.close(1001, 'Server shutting down');
                }
                this.sessions.clear();

                this.wss.close(() => {
                    this.wss = null;
                    resolve();
                });
            } else {
                resolve();
            }
        });
    }

    send(sessionId: number, data: Uint8Array): void {
        const session = this.sessions.get(sessionId);
        if (session && session.socket.readyState === WebSocket.OPEN) {
            session.socket.send(data);
        }
    }

    getSessionIds(): number[] {
        return Array.from(this.sessions.keys());
    }

    closeSession(sessionId: number, code?: number, reason?: string): void {
        const session = this.sessions.get(sessionId);
        if (session) {
            session.socket.close(code, reason);
        }
    }

    private handleConnection(socket: WebSocket): void {
        const sessionId = this.nextSessionId++;
        const session: SessionInfo = {
            id: sessionId,
            socket,
            connectedAt: new Date(),
        };
        this.sessions.set(sessionId, session);

        console.log(`[WsTransport] Session ${sessionId} connected`);
        this.events.onConnect?.(sessionId);

        socket.on('message', (data: Buffer | ArrayBuffer | Buffer[]) => {
            try {
                let bytes: Uint8Array;
                if (data instanceof Buffer) {
                    bytes = new Uint8Array(data);
                } else if (data instanceof ArrayBuffer) {
                    bytes = new Uint8Array(data);
                } else if (Array.isArray(data)) {
                    bytes = new Uint8Array(Buffer.concat(data));
                } else {
                    return;
                }

                // Handle async callback rejection - forward to onError
                const p = this.events.onBinaryMessage?.(sessionId, bytes);
                void Promise.resolve(p).catch((err) => {
                    this.events.onError?.(sessionId, err as Error);
                });
            } catch (error) {
                this.events.onError?.(sessionId, error as Error);
            }
        });

        socket.on('close', () => {
            console.log(`[WsTransport] Session ${sessionId} disconnected`);
            this.sessions.delete(sessionId);
            this.events.onDisconnect?.(sessionId);
        });

        socket.on('error', (error) => {
            console.error(`[WsTransport] Session ${sessionId} error:`, error.message);
            this.events.onError?.(sessionId, error);
        });
    }
}
