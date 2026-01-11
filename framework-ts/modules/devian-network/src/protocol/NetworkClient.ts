/**
 * Network Client
 * 
 * Handles WebSocket message processing for client-side network.
 * - Converts raw ws message to Uint8Array
 * - Parses frames and dispatches via network runtime
 * - Unknown opcodes are delegated to handler (never disconnects)
 * - All exceptions are caught internally and logged (never propagated)
 */

import type { INetworkRuntime } from './INetworkRuntime';
import type { UnknownOpcodeEvent } from './NetworkServer';
import { parseFrame } from '../shared/frame';

/** Network client options */
export interface NetworkClientOptions {
    /** Session ID (default: 0) */
    sessionId?: number;
    
    /**
     * Hook for unknown inbound opcodes (async supported).
     * Priority 1 (highest).
     */
    onUnknownInboundOpcode?: (event: UnknownOpcodeEvent) => void | Promise<void>;
    
    /**
     * Hook for invalid frames (async supported).
     */
    onInvalidFrame?: (rawBytes: Uint8Array) => void | Promise<void>;
    
    /**
     * Hook for errors (async supported).
     */
    onError?: (err: unknown) => void | Promise<void>;
}

/**
 * Network Client
 * 
 * Simplifies WebSocket message handling by:
 * - Converting raw ws data to Uint8Array
 * - Parsing frames
 * - Dispatching to runtime
 * - Handling unknown opcodes (never disconnects)
 */
export class NetworkClient {
    private readonly runtime: INetworkRuntime;
    private readonly options: NetworkClientOptions;
    private readonly sessionId: number;

    constructor(runtime: INetworkRuntime, options: NetworkClientOptions = {}) {
        this.runtime = runtime;
        this.options = options;
        this.sessionId = options.sessionId ?? 0;
    }

    /**
     * Handle raw WebSocket message.
     * Call this from ws.on('message', (raw) => client.onWsMessage(raw))
     * 
     * NOTE: All exceptions are caught internally - this method never throws.
     */
    onWsMessage(raw: unknown): void {
        // Use void Promise to handle async internally without exposing
        void this.handleMessage(raw);
    }

    /**
     * Internal async message handler.
     * All exceptions are caught and logged/forwarded to onError.
     */
    private async handleMessage(raw: unknown): Promise<void> {
        try {
            // Convert raw to Uint8Array
            const bytes = this.toUint8Array(raw);
            if (!bytes) {
                console.warn(`[NetworkClient] Unknown raw message type`);
                return;
            }

            // Parse frame
            const frame = parseFrame(bytes);
            if (!frame) {
                await this.handleInvalidFrame(bytes);
                return;
            }

            const { opcode, payload } = frame;

            // Check if opcode is known
            const opcodeName = this.runtime.getInboundOpcodeName(opcode);
            if (opcodeName === null) {
                // Unknown opcode - delegate to handler (NEVER disconnect)
                await this.handleUnknownOpcode({ sessionId: this.sessionId, opcode, payload });
                return;
            }

            // Dispatch to runtime
            try {
                await this.runtime.dispatchInbound(this.sessionId, opcode, payload);
            } catch (err) {
                console.error(`[NetworkClient] dispatchInbound failed opcode=${opcode} (${opcodeName})`, err);
                await this.notifyError(err);
            }
        } catch (err) {
            // Outermost catch - absorb any unexpected errors
            console.error(`[NetworkClient] handleMessage failed`, err);
            await this.notifyError(err);
        }
    }

    /**
     * Convert raw WebSocket data to Uint8Array.
     * Supports: Buffer, ArrayBuffer, Buffer[], Uint8Array
     */
    private toUint8Array(raw: unknown): Uint8Array | null {
        if (raw instanceof Uint8Array) {
            return raw;
        }
        if (typeof Buffer !== 'undefined' && Buffer.isBuffer(raw)) {
            return new Uint8Array(raw);
        }
        if (raw instanceof ArrayBuffer) {
            return new Uint8Array(raw);
        }
        if (Array.isArray(raw) && raw.every(item => typeof Buffer !== 'undefined' && Buffer.isBuffer(item))) {
            return new Uint8Array(Buffer.concat(raw as Buffer[]));
        }
        return null;
    }

    /**
     * Handle invalid frame.
     */
    private async handleInvalidFrame(rawBytes: Uint8Array): Promise<void> {
        try {
            if (this.options.onInvalidFrame) {
                await this.options.onInvalidFrame(rawBytes);
            } else {
                console.warn(`[NetworkClient] Invalid frame (${rawBytes.length} bytes)`);
            }
        } catch (err) {
            console.error(`[NetworkClient] onInvalidFrame handler failed`, err);
        }
    }

    /**
     * Handle unknown opcode.
     * 
     * Priority:
     * 1. options.onUnknownInboundOpcode (if set)
     * 2. runtime.handleUnknownInboundOpcode (if implemented)
     * 3. Default warn log
     * 
     * NOTE: Never disconnects.
     */
    private async handleUnknownOpcode(event: UnknownOpcodeEvent): Promise<void> {
        try {
            // Priority 1: options hook
            if (this.options.onUnknownInboundOpcode) {
                await this.options.onUnknownInboundOpcode(event);
                return;
            }

            // Priority 2: runtime handler
            if (this.runtime.handleUnknownInboundOpcode) {
                await this.runtime.handleUnknownInboundOpcode(event);
                return;
            }

            // Priority 3: default warn log (NEVER disconnect)
            console.warn(
                `[NetworkClient] Unknown opcode ${event.opcode} (${event.payload.length} bytes)`
            );
        } catch (err) {
            console.error(`[NetworkClient] unknown opcode handler failed opcode=${event.opcode}`, err);
        }
    }

    /**
     * Notify error to options.onError if set.
     */
    private async notifyError(err: unknown): Promise<void> {
        try {
            if (this.options.onError) {
                await this.options.onError(err);
            }
        } catch (notifyErr) {
            console.error(`[NetworkClient] onError handler failed`, notifyErr);
        }
    }
}
