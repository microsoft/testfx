// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpCommandLineProvider : CommandLineOptionsProviderBase
{
    public const string HangDumpOptionName = "hangdump";
    public const string HangDumpFileNameOptionName = "hangdump-filename";
    public const string HangDumpTimeoutOptionName = "hangdump-timeout";
    public const string HangDumpTypeOptionName = "hangdump-type";
    public const string HangDumpTypeIfSupportedOptionName = "hangdump-type-if-supported";

    // The "supported on the current TFM" set used to validate --hangdump-type and to drive
    // the runtime fallback of --hangdump-type-if-supported. 'Triage' is only available on
    // .NET Core because the underlying createdump implementation has no equivalent on
    // .NET Framework, where we fall back to MiniDumpWriteDump.
#if NETCOREAPP
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full", "Triage", "None"];
#else
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full", "None"];
#endif

    // The "any value the user can request" set used to validate --hangdump-type-if-supported
    // regardless of TFM. When a value is in this set but not in HangDumpTypeOptions on the
    // current runtime, the lifetime handler maps it (via MapToSupportedDumpType) to the
    // closest supported dump type — e.g. 'Triage' -> 'Mini' on .NET Framework — and emits an
    // informational message describing the substitution.
    private static readonly string[] AllHangDumpTypeOptions = ["Mini", "Heap", "Full", "Triage", "None"];

    private static readonly string HangDumpTypeOptionsFormatted = string.Join(", ", Array.ConvertAll(HangDumpTypeOptions, option => $"'{option}'"));

    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(HangDumpOptionName, ExtensionResources.HangDumpOptionDescription, ArgumentArity.Zero, false),
        new(HangDumpTimeoutOptionName, ExtensionResources.HangDumpTimeoutOptionDescription, ArgumentArity.ExactlyOne, false),
        new(HangDumpFileNameOptionName, ExtensionResources.HangDumpFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        new(
            HangDumpTypeOptionName,
            string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionDescription, HangDumpTypeOptionsFormatted),
            ArgumentArity.ExactlyOne,
            false),
        new(HangDumpTypeIfSupportedOptionName, ExtensionResources.HangDumpTypeIfSupportedOptionDescription, ArgumentArity.ExactlyOne, false)
    ];

    public HangDumpCommandLineProvider()
        : base(
            nameof(HangDumpCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.HangDumpExtensionDisplayName,
            ExtensionResources.HangDumpExtensionDescription,
            CachedCommandLineOptions)
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == HangDumpTimeoutOptionName && !TimeSpanParser.TryParse(arguments[0], out TimeSpan _))
        {
            return ValidationResult.InvalidTask(ExtensionResources.HangDumpTimeoutOptionInvalidArgument);
        }

        if (commandOption.Name == HangDumpTypeOptionName)
        {
            return ValidateAllowedValuesAsync(arguments[0], HangDumpTypeOptions, ExtensionResources.HangDumpTypeOptionInvalidType);
        }

        if (commandOption.Name == HangDumpTypeIfSupportedOptionName)
        {
            // The "-if-supported" variant accepts the full set of dump types regardless of TFM:
            // the runtime fallback is what makes the option safe to leave in a shared command
            // line. Anything outside the full set is still a user typo and must be rejected.
            return ValidateAllowedValuesAsync(arguments[0], AllHangDumpTypeOptions, ExtensionResources.HangDumpTypeOptionInvalidType);
        }

        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        bool hasHangDumpSubOption = commandLineOptions.IsOptionSet(HangDumpTimeoutOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpFileNameOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpTypeOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpTypeIfSupportedOptionName);

        return hasHangDumpSubOption && !commandLineOptions.IsOptionSet(HangDumpOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.MissingHangDumpMainOption)
            : commandLineOptions.IsOptionSet(HangDumpTypeOptionName) && commandLineOptions.IsOptionSet(HangDumpTypeIfSupportedOptionName)
                ? ValidationResult.InvalidTask(ExtensionResources.HangDumpTypeAndIfSupportedAreMutuallyExclusiveErrorMessage)
                : ValidationResult.ValidTask;
    }

    // Returns true when 'value' (an already-validated entry from the full hang dump type set)
    // is available on the current runtime, i.e. when --hangdump-type would have accepted it.
    internal static bool IsHangDumpTypeSupportedOnCurrentRuntime(string value)
        => HangDumpTypeOptions.Contains(value, StringComparer.OrdinalIgnoreCase);

    // Maps a requested dump type (any value from the full set) to the closest supported value
    // on the current runtime. Used by --hangdump-type-if-supported when the requested value is
    // unavailable. The mapping intentionally prefers the closest size/intent over the global
    // default 'Full' so a user asking for the lightest dump does not end up with the heaviest
    // one (e.g. a CI runner with little disk).
    internal static string MapToSupportedDumpType(string requested)
    {
        if (IsHangDumpTypeSupportedOnCurrentRuntime(requested))
        {
            return requested;
        }

        // 'Triage' is only available on .NET Core. On .NET Framework, the closest equivalent
        // is 'Mini' (both are small, fast dumps suitable for crash analysis).
        if (string.Equals(requested, "Triage", StringComparison.OrdinalIgnoreCase))
        {
            return "Mini";
        }

        // No specific mapping known; preserve the historical default. New per-value mappings
        // should be added above this line as we learn about additional unsupported values.
        return "Full";
    }
}
