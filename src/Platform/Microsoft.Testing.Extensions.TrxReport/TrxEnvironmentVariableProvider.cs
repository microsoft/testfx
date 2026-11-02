// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxEnvironmentVariableProvider : global::Microsoft.Testing.Extensions.PipeNameEnvironmentVariableProviderBase
{
    public const string TRXNAMEDPIPENAME = nameof(TRXNAMEDPIPENAME);

    private readonly ICommandLineOptions _commandLineOptions;

    public TrxEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, NamedPipeServerEndpoint endpoint)
        : base(endpoint, TRXNAMEDPIPENAME)
    {
        _commandLineOptions = commandLineOptions;
    }

    public override string Uid => nameof(TrxEnvironmentVariableProvider);

    public override string DisplayName => ExtensionResources.TrxReportGeneratorDisplayName;

    public override string Description => ExtensionResources.TrxReportGeneratorDescription;

    protected override bool ShouldValidatePipeNameValue => false;

    public override Task<bool> IsEnabledAsync()
#pragma warning disable SA1114 // Parameter list should follow declaration
        => Task.FromResult(
            // TrxReportGenerator is enabled only when trx report is enabled
            _commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)
            // TRX requires (and will trigger) a controller-managed test host whenever the current
            // platform supports process restart; this is what makes plain --report-trx controller-backed.
            && TrxModeHelpers.IsTestHostControllerSupported);
#pragma warning restore SA1114 // Parameter list should follow declaration

    protected override string GetMissingEnvironmentVariableErrorMessage(string environmentVariableName)
        => string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportGeneratorMissingTrxNamedPipeEnvironmentVariable, environmentVariableName);

    // This method is never called because ShouldValidatePipeNameValue is false, so value validation is skipped.
    protected override string GetInvalidEnvironmentVariableValueErrorMessage(string environmentVariableName, string? actualValue, string expectedValue)
        => throw ApplicationStateGuard.Unreachable();
}
