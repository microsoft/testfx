// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpEnvironmentVariableProvider : global::Microsoft.Testing.Extensions.PipeNameEnvironmentVariableProviderBase
{
    public const string PipeNameEnvironmentVariableName = "TESTINGPLATFORM_HANGDUMP_PIPENAME";

    private readonly ICommandLineOptions _commandLineOptions;

    public HangDumpEnvironmentVariableProvider(ICommandLineOptions commandLineOptions, string pipeName)
        : base(pipeName, PipeNameEnvironmentVariableName)
    {
        _commandLineOptions = commandLineOptions;
    }

    public override string Uid => nameof(HangDumpEnvironmentVariableProvider);

    public override string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public override string Description => ExtensionResources.HangDumpExtensionDescription;

    public override Task<bool> IsEnabledAsync() => Task.FromResult(HangDumpOptions.IsEnabled(_commandLineOptions));

    protected override string GetMissingEnvironmentVariableErrorMessage(string environmentVariableName)
        => string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableIsMissingErrorMessage, environmentVariableName);

    protected override string GetInvalidEnvironmentVariableValueErrorMessage(string environmentVariableName, string? actualValue, string expectedValue)
        => string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpEnvironmentVariableInvalidValueErrorMessage, environmentVariableName, actualValue, expectedValue);
}
