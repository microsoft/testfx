// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class ApplicationStateGuard
{
    public static UnreachableException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new($"This program location is thought to be unreachable. File='{path}' Line={line}");
}