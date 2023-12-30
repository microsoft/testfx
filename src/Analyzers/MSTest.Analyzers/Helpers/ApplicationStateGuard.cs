// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace MSTest.Analyzers.Helpers;

internal static class ApplicationStateGuard
{
    public static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new($"This program location is thought to be unreachable. File='{path}' Line={line}");
}
