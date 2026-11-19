// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal struct TestNodeStatistics(long totalDiscoveredTests, long totalPassedTests, long totalFailedTests, long totalPassedRetries, long totalFailedRetries)
{
    public long TotalDiscoveredTests { get; set; } = totalDiscoveredTests;

    public long TotalPassedTests { get; set; } = totalPassedTests;

    public long TotalFailedTests { get; set; } = totalFailedTests;

    public long TotalPassedRetries { get; set; } = totalPassedRetries;

    public long TotalFailedRetries { get; set; } = totalFailedRetries;
}
