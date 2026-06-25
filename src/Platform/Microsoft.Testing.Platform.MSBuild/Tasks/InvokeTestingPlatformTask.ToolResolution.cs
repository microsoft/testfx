// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.MSBuild.Tasks;

using static Microsoft.Testing.Platform.MSBuild.Tasks.DotnetMuxerLocator;

namespace Microsoft.Testing.Platform.MSBuild;

public partial class InvokeTestingPlatformTask
{
    /// <inheritdoc />
    protected override string ToolName
    {
        get
        {
            if (TryGetRunCommand() is string runCommand)
            {
                Log.LogMessage(MessageImportance.Low, $"Constructed target path via similar logic as to RunCommand: '{runCommand}'");
                return Path.GetFileName(runCommand);
            }

            // If target dll ends with .dll we're in the "dotnet" context
            if (TargetPath.ItemSpec.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Target path is a dll '{TargetPath.ItemSpec}'");
                return DotnetRunnerName;
            }

            // If the target is an exe and we're not on Windows we try with the mono runner.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TargetPath.ItemSpec.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
            {
                Log.LogMessage(MessageImportance.Low, $"Target is an '.exe' on '{RuntimeInformation.OSDescription}' trying with the mono runner.");
                return MonoRunnerName;
            }

            return Path.GetFileName(TargetPath.ItemSpec);
        }
    }

    /// <inheritdoc />
    public override string ToolExe { get => base.ToolExe; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    protected override string? GenerateFullPathToTool()
    {
        if (TryGetRunCommand() is string runCommand)
        {
            return runCommand;
        }

        // If it's not netcore and we're on Windows we expect the TargetPath to be the executable, otherwise we try with mono.
        if (!IsNetCoreApp && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log.LogMessage(MessageImportance.Low, $"Test application is not a .NETCoreApp, tool path: '{TargetPath.ItemSpec}'");
            return TargetPath.ItemSpec;
        }

        string dotnetRunnerName = ToolName;
        Log.LogMessage(MessageImportance.Low, $"Tool name: '{dotnetRunnerName}'");

        // We look for dotnet muxer only if we're not running with mono.
        if (dotnetRunnerName != MonoRunnerName)
        {
            if (DotnetHostPath is not null && File.Exists(DotnetHostPath.ItemSpec) && IsCurrentProcessArchitectureCompatible())
            {
                Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using DOTNET_HOST_PATH environment variable: '{DotnetHostPath.ItemSpec}'");
                return DotnetHostPath.ItemSpec;
            }

            Log.LogMessage(MessageImportance.Low, $"Current process architecture '{_currentProcessArchitecture}'. Requested test architecture '{TestArchitecture.ItemSpec}'");

#if NETCOREAPP
            PlatformArchitecture targetArchitecture = Enum.Parse<PlatformArchitecture>(TestArchitecture.ItemSpec, ignoreCase: true);
#else
            var targetArchitecture = (PlatformArchitecture)Enum.Parse(typeof(PlatformArchitecture), TestArchitecture.ItemSpec, ignoreCase: true);
#endif
            StringBuilder resolutionLog = new();
            DotnetMuxerLocator dotnetMuxerLocator = new(log => resolutionLog.AppendLine(log));
            if (dotnetMuxerLocator.TryGetDotnetPathByArchitecture(targetArchitecture, out string? dotnetPath))
            {
                Log.LogMessage(MessageImportance.Low, resolutionLog.ToString());
                Log.LogMessage(MessageImportance.Low, $"dotnet muxer tool path found using architecture: '{TestArchitecture.ItemSpec}' '{dotnetPath}'");
                return dotnetPath;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, resolutionLog.ToString());
                Log.LogError(string.Format(CultureInfo.InvariantCulture, Resources.MSBuildResources.IncompatibleArchitecture, dotnetRunnerName, TestArchitecture.ItemSpec));
                return null;
            }
        }

        string values = Environment.GetEnvironmentVariable("PATH")!;
        foreach (string? p in values.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(p, dotnetRunnerName);
            if (File.Exists(fullPath))
            {
                Log.LogMessage(MessageImportance.Low, $"Runner tool path found using PATH environment variable: '{fullPath}'");
                return fullPath;
            }
        }

        Log.LogError(Resources.MSBuildResources.FullPathToolCalculationFailed, dotnetRunnerName);

        return null;
    }

    private bool IsCurrentProcessArchitectureCompatible() =>
#if NETCOREAPP
        _currentProcessArchitecture == Enum.Parse<Architecture>(TestArchitecture.ItemSpec, ignoreCase: true);
#else
        _currentProcessArchitecture == (Architecture)Enum.Parse(typeof(Architecture), TestArchitecture.ItemSpec, ignoreCase: true);
#endif

    private string? TryGetRunCommand()
    {
        // This condition specifically handles this part:
        // https://github.com/dotnet/sdk/blob/5846d648f2280b54a54e481f55de4d9eea0e6a0e/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1152-L1155
        // The more correct logic is implementing https://github.com/microsoft/testfx/issues/5091
        // What we want to do here is to avoid using 'dotnet exec' if possible, and run the executable directly instead.
        // When running with dotnet exec, we are run under dotnet.exe process, which can break some scenarios (e.g, loading PRI in WinUI tests).
        // It seems like WinUI would try to resolve the PRI file relative to the path of the process, so relative to, e.g, C:\Program Files\dotnet
        if (IsNetCoreApp &&
            bool.TryParse(IsExecutable?.ItemSpec, out bool isExecutable) && isExecutable &&
            bool.TryParse(UseAppHost?.ItemSpec, out bool useAppHost) && useAppHost)
        {
            string runCommand = $"{TargetDir?.ItemSpec}{AssemblyName?.ItemSpec}{NativeExecutableExtension?.ItemSpec}";
            if (File.Exists(runCommand))
            {
                return runCommand;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override string GenerateCommandLineCommands()
    {
        Build.Utilities.CommandLineBuilder builder = new();

        if (ToolName == DotnetRunnerName && IsNetCoreApp)
        {
            // In most cases, if ToolName is "dotnet.exe", that means we are given a "dll" file.
            // In turn, that means we are not .NET Framework (because we will use Exe otherwise).
            // In case ToolName ended up being "dotnet.exe" and we are
            // .NET Framework, that means it's the user's assembly that is named "dotnet".
            // In that case, we want to execute the tool (user's executable) directly.
            // So, we only only "exec" if we are .NETCoreApp
            builder.AppendSwitch("exec");
            builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
        }
        else if (ToolName == MonoRunnerName)
        {
            // If ToolName is "mono", that means TargetPath is an "exe" file and we are not running on Windows.
            // In this case, we use the given exe file as an argument to mono.
            builder.AppendFileNameIfNotNull(TargetPath.ItemSpec);
        }

        // If we are not "dotnet.exe" and not "mono", then we are given an executable from user and we are running on Windows.
        builder.AppendSwitchIfNotNull($"--{MSBuildConstants.MSBuildNodeOptionKey} ", _pipeNameDescription.Name);

        if (!RoslynString.IsNullOrEmpty(TestingPlatformCommandLineArguments?.ItemSpec))
        {
            builder.AppendTextUnquoted($" {TestingPlatformCommandLineArguments.ItemSpec} ");
        }

        if (VSTestCLIRunSettings?.Length > 0)
        {
            foreach (ITaskItem taskItem in VSTestCLIRunSettings)
            {
                builder.AppendTextUnquoted($" {taskItem.ItemSpec}");
            }
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    protected override bool ValidateParameters()
    {
        if (!_fileSystem.Exist(TargetPath.ItemSpec))
        {
            Log.LogError(Resources.MSBuildResources.InvalidTargetPath, TargetPath.ItemSpec);
            return false;
        }

        return true;
    }
}
