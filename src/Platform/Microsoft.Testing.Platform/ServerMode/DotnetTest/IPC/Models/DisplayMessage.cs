// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// A generic host display message forwarded over the pipe so that warning/error diagnostics produced by the
// test host outside of test results (e.g. hang/crash dump diagnostics, retry summaries, generic
// extension/framework warnings and errors) reach the SDK even though the pipe protocol installs a forwarding
// output device that otherwise discards host output. Level is one of DisplayMessageLevels; the SDK maps it to
// its TerminalTestReporter sink (Information -> WriteMessage, Warning -> WriteWarningMessage,
// Error -> WriteErrorMessage). Introduced with protocol version 1.3.0; the host only sends it when the SDK
// negotiates 1.3.0 or later.
internal sealed record DisplayMessage(string? ExecutionId, string? InstanceId, byte Level, string? Text) : IRequest;
