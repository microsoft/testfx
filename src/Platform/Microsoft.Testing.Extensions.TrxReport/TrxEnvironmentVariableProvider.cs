// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    public const string TRXNAMEDPIPENAME = nameof(TRXNAMEDPIPENAME);

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly string _pipeName;

    public TrxEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, string pipeName)
    {
        _commandLineOptions = commandLineOptions;
        _pipeName = pipeName;
    }

    public string Uid => nameof(TrxEnvironmentVariableProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.TrxReportGeneratorDisplayName;

    public string Description => ExtensionResources.TrxReportGeneratorDescription;

    public Task<bool> IsEnabledAsync()
#pragma warning disable SA1114 // Parameter list should follow declaration
        => Task.FromResult(
            // TrxReportGenerator is enabled only when trx report is enabled
            _commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)
            // TestController is not used when we run in server mode
            && !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey)
            // If crash dump is not enabled we run trx in-process only
            && _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName));
#pragma warning restore SA1114 // Parameter list should follow declaration

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(new(TRXNAMEDPIPENAME, _pipeName, false, true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
        if (!environmentVariables.TryGetVariable(TRXNAMEDPIPENAME, out _))
        {
            return Task.FromResult(
                ValidationResult.Invalid(
                    string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportGeneratorMissingTrxNamedPipeEnvironmentVariable, TRXNAMEDPIPENAME)));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
