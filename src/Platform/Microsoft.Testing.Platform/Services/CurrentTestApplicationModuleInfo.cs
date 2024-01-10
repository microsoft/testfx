// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
#else
using System.Reflection;
#endif

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class CurrentTestApplicationModuleInfo(IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler process) : ITestApplicationModuleInfo
{
    private readonly IRuntimeFeature _runtimeFeature = runtimeFeature;
    private readonly IEnvironment _environment = environment;
    private readonly IProcessHandler _process = process;
    private static readonly string[] MuxerExec = ["exec"];

    public bool IsCurrentTestApplicationHostDotnetMuxer
    {
        get
        {
            string? processPath = GetProcessPath(_environment, _process, false);
            return processPath is not null
                && Path.GetFileNameWithoutExtension(processPath) == "dotnet";
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
        => IsCurrentTestApplicationModuleExecutable && !IsCurrentTestApplicationHostDotnetMuxer;

#if NETCOREAPP
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "We handle the singlefile/native aot use case")]
#endif
    public string GetCurrentTestApplicationFullPath()
    {
        string? moduleName = null;

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#pragma warning disable IL3000
            moduleName = Assembly.GetEntryAssembly()?.Location;
#pragma warning restore IL3000
        }

        moduleName = TAString.IsNullOrEmpty(moduleName)
            ? GetProcessPath(_environment, _process)
            : moduleName;

        ApplicationStateGuard.Ensure(moduleName is not null);
        return moduleName;
    }

    public string GetProcessPath()
        => GetProcessPath(_environment, _process, throwOnNull: true)!;

    public string[] GetCommandLineArgs()
        => _environment.GetCommandLineArgs();

    public string GetCommandLineArguments()
    {
        string executableFileName = Path.GetFileNameWithoutExtension(GetCurrentTestApplicationFullPath());
        if (IsAppHostOrSingleFileOrNativeAot)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                executableFileName += ".exe";
            }
        }

        return _environment.CommandLine[_environment.CommandLine.IndexOf(executableFileName, StringComparison.InvariantCultureIgnoreCase)..];
    }

    private static string? GetProcessPath(IEnvironment environment, IProcessHandler process, bool throwOnNull = false)
#if NETCOREAPP
    {
        string? processPath = environment.ProcessPath;
        ApplicationStateGuard.Ensure(processPath is not null || !throwOnNull);

        return processPath;
    }
#else
        => process.GetCurrentProcess().MainModule.FileName;
#endif

    public ExecutableInfo GetCurrentExecutableInfo()
    {
        string currentTestApplicationFullPath = GetCurrentTestApplicationFullPath();
        bool isDotnetMuxer = IsCurrentTestApplicationHostDotnetMuxer;
        bool isAppHost = IsAppHostOrSingleFileOrNativeAot;
        string processPath = GetProcessPath();
        string[] commandLineArguments = GetCommandLineArgs();
        string fileName = processPath;
        IEnumerable<string> arguments = isAppHost
            ? commandLineArguments.Skip(1)
            : isDotnetMuxer
                ? MuxerExec.Concat(commandLineArguments)
                : commandLineArguments;

        return new(fileName, arguments, Path.GetDirectoryName(currentTestApplicationFullPath)!);
    }
}
