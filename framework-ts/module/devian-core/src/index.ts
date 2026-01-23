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
export { IEntity, IEntityKey } from './IEntity';
export { ITableContainer, IKeyedTableContainer } from './ITableContainer';
export { ICodec } from './ICodec';

// Network exports (Devian.Net equivalent)
export * from './net';

// Proto exports (Devian.Proto equivalent)
export * from './proto';
