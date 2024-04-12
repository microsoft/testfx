# Implement the Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework

The `Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework` is implemented by extensions that provide a test framework:

```cs
public interface ITestFramework : IExtension
{
    Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context);
    Task ExecuteRequestAsync(ExecuteRequestContext context);
    Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context);
}
```

The `ITestFramework` interface inherits from the [IExtension](iextension.md) interface, which is an interface that all extension points inherit from. `IExtension` is used to retrieve the name and description of the extension. The `IExtension` also provides a way to dynamically enable or disable the extension in setup, through `Task<bool> IsEnabledAsync()`, please make sure that you return `true` from this method if you have no special needs.

## CreateTestSessionAsync

The `CreateTestSessionAsync` is called at the start of the test session and is used to initialize the test framework. As we can see the api accepts a `CloseTestSessionContext` object and returns a `CloseTestSessionResult`.

```cs
public sealed class CreateTestSessionContext : TestSessionContext
{
    public SessionUid SessionUid { get; }
    public ClientInfo Client { get; }
    public CancellationToken CancellationToken { get; }
}

public readonly struct SessionUid
{
    public string Value { get; }
}

public sealed class ClientInfo
{
    public string Id { get; }
    public string Version { get; }
}
```

The `SessionUid` serves as the unique identifier for the current test session, providing a logical connection to the session's results.
The `ClientInfo` provides details about the entity invoking the test framework. This information can be utilized by the test framework to modify its behavior. For example, as of the time this document was written, a console execution would report a client name such as "testingplatform-console".
The `CancellationToken` is used to halt the execution of `CreateTestSessionAsync`.

The return object is a `CloseTestSessionResult`:

```cs
public sealed class CreateTestSessionResult
{
    public string? WarningMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
}
```

The `IsSuccess` can be used to indicate whether the session creation was successful. If it returns false, the test execution will be halted.

## CloseTestSessionAsync

The `CloseTestSessionAsync` mirrors the `CreateTestSessionAsync` in functionality, with the only difference being the object names. For more details, refer to the `CreateTestSessionAsync` section.

## ExecuteRequestAsync

Soon.
