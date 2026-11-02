// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Marks a GitHub Actions workflow command that must reach the runner unchanged, including under
/// <c>dotnet test</c>. The forwarding output device sends it as an informational display message when
/// protocol 1.3.0 or later is negotiated; other output devices handle it like ordinary text.
/// </summary>
internal sealed class GitHubActionsCommandOutputDeviceData : TextOutputDeviceData
{
    public GitHubActionsCommandOutputDeviceData(string text)
        : base(text)
    {
    }
}
