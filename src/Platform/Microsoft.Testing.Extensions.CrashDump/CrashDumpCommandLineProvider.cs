// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpCommandLineProvider : ICommandLineOptionsProvider
{
    private static readonly string[] DumpTypeOptions = ["Mini", "Heap", "Triage", "Full"];

    public string Uid => nameof(CrashDumpCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => CrashDumpResources.CrashDumpDisplayName;

    public string Description => CrashDumpResources.CrashDumpDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new[]
        {
            new CommandLineOption(CrashDumpCommandLineOptions.CrashDumpOptionName, CrashDumpResources.CrashDumpOptionDescription, ArgumentArity.Zero, false),
            new CommandLineOption(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName, CrashDumpResources.CrashDumpFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            new CommandLineOption(CrashDumpCommandLineOptions.CrashDumpTypeOptionName, CrashDumpResources.CrashDumpTypeOptionDescription, ArgumentArity.ExactlyOne, false),
        };

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName)
        {
            if (!DumpTypeOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpTypeOptionInvalidType, arguments[0]));
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
