/**
 * Protocol Server
 * 
 * Combines transport and protocol runtime to handle network messages.
 * Unknown opcodes are delegated to a hook (never disconnects).
 */

import type { ITransport } from '../transport/ITransport';
import type { IServerProtocolRuntime, SendFn } from './IProtocolRuntime';
import { parseFrame } from '../shared/frame';

/** Unknown opcode event data */
export interface UnknownOpcodeEvent {
    sessionId: number;
    opcode: number;
    payload: Uint8Array;
}

/** Protocol server options */
export interface ProtocolServerOptions {
    /**
     * Hook for unknown inbound opcodes (async supported).
     * Default: logs a warning (never disconnects)
     */
    onUnknownInboundOpcode?: (event: UnknownOpcodeEvent) => void | Promise<void>;
}

/**
 * Protocol Server
 * 
 * - Receives binary messages from transport
 * - Parses frames and dispatches via protocol runtime
 * - Unknown opcodes are delegated to hook (no disconnect)
 */
export class ProtocolServer {
    private readonly transport: ITransport;
    private readonly runtime: IServerProtocolRuntime;
    private readonly options: ProtocolServerOptions;
    private outboundProxy: unknown = null;

    constructor(
        transport: ITransport,
        runtime: IServerProtocolRuntime,
        options: ProtocolServerOptions = {}
    ) {
        this.transport = transport;
        this.runtime = runtime;
        this.options = options;
    }

    /**
     * Handle binary message from transport.
     * Called by transport's onBinaryMessage event.
     */
    async onBinaryMessage(sessionId: number, data: Uint8Array): Promise<void> {
        const frame = parseFrame(data);
        if (!frame) {
            console.warn(`[ProtocolServer] Invalid frame from session ${sessionId}`);
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
        } catch (error) {
            console.error(`[ProtocolServer] Error dispatching opcode ${opcode} (${opcodeName}):`, error);
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
     * Default: logs warning. Never disconnects.
     */
    private async handleUnknownOpcode(event: UnknownOpcodeEvent): Promise<void> {
        if (this.options.onUnknownInboundOpcode) {
            await this.options.onUnknownInboundOpcode(event);
        } else {
            // Default: warn log only (NEVER disconnect)
            console.warn(
                `[ProtocolServer] Unknown opcode ${event.opcode} from session ${event.sessionId} (${event.payload.length} bytes)`
            );
        }
    }
}
