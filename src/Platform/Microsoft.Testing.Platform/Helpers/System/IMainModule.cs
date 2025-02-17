// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IMainModule
{
#if NETCOREAPP
    [UnsupportedOSPlatform("browser")]
    string? FileName { get; }
#else
    string FileName { get; }
#endif
}
