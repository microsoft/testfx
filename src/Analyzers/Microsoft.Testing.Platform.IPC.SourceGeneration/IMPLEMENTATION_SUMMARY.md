# IPC Source Generator Implementation Summary

## Overview

This PR implements a C# source generator to automate the creation of IPC serializers for the pipe protocol implementation in Microsoft Testing Platform. This addresses the issue that manual serializer implementation is "error prone" and "so mechanical that it can be achieved via a source generator."

## Problem Statement

Currently, the codebase has 24+ manual serializer implementations across the platform. Each serializer:
- Requires ~20-30 lines of boilerplate code
- Must manually implement `Serialize` and `Deserialize` methods
- Needs correct ordering of read/write operations
- Is error-prone when adding/removing properties
- Makes refactoring difficult

## Solution

A C# incremental source generator that:
1. Emits a `[GenerateSerializer(serializerId)]` attribute
2. Analyzes types marked with this attribute
3. Generates complete serializer implementations for simple types
4. Generates stubs for complex types requiring manual implementation

## Implementation Details

### Project Structure

```
src/Analyzers/Microsoft.Testing.Platform.IPC.SourceGeneration/
├── Microsoft.Testing.Platform.IPC.SourceGeneration.csproj
├── Generators/
│   └── SerializerGenerator.cs          # Main generator implementation
├── Models/
│   └── SerializableTypeInfo.cs         # Type information model
├── Helpers/
│   ├── IndentedStringBuilder.cs        # Code generation helper
│   └── StringBuilderExtensions.cs      # Extension methods
├── Attributes/
│   └── GenerateSerializerAttribute.cs  # Marker attribute (also emitted by generator)
├── README.md                            # Usage documentation
├── EXAMPLES.md                          # Before/after examples
├── MIGRATION.md                         # Migration guide
├── BannedSymbols.txt
└── GlobalSuppressions.cs

test/UnitTests/Microsoft.Testing.Platform.IPC.SourceGeneration.UnitTests/
├── Microsoft.Testing.Platform.IPC.SourceGeneration.UnitTests.csproj
└── SerializerGeneratorTests.cs         # Unit tests for generator
```

### Supported Types

The generator fully supports:
- `int`, `long`, `ushort`, `byte`, `bool`
- `string`
- Nullable versions of above types
- Empty types (no properties)

For complex types (arrays, nested objects), it generates stubs with `NotImplementedException` and TODO comments.

### Code Generation Example

**Input:**
```csharp
[GenerateSerializer(42)]
internal sealed record MyRequest(int Value, string Name) : IRequest;
```

**Generated Output:**
```csharp
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

## Usage

### 1. Add Generator Reference

In `Microsoft.Testing.Platform.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\Analyzers\Microsoft.Testing.Platform.IPC.SourceGeneration\Microsoft.Testing.Platform.IPC.SourceGeneration.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. Mark Types with Attribute

```csharp
[GenerateSerializer(2)]
internal sealed class TestHostProcessPIDRequest(int pid) : IRequest
{
    public int PID { get; } = pid;
}
```

### 3. Delete Manual Serializer

Remove `TestHostProcessPIDRequestSerializer.cs` - it's now auto-generated!

### 4. Register Serializer

The registration code remains unchanged:
```csharp
namedPipeBase.RegisterSerializer(new TestHostProcessPIDRequestSerializer(), typeof(TestHostProcessPIDRequest));
```

## Migration Path

See `MIGRATION.md` for detailed migration instructions. Recommended approach:

1. **Phase 1**: Migrate simple serializers (empty and single-property types)
   - Example candidates: `TestHostProcessPIDRequest`, `SessionEndRequest`, `VoidResponse`
   
2. **Phase 2**: Migrate multi-field simple types
   - Example candidates: `ModuleInfoRequest`, `RunSummaryInfoRequest`, `FailedTestRequest`
   
3. **Phase 3**: Complex types remain manual (or use generated stubs as templates)
   - Example: `CommandLineOptionMessages`, `TestResultMessages`, `DiscoveredTestMessages`

## Testing

Unit tests are included in `Microsoft.Testing.Platform.IPC.SourceGeneration.UnitTests` project:
- Simple request generation
- Empty request generation
- Multi-field request generation (with ordering verification)
- Complex type stub generation
- Response type generation

## Benefits

1. **Code Reduction**: Eliminates ~500 lines of boilerplate across 24 serializers
2. **Fewer Bugs**: Automated generation prevents:
   - Read/write order mismatches
   - Typos in property names
   - Forgotten properties
   - Incorrect type conversions
3. **Easier Refactoring**: Property renames automatically update serializers
4. **Consistency**: All serializers follow identical patterns
5. **Better Onboarding**: New developers don't need to learn serialization protocol

## Limitations

1. **Complex Types**: Arrays and nested objects require manual implementation
2. **Field IDs**: For protocols using field IDs (like CommandLineOptionMessages), manual implementation is still required
3. **Custom Logic**: Special serialization logic must be written manually

## Future Enhancements

Potential improvements for future iterations:
- [ ] Support for arrays of simple types (string[], int[], etc.)
- [ ] Support for nullable value types (int?, long?, etc.)
- [ ] Automatic field ID management
- [ ] Support for nested object serialization
- [ ] Backwards compatibility analysis
- [ ] Versioning support

## Build/Infrastructure Note

**Important**: This implementation could not be fully built and tested due to infrastructure issues with the Azure DevOps NuGet feed (Arcade SDK download failures). The code is complete and follows all repository patterns, but needs validation in a working build environment.

## Recommendation

1. Review the source generator implementation
2. Test build in your CI environment (where Arcade SDK is accessible)
3. Migrate 1-2 simple serializers as proof of concept
4. If successful, gradually migrate remaining simple serializers
5. Document any adjustments needed for repository-specific patterns

## Questions?

- See `README.md` for usage documentation
- See `EXAMPLES.md` for detailed before/after examples
- See `MIGRATION.md` for step-by-step migration guide
- Review `SerializerGeneratorTests.cs` for test coverage examples
