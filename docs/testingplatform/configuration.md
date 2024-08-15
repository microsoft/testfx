# The `IConfiguration`

The `IConfiguration` interface can be retrived using the [`IServiceProvider`](iserviceprovider.md) and provides access to the configuration settings for the testing framework and any extension points. By default, these configurations are loaded from:

* Environment variables
* A JSON file named `[assemblyName].testconfig.json` located near the entry point assembly.

**The order of precedence is maintained, which means that if a configuration is found in the environment variables, the JSON file will not be processed.**

The interface is a straightforward key-value pair of strings:

```cs
public interface IConfiguration
{
    string? this[string key] { get; }
}
```

## JSON configuration file

The JSON file follows a hierarchical structure. To access child properties, you need to use the `:` separator. For example, consider a configuration for a potential testing framework like:

```json
{
  "CustomTestingFramework": {
    "DisableParallelism": true
  }
}
```

The code snippet would look something like this:

```cs
IServiceProvider serviceProvider = ...get the service provider...
IConfiguration configuration = serviceProvider.GetConfiguration();
if (configuration["CustomTestingFramework:DisableParallelism"] == bool.TrueString)
{
  ...
}
```

In the case of an array, such as:

```json
{
  "CustomTestingFramework": {
    "Engine": [
      "ThreadPool",
      "CustomThread"
    ]
  }
}
```

The syntax to access to the fist element ("ThreadPool") is:

```cs
IServiceProvider serviceProvider = ...get the service provider...
IConfiguration configuration = serviceProvider.GetConfiguration();
var fistElement = configuration["CustomTestingFramework:Engine:0"];
```

## Environment variables

The `:` separator doesn't work with environment variable hierarchical keys on all platforms. `__`, the double underscore, is:

* Supported by all platforms. For example, the `:` separator is not supported by [Bash](https://linuxhint.com/bash-environment-variables/), but `__` is.
* Automatically replaced by a `:`

For instance, the environment variable can be set as follows (This example is applicable for Windows):

```bash
setx CustomTestingFramework__DisableParallelism=True
```

You can choose not to use the environment variable configuration source when creating the `ITestApplicationBuilder`:

```cs
var testApplicationOptions = new TestApplicationOptions();
testApplicationOptions.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
ITestApplicationBuilder testApplicationBuilder = await TestApplication.CreateBuilderAsync(args, testApplicationOptions);
```
