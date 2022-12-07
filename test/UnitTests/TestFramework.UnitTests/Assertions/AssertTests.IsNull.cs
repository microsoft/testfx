// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;
public partial class AssertTests : TestContainer
{
    public void IsNotNull_WhenNonNullNullableValue_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj);
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj, "my message");
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndCompositeMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj, "my message with {0}", "some arg");
        _ = obj.ToString(); // No potential NRE warning
    }

    private object? GetObj() => new();
}
