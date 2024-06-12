// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal interface ICommandLineHandler
{
    string[] Arguments { get; }

    bool IsHelpInvoked();

    Task PrintHelpAsync(ITool[]? availableTools = null);
}
