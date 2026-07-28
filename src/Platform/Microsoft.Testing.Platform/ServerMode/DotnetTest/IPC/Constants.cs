// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
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

[Embedded]
internal static class SessionEventTypes
{
    internal const byte TestSessionStart = 0;
    internal const byte TestSessionEnd = 1;
}

[Embedded]
internal static class DisplayMessageLevels
{
    // The severity of a generic host display message forwarded over the pipe. The SDK maps each level
    // to its TerminalTestReporter sink: Information -> WriteMessage, Warning -> WriteWarningMessage,
    // Error -> WriteErrorMessage. Values must stay stable (they flow over IPC to dotnet test).
    internal const byte Information = 0;
    internal const byte Warning = 1;
    internal const byte Error = 2;
}

[Embedded]
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

    // Identifies the orchestration feature responsible for the orchestrator host
    // (e.g. "retry"). Only sent by the test host orchestrator so consumers (e.g.
    // dotnet test in the SDK) can understand why an orchestrator is participating
    // in the run. The value is the orchestrator extension Uid.
    internal const byte OrchestratorFeature = 11;

    // Carries the OS-level name of the reverse "server control" pipe. Only ever sent by the SDK
    // (dotnet test) in its handshake reply. Its presence is the capability signal for
    // server-initiated session cancellation: when the test host sees a non-empty value it opens a
    // NamedPipeClient to that pipe and parks a long-poll WaitForServerControlRequest so the SDK can
    // push a ServerControlMessage (e.g. CancelSession) at any time - even while the test host is
    // otherwise silent. An older SDK never sends this property, so the feature stays disabled.
    //
    // SDK-side contract (duplicated by hand in dotnet/sdk - keep in sync):
    //   * Every process that performs the handshake and receives this property (test host, test host
    //     controller, orchestrator) opens its own client to the advertised name, so the SDK must be able to
    //     accept one control connection per connecting process (e.g. one server instance per process, or a
    //     distinct pipe name advertised per handshake reply).
    //   * The SDK MUST keep the control pipe open for the whole data session. The test host treats an early
    //     pipe drop as "host gone => cancel", so closing the control pipe before the data session ends would be
    //     interpreted as a cancellation.
    internal const byte ServerControlPipeName = 12;

    // The 1-based attempt number of the test host in a retry sequence: 1 for the initial run and every
    // non-retried run, incremented by the retry orchestrator for each subsequent attempt. Multiple test-host
    // instances, such as shards, can belong to the same attempt. Only test hosts send this property.
    //
    // The value is carried from the orchestrator to each launched test host through the
    // TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER environment variable. When the variable is absent, the test host
    // reports "1". This is an additive, capability-style property: consumers that do not understand it ignore it,
    // so it is not gated on the negotiated protocol version.
    internal const byte AttemptNumber = 13;

    // Semicolon-separated reverse-DNS artifact kinds supported by the post-processors registered in this app.
    // Missing means the app does not advertise artifact post-processing.
    internal const byte SupportedPostProcessorKinds = 14;

    // Semicolon-separated lowercase file extensions used as a compatibility fallback for untagged artifacts.
    internal const byte SupportedPostProcessorExtensionsLegacy = 15;
}

[Embedded]
internal static class ServerControlKinds
{
    // The kind of a ServerControlMessage the SDK pushes to the test host over the reverse control pipe.
    // Values must stay stable (they flow over IPC to dotnet test). Reserve additional values for future
    // signals (drain, pause, ...).
    internal const byte CancelSession = 1;
}

[Embedded]
internal static class HandshakeMessageHostTypes
{
    // A regular (console or server) test host that actually runs tests.
    internal const string TestHost = "TestHost";

    // A test host controller process that restarts/monitors the test host.
    internal const string TestHostController = "TestHostController";

    // A test host running in server (JSON-RPC) mode.
    internal const string ServerTestHost = "ServerTestHost";

    // A test host orchestrator process (e.g. the retry orchestrator) that drives
    // one or more test host executions.
    internal const string TestHostOrchestrator = "TestHostOrchestrator";

    // A tool host relaunched by dotnet test to run artifact post-processors.
    internal const string ArtifactPostProcessor = "ArtifactPostProcessor";
}

[Embedded]
internal static class HandshakeMessageExecutionModes
{
    // Standard test run.
    internal const string Run = "run";

    // The host is going to print command-line help (e.g. --help, -?).
    internal const string Help = "help";

    // The host is going to discover tests (e.g. --list-tests).
    internal const string Discover = "discover";

    // Artifact post-processing or another non-test tool.
    internal const string Tool = "tool";
}

[Embedded]
internal static class ProtocolConstants
{
    // The change between 1.0.0 and 1.1.0 is that TerminalOutputDevice is no longer plugged in.
    // That's not really a protocol change, but we use the version to signal to the SDK that it
    // can safely keep the test host's standard output/error visible (TerminalTestReporter and
    // host output will no longer collide).
    // When both sides advertise 1.1.0 and we negotiate to that version, the SDK can keep its
    // live output enabled.
    //
    // 1.2.0 adds the AzureDevOpsLogMessage: under the pipe protocol the host installs a no-op output
    // device (see below), so any Azure DevOps logging commands (##[group], ##vso[...]) produced by the
    // AzureDevOpsReport extension would otherwise be swallowed. When both sides negotiate 1.2.0 the host
    // forwards those marked lines to the SDK over the pipe, and the SDK writes them verbatim to its
    // TerminalTestReporter so they reach the pipeline log. An older SDK that only negotiates 1.1.0 never
    // receives the message (the host gates forwarding on the negotiated version), so it stays compatible.
    //
    // 1.3.0 adds the generic DisplayMessage: under the pipe protocol the host's forwarding output device still
    // discards regular informational output, but relays durable session messages and warning/error host messages
    // (SessionMessageOutputDeviceData / WarningMessageOutputDeviceData / ErrorMessageOutputDeviceData) to the SDK
    // as DisplayMessage so that framework and extension output produced outside test results is no longer swallowed
    // in multi-assembly runs. The SDK routes each DisplayMessage to its TerminalTestReporter's WriteMessage,
    // WriteWarningMessage, or WriteErrorMessage according to level. Unlike the AzureDevOps path, this is not gated
    // on an Azure DevOps agent. The host gates forwarding on the negotiated version, so an older SDK (<= 1.2.0)
    // never receives the message.
    //
    // NOTE: Under the pipe protocol the host installs a forwarding output device
    // (DotnetTestPassthroughOutputDevice) regardless of the negotiated protocol version (the SDK's
    // TerminalTestReporter owns user-facing output). It still discards regular (informational) output but,
    // depending on the negotiated version, relays: Azure DevOps logging commands as AzureDevOpsLogMessage (1.2.0+,
    // only on an Azure DevOps agent), and durable session plus warning/error host messages as DisplayMessage
    // (1.3.0+, always). See OutputDeviceManager.BuildAsync. With an old SDK that only supports 1.0.0, both sides
    // will produce no live output (the SDK suppresses its TerminalTestReporter to avoid colliding with the host
    // output it expected before this change). Users must update to an SDK that negotiates 1.1.0 to see live output
    // via the SDK's TerminalTestReporter.
    // 1.4.0 adds the reverse "server control" channel used for server-initiated session cancellation. When the
    // SDK advertises a ServerControlPipeName in its handshake reply, the test host opens a NamedPipeClient to that
    // pipe and parks a long-poll WaitForServerControlRequest; the SDK completes it with a ServerControlMessage
    // (e.g. CancelSession) whenever it wants the test host to stop cooperatively (global --maximum-failed-tests,
    // --timeout, ...). The feature is gated on the presence of the handshake property (a capability), not on this
    // version string, so an older SDK that never advertises the pipe leaves the feature disabled. The version is
    // still bumped so the negotiated-version state advances in lockstep.
    internal const string SupportedVersions = "1.0.0;1.1.0;1.2.0;1.3.0;1.4.0";
}
