// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Platform.Services;

internal interface ITestApplicationProcessExitCode : IDataConsumer
{
    bool HasTestAdapterTestSessionFailure { get; }

    string? TestAdapterTestSessionFailureErrorMessage { get; }

    Task SetTestAdapterTestSessionFailureAsync(string errorMessage);

    // If we decide to open this extension we should make it Task<int> GetProcessExitCodeAsync();
    int GetProcessExitCode();

    Statistics GetStatistics();
}

internal sealed class Statistics
{
    public int TotalRanTests { get; set; }

    public int TotalFailedTests { get; set; }
}
