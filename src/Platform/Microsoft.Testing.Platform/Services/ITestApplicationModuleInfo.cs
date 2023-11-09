﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal interface ITestApplicationModuleInfo
{
    string GetCommandLineArguments();

    string GetCurrentTestApplicationFullPath();

    string GetProcessPath();

    string[] GetCommandLineArgs();

    ExecutableInfo GetCurrentExecutableInfo();

    bool IsCurrentTestApplicationHostDotnetMuxer { get; }

    bool IsCurrentTestApplicationModuleExecutable { get; }

    bool IsAppHostOrSingleFileOrNativeAot { get; }
}
