// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CompositeTestExecutionFilterTests
{
    [TestMethod]
    public void Constructor_WithUnsupportedOperator_ThrowsArgumentOutOfRangeException()
    {
        const TestExecutionFilterOperator unsupportedOperator = (TestExecutionFilterOperator)999;

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = new CompositeTestExecutionFilter(unsupportedOperator, new NopFilter(), new NopFilter()));

        Assert.AreEqual("operator", exception.ParamName);
        Assert.AreEqual(unsupportedOperator, exception.ActualValue);
    }
}
