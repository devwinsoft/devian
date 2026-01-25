/**
 * Network Runtime Interface
 * 
 * This interface is implemented by each {group} package's generated runtime.
 * The devian-network module only knows this interface, not the concrete implementations.
 */

import type { UnknownOpcodeEvent } from './NetworkServer';

/** Send function type for outbound proxy */
export type SendFn = (sessionId: number, frame: Uint8Array) => void;

/**
 * Network Runtime Interface
 * 
 * Each protocol group (e.g., devian-protocol-game) provides an implementation
 * that knows its own opcodes, stub dispatch, and proxy creation.
 */
export interface INetworkRuntime {
    /**
     * Get the name of an inbound opcode.
     * @returns opcode name or null if unknown
     */
    getInboundOpcodeName(opcode: number): string | null;

    /**
     * Dispatch inbound message to registered handlers.
     * @param sessionId Session that sent the message
     * @param opcode Message opcode
     * @param payload Message payload bytes
     */
    dispatchInbound(sessionId: number, opcode: number, payload: Uint8Array): Promise<void>;

    /**
     * Create outbound proxy for sending messages.
     * @param sendFn Function to send frame bytes
     * @returns Proxy instance (type depends on protocol group)
     */
    createOutboundProxy(sendFn: SendFn): unknown;

    /**
     * Handle unknown inbound opcode (optional).
     * If implemented, NetworkServer/NetworkClient will call this when options handler is not set.
     * @param event Unknown opcode event data
     */
    handleUnknownInboundOpcode?(event: UnknownOpcodeEvent): void | Promise<void>;
}
