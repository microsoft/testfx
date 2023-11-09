﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Platform.Tools;

internal interface IToolCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    string ToolName { get; }
}
