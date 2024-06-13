// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class RuntimeContext
{
    // TODO: We should be using a capability from the runner instead of looking at environment variables.
    public static bool IsInHotReloadContext()
        => Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1"
        || Environment.GetEnvironmentVariable("TESTINGPLATFORM_HOTRELOAD_ENABLED") == "1";
}
