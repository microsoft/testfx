// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC;

internal static class TestStates
{
    internal const byte Discovered = 0;
    internal const byte Passed = 1;
    internal const byte Skipped = 2;
    internal const byte Failed = 3;
    internal const byte Error = 4;
    internal const byte Timeout = 5;
    internal const byte Cancelled = 6;
    internal const byte InProgress = 7;
}

internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

internal static class HandshakeMessagePropertyNames
{
    internal const byte PID = 0;
    internal const byte Architecture = 1;
    internal const byte Framework = 2;
    internal const byte OS = 3;
    internal const byte SupportedProtocolVersions = 4;
    internal const byte HostType = 5;
    internal const byte ModulePath = 6;
    internal const byte ExecutionId = 7;
    internal const byte InstanceId = 8;
    internal const byte IsIDE = 9;

    // Reports which command-line execution mode the test host is running in,
    // so consumers (e.g. dotnet test in the SDK) can detect mismatches such
    // as a help/list-tests option leaking from RunArguments into a normal run.
    // Values come from HandshakeMessageExecutionModes.
    internal const byte ExecutionMode = 10;
}

internal static class HandshakeMessageExecutionModes
{
    // Standard test run.
    internal const string Run = "run";

    // The host is going to print command-line help (e.g. --help, -?).
    internal const string Help = "help";

    // The host is going to discover tests (e.g. --list-tests).
    internal const string Discover = "discover";
}

// Wire-protocol values for HandshakeMessagePropertyNames.HostType.
// dotnet test in the dotnet/sdk repository keys behavior on these strings
// (e.g. only the "TestHost" value triggers per-assembly run bookkeeping
// such as the "(Try N) Running tests from ..." terminal line and the
// requirement that InstanceId be present). Renaming or removing the
// "TestHost" value is a binary-breaking change for older platform <-> SDK
// pairings; the other values are keyed on by specific SDK code paths but
// unknown HostType strings are silently tolerated by the SDK, so adding
// new values here (e.g. InformativeHost) is forward-compatible.
internal static class HandshakeMessageHostTypes
{
    // The actual test execution / discovery host process (ConsoleTestHost).
    internal const string TestHost = "TestHost";

    // The optional out-of-process controller that hosts/extends the test
    // host process (TestHostControllersTestHost).
    internal const string TestHostController = "TestHostController";

    // A host that performs the handshake but is not going to execute or
    // discover tests, e.g. when responding to --help in server mode. Lets
    // the SDK skip TestHost-specific bookkeeping (such as printing
    // "(Try N) Running tests from ...") while still validating the
    // handshake/protocol version. The explicit semantic signal for what
    // the host is doing remains HandshakeMessagePropertyNames.ExecutionMode.
    internal const string InformativeHost = "InformativeHost";
}

internal static class ProtocolConstants
{
    // The protocol version is intentionally NOT bumped when new optional
    // handshake properties (such as IsIDE or ExecutionMode) are added.
    // The SDK only supports a single version at a time, so bumping here
    // would require a coordinated SDK rollout that adds the new version to
    // its supported list BEFORE this change ships, otherwise handshake
    // negotiation would fail. Unknown property bytes are ignored by older
    // SDKs, so additive properties are forward-compatible without a bump.
    internal const string Version = "1.0.0";
}
