// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher

internal sealed partial class AzureDevOpsTestResultsPublisher
{
    private static DateTimeOffset? Min(DateTimeOffset? left, DateTimeOffset? right)
        => left is null ? right : right is null || left <= right ? left : right;

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
        => left is null ? right : right is null || left >= right ? left : right;

}
