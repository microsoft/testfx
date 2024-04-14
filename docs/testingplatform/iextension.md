# The `IExtension` interface

The `IExtension` interface serves as the foundational interface for all extensibility points within the testing platform. It is primarily used to obtain descriptive information about the extension and, most importantly, to enable or disable the extension itself.

Let's delve into the specifics:

```cs
public interface IExtension
{
    string Uid { get; }
    string Version { get; }
    string DisplayName { get; }
    string Description { get; 
    Task<bool> IsEnabledAsync();
}
```

`Uid`: This is the unique identifier for the extension. It's crucial to choose a unique value for this string to avoid conflicts with other extensions.

`Version`: This represents the version of the interface. It MUST use [**semantic versioning**](https://semver.org/).

`DisplayName`: This is a user-friendly name that will appear in logs and when you request information using the `--info` command line option.

`Description`: The description of the extension, will appear when you request information using the `--info` command line option.

`IsEnabledAsync()`: This method is invoked by the testing platform when the extension is being instantiated. If the method returns false, the extension will be excluded.
This method typically makes decisions based on the [configuration file](configuration.md) file or some [custom command line options](icommandlineoptions.md). Users often specify `--customExtensionOption` in the command line to opt into the extension itself.