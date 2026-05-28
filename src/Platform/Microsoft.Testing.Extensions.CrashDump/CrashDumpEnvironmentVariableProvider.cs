// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    public const string SequenceFileEnvironmentVariableName = "TESTINGPLATFORM_CRASHDUMP_SEQUENCE_FILE";

    private const string EnableMiniDumpVariable = "DbgEnableMiniDump";
    private const string MiniDumpTypeVariable = "DbgMiniDumpType";
    private const string MiniDumpNameVariable = "DbgMiniDumpName";
    private const string EnableCrashReportVariable = "EnableCrashReport";
    private const string EnableCrashReportOnlyVariable = "EnableCrashReportOnly";
    private const string EnabledValue = "1";

    private static readonly string[] Prefixes = ["DOTNET_", "COMPlus_"];
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly CrashDumpConfiguration _crashDumpGeneratorConfiguration;
    private readonly ILogger<CrashDumpEnvironmentVariableProvider> _logger;

    private string? _miniDumpNameValue;
    private string? _sequenceFileValue;

    public CrashDumpEnvironmentVariableProvider(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        CrashDumpConfiguration crashDumpGeneratorConfiguration,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CrashDumpEnvironmentVariableProvider>();
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
        _crashDumpGeneratorConfiguration = crashDumpGeneratorConfiguration;
    }

    /// <inheritdoc />
    public string Uid => nameof(CrashDumpEnvironmentVariableProvider);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => CrashDumpResources.CrashDumpDisplayName;

    /// <inheritdoc />
    public string Description => CrashDumpResources.CrashDumpDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
        => Task.FromResult(
            (_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName) ||
             IsCrashReportEffective(_commandLineOptions)) &&
            _crashDumpGeneratorConfiguration.Enable);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        // IsEnabledAsync gates this method, so at least one of --crashdump / --crash-report /
        // --crash-report-if-supported is set here.
        // '--crash-report-if-supported' contributes to the effective crash-report configuration
        // only when the current OS actually honors the underlying runtime env vars. On Windows
        // the .NET runtime ignores DOTNET_EnableCrashReport(Only) (see dotnet/runtime#80191),
        // so we silently drop the request and let the test run continue without a crash report.
        bool crashReportEnabled = IsCrashReportEffective(_commandLineOptions);

        foreach (string prefix in Prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{EnableMiniDumpVariable}", EnabledValue, false, true));
        }

        if (crashReportEnabled)
        {
            bool crashDumpEnabled = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);

            // When a dump is also requested, emit a crash report alongside it.
            // Otherwise emit only the crash report (no dump file).
            string reportVariable = crashDumpEnabled ? EnableCrashReportVariable : EnableCrashReportOnlyVariable;
            foreach (string prefix in Prefixes)
            {
                environmentVariables.SetVariable(new($"{prefix}{reportVariable}", EnabledValue, false, true));
            }
        }

        string miniDumpTypeValue = "4";

        if (_commandLineOptions.TryGetOptionArgumentList(CrashDumpCommandLineOptions.CrashDumpTypeOptionName, out string[]? dumpTypeString))
        {
            switch (dumpTypeString[0].ToLowerInvariant().Trim())
            {
                case "mini":
                    {
                        miniDumpTypeValue = "1";
                        break;
                    }

                case "heap":
                    {
                        miniDumpTypeValue = "2";
                        break;
                    }

                case "triage":
                    {
                        miniDumpTypeValue = "3";
                        break;
                    }

                case "full":
                    {
                        miniDumpTypeValue = "4";
                        break;
                    }

                default:
                    {
                        miniDumpTypeValue = dumpTypeString[0];
                        break;
                    }
            }
        }

        foreach (string prefix in Prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{MiniDumpTypeVariable}", miniDumpTypeValue, false, true));
        }

        string testAppName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw ApplicationStateGuard.Unreachable();
        _miniDumpNameValue = _commandLineOptions.TryGetOptionArgumentList(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName, out string[]? dumpFileName)
            ? Path.Combine(_configuration.GetTestResultDirectory(), dumpFileName[0])
            : Path.Combine(_configuration.GetTestResultDirectory(), $"{testAppName}_%p_crash.dmp");
        _crashDumpGeneratorConfiguration.DumpFileNamePattern = _miniDumpNameValue;
        foreach (string prefix in Prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{MiniDumpNameVariable}", _miniDumpNameValue, false, true));
        }

        if (IsSequenceLoggingEnabled())
        {
            // The sequence file is written directly by the testhost (not by the .NET runtime), so it
            // does not need any of the runtime "createdump" placeholders (%p, %e, %h, %t). We compose
            // a deterministic path next to the (eventual) dump file so the testhost and the controller
            // agree on the exact path to write to / read from without any per-side expansion.
            //
            // The path includes a per-controller-instance unique token so that parallel testhost
            // launches targeting the same results directory cannot stomp on each other's sequence
            // files (a write collision would otherwise be silently lost; a graceful exit of one host
            // would also delete the sibling host's sequence file before it could be published).
            string uniqueToken = Guid.NewGuid().ToString("N").Substring(0, 8);
            string sequenceFileName = dumpFileName is not null
                ? $"{StripRuntimePlaceholders(dumpFileName[0])}_{uniqueToken}.sequence.log"
                : $"{testAppName}_{uniqueToken}_crash.sequence.log";
            _sequenceFileValue = Path.Combine(_configuration.GetTestResultDirectory(), sequenceFileName);
            _crashDumpGeneratorConfiguration.SequenceFileName = _sequenceFileValue;
            environmentVariables.SetVariable(new(SequenceFileEnvironmentVariableName, _sequenceFileValue, isSecret: false, isLocked: true));
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{MiniDumpNameVariable}: {_miniDumpNameValue}");
            _logger.LogTrace($"{MiniDumpTypeVariable}: {miniDumpTypeValue}");
            if (_sequenceFileValue is not null)
            {
                _logger.LogTrace($"{SequenceFileEnvironmentVariableName}: {_sequenceFileValue}");
            }
        }

        return Task.CompletedTask;
    }

    private bool IsSequenceLoggingEnabled()
    {
        if (!_commandLineOptions.TryGetOptionArgumentList(CrashDumpCommandLineOptions.CrashSequenceOptionName, out string[]? arguments))
        {
            // Default: on whenever --crashdump or --crash-report is set (IsEnabledAsync gates this method).
            return true;
        }

        return !CommandLineOptionArgumentValidator.IsOffValue(arguments[0]);
    }

    private static string StripRuntimePlaceholders(string pattern)
    {
        // Drop the .NET runtime's "createdump" placeholders (%p, %e, %h, %t, ...) from a dump filename
        // pattern so we obtain a concrete, deterministic file path that the testhost extension and the
        // controller can agree on without any per-side expansion. "%%" remains a literal "%" per the
        // runtime's escaping convention.
        var sb = new StringBuilder(pattern.Length);
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '%' && i + 1 < pattern.Length)
            {
                if (pattern[i + 1] == '%')
                {
                    sb.Append('%');
                    i++;
                    continue;
                }

                // Drop the single-character placeholder (e.g. %p, %e, %h, %t).
                i++;
                continue;
            }

            sb.Append(pattern[i]);
        }

        return sb.ToString();
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
#if !NETCOREAPP
        return ValidationResult.InvalidTask(CrashDumpResources.CrashDumpNotSupportedInNonNetCoreErrorMessage);
#else
        StringBuilder errors = new();

        // IsEnabledAsync gates this method, so at least one of --crashdump / --crash-report /
        // --crash-report-if-supported is set here. Match the env-var-setting logic in
        // UpdateAsync: --crash-report-if-supported on Windows is silently ignored.
        bool crashReportEnabled = IsCrashReportEffective(_commandLineOptions);

        ValidateBothPrefixes(EnableMiniDumpVariable, EnabledValue);

        if (crashReportEnabled)
        {
            bool crashDumpEnabled = _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName);
            string reportVariable = crashDumpEnabled ? EnableCrashReportVariable : EnableCrashReportOnlyVariable;
            ValidateBothPrefixes(reportVariable, EnabledValue);
        }

        foreach (string prefix in Prefixes)
        {
            if (!environmentVariables.TryGetVariable($"{prefix}{MiniDumpTypeVariable}", out OwnedEnvironmentVariable? miniDumpType))
            {
                AddError(errors, $"{prefix}{MiniDumpTypeVariable}", "Valid values are 1, 2, 3, 4", miniDumpType?.Value);
            }
            else
            {
                if (miniDumpType is null || miniDumpType.Value is null)
                {
                    throw new InvalidOperationException("Unexpected missing MiniDumpTypeVariable variable");
                }

                if (!miniDumpType.Value.Equals("1", StringComparison.OrdinalIgnoreCase) &&
                    !miniDumpType.Value.Equals("2", StringComparison.OrdinalIgnoreCase) &&
                    !miniDumpType.Value.Equals("3", StringComparison.OrdinalIgnoreCase) &&
                    !miniDumpType.Value.Equals("4", StringComparison.OrdinalIgnoreCase))
                {
                    AddError(errors, $"{prefix}{MiniDumpTypeVariable}", "Valid values are 1, 2, 3, 4", miniDumpType.Value);
                }
            }
        }

        foreach (string prefix in Prefixes)
        {
            if (!environmentVariables.TryGetVariable($"{prefix}{MiniDumpNameVariable}", out OwnedEnvironmentVariable? miniDumpName)
            || miniDumpName.Value != _miniDumpNameValue)
            {
                AddError(errors, $"{prefix}{MiniDumpNameVariable}", _miniDumpNameValue, miniDumpName?.Value);
            }
        }

        return errors.Length > 0 ? Task.FromResult(ValidationResult.Invalid(errors.ToString())) : ValidationResult.ValidTask;

        static void AddError(StringBuilder errors, string variableName, string? expectedValue, string? actualValue)
        {
            string actualValueString = actualValue ?? "<null>";
            errors.AppendLine(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpInvalidEnvironmentVariableValueErrorMessage, variableName, expectedValue, actualValueString));
        }

        void ValidateBothPrefixes(string variableName, string expectedValue)
        {
            foreach (string prefix in Prefixes)
            {
                if (!environmentVariables.TryGetVariable($"{prefix}{variableName}", out OwnedEnvironmentVariable? variable)
                    || variable.Value != expectedValue)
                {
                    AddError(errors, $"{prefix}{variableName}", expectedValue, variable?.Value);
                }
            }
        }
#endif
    }

    // The "if-supported" companion of --crash-report only contributes to the effective
    // crash-report configuration on platforms where the underlying runtime env vars are
    // honored. Two scenarios disqualify the runtime entirely:
    //  - .NET Framework, where the env-var-based createdump/crashreport mechanism is not
    //    available at all (see ValidateTestHostEnvironmentVariablesAsync below);
    //  - Windows on .NET (Core), where the runtime ignores DOTNET_EnableCrashReport(Only)
    //    (see dotnet/runtime#80191).
    // In both cases we treat the option as a silent no-op so that a single command line
    // can be reused across CI matrices without per-OS / per-TFM branching.
    internal static bool IsCrashReportEffective(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportOptionName))
        {
            return true;
        }

        if (!commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashReportIfSupportedOptionName))
        {
            return false;
        }

#if !NETCOREAPP
        return false;
#else
        return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }
}
