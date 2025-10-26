# Migration Example: Converting to Generated Serializers

This document shows how to migrate existing manually-written serializers to use the source generator.

## Example 1: TestHostProcessPIDRequest (Simple Type)

### Before (Manual Implementation)

**TestHostProcessPIDRequest.cs:**
```csharp
namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed class TestHostProcessPIDRequest(int pid) : IRequest
{
    public int PID { get; } = pid;
}
```

**TestHostProcessPIDRequestSerializer.cs:**
```csharp
namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class TestHostProcessPIDRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestHostProcessPIDRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        int pid = ReadInt(stream);
        return new TestHostProcessPIDRequest(pid);
    }

    public void Serialize(object obj, Stream stream)
    {
        var testHostProcessPIDRequest = (TestHostProcessPIDRequest)obj;
        WriteInt(stream, testHostProcessPIDRequest.PID);
    }
}
```

### After (With Source Generator)

**TestHostProcessPIDRequest.cs:**
```csharp
namespace Microsoft.Testing.Platform.IPC.Models;

[GenerateSerializer(TestHostProcessPIDRequestFieldsId.MessagesSerializerId)]
internal sealed class TestHostProcessPIDRequest(int pid) : IRequest
{
    public int PID { get; } = pid;
}
```

**TestHostProcessPIDRequestSerializer.cs:** *(AUTO-GENERATED - Do not edit)*

The serializer is now automatically generated at compile time.

**Benefits:**
- Eliminated 15 lines of manual code
- No more risk of typos or mismatched read/write order
- Consistent with other generated serializers

---

## Example 2: ModuleInfoRequest (Multiple Fields)

### Before (Manual Implementation)

**ModuleInfoRequest.cs:**
```csharp
namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed record ModuleInfoRequest(
    string FrameworkDescription,
    string ProcessArchitecture,
    string TestResultFolder) : IRequest;
```

**ModuleInfoRequestSerializer.cs:**
```csharp
namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed class ModuleInfoRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 1;

    public object Deserialize(Stream stream)
        => new ModuleInfoRequest(ReadString(stream), ReadString(stream), ReadString(stream));

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var moduleInfo = (ModuleInfoRequest)objectToSerialize;
        WriteString(stream, moduleInfo.FrameworkDescription);
        WriteString(stream, moduleInfo.ProcessArchitecture);
        WriteString(stream, moduleInfo.TestResultFolder);
    }
}
```

### After (With Source Generator)

**ModuleInfoRequest.cs:**
```csharp
namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

[GenerateSerializer(1)]
internal sealed record ModuleInfoRequest(
    string FrameworkDescription,
    string ProcessArchitecture,
    string TestResultFolder) : IRequest;
```

**ModuleInfoRequestSerializer.cs:** *(AUTO-GENERATED)*

**Benefits:**
- Eliminated 18 lines of boilerplate
- Generator ensures read order matches write order
- Easy to add/remove fields - just update the record and regenerate

---

## Example 3: SessionEndRequest (Empty Type)

### Before (Manual Implementation)

**SessionEndRequest.cs:**
```csharp
namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class SessionEndSerializerRequest : IRequest;
```

**SessionEndSerializerRequestSerializer.cs:**
```csharp
namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class SessionEndSerializerRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 2;

    public object Deserialize(Stream stream)
        => new SessionEndSerializerRequest();

    public void Serialize(object _, Stream __)
    {
    }
}
```

### After (With Source Generator)

**SessionEndRequest.cs:**
```csharp
namespace Microsoft.Testing.Extensions.HangDump.Serializers;

[GenerateSerializer(2)]
internal sealed class SessionEndSerializerRequest : IRequest;
```

**SessionEndSerializerRequestSerializer.cs:** *(AUTO-GENERATED)*

---

## Example 4: Complex Type (Partial Generation)

For complex types with nested objects or arrays, the generator creates stubs:

**TestResultMessages.cs:**
```csharp
[GenerateSerializer(6)]  // Generator creates stubs for manual completion
internal sealed record TestResultMessages(
    string ExecutionId,
    string InstanceId,
    SuccessfulTestResultMessage[]? SuccessfulTestMessages,
    FailedTestResultMessage[]? FailedTestMessages) : IRequest;
```

The generated serializer will contain:
```csharp
public object Deserialize(Stream stream)
{
    // TODO: Implement complex deserialization logic
    throw new NotImplementedException("Deserialization for TestResultMessages needs manual implementation");
}
```

For these complex cases, you'll need to manually implement the serialization logic, but the generator still provides the class structure and simple field handling as a starting point.

---

## Migration Strategy

1. **Start with simple types**: Convert empty and single-field requests/responses first
2. **Test incrementally**: Migrate one serializer at a time and test
3. **Complex types**: Use the generated stubs as templates and fill in manual logic
4. **Remove manual serializers**: Once generated serializer works, delete the manual .cs file
5. **Update registrations**: Ensure RegisterSerializer calls use the generated serializer

## Rollback

If you need to rollback:
1. Remove the `[GenerateSerializer]` attribute from the Request/Response
2. The manual serializer file will still exist and will be used instead
