# The logging system

The testing platform comes with an integrated logging system that generates a log file. You can view the logging options by running the `--help` command.
The options you can choose from include:

```dotnetcli
--diagnostic                             Enable the diagnostic logging. The default log level is 'Trace'. The file will be written in the output directory with the name log_[MMddHHssfff].diag
--diagnostic-filelogger-synchronouswrite Force the built-in file logger to write the log synchronously. Useful for scenario where you don't want to lose any log (i.e. in case of crash). Note that this is slowing down the test execution.
--diagnostic-output-directory            Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory.
--diagnostic-output-fileprefix           Prefix for the log file name that will replace '[log]_.'
--diagnostic-verbosity                   Define the level of the verbosity for the --diagnostic. The available values are 'Trace', 'Debug', 'Information', 'Warning', 'Error', and 'Critical'
```

From a coding standpoint, to log information, you need to obtain the `ILoggerFactory` from the [`IServiceProvider`](iserviceprovider.md).
The `ILoggerFactory` API is as follows:

```cs
public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}

public static class LoggerFactoryExtensions
{
    public static ILogger<TCategoryName> CreateLogger<TCategoryName>(this ILoggerFactory factory);
}
```

The logger factory allows you to create an `ILogger` object using the `CreateLogger` API. There's also a convenient API that accepts a generic argument, which will be used as the category name.

```cs
public interface ILogger
{
    Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);
    bool IsEnabled(LogLevel logLevel);
}

public interface ILogger<out TCategoryName> : ILogger
{
}

public static class LoggingExtensions
{
    public static Task LogTraceAsync(this ILogger logger, string message);
    public static Task LogDebugAsync(this ILogger logger, string message);
    public static Task LogInformationAsync(this ILogger logger, string message);
    public static Task LogWarningAsync(this ILogger logger, string message);
    public static Task LogErrorAsync(this ILogger logger, string message);
    public static Task LogErrorAsync(this ILogger logger, string message, Exception ex);
    public static Task LogErrorAsync(this ILogger logger, Exception ex);
    public static Task LogCriticalAsync(this ILogger logger, string message);
    public static void LogTrace(this ILogger logger, string message);
    public static void LogDebug(this ILogger logger, string message);
    public static void LogInformation(this ILogger logger, string message);
    public static void LogWarning(this ILogger logger, string message);
    public static void LogError(this ILogger logger, string message);
    public static void LogError(this ILogger logger, string message, Exception ex);
    public static void LogError(this ILogger logger, Exception ex);
    public static void LogCritical(this ILogger logger, string message);
}
```

The `ILogger` object, which is created by the `ILoggerFactory`, offers APIs for logging information at various levels. These logging levels include:

```cs
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None
}
```

Here's an example of how you might use the logging API:

```cs
...
IServiceProvider serviceProvider = ...get service provider...
ILoggerFactory loggerFactory = serviceProvider.GetLoggerFactory();
ILogger<TestingFramework> logger = loggerFactory.CreateLogger<TestingFramework>();
...
if (_logger.IsEnabled(LogLevel.Information))
{
    await _logger.LogInformationAsync($"Executing request of type '{context.Request}'");
}
...
```

Keep in mind that to prevent unnecessary allocation, you should check if the level is *enabled* using the `ILogger.IsEnabled(LogLevel)` API.
