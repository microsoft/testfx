// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal interface ICommandLineHandler
{
    bool IsHelpInvoked();

    Task PrintHelpAsync(IOutputDevice outputDevice, IReadOnlyList<ITool>? availableTools = null);
}
