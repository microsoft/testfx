// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.Helpers;

internal static class RunSettingsHelpers
{
    // TODO: There are two code paths that read runsettings. One is calling the ICommandLineOptions overload, other is calling the CommandLineParseResult overload.
    // Figure out if they can/should be unified so that we do one I/O operation instead of two.
    public static string ReadRunSettings(ICommandLineOptions commandLineOptions, IFileSystem fileSystem)
    {
        _ = commandLineOptions.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames);
        return ReadRunSettings(fileNames, fileSystem);
    }

    public static string ReadRunSettings(CommandLineParseResult commandLineParseResult, IFileSystem fileSystem)
    {
        _ = commandLineParseResult.TryGetOptionArgumentList(RunSettingsCommandLineOptionsProvider.RunSettingsOptionName, out string[]? fileNames);
        return ReadRunSettings(fileNames, fileSystem);
    }

    private static string ReadRunSettings(string[]? runsettingsFileFromCommandLine, IFileSystem fileSystem)
    {
        if (runsettingsFileFromCommandLine is not null &&
            runsettingsFileFromCommandLine.Length == 1 &&
            fileSystem.ExistFile(runsettingsFileFromCommandLine[0]))
        {
            return fileSystem.ReadAllText(runsettingsFileFromCommandLine[0]);
        }
        else
        {
            string? envVariableRunSettings = Environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
            if (!RoslynString.IsNullOrEmpty(envVariableRunSettings))
            {
                return envVariableRunSettings;
            }
            else
            {
                string? runSettingsFilePath = Environment.GetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE");

                if (!RoslynString.IsNullOrEmpty(runSettingsFilePath) && File.Exists(runSettingsFilePath))
                {
                    return fileSystem.ReadAllText(runSettingsFilePath);
                }
            }
        }

        return string.Empty;
    }
}
