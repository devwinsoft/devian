/**
 * Devian Network Server Module
 * 
 * Provides common networking infrastructure for Devian protocol servers.
 * Group-specific runtime is provided by devian-protocol-{group} packages.
 */

// Shared utilities
export { parseFrame, packFrame, type ParsedFrame, MIN_FRAME_SIZE } from './shared/frame';
export { TaggedBigIntCodec, defaultCodec, type ICodec } from './shared/codec';

// Transport
export { type ITransport, type ITransportEvents } from './transport/ITransport';
export { WsTransport, type WsTransportOptions } from './transport/WsTransport';

// Protocol
export { type IServerProtocolRuntime, type SendFn } from './protocol/IProtocolRuntime';
export { ProtocolServer, type ProtocolServerOptions, type UnknownOpcodeEvent } from './protocol/ProtocolServer';
