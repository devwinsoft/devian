/**
 * Network Server
 * 
 * Combines transport and network runtime to handle network messages.
 * Unknown opcodes are delegated to a hook (never disconnects).
 */

import type { ITransport } from '../transport/ITransport';
import type { INetworkRuntime, SendFn } from './INetworkRuntime';
import { parseFrame } from '../shared/frame';

/** Unknown opcode event data */
export interface UnknownOpcodeEvent {
    sessionId: number;
    opcode: number;
    payload: Uint8Array;
}

/** Network server options */
export interface NetworkServerOptions {
    /**
     * Hook for unknown inbound opcodes (async supported).
     * Default: logs a warning (never disconnects)
     * 
     * NOTE: Exceptions from this hook are caught and logged, never propagated.
     */
    onUnknownInboundOpcode?: (event: UnknownOpcodeEvent) => void | Promise<void>;
}

/**
 * Network Server
 * 
 * - Receives binary messages from transport
 * - Parses frames and dispatches via network runtime
 * - Unknown opcodes are delegated to hook (no disconnect)
 * - All exceptions are caught internally and logged (never propagated)
 */
export class NetworkServer {
    private readonly transport: ITransport;
    private readonly runtime: INetworkRuntime;
    private readonly options: NetworkServerOptions;
    private outboundProxy: unknown = null;

    constructor(
        transport: ITransport,
        runtime: INetworkRuntime,
        options: NetworkServerOptions = {}
    ) {
        this.transport = transport;
        this.runtime = runtime;
        this.options = options;
    }

    /**
     * Handle binary message from transport.
     * Called by transport's onBinaryMessage event.
     * 
     * NOTE: All exceptions are caught internally - this method never throws/rejects.
     */
    async onBinaryMessage(sessionId: number, data: Uint8Array): Promise<void> {
        try {
            const frame = parseFrame(data);
            if (!frame) {
                console.warn(`[NetworkServer] Invalid frame from session ${sessionId}`);
                return;
            }

            const { opcode, payload } = frame;

            // Check if opcode is known
            const opcodeName = this.runtime.getInboundOpcodeName(opcode);
            if (opcodeName === null) {
                // Unknown opcode - delegate to hook (NEVER disconnect)
                await this.handleUnknownOpcode({ sessionId, opcode, payload });
                return;
            }

            // Dispatch to runtime
            try {
                await this.runtime.dispatchInbound(sessionId, opcode, payload);
            } catch (err) {
                console.error(`[NetworkServer] dispatchInbound failed sid=${sessionId} opcode=${opcode} (${opcodeName})`, err);
            }
        } catch (err) {
            // Outermost catch - absorb any unexpected errors (parseFrame, etc.)
            console.error(`[NetworkServer] onBinaryMessage failed sid=${sessionId}`, err);
        }
    }

    /**
     * Create outbound proxy for sending messages.
     * @returns Typed proxy instance
     */
    createOutboundProxy<TProxy>(): TProxy {
        if (!this.outboundProxy) {
            const sendFn: SendFn = (sessionId, frame) => {
                this.transport.send(sessionId, frame);
            };
            this.outboundProxy = this.runtime.createOutboundProxy(sendFn);
        }
        return this.outboundProxy as TProxy;
    }

    /**
     * Get transport instance for direct access.
     */
    getTransport(): ITransport {
        return this.transport;
    }

    /**
     * Handle unknown opcode (async supported).
     * 
     * Priority:
     * 1. options.onUnknownInboundOpcode (if set)
     * 2. runtime.handleUnknownInboundOpcode (if implemented)
     * 3. Default warn log
     * 
     * NOTE: Exceptions from handlers are caught and logged, never propagated.
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
                `[NetworkServer] Unknown opcode ${event.opcode} from session ${event.sessionId} (${event.payload.length} bytes)`
            );
        } catch (err) {
            console.error(`[NetworkServer] unknown opcode handler failed sid=${event.sessionId} opcode=${event.opcode}`, err);
        }
    }
}
