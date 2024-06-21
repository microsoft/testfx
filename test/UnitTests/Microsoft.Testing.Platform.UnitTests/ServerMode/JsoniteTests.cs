// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class JsoniteTests : TestBase
{
    public JsoniteTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public void Serialize_DateTimeOffset()
    {
#if !NETCOREAPP
        string actual = Jsonite.Json.Serialize(new DateTimeOffset(2023, 01, 01, 01, 01, 01, 01, TimeSpan.Zero));

        // Assert
        Assert.AreEqual("2023-01-01T01:01:01.0010000+00:00", actual.Trim('"'));
#endif
    }
}
