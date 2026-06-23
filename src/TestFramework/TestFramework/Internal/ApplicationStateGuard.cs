// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* This file is intentionally copied across layered projects; keep it in sync with the canonical copy at
 * src\Platform\Microsoft.Testing.Platform\Helpers\ApplicationStateGuard.cs. */

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class ApplicationStateGuard
{
    // Keep behavior aligned with src/Platform/Microsoft.Testing.Platform/Helpers/ApplicationStateGuard.cs.
    public static UnreachableException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new($"This program location is thought to be unreachable. File='{path}' Line={line}");
}
