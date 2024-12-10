// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    private const string EnableMiniDumpVariable = "DbgEnableMiniDump";
    private const string MiniDumpTypeVariable = "DbgMiniDumpType";
    private const string MiniDumpNameVariable = "DbgMiniDumpName";
    private const string CreateDumpDiagnosticsVariable = "CreateDumpDiagnostics";
    private const string CreateDumpVerboseDiagnosticsVariable = "CreateDumpVerboseDiagnostics";
    private const string EnableMiniDumpValue = "1";

    private readonly string[] _prefixes = ["DOTNET_", "COMPlus_"];
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly CrashDumpConfiguration _crashDumpGeneratorConfiguration;
    private readonly ILogger<CrashDumpEnvironmentVariableProvider> _logger;

    private string? _miniDumpNameValue;

    public CrashDumpEnvironmentVariableProvider(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        CrashDumpConfiguration crashDumpGeneratorConfiguration,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CrashDumpEnvironmentVariableProvider>();
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _crashDumpGeneratorConfiguration = crashDumpGeneratorConfiguration;
    }

    /// <inheritdoc />
    public string Uid => nameof(CrashDumpEnvironmentVariableProvider);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => CrashDumpResources.CrashDumpDisplayName;

    /// <inheritdoc />
    public string Description => CrashDumpResources.CrashDumpDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName) && _crashDumpGeneratorConfiguration.Enable);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        foreach (string prefix in _prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{EnableMiniDumpVariable}", EnableMiniDumpValue, false, true));
            environmentVariables.SetVariable(new($"{prefix}{CreateDumpDiagnosticsVariable}", EnableMiniDumpValue, false, true));
            environmentVariables.SetVariable(new($"{prefix}{CreateDumpVerboseDiagnosticsVariable}", EnableMiniDumpValue, false, true));
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

        foreach (string prefix in _prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{MiniDumpTypeVariable}", miniDumpTypeValue, false, true));
        }

        _miniDumpNameValue = _commandLineOptions.TryGetOptionArgumentList(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName, out string[]? dumpFileName)
            ? Path.Combine(_configuration.GetTestResultDirectory(), dumpFileName[0])
            : Path.Combine(_configuration.GetTestResultDirectory(), $"{Path.GetFileName(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath())}_%p_crash.dmp");
        _crashDumpGeneratorConfiguration.DumpFileNamePattern = _miniDumpNameValue;
        foreach (string prefix in _prefixes)
        {
            environmentVariables.SetVariable(new($"{prefix}{MiniDumpNameVariable}", _miniDumpNameValue, false, true));
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{MiniDumpNameVariable}: {_miniDumpNameValue}");
            _logger.LogTrace($"{MiniDumpTypeVariable}: {miniDumpTypeValue}");
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
        StringBuilder errors = new();

#if !NETCOREAPP
        return ValidationResult.InvalidTask(CrashDumpResources.CrashDumpNotSupportedInNonNetCoreErrorMessage);
#else
        foreach (string prefix in _prefixes)
        {
            if (!environmentVariables.TryGetVariable($"{prefix}{EnableMiniDumpVariable}", out OwnedEnvironmentVariable? enableMiniDump)
            || enableMiniDump.Value != EnableMiniDumpValue)
            {
                AddError(errors, $"{prefix}{EnableMiniDumpVariable}", EnableMiniDumpValue, enableMiniDump?.Value);
            }
        }

        foreach (string prefix in _prefixes)
        {
            if (!environmentVariables.TryGetVariable($"{prefix}{CreateDumpDiagnosticsVariable}", out OwnedEnvironmentVariable? enableMiniDump)
            || enableMiniDump.Value != EnableMiniDumpValue)
            {
                AddError(errors, $"{prefix}{CreateDumpDiagnosticsVariable}", EnableMiniDumpValue, enableMiniDump?.Value);
            }
        }

        foreach (string prefix in _prefixes)
        {
            if (!environmentVariables.TryGetVariable($"{prefix}{CreateDumpVerboseDiagnosticsVariable}", out OwnedEnvironmentVariable? enableMiniDump)
            || enableMiniDump.Value != EnableMiniDumpValue)
            {
                AddError(errors, $"{prefix}{CreateDumpVerboseDiagnosticsVariable}", EnableMiniDumpValue, enableMiniDump?.Value);
            }
        }

        foreach (string prefix in _prefixes)
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

        foreach (string prefix in _prefixes)
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
            errors.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpInvalidEnvironmentVariableValueErrorMessage, variableName, expectedValue, actualValueString));
        }
#endif
    }
}
