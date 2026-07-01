// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// A single line of Azure DevOps "passthrough" output produced by the AzureDevOpsReport extension
// (e.g. ##[group], ##[endgroup], ##vso[...]) that must reach the pipeline log even though the pipe
// protocol installs a no-op output device on the host. The SDK writes LogText verbatim to its
// TerminalTestReporter. Introduced with protocol version 1.2.0; the host only sends it when the SDK
// negotiates 1.2.0 or later.
internal sealed record AzureDevOpsLogMessage(string? ExecutionId, string? InstanceId, string? LogText) : IRequest;
