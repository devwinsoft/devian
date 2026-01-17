# Google.Protobuf.dll Required

This folder requires `Google.Protobuf.dll` (version 3.25.1) for Unity compilation.

## How to obtain

### Option 1: NuGet Cache (Recommended)
After running `dotnet restore` on the framework-cs solution, copy from:
```
~/.nuget/packages/google.protobuf/3.25.1/lib/netstandard2.0/Google.Protobuf.dll
```

### Option 2: Direct Download
Download from NuGet.org:
https://www.nuget.org/packages/Google.Protobuf/3.25.1

Extract the .nupkg (it's a zip) and copy:
```
lib/netstandard2.0/Google.Protobuf.dll
```

## Target Location
Place the DLL in this folder:
```
com.devian.protobuf/Runtime/Plugins/Google.Protobuf.dll
```

## Note
Without this DLL, the Unity project will fail to compile with errors referencing Google.Protobuf types.
