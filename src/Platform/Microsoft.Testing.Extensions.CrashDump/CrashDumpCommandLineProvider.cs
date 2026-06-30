// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpCommandLineProvider : CommandLineOptionsProviderBase
{
    private static readonly string[] DumpTypeOptions = ["Mini", "Heap", "Triage", "Full"];
    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(CrashDumpCommandLineOptions.CrashDumpOptionName, CrashDumpResources.CrashDumpOptionDescription, ArgumentArity.Zero, false),
        new(CrashDumpCommandLineOptions.CrashReportOptionName, CrashDumpResources.CrashReportOptionDescription, ArgumentArity.Zero, false),
        new(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName, CrashDumpResources.CrashReportIfSupportedOptionDescription, ArgumentArity.Zero, false),
        new(CrashDumpCommandLineOptions.CrashSequenceOptionName, CrashDumpResources.CrashSequenceOptionDescription, ArgumentArity.ExactlyOne, false),
        new(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName, CrashDumpResources.CrashDumpFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        new(CrashDumpCommandLineOptions.CrashDumpTypeOptionName, CrashDumpResources.CrashDumpTypeOptionDescription, ArgumentArity.ExactlyOne, false)
    ];

    public CrashDumpCommandLineProvider()
        : base(
            nameof(CrashDumpCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            CrashDumpResources.CrashDumpDisplayName,
            CrashDumpResources.CrashDumpDescription,
            CachedCommandLineOptions)
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName)
        {
            return ValidateAllowedValuesAsync(arguments[0], DumpTypeOptions, CrashDumpResources.CrashDumpTypeOptionInvalidType);
        }

        if (commandOption.Name == CrashDumpCommandLineOptions.CrashSequenceOptionName
            && !CommandLineOptionArgumentValidator.IsValidBooleanArgument(arguments[0]))
        {
            return ValidationResult.InvalidTask(CrashDumpResources.CrashSequenceOptionInvalidArgument);
        }

        // We intentionally do not enforce a '.dmp' extension on --crashdump-filename: dotnet-dump
        // and the Windows MiniDumpWriteDump APIs both accept arbitrary file names, and users
        // sometimes script around custom suffixes (e.g. timestamps appended by an outer wrapper).
        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => IsCrashDumpMainOptionMissing(commandLineOptions)
        ? ValidationResult.InvalidTask(CrashDumpResources.MissingCrashDumpMainOption)
        : AreCrashReportOptionsMutuallyExclusive(commandLineOptions)
        ? ValidationResult.InvalidTask(CrashDumpResources.CrashReportAndIfSupportedAreMutuallyExclusiveErrorMessage)
        : IsCrashReportUnsupportedOnCurrentPlatform(commandLineOptions)
        ? ValidationResult.InvalidTask(CrashDumpResources.CrashReportNotSupportedOnWindowsErrorMessage)
        : ValidationResult.ValidTask;

    private static bool AreCrashReportOptionsMutuallyExclusive(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName) &&
            commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName);

    private static bool IsCrashReportUnsupportedOnCurrentPlatform(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName) &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsCrashDumpMainOptionMissing(ICommandLineOptions commandLineOptions)
    {
        bool hasCrashDumpSubOption = commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName) ||
            commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpTypeOptionName) ||
            commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashSequenceOptionName);
        bool hasCrashDumpMainOption = commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName) ||
            commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName) ||
            commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName);

        return hasCrashDumpSubOption && !hasCrashDumpMainOption;
    }
}
