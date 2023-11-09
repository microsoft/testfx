// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

#pragma warning disable IDE1006 // Naming Styles
internal static class JsonRpcMethods
{
    public const string Initialize = "initialize";
    public const string TestingDiscoverTests = "testing/discoverTests";
    public const string TestingRunTests = "testing/runTests";
    public const string TestingTestUpdatesTests = "testing/testUpdates/tests";
    public const string TelemetryUpdate = "telemetry/update";
    public const string ClientLaunchDebugger = "client/launchDebugger";
    public const string ClientAttachDebugger = "client/attachDebugger";
    public const string ClientLog = "client/log";
    public const string Exit = "exit";
    public const string CancelRequest = "$/cancelRequest";
}

internal static class JsonRpcStrings
{
    // Common
    public const string JsonRpc = "jsonrpc";
    public const string Id = "id";
    public const string Method = "method";
    public const string Params = "params";
    public const string Result = "result";
    public const string Code = "code";
    public const string Error = "error";
    public const string Data = "data";

    // Initialize request and response
    public const string ProcessId = "processId";
    public const string ClientInfo = "clientInfo";
    public const string ServerInfo = "serverInfo";
    public const string Name = "name";
    public const string Version = "version";

    // Capabilities
    public const string Capabilities = "capabilities";
    public const string Testing = "testing";
    public const string DebuggerProvider = "debuggerProvider";
    public const string SupportsDiscovery = "supportsDiscovery";
    public const string MultiRequestSupport = "experimental_multiRequestSupport";
    public const string VSTestProviderSupport = "vstestProvider";

    // Discovery and run
    public const string RunId = "runId";
    public const string Tests = "tests";
    public const string Filter = "filter";

    // Test change message and test change properties
    internal const string Parent = "parent";
    internal const string Node = "node";
    internal const string Changes = "changes";

    // Logging
    public const string Level = "level";
    public const string Message = "message";

    // Telemetry
    public const string EventName = "EventName";
    public const string Metrics = "metrics";

    // Process
    public const string Program = "Program";
    public const string Args = "Args";
    public const string WorkingDirectory = "WorkingDirectory";
    public const string EnvironmentVariables = "EnvironmentVariables";

    // Artifacts
    public const string Attachments = "attachments";
    public const string Uri = "uri";
    public const string Producer = "producer";
    public const string Type = "type";
    public const string DisplayName = "display-name";
    public const string Description = "description";
    public const string Uid = "uid";
}
#pragma warning restore IDE1006 // Naming Styles
