// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal readonly struct FlakyStats
{
    public FlakyStats(int passCount, int failCount)
    {
        PassCount = passCount;
        FailCount = failCount;
    }

    public int PassCount { get; }

    public int FailCount { get; }

    public int TotalCount => PassCount + FailCount;

    public double FailureRate => TotalCount == 0 ? 0 : (double)FailCount / TotalCount;
}
