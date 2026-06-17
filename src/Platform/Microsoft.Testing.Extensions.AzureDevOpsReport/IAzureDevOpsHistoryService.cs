// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal interface IAzureDevOpsHistoryService
{
    int HistoryWindowInDays { get; }

    bool TryGetStats(string testName, out FlakyStats stats);

    bool IsLikelyFlaky(string testName, double threshold);

    bool TryGetDurationStats(string testName, out DurationHistoryStats stats);
}
