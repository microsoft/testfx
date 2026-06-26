// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Marks an output-device line as an Azure DevOps pipeline command (for example <c>##[group]</c>,
/// <c>##[endgroup]</c> or <c>##vso[...]</c>) produced by the AzureDevOpsReport extension that must
/// reach the pipeline log even under the dotnet test pipe protocol.
/// </summary>
/// <remarks>
/// In a single-assembly run this renders like any other <see cref="TextOutputDeviceData"/> (the
/// terminal output device writes its <see cref="TextOutputDeviceData.Text"/> verbatim). Under the
/// dotnet test pipe protocol the host installs <see cref="DotnetTestPassthroughOutputDevice"/>, which
/// recognizes this marker and forwards the line to the SDK over the protocol (version 1.2.0+), so the
/// Azure DevOps logging commands are not swallowed in multi-assembly runs.
/// </remarks>
internal sealed class AzureDevOpsCommandOutputDeviceData : TextOutputDeviceData
{
    public AzureDevOpsCommandOutputDeviceData(string text)
        : base(text)
    {
    }
}
