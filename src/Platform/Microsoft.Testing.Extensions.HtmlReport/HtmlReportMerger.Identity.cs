// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.HtmlReport;

internal static partial class HtmlReportMerger
{
    private static TestIdentity CreateTestIdentity(MergedTest test)
        => new(
            ReadRequiredString(test.Test, "uid"),
            test.ProducingTestModule ?? string.Empty,
            test.TargetFramework ?? string.Empty,
            test.Architecture ?? string.Empty);

    private static RetryIdentity CreateRetryBaseIdentity(MergedTest test)
        => new(
            CreateTestIdentity(test),
            ReadRequiredString(test.Test, "displayName"));
}
