// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Extensions.VSTestBridge.Helpers;

internal static class DebugUtils
{
    private const string VSTestBridgeAttachDebuggerEnvVar = "TESTINGPLATFORM_VSTESTBRIDGE_ATTACH_DEBUGGER";

    public static void LaunchAttachDebugger()
    {
        if (Environment.GetEnvironmentVariable(VSTestBridgeAttachDebuggerEnvVar) == "1")
        {
            Debugger.Launch();
        }
    }
}
