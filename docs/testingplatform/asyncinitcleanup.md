# Async extension initialization and cleanup

The creation of the testing framework and extensions through factories adheres to the standard .NET object creation mechanism, which uses synchronous constructors. If an extension requires intensive initialization (such as accessing the file system or network), it cannot employ the *async/await* pattern in the constructor because constructors return void, not `Task`.

Therefore, the testing platform provides a method to initialize an extension using the async/await pattern through a simple interface. For symmetry, it also offers an async interface for cleanup that extensions can implement seamlessly.

```cs
public interface IAsyncInitializableExtension
{
    Task InitializeAsync();
}

public interface IAsyncCleanableExtension
{
    Task CleanupAsync();
}
```

`IAsyncInitializableExtension.InitializeAsync`: This method is assured to be invoked following the creation factory.

`IAsyncCleanableExtension.CleanupAsync`: This method is assured to be invoked during the termination of the testing session, prior to the default `DisposeAsync` or `Dispose`.

> [!NOTE]
> By default, the testing platform will call `DisposeAsync` if it's available, or `Dispose` if it's implemented. It's important to note that the testing platform will not call both dispose methods but will prioritize the async one if implemented.
