# Devian Core

Devian unified runtime package for Unity.

Contains:
- **Core** (namespace: `Devian`) - Parser, Entity, TableContainer
- **Network** (namespace: `Devian.Net`) - Frame, WebSocket, HTTP RPC
- **Protobuf** (namespace: `Devian.Proto`) - DFF, Protobuf conversion

## Installation

Add to your Unity project's manifest.json:

```json
{
  "dependencies": {
    "com.devian.core": "0.1.0"
  }
}
```

## Assembly References

- `Devian.Core` - Core functionality
- `Devian.Network` - Network functionality  
- `Devian.Protobuf` - Protobuf functionality
