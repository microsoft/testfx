// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal static class TestStates
{
    internal const string Passed = "Passed";

    internal const string Skipped = "Skipped";

    internal const string Failed = "Failed";

    internal const string Error = "Error";

    internal const string Timeout = "Timeout";

    internal const string Cancelled = "Cancelled";
}

internal static class SessionEventTypes
{
    internal const string TestSessionStart = "TestSessionStart";
    internal const string TestSessionEnd = "TestSessionEnd";
}
