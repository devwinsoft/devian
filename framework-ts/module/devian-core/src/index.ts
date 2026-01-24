/**
 * Devian Core Runtime (Unified Module)
 * 
 * Base interfaces and types for Devian framework.
 * Includes Core, Network, and Protobuf functionality.
 * 
 * @see Devian (C# unified module)
 */

// Core exports
export { LoadMode } from './LoadMode';
export type { IEntity, IEntityKey } from './IEntity';
export type { ITableContainer, IKeyedTableContainer } from './ITableContainer';
export type { ICodec } from './ICodec';

// Network exports (Devian.Net equivalent)
export * from './net';

// Proto exports (Devian.Proto equivalent)
export * from './proto';
