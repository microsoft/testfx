# The `ICommandLineOptions`

The `ICommandLineOptions` service is utilized to fetch details regarding the command-line options that the platform has parsed. The APIs available include:

```cs
public interface ICommandLineOptions
{
    bool IsOptionSet(string optionName);
    bool TryGetOptionArgumentList(string optionName, out string[]? arguments);
}
```

The `ICommandLineOptions` can be obtained through certain APIs, such as the [ICommandLineOptionsProvider](icommandlineoptionsprovider.md), or you can retrieve an instance of it from the [IServiceProvider](iserviceprovider.md) via the extension method `serviceProvider.GetCommandLineOptions()`.

`ICommandLineOptions.IsOptionSet(string optionName)`: This method allows you to verify whether a specific option has been specified. When specifying the `optionName`, omit the `--` prefix. For example, if the user inputs `--myOption`, you should simply pass `myOption`.

`ICommandLineOptions.TryGetOptionArgumentList(string optionName, out string[]? arguments)`: This method enables you to check whether a specific option has been set and, if so, retrieve the corresponding value or values (if the arity is more than one). Similar to the previous case, the `optionName` should be provided without the `--` prefix.
