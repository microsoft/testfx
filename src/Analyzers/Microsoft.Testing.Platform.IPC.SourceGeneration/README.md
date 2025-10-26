# IPC Serializer Source Generator

This source generator automatically generates serializer boilerplate code for IPC Request/Response types in the Microsoft Testing Platform.

## Overview

The pipe protocol implementation requires serializers for each Request/Response type. Previously, these had to be written manually, which was error-prone and repetitive. This source generator automates the creation of serializer classes for simple types.

## Usage

### 1. Mark your Request/Response type with `[GenerateSerializer]`

```csharp
using Microsoft.Testing.Platform.IPC;

namespace MyNamespace;

[GenerateSerializer(serializerId: 42)]
internal sealed record MyRequest(int Value, string Name) : IRequest;
```

### 2. The generator will create a serializer class

The generator will automatically create `MyRequestSerializer.cs` with:

```csharp
namespace MyNamespace;

internal sealed class MyRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 42;

    public object Deserialize(Stream stream)
    {
        return new MyRequest(
            ReadInt(stream),
            ReadString(stream));
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var request = (MyRequest)objectToSerialize;
        WriteInt(stream, request.Value);
        WriteString(stream, request.Name);
    }
}
```

### 3. Register the serializer

```csharp
namedPipeBase.RegisterSerializer(new MyRequestSerializer(), typeof(MyRequest));
```

## Supported Types

The generator fully supports the following property types:
- `int` / `System.Int32`
- `long` / `System.Int64`
- `ushort` / `System.UInt16`
- `bool` / `System.Boolean`
- `byte` / `System.Byte`
- `string` / `System.String`

For complex types (nested objects, arrays), the generator creates stub methods with `NotImplementedException` and TODO comments, indicating manual implementation is needed.

## Serializer ID

Each serializer must have a unique ID. The ID is used for wire protocol versioning and must:
- Be unique across all serializers in the system
- Never change once assigned (for backwards compatibility)
- Be passed to the `[GenerateSerializer(serializerId)]` attribute

See `ObjectFieldIds.cs` for existing serializer IDs.

## Examples

### Simple Request (Fully Generated)

```csharp
[GenerateSerializer(100)]
internal sealed record GetStatusRequest(int ProcessId) : IRequest;
```

### Request with Multiple Fields (Fully Generated)

```csharp
[GenerateSerializer(101)]
internal sealed record ConfigurationRequest(
    string Name,
    int Timeout,
    bool IsEnabled) : IRequest;
```

### Empty Request (Fully Generated)

```csharp
[GenerateSerializer(102)]
internal sealed record ShutdownRequest : IRequest;
```

### Complex Request (Partial Generation)

```csharp
// For complex types, the generator creates stubs
[GenerateSerializer(103)]
internal sealed record ComplexRequest(
    string Name,
    NestedObject[] Items) : IRequest;

// The generated Deserialize/Serialize methods will contain:
// throw new NotImplementedException("Deserialization for ComplexRequest needs manual implementation");
```

## Benefits

1. **Reduced Boilerplate**: Eliminates repetitive serializer code
2. **Fewer Bugs**: Automated generation prevents common mistakes
3. **Consistency**: All generated serializers follow the same pattern
4. **Maintainability**: Changes to the protocol can be applied to the generator

## Limitations

- Complex types (nested objects, arrays, collections) require manual implementation
- The generator assumes properties are serialized in declaration order
- For field-ID based protocols (like CommandLineOptionMessages), manual implementation is still required

## Future Enhancements

Potential improvements for future versions:
- Support for arrays and collections of simple types
- Automatic field ID generation and management
- Support for nested object serialization
- Backwards compatibility validation
