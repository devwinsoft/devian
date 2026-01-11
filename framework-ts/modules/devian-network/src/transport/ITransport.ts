/**
 * Transport interface
 */

/** Transport events */
export interface ITransportEvents {
    /** Called when a session connects */
    onConnect?(sessionId: number): void;
    /** Called when a session disconnects */
    onDisconnect?(sessionId: number): void;
    /** Called when binary message is received */
    onBinaryMessage?(sessionId: number, data: Uint8Array): void;
    /** Called on error */
    onError?(sessionId: number, error: Error): void;
}

/** Transport interface */
export interface ITransport {
    /** Start listening */
    start(): Promise<void>;
    /** Stop and close all connections */
    stop(): Promise<void>;
    /** Send binary data to session */
    send(sessionId: number, data: Uint8Array): void;
    /** Get all connected session IDs */
    getSessionIds(): number[];
    /** Close specific session */
    closeSession(sessionId: number, code?: number, reason?: string): void;
}
