// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Marks an output-device line produced by the AzureDevOpsReport extension that must reach the Azure
/// DevOps pipeline log even under the dotnet test pipe protocol. This is usually an Azure DevOps logging
/// command (for example <c>##[group]</c>, <c>##[endgroup]</c> or <c>##vso[...]</c>), but it also carries
/// the extension's other report output, such as the slow-test progress lines.
/// </summary>
/// <remarks>
/// In a single-assembly run this renders like any other <see cref="TextOutputDeviceData"/> (the
/// terminal output device writes its <see cref="TextOutputDeviceData.Text"/> verbatim). Under the
/// dotnet test pipe protocol the host installs <see cref="DotnetTestPassthroughOutputDevice"/>, which
/// recognizes this marker and forwards the line to the SDK over the protocol (version 1.2.0+), so the
/// AzureDevOpsReport output is not swallowed in multi-assembly runs.
/// </remarks>
internal sealed class AzureDevOpsCommandOutputDeviceData : TextOutputDeviceData
{
    public AzureDevOpsCommandOutputDeviceData(string text)
        : base(text)
    {
    }
}
