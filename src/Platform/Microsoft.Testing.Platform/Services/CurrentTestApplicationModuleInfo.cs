// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class CurrentTestApplicationModuleInfo(IEnvironment environment, IProcessHandler process) : ITestApplicationModuleInfo
{
    private readonly IEnvironment _environment = environment;
    private readonly IProcessHandler _process = process;
    private ICommandLineArgumentsProvider? _commandLineArgumentsProvider;
    private static readonly string[] MuxerExec = ["exec"];

    internal void SetCommandLineArgumentsProvider(ICommandLineArgumentsProvider? commandLineArgumentsProvider)
    {
        _commandLineArgumentsProvider = commandLineArgumentsProvider;
    }

    public bool IsCurrentTestApplicationHostDotnetMuxer
    {
        get
        {
            string? processPath = GetProcessPath(_environment, _process);
            return processPath is not null
                && Path.GetFileNameWithoutExtension(processPath) == "dotnet";
        }
    }

    public bool IsCurrentTestApplicationHostMonoMuxer
    {
        get
        {
            string? processPath = GetProcessPath(_environment, _process);
            return processPath is not null
                && Path.GetFileNameWithoutExtension(processPath) is { } processName
                && processName is "mono" or "mono-sgen";
        }
    }

    public bool IsCurrentTestApplicationModuleExecutable
    {
        get
        {
            string? processPath = GetProcessPath(_environment, _process, true);
            return processPath != ".dll";
        }
    }

    public bool IsAppHostOrSingleFileOrNativeAot
        => IsCurrentTestApplicationModuleExecutable
        && !IsCurrentTestApplicationHostDotnetMuxer
        && !IsCurrentTestApplicationHostMonoMuxer;

    public string GetCurrentTestApplicationFullPath()
    {
        string? moduleName = TryGetCurrentTestApplicationFullPath();

        ApplicationStateGuard.Ensure(moduleName is not null);
        return moduleName;
    }

#if NETCOREAPP
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "We handle the singlefile/native aot use case")]
#endif
    public string? TryGetCurrentTestApplicationFullPath()
    {
        // This is empty in native app, or in single file app.
        string? moduleName = Assembly.GetEntryAssembly()?.Location;

        return RoslynString.IsNullOrEmpty(moduleName)
            ? GetProcessPath(_environment, _process)
            : moduleName;
    }

    public string? TryGetAssemblyName()
    {
        string? executableName = Assembly.GetEntryAssembly()?.GetName().Name;
        return RoslynString.IsNullOrEmpty(executableName)
            ? Path.GetFileNameWithoutExtension(GetProcessPath(_environment, _process))
            : executableName;
    }

    public string GetCurrentTestApplicationDirectory()
        => Path.GetDirectoryName(TryGetCurrentTestApplicationFullPath()) ?? AppContext.BaseDirectory;

    public string GetDisplayName()
        => TryGetCurrentTestApplicationFullPath() ?? TryGetAssemblyName() ?? "<unknown-assembly>";

    public string GetProcessPath()
        => GetProcessPath(_environment, _process, throwOnNull: true)!;

    private static string? GetProcessPath(IEnvironment environment, IProcessHandler process, bool throwOnNull = false)
    {
#if NETCOREAPP
        string? processPath = environment.ProcessPath;
#else
        using IProcess currentProcess = process.GetCurrentProcess();
        string? processPath = currentProcess.MainModule?.FileName;
#endif

        ApplicationStateGuard.Ensure(processPath is not null || !throwOnNull);
        return processPath;
    }

    public ExecutableInfo GetCurrentExecutableInfo()
    {
        bool isDotnetMuxer = IsCurrentTestApplicationHostDotnetMuxer;
        bool isAppHost = IsAppHostOrSingleFileOrNativeAot;
        bool isMonoMuxer = IsCurrentTestApplicationHostMonoMuxer;
        
        string[] environmentArgs = _environment.GetCommandLineArgs();
        string[] commandLineArguments;
        
        if (_commandLineArgumentsProvider is not null)
        {
            string[] customArgs = _commandLineArgumentsProvider.GetOriginalCommandLineArguments();
            
            if (isDotnetMuxer && environmentArgs.Length >= 2)
            {
                // For dotnet scenarios, we need to preserve the assembly path from environment
                // and combine it with the custom arguments
                // Environment: ["dotnet", "MyTest.dll"]
                // Custom: ["--retry-failed-tests", "1"]
                // Result: ["dotnet", "MyTest.dll", "--retry-failed-tests", "1"]
                commandLineArguments = environmentArgs.Take(2).Concat(customArgs).ToArray();
            }
            else
            {
                // For executable scenarios, use custom args but preserve the executable name from environment
                // Environment: ["MyTest.exe"]
                // Custom: ["--retry-failed-tests", "1"]
                // Result: ["MyTest.exe", "--retry-failed-tests", "1"]
                if (environmentArgs.Length >= 1)
                {
                    commandLineArguments = environmentArgs.Take(1).Concat(customArgs).ToArray();
                }
                else
                {
                    commandLineArguments = customArgs;
                }
            }
        }
        else
        {
            // Fallback to original behavior when no custom args are provided
            commandLineArguments = environmentArgs;
        }
        
        IEnumerable<string> arguments = (isAppHost, isDotnetMuxer, isMonoMuxer) switch
        {
            // When executable
            (true, _, _) => commandLineArguments.Skip(1),
            // When dotnet
            (_, true, _) => MuxerExec.Concat(commandLineArguments),
            // When mono
            (_, _, true) => commandLineArguments,
            // Otherwise
            _ => commandLineArguments,
        };

        return new(GetProcessPath(), arguments, GetCurrentTestApplicationDirectory());
    }
}
