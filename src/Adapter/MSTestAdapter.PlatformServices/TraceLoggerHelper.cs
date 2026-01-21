// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal static class TraceLoggerHelper
{
    public static ITraceLogger Instance { get; set; } = null!; // Expected to be set early by adapter.
}
