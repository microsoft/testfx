// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace TestFramework.ForTestingMSTest;

public static class TestApplicationBuilderExtensions
{
    public static void AddInternalTestFramework(this ITestApplicationBuilder testApplicationBuilder)
    {
        TestFrameworkExtension extension = new();
        TrxReportCapability trxReportCapability = new();
        testApplicationBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(trxReportCapability),
            (capabilities, serviceProvider) => new TestFramework(extension, serviceProvider.GetLoggerFactory()));
        testApplicationBuilder.AddTreeNodeFilterService(extension);
        testApplicationBuilder.AddMaximumFailedTestsService(extension);
    }
}
