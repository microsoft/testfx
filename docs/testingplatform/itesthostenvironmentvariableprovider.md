# The `ITestHostEnvironmentVariableProvider`

The `ITestHostEnvironmentVariableProvider` is an *out-of-process* extension that enables you to establish custom environment variables for the test host. Utilizing this extension point ensures that the testing platform will initiate a new host with the appropriate environment variables, as detailed in the [architecture](architecture.md) section.

To register a custom `ITestHostEnvironmentVariableProvider`, utilize the following API:

```cs
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
...
testApplicationBuilder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider =>
    => new CustomEnvironmentVariableForTestHost());
...
```

The factory utilizes the [IServiceProvider](iserviceprovider.md) to gain access to the suite of services offered by the testing platform.

> [!IMPORTANT]
> The sequence of registration is significant, as the APIs are called in the order they were registered.

The `ITestHostEnvironmentVariableProvider` interface includes the following methods and types:

```cs
public interface ITestHostEnvironmentVariableProvider : ITestHostControllersExtension, IExtension
{
    Task UpdateAsync(IEnvironmentVariables environmentVariables);
    Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables);
}

public interface IEnvironmentVariables : IReadOnlyEnvironmentVariables
{
    void SetVariable(EnvironmentVariable environmentVariable);
    void RemoveVariable(string variable);
}

public interface IReadOnlyEnvironmentVariables
{
    bool TryGetVariable(string variable, [NotNullWhen(true)] out OwnedEnvironmentVariable? environmentVariable);
}

public sealed class OwnedEnvironmentVariable : EnvironmentVariable
{
    public IExtension Owner { get; }
    public OwnedEnvironmentVariable(IExtension owner, string variable, string? value, bool isSecret, bool isLocked);
}

public class EnvironmentVariable
{
    public string Variable { get; }
    public string? Value { get; }
    public bool IsSecret { get; }
    public bool IsLocked { get; }
}
```

The `ITestHostEnvironmentVariableProvider` is a type of `ITestHostControllersExtension`, which serves as a base for all *test host controller* extensions. Like all other extension points, it also inherits from [IExtension](iextension.md). Therefore, like any other extension, you can choose to enable or disable it using the `IExtension.IsEnabledAsync` API.

Let's describe the api:

`UpdateAsync`: This update API provides an instance of the `IEnvironmentVariables` object, from which you can call the `SetVariable` or `RemoveVariable` methods. When using `SetVariable`, you must pass an object of type `EnvironmentVariable`, which requires the following specifications:

* `Variable`: The name of the environment variable.
* `Value`: The value of the environment variable.
* `IsSecret`: This indicates whether the environment variable contains sensitive information that should not be logged or accessible via the `TryGetVariable`.
* `IsLocked`: This determines whether other `ITestHostEnvironmentVariableProvider` extensions can modify this value.

`ValidateTestHostEnvironmentVariablesAsync`: This method is invoked after all the `UpdateAsync` methods of the registered `ITestHostEnvironmentVariableProvider` instances have been called. It allows you to *verify* the correct setup of the environment variables. It takes an object that implements `IReadOnlyEnvironmentVariables`, which provides the `TryGetVariable` method to fetch specific environment variable information with the `OwnedEnvironmentVariable` object type. After validation, you return a `ValidationResult` containing any failure reasons.

> [!NOTE]
> The testing platform, by default, implements and registers the `SystemEnvironmentVariableProvider`. This provider loads all the *current* environment variables. As the first registered provider, it executes first, granting access to the default environment variables for all other `ITestHostEnvironmentVariableProvider` user extensions.

If your extension requires intensive initialization and you need to use the async/await pattern, you can refer to the [`Async extension initialization and cleanup`](asyncinitcleanup.md). If you need to *share state* between extension points, you can refer to the [`CompositeExtensionFactory<T>`](compositeextensionfactory.md) section.
