// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.Telemetry;

internal sealed class AppInsightTelemetryClientFactory : ITelemetryClientFactory
{
    private readonly string? _localExportFilePath;

    public AppInsightTelemetryClientFactory(string? localExportFilePath = null)
        => _localExportFilePath = localExportFilePath;

    public ITelemetryClient Create(string? currentSessionId, string osVersion)
        => RoslynString.IsNullOrWhiteSpace(_localExportFilePath)
            ? new AppInsightTelemetryClient(currentSessionId, osVersion)
            : new LocalFileTelemetryClient(_localExportFilePath, currentSessionId, osVersion);
}
