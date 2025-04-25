// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsTests
{
    [TestMethod]
    public void ReportsTheFirstExistingFileInStackTraceWithTheRightLineNumberAndEscaping()
    {
        Exception error;
        try
        {
            throw new Exception("this is an error\nwith\rnewline");
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // Trim ## so when the test fails we don't report it to AzDO, the severity is invalid, and the result is confusing.
        string? text = AzureDevOpsReporter.GetErrorText(null, error, "severity", new SystemFileSystem())?.Trim('#');
        Assert.AreEqual("vso[task.logissue type=severity;sourcepath=test/UnitTests/Microsoft.Testing.Extensions.UnitTests/AzureDevOpsTests.cs;linenumber=18;columnnumber=1]this is an error%0Awith%0Dnewline", text);
    }
}
