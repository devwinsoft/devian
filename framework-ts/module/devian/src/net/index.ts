/**
 * Devian Network Module
 * 
 * Provides common networking infrastructure for Devian network servers and clients.
 * Group-specific runtime is provided by @devian/protocol-{group} packages.
 */

// Shared utilities
export { parseFrame, packFrame, type ParsedFrame, MIN_FRAME_SIZE } from './shared/frame';
export { TaggedBigIntCodec, defaultCodec, type ICodec } from './shared/codec';

// Transport
export { type ITransport, type ITransportEvents } from './transport/ITransport';
export { WsTransport, type WsTransportOptions } from './transport/WsTransport';

// Network
export { type INetworkRuntime, type SendFn } from './protocol/INetworkRuntime';
export { NetworkServer, type NetworkServerOptions, type UnknownOpcodeEvent } from './protocol/NetworkServer';
export { NetworkClient, type NetworkClientOptions } from './protocol/NetworkClient';
